using UnityEditor;
using UnityEngine;

namespace Internal
{
    [InitializeOnLoad]
    public static class MockSwitcher
    {
        static MockSwitcher()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode)
                return;

            // Mocks also live in scenes loaded by the normal startup. It manages to load the menu
            // before EnteredPlayMode, and a mock from that scene would start a second bootstrap on top of the first.
            if (Object.FindAnyObjectByType<StartupEntry>() != null)
                return;

            var mock = Object.FindAnyObjectByType<MockEntry>();

            if (mock == null)
                return;

            mock.Process().Forget();
        }
    }
}
