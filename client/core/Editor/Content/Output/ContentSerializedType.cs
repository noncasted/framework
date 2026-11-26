using System;

namespace Internal
{
    // A serialized class of the data of a bundle (a [Serializable] field, a [SerializeReference] instance), as the engine names
    // it in WriteResult.includedSerializeReferenceFQN: "<assembly>:<namespace>.<class>", a nested class as "Outer/Inner", a
    // generic one as "Name`1" - already the form of a type of a link.xml, so the name is kept as it is (as the
    // LinkXmlGenerator of the Scriptable Build Pipeline does).
    public readonly struct ContentSerializedType : IEquatable<ContentSerializedType>
    {
        private ContentSerializedType(string assembly, string fullName)
        {
            Assembly = assembly;
            FullName = fullName;
        }

        public string Assembly { get; }
        public string FullName { get; }

        public static ContentSerializedType Parse(string fqn)
        {
            var separator = fqn.IndexOf(':');

            if (separator <= 0 || separator == fqn.Length - 1)
                throw new FormatException($"[ContentLinkXml] A serialized class of a bundle is not <assembly>:<type>: '{fqn}'");

            return new ContentSerializedType(fqn.Substring(0, separator), fqn.Substring(separator + 1));
        }

        public bool Equals(ContentSerializedType other)
        {
            return Assembly == other.Assembly && FullName == other.FullName;
        }

        public override bool Equals(object obj)
        {
            return obj is ContentSerializedType other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Assembly, FullName);
        }

        public override string ToString()
        {
            return $"{Assembly}:{FullName}";
        }
    }
}
