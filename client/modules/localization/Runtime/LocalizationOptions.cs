using Internal;
using UnityEngine;

namespace Global.Localization
{
    // The asset of the game (its GlobalAssets catalogue), passed to AddLocalization.
    public class LocalizationOptions : EnvAsset
    {
        // Localization.json: every text of the game in every language (LocalizationTable).
        [SerializeField] private TextAsset _table;

        public TextAsset Table => _table;
    }
}
