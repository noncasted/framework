using Internal;
using UnityEngine;

namespace Global.Settings
{
    // The defaults of the settings: an asset of the game (its GlobalAssets catalogue), passed to AddSettings. A game
    // with a derived save derives the asset class too (class GameSettingsOptions : SettingsOptions<GameSettingsSave>),
    // the field of the defaults is serialized as before.
    public abstract class SettingsOptions<TSave> : EnvAsset where TSave : SettingsSave, new()
    {
        [SerializeField] private TSave _defaultValues = CreateDefaultValues();

        public TSave DefaultValues => _defaultValues;

        private static TSave CreateDefaultValues()
        {
            var values = new TSave();
            values.SetMasterVolume(1f);
            values.SetSoundsVolume(1f);
            values.SetMusicVolume(1f);
            return values;
        }
    }

    // The options of a game without settings of its own.
    public class SettingsOptions : SettingsOptions<SettingsSave>
    {
    }
}
