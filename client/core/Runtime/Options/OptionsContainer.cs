using UnityEngine;

namespace Internal
{
    // Project settings live as a catalogue asset: they are edited in Project Tools,
    // get into the build with the Internal catalogue group and are accessed as InternalAssets.OptionsContainer.
    public class OptionsContainer : EnvAsset
    {
        [SerializeField] private DebugOptions _debug = new();
        [SerializeField] private VersionOptions _version = new();
        [SerializeField] private BackendOptions _backend = new();
        [SerializeField] private PlatformOptions _platform = new();
        [SerializeField] private LogOptions _logs = new();

        public DebugOptions DebugOptions => _debug;
        public VersionOptions VersionOptions => _version;
        public BackendOptions BackendOptions => _backend;
        public PlatformOptions PlatformOptions => _platform;
        public LogOptions LogOptions => _logs;

        // Also gives its levels to the logs: what logs before the root scope runs on the defaults of LogOptions.
        public void Register(IBuilder builder)
        {
            Logs.Configure(_logs);

            builder.RegisterInstance(this);
            builder.RegisterInstance(PlatformOptions);
            builder.RegisterInstance(BackendOptions);
            builder.RegisterInstance(DebugOptions);
            builder.RegisterInstance(VersionOptions);
        }
    }
}