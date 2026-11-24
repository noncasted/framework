using System;
using System.Collections.Generic;
using Shared;

namespace Internal
{
    // The source of an event type of a network object that takes the events that came before it (NetworkEvents).
    public interface INetworkEventSource
    {
        void Hold(IEventPayload payload);
    }

    // The source of one event type of a network object (NetworkEvents.GetEvent). The events that came before any
    // listener - the ones NetworkEvents kept for the type and the ones received after them - are held and handed to the
    // first listener in their order on its Advise, so a later event never overtakes them; from then on it is a plain
    // ViewableDelegate. The listener comes through the interface (GetEvent returns IViewableDelegate<T>): the interface
    // is implemented again here, so its Advise is this one.
    public class NetworkEventSource<T> : ViewableDelegate<T>, IViewableDelegate<T>, INetworkEventSource
        where T : IEventPayload
    {
        // Created with the first held event, dropped once the first listener has them.
        private List<T> _held;

        public void Hold(IEventPayload payload)
        {
            _held ??= new List<T>();
            _held.Add((T)payload);
        }

        // An event from the session: after the held ones while they wait for the first listener.
        public void Receive(T payload)
        {
            if (_held != null)
            {
                _held.Add(payload);
                return;
            }

            Invoke(payload);
        }

        void IEventSourceBase<Action<T>>.Advise(IReadOnlyLifetime lifetime, Action<T> handler)
        {
            Advise(lifetime, handler);

            if (_held == null || lifetime.IsTerminated == true)
                return;

            var held = _held;
            _held = null;

            foreach (var payload in held)
                handler.Invoke(payload);
        }
    }
}
