using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Internal
{
    internal static class ContainerGraphAssetScanner
    {
        private const string SceneServicesFactoryGuid = "acd7e370b6b3474e8b51f677a9edd625";

        // A file with none of these holds nothing of the map: a big prefab of art is not parsed line by line after each save.
        private static readonly string[] Markers =
        {
            "_autoDetected", "_register", "_services", "ScopeEntity", "SceneServicesFactory", SceneServicesFactoryGuid
        };

        public static List<ContainerGraphAsset> Scan(string assetsRoot)
        {
            var results = new List<ContainerGraphAsset>();

            if (string.IsNullOrEmpty(assetsRoot) || Directory.Exists(assetsRoot) == false)
                return results;

            var projectRoot = Path.GetDirectoryName(assetsRoot);
            var resolver = new ContainerGraphTypeResolver(BuildPrefabMap(assetsRoot));
            ScanFiles(results, assetsRoot, projectRoot, resolver, "*.prefab", true);
            ScanFiles(results, assetsRoot, projectRoot, resolver, "*.unity", false);

            results.Sort((a, b) => {
                var path = string.CompareOrdinal(a.AssetPath, b.AssetPath);
                return path != 0 ? path : string.CompareOrdinal(a.HolderType, b.HolderType);
            });
            return results;
        }

        private static void ScanFiles(
            List<ContainerGraphAsset> results,
            string assetsRoot,
            string projectRoot,
            ContainerGraphTypeResolver resolver,
            string pattern,
            bool prefabs)
        {
            // A failure to read throws: taken for an empty result, it would delete what the generator did not see.
            var files = Directory.GetFiles(assetsRoot, pattern, SearchOption.AllDirectories);

            foreach (var file in files)
            {
                if (file.IndexOf($"{Path.DirectorySeparatorChar}Plugins{Path.DirectorySeparatorChar}",
                        StringComparison.Ordinal) >=
                    0)
                    continue;

                ParseFile(results, file, projectRoot, resolver, prefabs);
            }
        }

        private static void ParseFile(
            List<ContainerGraphAsset> results,
            string fullPath,
            string projectRoot,
            ContainerGraphTypeResolver resolver,
            bool prefab)
        {
            // A failure to read throws: taken for an empty result, it would delete what the generator did not see.
            var text = File.ReadAllText(fullPath);

            if (Array.TrueForAll(Markers, m => text.IndexOf(m, StringComparison.Ordinal) < 0))
                return;

            var lines = text.Split('\n');

            RequireNoVariantOverride(lines, fullPath);

            var blocks = YamlBlockParser.Parse(lines);
            var byId = YamlBlockParser.Index(blocks);
            var path = CatalogPaths.ToAssetPath(projectRoot, fullPath);

            foreach (var block in blocks)
            {
                if (prefab)
                {
                    if (block.AutoDetected.Count == 0 && block.Register.Count == 0)
                        continue;

                    if (IsEntityHolder(block) == false)
                        continue;

                    var types = resolver.Resolve(byId, block.AutoDetected, false);
                    AppendUnique(types, resolver.Resolve(byId, block.Register, false));
                    AddAsset(results, path, resolver.TypeName(block), types);
                    continue;
                }

                if (IsSceneFactory(block))
                {
                    AddAsset(
                        results,
                        path,
                        resolver.TypeName(block),
                        resolver.Resolve(byId, block.Services, true));
                    continue;
                }

                if (IsEntityHolder(block) == false)
                    continue;

                var entityTypes = resolver.Resolve(byId, block.AutoDetected, false);
                AppendUnique(entityTypes, resolver.Resolve(byId, block.Register, false));
                AddAsset(results, path, resolver.TypeName(block), entityTypes);
            }
        }

        private static bool IsEntityHolder(YamlBlock block)
        {
            if (block.AutoDetected.Count > 0 || block.Register.Count > 0)
                return true;

            var identifier = block.ClassIdentifier;

            return identifier.IndexOf("ScopeEntityView", StringComparison.Ordinal) >= 0 ||
                   identifier.IndexOf("ScopeEntity", StringComparison.Ordinal) >= 0;
        }

        private static bool IsSceneFactory(YamlBlock block)
        {
            if (block.ClassIdentifier.IndexOf("SceneServicesFactory", StringComparison.Ordinal) >= 0)
                return true;
            return string.Equals(block.ScriptGuid, SceneServicesFactoryGuid, StringComparison.OrdinalIgnoreCase);
        }

        private static void AddAsset(List<ContainerGraphAsset> results, string path, string holder, List<string> types)
        {
            if (string.IsNullOrEmpty(holder) && types.Count == 0)
                return;

            for (var i = 0; i < results.Count; i++)
            {
                var existing = results[i];

                if (existing.AssetPath != path || existing.HolderType != holder)
                    continue;

                var combined = new List<string>(existing.ComponentTypes);
                var seen = new HashSet<string>(combined, StringComparer.Ordinal);

                for (var t = 0; t < types.Count; t++)
                {
                    if (seen.Add(types[t]))
                        combined.Add(types[t]);
                }

                results[i] = new ContainerGraphAsset(path, holder, combined.ToArray());
                return;
            }

            results.Add(new ContainerGraphAsset(path, holder, types.ToArray()));
        }

        private static void AppendUnique(List<string> target, List<string> extra)
        {
            var seen = new HashSet<string>(target, StringComparer.Ordinal);

            for (var i = 0; i < extra.Count; i++)
            {
                if (seen.Add(extra[i]))
                    target.Add(extra[i]);
            }
        }

        internal static Dictionary<string, string> BuildPrefabMap(string assetsRoot)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            // A failure to read throws: taken for an empty result, it would delete what the generator did not see.
            var metas = Directory.GetFiles(assetsRoot, "*.prefab.meta", SearchOption.AllDirectories);

            var guidLine = new Regex(@"^guid: ([a-f0-9]{32})", RegexOptions.Compiled);

            foreach (var meta in metas)
            {
                string guid = null;

                // A meta that cannot be read throws: skipping it would drop the entities of its prefab from the map.
                foreach (var line in File.ReadLines(meta))
                {
                    var match = guidLine.Match(line.Trim());

                    if (match.Success == false)
                        continue;
                    guid = match.Groups[1].Value;
                    break;
                }

                if (string.IsNullOrEmpty(guid))
                    continue;

                var prefab = meta.Substring(0, meta.Length - ".meta".Length);

                if (File.Exists(prefab) == false)
                    continue;

                map[guid] = prefab;
            }

            return map;
        }

        // The blocks read here are the components (!u!114): a list overridden by a prefab variant lies in its PrefabInstance
        // (!u!1001), m_Modifications. It is not parsed, and without it the map holds the list of the base prefab.
        private static void RequireNoVariantOverride(string[] lines, string fullPath)
        {
            foreach (var line in lines)
            {
                var trimmed = line.TrimStart();

                if (trimmed.StartsWith("propertyPath: _autoDetected", StringComparison.Ordinal) == false &&
                    trimmed.StartsWith("propertyPath: _register", StringComparison.Ordinal) == false &&
                    trimmed.StartsWith("propertyPath: _services", StringComparison.Ordinal) == false)
                    continue;

                throw new InvalidOperationException(
                    $"[ContainerGraph] {fullPath} overrides a list of its base prefab ({trimmed}): not supported by the map, apply it to the base");
            }
        }
    }
}
