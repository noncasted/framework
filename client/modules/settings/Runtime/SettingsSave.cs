using System;
using Global.Localization;
using UnityEngine;

namespace Global.Settings
{
    // What the settings keep in the saves (ISaves, JsonUtility: [field: SerializeField] properties). A game with
    // settings of its own derives it and adds its fields; the derived save is stored under the name of the derived
    // type, one JSON object with the fields of both. A field added later reads as its default from an older save:
    // name a new flag so that the default keeps the old behaviour (IsShakeOff, not IsShakeOn).
    [Serializable]
    public class SettingsSave
    {
        [field: SerializeField] public float MasterVolume { get; set; }
        [field: SerializeField] public float SoundsVolume { get; set; }
        [field: SerializeField] public float MusicVolume { get; set; }

        // The language of the texts. Until the player picks one (IsLanguagePicked) it follows the system language
        // (Localizer.FromSystem) at every start: saves written before the setting existed are not picked either.
        [field: SerializeField] public Language Language { get; set; }
        [field: SerializeField] public bool IsLanguagePicked { get; set; }

        // The player applied the settings at least once: a save that was never applied takes the defaults of the
        // options asset at every start (so a changed default reaches the players who never touched the settings).
        [field: SerializeField] public bool WasChanged { get; set; }

        // A copy of the same type with the same fields; WasChanged is not copied (Apply sets it).
        public static TSave Copy<TSave>(TSave source) where TSave : SettingsSave, new()
        {
            var copy = new TSave();
            copy.CopyFrom(source);
            return copy;
        }

        // The derived save copies its own fields after the base ones: base.CopyFrom(source), then its fields from
        // (TDerived)source.
        public virtual void CopyFrom(SettingsSave source)
        {
            MasterVolume = source.MasterVolume;
            SoundsVolume = source.SoundsVolume;
            MusicVolume = source.MusicVolume;
            Language = source.Language;
            IsLanguagePicked = source.IsLanguagePicked;
        }
    }
}
