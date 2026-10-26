using UnityEngine;

namespace Internal
{
    /// <summary>
    /// Priority of background loading for the time of a setup. By default Unity gives the integration of async
    /// loading a few milliseconds per frame, so even an empty scene stretches over a dozen frames. While a loading
    /// screen is shown the smoothness of the frame is not needed: everything goes to loading, and on entering the
    /// game the previous priority comes back.
    /// </summary>
    public static class BackgroundLoading
    {
        private static ThreadPriority? _previous;

        public static void Boost()
        {
            if (_previous.HasValue)
                return;

            _previous = Application.backgroundLoadingPriority;
            Application.backgroundLoadingPriority = ThreadPriority.High;
        }

        public static void Restore()
        {
            if (_previous.HasValue == false)
                return;

            Application.backgroundLoadingPriority = _previous.Value;
            _previous = null;
        }
    }
}
