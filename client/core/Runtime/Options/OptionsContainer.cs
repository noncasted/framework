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

        public DebugOptions DebugOptions => _debug;
        public VersionOptions VersionOptions => _version;
        public BackendOptions BackendOptions => _backend;
        public PlatformOptions PlatformOptions => _platform;

        public void Register(IBuilder builder)
        {
            builder.RegisterInstance(this);
            builder.RegisterInstance(PlatformOptions);
            builder.RegisterInstance(BackendOptions);
            builder.RegisterInstance(DebugOptions);
            builder.RegisterInstance(VersionOptions);
        }
    }
}