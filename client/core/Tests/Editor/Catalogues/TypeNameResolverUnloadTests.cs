using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace Internal.Tests
{
    // TypeNameResolver of the Addressables fork. A handler in the process-wide AssemblyLoad event outlives the domain that
    // subscribed it and keeps that domain from unloading, so the editor must not have one. AssemblyLoadContext is not
    // visible to the compiler of the assemblies here, hence the reflection.
    [Category("Catalogues")]
    public class TypeNameResolverUnloadTests
    {
        const string ResolverAssembly = "Unity.ResourceManager";
        const string ResolverType = "UnityEngine.ResourceManagement.Util.TypeNameResolver";
        const BindingFlags AnyStatic = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

        [Test]
        public void Initialize_LeavesNoProcessWideHandler()
        {
            var resolver = Resolver();

            resolver.GetMethod("Initialize", AnyStatic).Invoke(null, null);

            Assert.AreEqual(0, AssemblyLoadHandlersOf(resolver.Assembly));
        }

        [Test]
        public void AssemblyLoadedAfterInitialize_IsResolved()
        {
            var resolver = Resolver();
            resolver.GetMethod("Initialize", AnyStatic).Invoke(null, null);

            var late = LoadNotYetLoadedAssembly();
            var expected = late.GetTypes().First(type => type.IsPublic);

            var resolved = resolver.GetMethod("Resolve", AnyStatic)
                .Invoke(null, new object[] { late.GetName().Name, expected.FullName });

            Assert.AreEqual(expected, resolved);
        }

        static Type Resolver()
        {
            var assembly = AppDomain.CurrentDomain.GetAssemblies().Single(item => item.GetName().Name == ResolverAssembly);
            return assembly.GetType(ResolverType, throwOnError: true);
        }

        static int AssemblyLoadHandlersOf(Assembly assembly)
        {
            var field = typeof(object).Assembly.GetType("System.Runtime.Loader.AssemblyLoadContext", throwOnError: true)
                .GetField("AssemblyLoad", AnyStatic);
            var handlers = (Delegate)field.GetValue(null);
            return handlers.GetInvocationList().Count(handler => handler.Method.DeclaringType.Assembly == assembly);
        }

        // A library of the runtime nothing has touched yet, with a public type of its own (a facade only forwards).
        static Assembly LoadNotYetLoadedAssembly()
        {
            var loaded = AppDomain.CurrentDomain.GetAssemblies().Select(item => item.GetName().Name).ToHashSet();
            var runtime = Path.GetDirectoryName(typeof(object).Assembly.Location);

            foreach (var path in Directory.GetFiles(runtime, "System.*.dll").OrderBy(item => item))
            {
                if (loaded.Contains(Path.GetFileNameWithoutExtension(path)))
                    continue;

                var assembly = Assembly.LoadFrom(path);
                if (assembly.GetTypes().Any(type => type.IsPublic))
                    return assembly;
            }

            throw new InvalidOperationException($"every library of {runtime} is loaded");
        }
    }
}
