using Internal;
using UnityEngine;

namespace Global.Settings
{
    // The defaults of the settings: an asset of the game (its GlobalAssets catalogue), passed to AddSettings. A game
    // with a derived save derives the asset class too (class GameSettingsOptions : SettingsOptions<GameSettingsSave>),
    // the field of the defaults is serialized as before.
    public abstract class SettingsOptions<TSave> : EnvAsset where TSave : SettingsSave, new()
    {
        [SerializeField] private TSave _defaultValues = new TSave
        {
            MasterVolume = 1f,
            SoundsVolume = 1f,
            MusicVolume = 1f
        };

        public TSave DefaultValues => _defaultValues;
    }

    // The options of a game without settings of its own.
    public class SettingsOptions : SettingsOptions<SettingsSave>
    {
    }
}
