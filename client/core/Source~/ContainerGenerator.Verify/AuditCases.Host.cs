using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ContainerGenerator.Verify {
    // Runs what the generator emitted: the real ContainerBuilder / GeneratedScopes / BuilderExtensions of the core are
    // compiled with small stubs of their Unity neighbours, the fixture installer runs over them, the generated class is
    // built by them. A scenario checks what was constructed, called and resolved.
    internal static partial class Program {
        private static byte[]? _runtimeImage;

        private static readonly string[] RuntimeFiles = {
            "Runtime/Tools/Container/Abstract/IContainer.cs",
            "Runtime/Tools/Container/Abstract/IContainerRegistry.cs",
            "Runtime/Tools/Container/Abstract/IServiceRegistration.cs",
            "Runtime/Tools/Container/Abstract/ServiceLifetime.cs",
            "Runtime/Tools/Container/Generated/ContainerGraphRootAttribute.cs",
            "Runtime/Tools/Container/Generated/ContainerInstallerAttribute.cs",
            "Runtime/Tools/Container/Generated/ContainerPrimitiveAttribute.cs",
            "Runtime/Tools/Container/Generated/ContainerScopeParentAttribute.cs",
            "Runtime/Tools/Container/Generated/InjectAttribute.cs",
            "Runtime/Tools/Container/Runtime/ContainerBuilder.cs",
            "Runtime/Tools/Container/Runtime/GeneratedScopes.cs",
            "Runtime/Tools/Container/Runtime/ServiceRegistration.cs",
            "Runtime/Scopes/Common/Builders/BuilderExtensions.cs",
            "Runtime/Scopes/Common/Builders/IBuilder.cs",
        };

        private static string CoreFolder([CallerFilePath] string file = "") {
            return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(file)!, "..", ".."));
        }

        private static byte[] RuntimeImage() {
            if (_runtimeImage != null)
                return _runtimeImage;

            var options = new CSharpParseOptions(LanguageVersion.Latest);
            var trees = new List<SyntaxTree> { CSharpSyntaxTree.ParseText(RuntimeStubs, options, "RuntimeStubs.cs") };
            foreach (var file in RuntimeFiles) {
                var path = Path.Combine(CoreFolder(), file);
                trees.Add(CSharpSyntaxTree.ParseText(File.ReadAllText(path), options, path));
            }

            var compilation = CSharpCompilation.Create(
                "Internal.Runtime",
                trees,
                MetadataRefs().Append(MetadataReference.CreateFromFile(typeof(Stack<>).Assembly.Location)),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            var stream = new MemoryStream();
            var result = compilation.Emit(stream);
            Require(result.Success, "the runtime of the scenarios does not compile:\n" +
                                    string.Join("\n", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
            _runtimeImage = stream.ToArray();
            return _runtimeImage;
        }

        private static MetadataReference RuntimeReference() {
            return MetadataReference.CreateFromImage(RuntimeImage());
        }

        // The last source holds Sample.Scenario.Run(); each source is an assembly that references the previous ones.
        private static void RunScenario(params string[] sources) {
            RunScenarioNamed("Scenario", sources);
        }

        private static void RunScenarioNamed(string assemblyPrefix, params string[] sources) {
            var images = new List<byte[]> { RuntimeImage() };
            var references = new List<MetadataReference> { RuntimeReference() };
            for (var i = 0; i < sources.Length; i++) {
                var name = i == sources.Length - 1 ? assemblyPrefix : assemblyPrefix + "Part" + i;
                var run = RunGenerator(Compile(sources[i], name, references, includeStubs: false), name);
                RequireNoGeneratorErrors(run);
                var image = EmitImage(run.Output, name);
                images.Add(image);
                references.Add(MetadataReference.CreateFromImage(image));
            }

            Execute(images);
        }

        private static GeneratorRun GenerateWithRuntime(string source, string assemblyName = "Scenario", params MetadataReference[] extra) {
            var references = new List<MetadataReference> { RuntimeReference() };
            references.AddRange(extra);
            return RunGenerator(Compile(source, assemblyName, references, includeStubs: false), assemblyName, requireEmit: false);
        }

        private static void RequireNoGeneratorErrors(GeneratorRun run) {
            var errors = run.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
            Require(errors.Length == 0, "generator errors:\n" + string.Join("\n", errors.Select(d => d.ToString())));
        }

        private static byte[] EmitImage(Compilation output, string name) {
            var stream = new MemoryStream();
            var result = output.Emit(stream);
            Require(result.Success, name + " does not compile with the generated code:\n" +
                                    string.Join("\n", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)) +
                                    "\n--- generated ---\n" + GeneratedText(output));
            return stream.ToArray();
        }

        private static string GeneratedText(Compilation output) {
            return string.Join("\n", output.SyntaxTrees
                                           .Where(t => t.FilePath.EndsWith("Container.g.cs", StringComparison.Ordinal))
                                           .Select(t => t.GetText().ToString()));
        }

        private static void Execute(List<byte[]> images) {
            var context = new AssemblyLoadContext("Scenario", isCollectible: true);
            try {
                var loaded = new List<Assembly>();
                var byName = new Dictionary<string, Assembly>(StringComparer.Ordinal);
                context.Resolving += (_, name) => byName.TryGetValue(name.Name ?? "", out var found) ? found : null;
                foreach (var image in images) {
                    var assembly = context.LoadFromStream(new MemoryStream(image));
                    byName[assembly.GetName().Name ?? ""] = assembly;
                    loaded.Add(assembly);
                }

                foreach (var assembly in loaded) {
                    foreach (var type in assembly.GetTypes()) {
                        type.GetMethod("RegisterGenerated", BindingFlags.NonPublic | BindingFlags.Static)?.Invoke(null, null);
                    }
                }

                var scenario = loaded[loaded.Count - 1].GetType("Sample.Scenario");
                Require(scenario != null, "the last source must declare Sample.Scenario");
                try {
                    scenario!.GetMethod("Run", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, null);
                }
                catch (TargetInvocationException exception) {
                    throw new Exception(exception.InnerException!.GetType().Name + ": " + exception.InnerException.Message);
                }
            }
            finally {
                context.Unload();
            }
        }

        private const string RuntimeStubs = @"
using System;
using System.Collections.Generic;

namespace Unity.Scripting.LifecycleManagement {
    [AttributeUsage(AttributeTargets.All)]
    public sealed class NoAutoStaticsCleanupAttribute : Attribute {}
}

namespace UnityEngine {
    public class Object {}
    public class MonoBehaviour : Object {}
    public class GameObject : Object {
        public T[] GetComponentsInChildren<T>(bool includeInactive) { return new T[0]; }
    }
}

namespace Internal {
    public interface IReadOnlyLifetime {
        ILifetime Child();
        void Listen(Action listener);
    }
    public interface ILifetime : IReadOnlyLifetime { void Terminate(); }
    public sealed class Lifetime : ILifetime {
        private readonly List<Action> _listeners = new List<Action>();
        public ILifetime Child() { var child = new Lifetime(); _listeners.Add(child.Terminate); return child; }
        public void Listen(Action listener) { _listeners.Add(listener); }
        public void Terminate() { foreach (var listener in _listeners) listener(); _listeners.Clear(); }
    }
    public static class ContainerThread { public static void Assert() {} }
    public interface IContainerDiagnostics {}
    public static class ContainerDiagnostics { public static long AllocatedBytes() { return 0; } }
    public interface IEventLoop {}
    public class EventLoop : IEventLoop {}
    public interface IViewInjector {}
    public class GeneratedViewInjector : IViewInjector {
        public GeneratedViewInjector(IContainer container) {}
    }
    public interface IScopeBuilder : IBuilder { ILifetime ScopeLifetime { get; } }
    public interface IEntityBuilder : IBuilder { ILifetime ScopeLifetime { get; } }
    public interface IEntityComponent { void Register(IEntityBuilder builder); }
    public interface ISceneService { void Create(IScopeBuilder builder); }
    public interface IScopeSetup { void OnSetup(IReadOnlyLifetime lifetime); }
    public interface IScopeDispose { void OnDispose(); }
    public interface IScopeEntityView {}
    public interface IEntityScopeLoader {
        void Load(IReadOnlyLifetime lifetime, object parent, IScopeEntityView view, Action<IEntityBuilder> construct);
    }

    public sealed class TestBuilder : IScopeBuilder, IEntityBuilder {
        private readonly ContainerBuilder _builder;
        public TestBuilder(ContainerBuilder builder) { _builder = builder; builder.AttachBuilder(this); }
        public IContainerRegistry Registry => _builder;
        public IEventLoop Events { get; } = new EventLoop();
        public IReadOnlyLifetime Lifetime { get; } = new Lifetime();
        public ILifetime ScopeLifetime { get; } = new Lifetime();
    }

    public static class TestHost {
        public static IContainer Build(Action<IScopeBuilder> construct, IContainer parent = null) {
            var root = GeneratedScopes.RootId(construct.Method);
            var builder = parent == null ? new ContainerBuilder(""Test"") : new ContainerBuilder(""Test"", parent, new Lifetime());
            var scope = new TestBuilder(builder);
            scope.RegisterInstance<IEventLoop>(scope.Events);
            construct(scope);
            return GeneratedScopes.Create(root, builder);
        }
    }

    // What the fixtures did: their constructors and [Inject] methods write here.
    public static class Trace {
        private static readonly List<string> _log = new List<string>();
        public static void Add(string entry) { _log.Add(entry); }
        public static void Clear() { _log.Clear(); }
        public static string Text => string.Join("","", _log);
    }

    public static class Check {
        public static void Equal(string expected, string actual, string what) {
            if (expected != actual)
                throw new Exception(what + "" | expected: "" + expected + "" | actual: "" + actual);
        }
        public static void True(bool condition, string what) {
            if (condition == false)
                throw new Exception(what);
        }
        public static void Throws(Action action, string fragment, string what) {
            try { action(); }
            catch (Exception exception) {
                if (exception.Message.IndexOf(fragment, StringComparison.Ordinal) < 0)
                    throw new Exception(what + "": threw another error: "" + exception.Message);
                return;
            }
            throw new Exception(what + "": must throw"");
        }
    }
}
";
    }
}
