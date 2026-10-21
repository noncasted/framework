using System.IO;
using UnityEngine;

namespace Internal
{
    /// <summary>
    /// Writes the frame dump. The dump is read not in the editor but outside - it is used to analyze
    /// where the time went inside the trace scopes, so the window does not need it and does not load it.
    /// </summary>
    public static class ProfilerFramesStorage
    {
        public static void Save(string path, ProfilerFramesData frames)
        {
            var directory = Path.GetDirectoryName(path);

            if (string.IsNullOrEmpty(directory) == false && Directory.Exists(directory) == false)
                Directory.CreateDirectory(directory);

            File.WriteAllText(path, JsonUtility.ToJson(frames));
        }
    }
}