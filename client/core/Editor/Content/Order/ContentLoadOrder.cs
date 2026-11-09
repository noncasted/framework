using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;

namespace Internal
{
    // The order in which a run of the game first asked for the objects and scenes of a content directory,
    // ProjectSettings/ContentLoadOrder/<name>.txt of the game (committed): the archive puts their files first, in this
    // order, so a load finds its files together (ContentArchiveOrder). Recorded in Play Mode while
    // Tools/Content/Record Load Order is on; a key the directory no longer has is skipped by the build.
    public static class ContentLoadOrder
    {
        private const string Folder = "ProjectSettings/ContentLoadOrder";
        private const string RecordingKey = "Framework.Content.RecordLoadOrder";

        public static string PathOf(string name)
        {
            return $"{Folder}/{name}.txt";
        }

        // Empty without a file: the archive keeps the order of the manifest.
        public static IReadOnlyList<ContentLoadKey> Read(string name)
        {
            var path = PathOf(name);

            if (File.Exists(path) == false)
                return new ContentLoadKey[0];

            return File.ReadAllLines(path)
                .Where(line => line.Length > 0)
                .Select(ContentLoadKey.Parse)
                .ToArray();
        }

        // Per project: the editor prefs are shared by every project of the machine.
        public static bool IsRecording
        {
            get => EditorPrefs.GetBool(RecordingPrefKey, false);
            set => EditorPrefs.SetBool(RecordingPrefKey, value);
        }

        private static string RecordingPrefKey => $"{RecordingKey}.{Path.GetFullPath(".")}";

        public static void Clear(string name)
        {
            var path = PathOf(name);

            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
