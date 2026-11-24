using System;
using System.Collections.Generic;
using MemoryPack;
using MemoryPack.Formatters;
using NUnit.Framework;
using Shared;

namespace Internal.Tests
{
    // The events of a network object that come before the source of their type (NetworkEvents): kept in the order of
    // arrival, at most MaxPendingEvents, and handed to the first listener of the type after GetEvent, ahead of the
    // events that come after them. The union over IEventPayload is declared by a game: the fixture puts the one of
    // its own types in and gives the one of the game back after.
    public partial class NetworkEventsBufferTests
    {
        private MemoryPackFormatter<IEventPayload> _gameUnion;
        private Lifetime _lifetime;
        private NetworkEvents _events;

        [OneTimeSetUp]
        public void RegisterPayloads()
        {
            if (MemoryPackFormatterProvider.IsRegistered<IEventPayload>() == true)
                _gameUnion = CurrentUnion();

            MemoryPackFormatterProvider.Register<IEventPayload>(new DynamicUnionFormatter<IEventPayload>(
                (0, typeof(FirstEvent)),
                (1, typeof(SecondEvent))));
        }

        [OneTimeTearDown]
        public void RestorePayloads()
        {
            if (_gameUnion != null)
                MemoryPackFormatterProvider.Register(_gameUnion);
        }

        [SetUp]
        public void SetUp()
        {
            _lifetime = new Lifetime();

            // Nothing is sent and the object is not asked for anything: the events of the session come in by Invoke.
            _events = new NetworkEvents(null, null);
        }

        [TearDown]
        public void TearDown()
        {
            _lifetime.Terminate();
        }

        [Test]
        public void UnknownType_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _events.Invoke(First(1)));
        }

        [Test]
        public void Buffered_GoesToFirstListener_InOrder()
        {
            _events.Invoke(First(1));
            _events.Invoke(First(2));
            _events.Invoke(First(3));

            var received = Listen<FirstEvent>();

            Assert.AreEqual(new[] { 1, 2, 3 }, received, "the buffer in the order of arrival on the Advise");

            _events.Invoke(First(4));

            Assert.AreEqual(new[] { 1, 2, 3, 4 }, received, "a live event after the buffer");
            Assert.IsNotNull(_events.Entries[typeof(FirstEvent)] as ViewableDelegate<FirstEvent>,
                "the source is still a ViewableDelegate of its type");
        }

        [Test]
        public void Buffered_LiveBeforeListener_GoesAfterIt()
        {
            _events.Invoke(First(1));
            var source = _events.GetEvent<FirstEvent>();
            _events.Invoke(First(2));

            var received = new List<int>();
            source.Advise(_lifetime, payload => received.Add(payload.Value));

            Assert.AreEqual(new[] { 1, 2 }, received, "an event after the source does not overtake the buffer");
        }

        [Test]
        public void Buffered_NotToSecondListener()
        {
            _events.Invoke(First(1));
            _events.Invoke(First(2));

            var first = Listen<FirstEvent>();
            var second = Listen<FirstEvent>();

            Assert.AreEqual(new[] { 1, 2 }, first);
            Assert.IsEmpty(second, "the buffer goes to the first listener only");

            _events.Invoke(First(3));

            Assert.AreEqual(new[] { 1, 2, 3 }, first);
            Assert.AreEqual(new[] { 3 }, second, "the live events go to every listener");
        }

        [Test]
        public void OtherType_KeepsItsBuffer()
        {
            _events.Invoke(First(1));
            _events.Invoke(Second(7));
            _events.Invoke(First(2));

            var second = Listen<SecondEvent>();

            Assert.AreEqual(new[] { 7 }, second, "only the events of its type");

            var first = Listen<FirstEvent>();

            Assert.AreEqual(new[] { 1, 2 }, first, "the other type has kept its buffer and its order");
        }

        [Test]
        public void Overflow_Throws()
        {
            for (var value = 0; value < NetworkEvents.MaxPendingEvents; value++)
                _events.Invoke(First(value));

            var exception = Assert.Throws<InvalidOperationException>(() => _events.Invoke(Second(65)));

            StringAssert.Contains(nameof(SecondEvent), exception.Message, "the type nobody listens to");
        }

        private List<int> Listen<T>() where T : IValueEvent, IEventPayload
        {
            var received = new List<int>();
            _events.GetEvent<T>().Advise(_lifetime, payload => received.Add(payload.Value));

            return received;
        }

        private static byte[] First(int value)
        {
            return MemoryPackSerializer.Serialize<IEventPayload>(new FirstEvent(value));
        }

        private static byte[] Second(int value)
        {
            return MemoryPackSerializer.Serialize<IEventPayload>(new SecondEvent(value));
        }

        private static MemoryPackFormatter<IEventPayload> CurrentUnion()
        {
            var reader = new MemoryPackReader(ReadOnlySpan<byte>.Empty, MemoryPackReaderOptionalStatePool.Rent(null));
            var formatter = (MemoryPackFormatter<IEventPayload>)reader.GetFormatter<IEventPayload>();
            reader.Dispose();

            return formatter;
        }

        public interface IValueEvent
        {
            int Value { get; }
        }

        [MemoryPackable]
        public partial class FirstEvent : IEventPayload, IValueEvent
        {
            [MemoryPackConstructor]
            public FirstEvent(int value)
            {
                Value = value;
            }

            public int Value { get; }
        }

        [MemoryPackable]
        public partial class SecondEvent : IEventPayload, IValueEvent
        {
            [MemoryPackConstructor]
            public SecondEvent(int value)
            {
                Value = value;
            }

            public int Value { get; }
        }
    }
}
