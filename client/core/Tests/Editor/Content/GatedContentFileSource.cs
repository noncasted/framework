using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;

namespace Internal.Tests
{
    // A slow source of content files: a download waits until the test opens it, then writes the file full of 7s. Counts
    // the downloads started and the ones aborted by the end of their lifetime.
    public sealed class GatedContentFileSource : IContentFileSource
    {
        public const byte Value = 7;

        private readonly Dictionary<string, UniTaskCompletionSource> _gates = new();
        private readonly List<string> _started = new();
        private readonly List<string> _aborted = new();

        public IReadOnlyList<string> Started => _started;
        public IReadOnlyList<string> Aborted => _aborted;

        public void Open(ContentFile file)
        {
            _gates[file.Name].TrySetResult();
        }

        public async UniTask Fetch(IReadOnlyLifetime lifetime, ContentFile file, string path)
        {
            var gate = new UniTaskCompletionSource();
            _gates[file.Name] = gate;
            _started.Add(file.Name);

            try
            {
                await gate.Task.AttachExternalCancellation(lifetime.Token);
            }
            catch (OperationCanceledException)
            {
                _aborted.Add(file.Name);
                throw;
            }

            File.WriteAllBytes(path, Enumerable.Repeat(Value, (int)file.Size).ToArray());
        }
    }
}
