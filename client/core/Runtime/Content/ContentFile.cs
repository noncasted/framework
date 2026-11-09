using System;
using UnityEngine;

namespace Internal
{
    // A file of a built content directory: its name in the folder (the content hash and the extension) and its size,
    // the size the store writes as zeros until the real file is fetched.
    [Serializable]
    public struct ContentFile : IEquatable<ContentFile>
    {
        public ContentFile(string name, long size)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("A content file needs a name", nameof(name));

            if (size < 0)
                throw new ArgumentOutOfRangeException(nameof(size), size, $"Negative size of the content file {name}");

            _name = name;
            _size = size;
        }

        [SerializeField] private string _name;
        [SerializeField] private long _size;

        public string Name => _name;
        public long Size => _size;

        public bool Equals(ContentFile other) => _name == other._name && _size == other._size;
        public override bool Equals(object obj) => obj is ContentFile other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(_name, _size);
        public override string ToString() => $"{_name} ({_size} bytes)";
    }
}
