using System.Runtime.InteropServices;
using Global.UI;
using Internal;
using UnityEngine;

namespace Global.Web
{
    // ISafeArea of the WebGL build: Unity does not know the safe area of a page, web-shell.js of the template measures
    // it and WebShell.jslib hands it over as fractions of the canvas. Re-read when the page reports a change or the
    // canvas (Screen) is resized.
    public class WebSafeArea : ISafeArea, IScopeBaseSetup, IUpdatable
    {
        public WebSafeArea(IUpdater updater)
        {
            _updater = updater;
        }

        private const int Left = 0;
        private const int Top = 1;
        private const int Right = 2;
        private const int Bottom = 3;

        private readonly IUpdater _updater;
        private readonly ViewableProperty<Rect> _area = new();

        private int _revision = -1;
        private int _width;
        private int _height;

        public IViewableProperty<Rect> Area => _area;

        // The functions of the .jslib exist only in a WebGL player: an IL2CPP standalone build fails to link them.
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern int WebShellSafeAreaRevision();

        [DllImport("__Internal")]
        private static extern float WebShellSafeAreaInset(int side);
#else
        private static int WebShellSafeAreaRevision() =>
            throw Internal.JsLibStub.Missing();

        private static float WebShellSafeAreaInset(int side) =>
            throw Internal.JsLibStub.Missing();
#endif

        public void OnBaseSetup(IReadOnlyLifetime lifetime)
        {
            OnUpdate(0f);
            _updater.Add(lifetime, this);
        }

        public void OnUpdate(float delta)
        {
            var revision = WebShellSafeAreaRevision();

            if (revision == _revision && Screen.width == _width && Screen.height == _height)
                return;

            _revision = revision;
            _width = Screen.width;
            _height = Screen.height;

            var left = Mathf.Round(WebShellSafeAreaInset(Left) * _width);
            var right = Mathf.Round(WebShellSafeAreaInset(Right) * _width);
            var top = Mathf.Round(WebShellSafeAreaInset(Top) * _height);
            var bottom = Mathf.Round(WebShellSafeAreaInset(Bottom) * _height);

            _area.Set(new Rect(left, bottom, _width - left - right, _height - top - bottom));
        }
    }
}
