using System;
using UnityEngine;

namespace Internal
{
    // A file of a built content directory: its name in the archive of the directory (the content hash and the extension)
    // and its span there, the compressed blocks that hold it and nothing else. Size is the bytes a player downloads for it.
    [Serializable]
    public struct ContentFile : IEquatable<ContentFile>
    {
        public ContentFile(string name, long offset, long size)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("A content file needs a name", nameof(name));

            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset), offset, $"Negative offset of the content file {name}");

            if (size <= 0)
                throw new ArgumentOutOfRangeException(nameof(size), size, $"The content file {name} has no bytes");

            _name = name;
            _offset = offset;
            _size = size;
        }

        [SerializeField] private string _name;
        [SerializeField] private long _offset;
        [SerializeField] private long _size;

        public string Name => _name;
        public long Offset => _offset;
        public long Size => _size;
        public long End => _offset + _size;

        public bool Equals(ContentFile other) => _name == other._name && _offset == other._offset && _size == other._size;
        public override bool Equals(object obj) => obj is ContentFile other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(_name, _offset, _size);
        public override string ToString() => $"{_name} [{_offset}, {End})";
    }
}
