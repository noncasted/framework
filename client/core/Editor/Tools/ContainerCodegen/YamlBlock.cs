using System.Collections.Generic;

namespace Internal
{
    // A component (!u!114) of a prefab or a scene as text: what the scanner of the container assets reads from it.
    internal sealed class YamlBlock
    {
        public YamlBlock(string fileId)
        {
            FileId = fileId;
        }

        public string FileId { get; }
        public string ClassIdentifier { get; private set; } = "";
        public string ScriptGuid { get; private set; } = "";
        public string CorrespondingGuid { get; private set; } = "";
        public string CorrespondingFileId { get; private set; } = "";
        public List<string> AutoDetected { get; } = new List<string>();
        public List<string> Register { get; } = new List<string>();
        public List<string> Services { get; } = new List<string>();

        public void ReadClassIdentifier(string identifier)
        {
            ClassIdentifier = identifier;
        }

        public void ReadScriptGuid(string guid)
        {
            ScriptGuid = guid;
        }

        public void ReadCorrespondingGuid(string guid)
        {
            CorrespondingGuid = guid;
        }

        public void ReadCorrespondingFileId(string fileId)
        {
            CorrespondingFileId = fileId;
        }
    }
}
