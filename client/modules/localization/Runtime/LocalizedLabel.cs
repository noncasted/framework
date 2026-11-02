using System;
using Internal;
using TMPro;

namespace Global.Localization
{
    // Keeps a text component in the current language: the text set last is shown again on a language change.
    // One per component; the component is never written past it, or a language change brings the old text back.
    public class LocalizedLabel
    {
        public LocalizedLabel(IReadOnlyLifetime lifetime, TMP_Text label)
        {
            _label = label;
            Localizer.Current.Advise(lifetime, (_, _) => Apply());
        }

        private readonly TMP_Text _label;

        private LocalizedText _text = LocalizedText.Empty;

        public TMP_Text Component => _label;
        public LocalizedText Text => _text;

        public void Set(LocalizedText text)
        {
            _text = text;
            Apply();
        }

        private void Apply()
        {
            // The text setter of a destroyed TMP_Text does not throw: the subscription would go on unnoticed.
            if (_label == null)
                throw new InvalidOperationException("[Localization] The label is destroyed before the lifetime it was given");

            _label.text = _text.Resolve();
        }
    }
}
