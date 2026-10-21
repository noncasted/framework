using UnityEngine;
using UnityEngine.UI;

namespace Global.UI
{
    [DisallowMultipleComponent]
    public class LoadingScreenAnimation : MonoBehaviour
    {
        [SerializeField] private float _loopTime;
        [SerializeField] private Sprite[] _sequence;
        [SerializeField] private Image _image;

        // Frames trimmed to their content (Aseprite): the image takes the size of each frame times this and its pivot.
        // 0 - the frames are of one size, the image keeps its rect.
        [SerializeField, Min(0f)] private float _pixelScale;

        private float _time;

        public void UpdateAnimation(float delta)
        {
            if (_sequence.Length == 0)
                return;

            _time += delta;

            var index = Mathf.FloorToInt(_time / _loopTime * _sequence.Length) % _sequence.Length;
            var sprite = _sequence[index];

            if (_image.sprite == sprite)
                return;

            _image.sprite = sprite;

            if (_pixelScale == 0f)
                return;

            // Aseprite trims every frame to its content, so frames differ in size.
            // The sprite pivot is the same canvas point in every frame: pin it to the rect position.
            var size = sprite.rect.size;
            var rectTransform = _image.rectTransform;
            rectTransform.pivot = sprite.pivot / size;
            rectTransform.sizeDelta = size * _pixelScale;
        }
    }
}