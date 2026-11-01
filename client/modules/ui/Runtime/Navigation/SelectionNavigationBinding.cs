using UnityEngine;
using UnityEngine.UI;

namespace Global.UI
{
    // The navigation of the selected one is set here each frame, explicit, to the nearest candidate of the same window in
    // each direction (the automatic one of uGUI looks at all the selectables of all canvases); a horizontal slider
    // keeps left and right for its value. Its own navigation is given back when the selection leaves it.
    internal sealed class SelectionNavigationBinding
    {
        private Selectable _navigated;
        private Navigation _navigatedOwn;

        public void Bind(Selectable current, NavigationCandidates candidates)
        {
            if (_navigated != current)
            {
                Release();
                _navigated = current;
                _navigatedOwn = current.navigation;
            }

            var keepsHorizontal = current is Slider slider &&
                                  (slider.direction == Slider.Direction.LeftToRight ||
                                   slider.direction == Slider.Direction.RightToLeft);

            current.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = NearestSelectableFinder.Find(candidates, current, Vector2.up),
                selectOnDown = NearestSelectableFinder.Find(candidates, current, Vector2.down),
                selectOnLeft = keepsHorizontal == true ? null : NearestSelectableFinder.Find(candidates, current, Vector2.left),
                selectOnRight = keepsHorizontal == true ? null : NearestSelectableFinder.Find(candidates, current, Vector2.right)
            };
        }

        public void Release()
        {
            // a destroyed one has nothing to give back
            if (_navigated != null)
                _navigated.navigation = _navigatedOwn;

            _navigated = null;
        }
    }
}
