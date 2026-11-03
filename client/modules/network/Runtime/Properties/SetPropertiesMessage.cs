using System;
using System.Collections.Generic;
using MemoryPack;
using Shared;

namespace Internal
{
    // The bytes MemoryPack writes for OneWayMessageFromClient { Context = SharedSessionObject.SetProperties } through
    // IMessageFromClient, written straight from the serialized values of the properties: the message objects and a
    // byte[] per value are not made. The same bytes as the generated formatters (SetPropertiesMessageTests compares
    // them); a change of these types or of their tags changes this writer too.
    public static class SetPropertiesMessage
    {
        // [MemoryPackUnion(0, typeof(OneWayMessageFromClient))] of IMessageFromClient.
        private const ushort OneWayTag = 0;

        // OneWayMessageFromClient: Context. SetProperties: Entries. SetProperty: ObjectId, PropertyId, Value.
        private const byte OneWayMembers = 1;
        private const byte SetPropertiesMembers = 1;
        private const byte SetPropertyMembers = 3;

        public static void Write(NetworkBuffer buffer, IReadOnlyList<Entry> entries, NetworkBuffer values)
        {
            var state = MemoryPackWriterOptionalStatePool.Rent(null);

            try
            {
                var writer = new MemoryPackWriter<NetworkBuffer>(ref buffer, state);

                writer.WriteUnionHeader(OneWayTag);
                writer.WriteObjectHeader(OneWayMembers);
                writer.WriteUnionHeader(NetworkContextTags.ObjectSetProperties);
                writer.WriteObjectHeader(SetPropertiesMembers);
                writer.WriteCollectionHeader(entries.Count);

                for (var i = 0; i < entries.Count; i++)
                {
                    var entry = entries[i];

                    writer.WriteObjectHeader(SetPropertyMembers);
                    writer.WriteUnmanaged(entry.ObjectId);
                    writer.WriteUnmanaged(entry.PropertyId);
                    writer.WriteUnmanagedSpan(values.Span(entry.ValueStart, entry.ValueLength));
                }

                writer.Flush();
            }
            finally
            {
                ((IDisposable)state).Dispose();
            }
        }

        // A property written since the last sync: its value is the bytes ValueStart..+ValueLength of the values buffer.
        public readonly struct Entry
        {
            public Entry(int objectId, int propertyId, int valueStart, int valueLength)
            {
                ObjectId = objectId;
                PropertyId = propertyId;
                ValueStart = valueStart;
                ValueLength = valueLength;
            }

            public readonly int ObjectId;
            public readonly int PropertyId;
            public readonly int ValueStart;
            public readonly int ValueLength;
        }
    }
}
