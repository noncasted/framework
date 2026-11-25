using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;

namespace Internal
{
    // List of catalog groups: those added explicitly by hand (json) plus those found
    // in already tagged assets. The json keeps an empty group from disappearing until its first asset.
    // It also stores group settings that cannot be derived from asset markup.
    [NoAutoStaticsCleanup]
    public abstract class CatalogGroupsRegistry
    {
        private static readonly JsonSerializerSettings JsonSettings = new()
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore
        };

        private static readonly JsonSerializerSettings ReadSettings = new() { MissingMemberHandling = MissingMemberHandling.Error };

        private readonly ILog _log;

        private string[] _cachedGroups;
        private HashSet<string> _cachedPreloaded;

        protected CatalogGroupsRegistry(string logTag, string assetPath)
        {
            _log = Logs.Of(GetType());
            LogTag = logTag;
            AssetPath = assetPath;
        }

        public string AssetPath { get; }

        protected string LogTag { get; }

        public IReadOnlyList<string> GetGroups()
        {
            if (_cachedGroups != null)
                return _cachedGroups;

            var names = new SortedSet<string>(StringComparer.Ordinal);

            foreach (var group in ReadFile().groups)
                names.Add(group);

            foreach (var group in DiscoverGroups())
                names.Add(group);

            _cachedGroups = new string[names.Count];
            names.CopyTo(_cachedGroups);
            return _cachedGroups;
        }

        public bool TryAddGroup(string name, out string groupName)
        {
            groupName = NormalizeGroup(name);

            if (string.IsNullOrEmpty(groupName))
            {
                _log.Error("Group name is empty after sanitize.");
                return false;
            }

            var file = ReadFile();

            foreach (var existing in file.groups)
            {
                if (string.Equals(existing, groupName, StringComparison.OrdinalIgnoreCase))
                {
                    groupName = existing;
                    Invalidate();
                    return true;
                }
            }

            file.groups.Add(groupName);
            WriteFile(file);
            Invalidate();
            return true;
        }

        // A group loads on Retain by default: the json lists only the preloaded ones (ContentRoot.PreloadedGroups).
        public bool IsPreloaded(string group)
        {
            _cachedPreloaded ??= new HashSet<string>(ReadFile().preloaded ?? new List<string>(), StringComparer.Ordinal);

            return _cachedPreloaded.Contains(CatalogNaming.ToGroupName(group));
        }

        // Returns true if the setting changed and the catalog must be regenerated.
        public bool SetPreloaded(string group, bool isPreloaded)
        {
            var groupName = CatalogNaming.ToGroupName(group);

            if (string.IsNullOrEmpty(groupName) || IsPreloaded(groupName) == isPreloaded)
                return false;

            var file = ReadFile();
            var preloaded = file.preloaded ?? new List<string>();

            if (isPreloaded)
                preloaded.Add(groupName);
            else
                preloaded.RemoveAll(name => string.Equals(name, groupName, StringComparison.Ordinal));

            preloaded.Sort(StringComparer.Ordinal);
            file.preloaded = preloaded.Count > 0 ? preloaded : null;
            WriteFile(file);
            Invalidate();
            return true;
        }

        public void Invalidate()
        {
            _cachedGroups = null;
            _cachedPreloaded = null;
        }

        // How a name typed by hand turns into a group name.
        protected virtual string NormalizeGroup(string name)
        {
            return CatalogNaming.ToGroupName(name);
        }

        // Groups derived from the markup of the assets themselves.
        protected abstract IEnumerable<string> DiscoverGroups();

        // An unreadable file (merge conflict markers, an unknown key) is not an empty registry: the generators would take
        // every preloaded group for a lazy one, and the next write would replace the file.
        private RegistryFile ReadFile()
        {
            var fullPath = CatalogPaths.ToFullPath(AssetPath);

            if (File.Exists(fullPath) == false)
                return new RegistryFile();

            RegistryFile file;

            try
            {
                file = JsonConvert.DeserializeObject<RegistryFile>(File.ReadAllText(fullPath), ReadSettings);
            }
            catch (JsonException exception)
            {
                throw new InvalidOperationException($"[{LogTag}] {AssetPath} is not valid json or has an unknown key (nonAddressable is replaced by preloaded). Fix the file.",
                    exception);
            }

            if (file == null)
                throw new InvalidOperationException($"[{LogTag}] {AssetPath} is empty. Fix or delete the file.");

            file.groups = file.groups?.FindAll(group => string.IsNullOrWhiteSpace(group) == false) ??
                          new List<string>();

            return file;
        }

        private void WriteFile(RegistryFile file)
        {
            var fullPath = CatalogPaths.ToFullPath(AssetPath);
            var directory = Path.GetDirectoryName(fullPath);

            if (string.IsNullOrEmpty(directory) == false && Directory.Exists(directory) == false)
                Directory.CreateDirectory(directory);

            File.WriteAllText(fullPath, JsonConvert.SerializeObject(file, JsonSettings));
            AssetDatabase.ImportAsset(AssetPath);
        }

        [Serializable]
        private sealed class RegistryFile
        {
            public List<string> groups = new();
            public List<string> preloaded;
        }
    }
}
