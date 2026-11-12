using System;
using UnityEngine;

namespace Internal
{
    // The keys of PlayerPrefs (IndexedDB on the web) that belong to one player of this device: "<name>:<scope>".
    // - In the editor the scope is a stable hash of Application.dataPath: every copy of the project and every MPPM
    //   clone (it runs from Library/VP/<clone>) is its own player, and the same one between launches
    //   (string.GetHashCode is randomized per process on CoreCLR - it gave a new user on every editor start).
    // - Outside the editor the scope is a constant: the page path never splits the player.
    // The keys of a game live in the game; the keys that are not per player stay as they are (saves of a publisher).
    // Every write is followed by PlayerPrefs.Save(): on the web an unsaved value is lost with the tab.
    public static class LocalStorageKeys
    {
        // The guest of this device: its id and the secret the server issued, one record per backend environment -
        // a guest of the local backend is not a guest of production and must not overwrite it.
        public const string Guest = "Auth.Guest";

        private const string PlayerScope = "player";

        public static string Scope
        {
            get
            {
#if UNITY_EDITOR
                return Shared.StableHash.Get(Application.dataPath).ToString("x8");
#else
                return PlayerScope;
#endif
            }
        }

        public static string Get(string name)
        {
            // "<name>.<environment>" is the key of a record of that environment (Get(name, environment)).
            foreach (BackendEnvironment environment in Enum.GetValues(typeof(BackendEnvironment)))
            {
                if (name.EndsWith("." + environment, StringComparison.Ordinal) == true)
                    throw new ArgumentException(
                        $"The key {name} ends with an environment, it is taken by Get(name, environment)", nameof(name));
            }

            return $"{name}:{Scope}";
        }

        // "<name>.<environment>:<scope>": a record that belongs to one backend environment.
        public static string Get(string name, BackendEnvironment environment)
        {
            return $"{name}.{environment}:{Scope}";
        }
    }
}
