using Cysharp.Threading.Tasks;
using Internal;

namespace Global.Telemetry.Tests
{
    public static class ContainerRoots
    {
        private static ClientTelemetryOptions Options(bool enabledInEditor)
        {
            return new ClientTelemetryOptions(
                sendIntervalSeconds: 0.01f,
                sampleIntervalSeconds: 0.01f,
                enabledInEditor: enabledInEditor);
        }

        private static void AddTelemetry(IScopeBuilder builder, bool enabledInEditor)
        {
            builder.RegisterInstance<ITelemetryTransport>(ContainerProbe.Transport);
            builder.AddClientTelemetry(Options(enabledInEditor));
        }

        [ContainerGraphRoot]
        public static UniTask WithCollectors(IScopeBuilder builder)
        {
            AddTelemetry(builder, true);
            builder.Register<SessionOnLoad>().AsSelf();
            builder.Register<FirstCollector>().As<ITelemetryCollector>();
            builder.Register<SecondCollector>().As<ITelemetryCollector>();
            return UniTask.CompletedTask;
        }

        [ContainerGraphRoot]
        public static UniTask WithoutCollectors(IScopeBuilder builder)
        {
            AddTelemetry(builder, true);
            builder.Register<SessionOnLoad>().AsSelf();
            return UniTask.CompletedTask;
        }

        [ContainerGraphRoot]
        public static UniTask WithoutSession(IScopeBuilder builder)
        {
            AddTelemetry(builder, true);
            builder.Register<FirstCollector>().As<ITelemetryCollector>();
            return UniTask.CompletedTask;
        }

        [ContainerGraphRoot]
        public static UniTask WithStandardCollectors(IScopeBuilder builder)
        {
            builder.RegisterInstance<IUpdater>(ContainerProbe.Updater);
            AddTelemetry(builder, true);
            builder.Register<SessionOnLoad>().AsSelf();
            builder.AddStandardTelemetryCollectors();
            return UniTask.CompletedTask;
        }

        [ContainerGraphRoot]
        public static UniTask OverTheSocket(IScopeBuilder builder)
        {
            builder.RegisterInstance<ITelemetrySocket>(ContainerProbe.Socket);
            builder.AddSocketClientTelemetry(Options(true));
            builder.Register<SessionOnLoad>().AsSelf();
            return UniTask.CompletedTask;
        }

        [ContainerGraphRoot]
        public static UniTask OverHttp(IScopeBuilder builder)
        {
            builder.AddHttpClientTelemetry(Options(true), new TelemetryHttpOptions("http://127.0.0.1:9/api/telemetry", 1f));
            return UniTask.CompletedTask;
        }

        [ContainerGraphRoot]
        public static UniTask MetaOfAGame(IScopeBuilder builder)
        {
            builder.RegisterInstance<ITelemetrySocket>(ContainerProbe.Socket);
            builder.AddSocketClientTelemetry(Options(true));
            return UniTask.CompletedTask;
        }

        [ContainerGraphRoot]
        [ContainerScopeParent(typeof(ContainerRoots), nameof(MetaOfAGame))]
        public static UniTask BelowTheMeta(IScopeBuilder builder)
        {
            builder.Register<SessionOnLoad>().AsSelf();
            return UniTask.CompletedTask;
        }

        [ContainerGraphRoot]
        public static UniTask OffInEditor(IScopeBuilder builder)
        {
            AddTelemetry(builder, false);
            builder.Register<SessionOnLoad>().AsSelf();
            return UniTask.CompletedTask;
        }
    }
}
