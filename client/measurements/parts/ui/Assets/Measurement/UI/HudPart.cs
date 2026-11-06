using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Measurement
{
    /// <summary>
    ///     A camera canvas with a layout, an image, a TextMeshPro label and a button: the UI every game has.
    ///     Absent from the variants that strip uGUI and TextMeshPro.
    /// </summary>
    public sealed class HudPart : MonoBehaviour, IMeasurementPart
    {
        private const int Frames = 30;

        [SerializeField] private TMP_Text _label;
        [SerializeField] private Image _bar;
        [SerializeField] private Button _button;

        private int _clicks;

        public string Name => "hud";

        public void Init(TMP_Text label, Image bar, Button button)
        {
            _label = label;
            _bar = bar;
            _button = button;
        }

        public async UniTask<string> Run(CancellationToken cancellation)
        {
            _button.onClick.AddListener(() => _clicks++);

            for (var frame = 1; frame <= Frames; frame++)
            {
                _label.SetText("Frame {0} of {1}: {2:1}", frame, Frames, frame * 100f / Frames);
                _bar.fillAmount = frame / (float)Frames;
                _button.onClick.Invoke();

                await UniTask.Yield(cancellation);
            }

            Canvas.ForceUpdateCanvases();

            if (_label.textInfo.characterCount == 0)
                throw new InvalidOperationException("The TextMeshPro label laid out no characters");

            return $"{_label.textInfo.characterCount} characters, {_clicks} clicks";
        }
    }
}
