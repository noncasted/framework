using System;
using Internal;
using UnityEngine;

namespace Global.UI
{
    // ISafeArea from Screen.safeArea: editor (Device Simulator), native builds. Substitute replaces it for Play checks
    // (a notch in the plain Game view), like GamepadReader.Substitute.
    public class ScreenSafeArea : ISafeArea, IScopeBaseSetup, IUpdatable
    {
        public ScreenSafeArea(IUpdater updater)
        {
            _updater = updater;
        }

        private readonly IUpdater _updater;
        private readonly ViewableProperty<Rect> _area = new();

        private Func<Rect> _substitute;

        public IViewableProperty<Rect> Area => _area;

        public void OnBaseSetup(IReadOnlyLifetime lifetime)
        {
            OnUpdate(0f);
            _updater.Add(lifetime, this);
        }

        public void OnUpdate(float delta)
        {
            _area.Set(_substitute == null ? Screen.safeArea : _substitute());
        }

        public void Substitute(IReadOnlyLifetime lifetime, Func<Rect> area)
        {
            _substitute = area;
            lifetime.Listen(() => _substitute = null);
        }
    }
}
