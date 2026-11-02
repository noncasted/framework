using System;
using System.Collections.Generic;
using System.IO;

namespace Internal
{
    // The class names of the components: by the script, by the class identifier, through the source prefab of a stripped block.
    internal sealed class ContainerGraphTypeResolver
    {
        public ContainerGraphTypeResolver(Dictionary<string, string> guidToPrefab)
        {
            _guidToPrefab = guidToPrefab;
        }

        private readonly ScriptTypes _scriptTypes = new ScriptTypes();
        private readonly Dictionary<string, string> _guidToPrefab;

        private readonly Dictionary<string, Dictionary<string, YamlBlock>> _prefabCache =
            new Dictionary<string, Dictionary<string, YamlBlock>>(StringComparer.OrdinalIgnoreCase);

        public string TypeName(YamlBlock block)
        {
            if (string.IsNullOrEmpty(block.ScriptGuid) == false &&
                _scriptTypes.TryGet(block.ScriptGuid, out var mapped))
                return mapped;

            var identifier = block.ClassIdentifier;
            var separator = identifier.IndexOf("::", StringComparison.Ordinal);

            if (separator >= 0 && separator + 2 < identifier.Length)
                return identifier.Substring(separator + 2);

            return identifier ?? "";
        }

        public List<string> Resolve(Dictionary<string, YamlBlock> byId, List<string> fileIds, bool sort)
        {
            var types = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var id in fileIds)
            {
                if (byId.TryGetValue(id, out var block) == false)
                    continue;

                var type = ResolveBlockType(block);

                if (string.IsNullOrEmpty(type) || seen.Add(type) == false)
                    continue;

                types.Add(type);
            }

            if (sort)
                types.Sort(StringComparer.Ordinal);
            return types;
        }

        private string ResolveBlockType(YamlBlock block)
        {
            var type = TypeName(block);

            if (string.IsNullOrEmpty(type) == false)
                return type;

            if (string.IsNullOrEmpty(block.CorrespondingGuid) || string.IsNullOrEmpty(block.CorrespondingFileId))
                return "";

            if (_guidToPrefab.TryGetValue(block.CorrespondingGuid, out var prefabPath) == false)
                return "";

            var prefabBlocks = PrefabBlocks(prefabPath);

            if (prefabBlocks.TryGetValue(block.CorrespondingFileId, out var source) == false)
                return "";
            return TypeName(source);
        }

        private Dictionary<string, YamlBlock> PrefabBlocks(string prefabPath)
        {
            if (_prefabCache.TryGetValue(prefabPath, out var existing))
                return existing;

            var map = YamlBlockParser.Index(YamlBlockParser.Parse(File.ReadAllLines(prefabPath)));

            _prefabCache[prefabPath] = map;
            return map;
        }
    }
}
