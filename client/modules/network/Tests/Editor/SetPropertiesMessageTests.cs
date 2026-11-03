using System;
using System.Collections.Generic;
using MemoryPack;
using NUnit.Framework;
using Shared;

namespace Internal.Tests
{
    // SetPropertiesMessage writes the bytes of the generated formatters: the backend reads the message as before.
    public class SetPropertiesMessageTests
    {
        // OneWay tag 0, 1 member; SetProperties tag 33, 1 member; 1 entry; SetProperty 3 members: object 1048577,
        // property 7, value Serialize(5) = 4 bytes.
        private const string GoldenHex = "00012101010000000301001000070000000400000005000000";

        [Test]
        public void Write_OneIntValue_GoldenBytes()
        {
            var values = new NetworkBuffer();
            var property = new NetworkProperty<int>(7);
            property.Set(5);
            property.Collect(values);

            var entries = new List<SetPropertiesMessage.Entry> { new(1048577, 7, 0, values.Count) };

            Assert.AreEqual(GoldenHex, Hex(Write(entries, values)));
            Assert.AreEqual(GoldenHex, Hex(Generated(new[] { (1048577, 7, MemoryPackSerializer.Serialize(5)) })));
        }

        [Test]
        public void Write_SeveralValues_SameBytesAsGeneratedFormatters()
        {
            var sources = new (int ObjectId, int PropertyId, byte[] Value)[]
            {
                (1, 0, MemoryPackSerializer.Serialize(-3)),
                (1048577, 12, MemoryPackSerializer.Serialize("text")),
                (2, 1, MemoryPackSerializer.Serialize(new List<int>(Range(500)))),
                (3, 2, MemoryPackSerializer.Serialize<string>(null)),
                (int.MaxValue, int.MinValue, Array.Empty<byte>())
            };

            var values = new NetworkBuffer(16);
            var entries = new List<SetPropertiesMessage.Entry>();

            foreach (var (objectId, propertyId, value) in sources)
            {
                entries.Add(new SetPropertiesMessage.Entry(objectId, propertyId, values.Count, value.Length));
                value.CopyTo(values.GetSpan(value.Length));
                values.Advance(value.Length);
            }

            CollectionAssert.AreEqual(Generated(sources), Write(entries, values));
        }

        // The collected value is the bytes of Collect() (the creation of an entity sends those).
        [Test]
        public void CollectIntoBuffer_SameBytesAsCollect()
        {
            var property = new NetworkProperty<List<int>>(0);
            property.Set(new List<int>(Range(100)));

            var buffer = new NetworkBuffer(8);
            buffer.GetSpan(3);
            buffer.Advance(3);
            property.Collect(buffer);

            CollectionAssert.AreEqual(property.Collect(), buffer.Span(3, buffer.Count - 3).ToArray());
        }

        private static byte[] Write(IReadOnlyList<SetPropertiesMessage.Entry> entries, NetworkBuffer values)
        {
            var message = new NetworkBuffer(8);
            SetPropertiesMessage.Write(message, entries, values);
            return message.Written.ToArray();
        }

        private static byte[] Generated(IEnumerable<(int ObjectId, int PropertyId, byte[] Value)> sources)
        {
            var entries = new List<SharedSessionObject.SetProperty>();

            foreach (var (objectId, propertyId, value) in sources)
                entries.Add(new SharedSessionObject.SetProperty { ObjectId = objectId, PropertyId = propertyId, Value = value });

            IMessageFromClient message = new OneWayMessageFromClient
            {
                Context = new SharedSessionObject.SetProperties { Entries = entries }
            };

            return MemoryPackSerializer.Serialize(message);
        }

        private static IEnumerable<int> Range(int count)
        {
            for (var i = 0; i < count; i++)
                yield return i * 7919;
        }

        private static string Hex(byte[] bytes)
        {
            return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        }
    }
}
