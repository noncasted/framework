using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Internal
{
    // Reads the components of a prefab or a scene from its yaml, line by line: only the fields the asset map needs.
    internal static class YamlBlockParser
    {
        private static readonly Regex FileIdHeader = new Regex(@"^--- !u!114 &(-?\d+)", RegexOptions.Compiled);
        private static readonly Regex ScriptGuid = new Regex(@"guid: ([a-f0-9]{32})", RegexOptions.Compiled);
        private static readonly Regex FileIdRef = new Regex(@"fileID: (-?\d+)", RegexOptions.Compiled);

        public static List<YamlBlock> Parse(string[] lines)
        {
            var blocks = new List<YamlBlock>();
            YamlBlock current = null;
            string listField = null;

            foreach (var raw in lines)
            {
                var line = raw.TrimEnd('\r');
                var header = FileIdHeader.Match(line);

                if (header.Success)
                {
                    if (current != null)
                        blocks.Add(current);

                    current = new YamlBlock(header.Groups[1].Value);
                    listField = null;
                    continue;
                }

                if (current == null)
                    continue;

                var trimmed = line.Trim();

                if (trimmed.StartsWith("m_EditorClassIdentifier:", StringComparison.Ordinal))
                {
                    current.ReadClassIdentifier(trimmed.Substring("m_EditorClassIdentifier:".Length).Trim());
                    listField = null;
                    continue;
                }

                var guidMatch = ScriptGuid.Match(trimmed);

                if (trimmed.StartsWith("m_Script:", StringComparison.Ordinal) && guidMatch.Success)
                    current.ReadScriptGuid(guidMatch.Groups[1].Value);

                if (trimmed.StartsWith("m_CorrespondingSourceObject:", StringComparison.Ordinal))
                {
                    var correspondingGuid = ScriptGuid.Match(trimmed);

                    if (correspondingGuid.Success)
                        current.ReadCorrespondingGuid(correspondingGuid.Groups[1].Value);
                    var correspondingId = FileIdRef.Match(trimmed);

                    if (correspondingId.Success)
                        current.ReadCorrespondingFileId(correspondingId.Groups[1].Value);
                }

                if (trimmed == "_autoDetected:" || trimmed.StartsWith("_autoDetected:", StringComparison.Ordinal))
                {
                    listField = "auto";
                    ParseInlineList(current.AutoDetected, trimmed);
                    continue;
                }

                if (trimmed == "_register:" || trimmed.StartsWith("_register:", StringComparison.Ordinal))
                {
                    listField = "register";
                    ParseInlineList(current.Register, trimmed);
                    continue;
                }

                if (trimmed == "_services:" || trimmed.StartsWith("_services:", StringComparison.Ordinal))
                {
                    listField = "services";
                    ParseInlineList(current.Services, trimmed);
                    continue;
                }

                if (listField != null && trimmed.StartsWith("-", StringComparison.Ordinal))
                {
                    var fileId = FileIdRef.Match(trimmed);

                    if (fileId.Success)
                        ListFor(current, listField).Add(fileId.Groups[1].Value);
                    continue;
                }

                if (trimmed.StartsWith("_", StringComparison.Ordinal) ||
                    trimmed.StartsWith("m_", StringComparison.Ordinal))
                    listField = null;
            }

            if (current != null)
                blocks.Add(current);

            return blocks;
        }

        private static void ParseInlineList(List<string> target, string trimmed)
        {
            var index = trimmed.IndexOf('[');

            if (index < 0)
                return;

            var end = trimmed.IndexOf(']', index + 1);

            if (end < 0)
                return;

            var inner = trimmed.Substring(index + 1, end - index - 1);

            if (string.IsNullOrWhiteSpace(inner))
                return;

            foreach (Match match in FileIdRef.Matches(inner))
                target.Add(match.Groups[1].Value);
        }

        public static Dictionary<string, YamlBlock> Index(List<YamlBlock> blocks)
        {
            var byId = new Dictionary<string, YamlBlock>(StringComparer.Ordinal);

            foreach (var block in blocks)
            {
                if (string.IsNullOrEmpty(block.FileId) == false)
                    byId[block.FileId] = block;
            }

            return byId;
        }

        private static List<string> ListFor(YamlBlock block, string field)
        {
            if (field == "auto")
                return block.AutoDetected;

            if (field == "register")
                return block.Register;
            return block.Services;
        }
    }
}
