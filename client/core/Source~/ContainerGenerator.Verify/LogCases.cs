using System;
using System.Linq;

namespace ContainerGenerator.Verify {
    // ILog<T> is given by every scope without a registration: the argument is Logs.Of<T>().
    internal static partial class Program {
        private static void RunLogCases() {
            Run("ILog<T> of a constructor is Logs.Of<T>()", TestLogOfConstructor);
            Run("ILog<T> of an [Inject] method is Logs.Of<T>()", TestLogOfInject);
        }

        private static void TestLogOfConstructor() {
            var emitted = EmitRoot(LogConstructorSource, "LogRoot", out var graph);
            Require(graph.Diagnostics.Count == 0,
                "ILog<T> must need no registration: " + string.Join(", ", graph.Diagnostics.Select(d => d.Descriptor.Id)));
            Require(emitted.IndexOf("global::Internal.Logs.Of<global::Sample.Service>()", StringComparison.Ordinal) >= 0,
                "the log of the service must be Logs.Of<Service>():\n" + emitted);
            Require(emitted.IndexOf("global::Internal.Logs.Of<global::Sample.Pool<global::Sample.Service>>()", StringComparison.Ordinal) >= 0,
                "a generic argument must stay as it is:\n" + emitted);
            RequireCompiles(emitted, LogConstructorSource);
        }

        private static void TestLogOfInject() {
            var emitted = EmitRoot(LogInjectSource, "LogInjectRoot", out var graph);
            Require(graph.Diagnostics.Count == 0,
                "ILog<T> must need no registration: " + string.Join(", ", graph.Diagnostics.Select(d => d.Descriptor.Id)));
            Require(emitted.IndexOf("Setup(_config, global::Internal.Logs.Of<global::Sample.InjectedService>())", StringComparison.Ordinal) >= 0,
                "the [Inject] method must get Logs.Of<InjectedService>():\n" + emitted);
            RequireCompiles(emitted, LogInjectSource);
        }

        private const string LogConstructorSource = @"
using Internal;
namespace Sample {
    public class Pool<T> {}
    public class Service { public Service(ILog<Service> log, ILog<Pool<Service>> poolLog) {} }
    public static class LogRoot {
        public static void Construct(IScopeBuilder builder) {
            builder.Register<Service>();
        }
    }
}
";

        private const string LogInjectSource = @"
using Internal;
namespace Sample {
    public class Config {}
    public class InjectedService {
        [Inject]
        public void Setup(Config config, ILog<InjectedService> log) {}
    }
    public static class LogInjectRoot {
        public static void Construct(IScopeBuilder builder) {
            builder.Register<Config>();
            builder.Register<InjectedService>();
        }
    }
}
";
    }
}
