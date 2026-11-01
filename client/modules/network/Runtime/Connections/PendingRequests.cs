using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Shared;

namespace Internal
{
    // The requests of a writer waiting for their responses, by the request id.
    public class PendingRequests
    {
        private readonly Dictionary<int, UniTaskCompletionSource<INetworkContext>> _entries = new();

        public UniTaskCompletionSource<INetworkContext> Add(int requestId)
        {
            var completion = new UniTaskCompletionSource<INetworkContext>();
            _entries.Add(requestId, completion);
            return completion;
        }

        public void Remove(int requestId)
        {
            _entries.Remove(requestId);
        }

        public bool TryComplete(int requestId, INetworkContext response)
        {
            if (_entries.TryGetValue(requestId, out var completion) == false)
                return false;

            completion.TrySetResult(response);
            _entries.Remove(requestId);
            return true;
        }

        public bool TryFail(int requestId, Exception exception)
        {
            if (_entries.TryGetValue(requestId, out var completion) == false)
                return false;

            completion.TrySetException(exception);
            return true;
        }

        public void CancelAll()
        {
            // Copy: TrySetCanceled synchronously wakes the waiters, and they remove themselves from the dictionary.
            var pending = new List<UniTaskCompletionSource<INetworkContext>>(_entries.Values);
            _entries.Clear();

            foreach (var completion in pending)
                completion.TrySetCanceled();
        }
    }
}
