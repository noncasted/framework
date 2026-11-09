using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;

namespace Internal.Tests
{
    // A slow source of the ranges of an archive: a request waits until the test opens a file of it, then writes the range
    // full of 7s. Counts the requests started (by their first file) and the ones aborted by the end of their lifetime.
    internal sealed class GatedContentRangeSource : IContentRangeSource
    {
        public const byte Value = 7;

        private readonly List<(ContentRange Range, UniTaskCompletionSource Gate)> _gates = new();
        private readonly List<ContentRange> _started = new();
        private readonly List<ContentRange> _aborted = new();

        public IReadOnlyList<ContentRange> Started => _started;
        public IReadOnlyList<ContentRange> Aborted => _aborted;

        public void Open(ContentFile file)
        {
            _gates.Last(gate => gate.Range.Files.Contains(file)).Gate.TrySetResult();
        }

        public async UniTask Fetch(IReadOnlyLifetime lifetime, ContentRange range, string path)
        {
            var gate = new UniTaskCompletionSource();
            _gates.Add((range, gate));
            _started.Add(range);

            try
            {
                await gate.Task.AttachExternalCancellation(lifetime.Token);
            }
            catch (OperationCanceledException)
            {
                _aborted.Add(range);
                throw;
            }

            using var stream = new FileStream(path, FileMode.Open, FileAccess.Write);
            stream.Position = range.Offset;
            stream.Write(Enumerable.Repeat(Value, (int)range.Length).ToArray(), 0, (int)range.Length);
        }

        public void Pin(string path)
        {
        }
    }
}
