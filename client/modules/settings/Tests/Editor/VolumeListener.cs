using Internal;

namespace Global.Settings.Tests
{
    // Reads the volume in its own setup: registered before the settings, so it is set up before them.
    public class VolumeListener : IScopeSetup
    {
        public VolumeListener(ISettings<SettingsSave> settings)
        {
            _settings = settings;
        }

        private readonly ISettings<SettingsSave> _settings;

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            SettingsContainerProbe.See(_settings.MasterVolume.Value);
        }
    }
}
