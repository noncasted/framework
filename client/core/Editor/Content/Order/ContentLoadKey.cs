using System;

namespace Internal
{
    // An object (the GUID of its asset and its local id) or a scene (its GUID) a load of the game asked the content
    // directory for: a line of the load order.
    public readonly struct ContentLoadKey : IEquatable<ContentLoadKey>
    {
        private ContentLoadKey(string guid, long localId, bool isScene)
        {
            if (string.IsNullOrEmpty(guid))
                throw new ArgumentException("[Content] A load key without a GUID", nameof(guid));

            Guid = guid;
            LocalId = localId;
            IsScene = isScene;
        }

        public string Guid { get; }
        public long LocalId { get; }
        public bool IsScene { get; }

        public static ContentLoadKey Object(string guid, long localId)
        {
            return new ContentLoadKey(guid, localId, false);
        }

        public static ContentLoadKey Scene(string guid)
        {
            return new ContentLoadKey(guid, 0, true);
        }

        // "object <guid> <local id>" or "scene <guid>".
        public static ContentLoadKey Parse(string line)
        {
            var parts = line.Split(' ');

            if (parts.Length == 3 && parts[0] == "object" && long.TryParse(parts[2], out var localId))
                return Object(parts[1], localId);

            if (parts.Length == 2 && parts[0] == "scene")
                return Scene(parts[1]);

            throw new FormatException($"[Content] '{line}' is no line of a load order: 'object <guid> <local id>' or 'scene <guid>'");
        }

        public override string ToString()
        {
            return IsScene ? $"scene {Guid}" : $"object {Guid} {LocalId}";
        }

        public bool Equals(ContentLoadKey other)
        {
            return Guid == other.Guid && LocalId == other.LocalId && IsScene == other.IsScene;
        }

        public override bool Equals(object obj)
        {
            return obj is ContentLoadKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Guid, LocalId, IsScene);
        }
    }
}
