using System;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;

namespace Internal.Tests
{
    // A source of the ranges of the archive of a content directory that copies them from its built archive, a frame
    // later, and counts the files copied and the requests: the Range fetch of the web without a page. Like the page, it
    // keeps the time of the archive pinned once the store is ready for the registration.
    internal sealed class CopyContentRangeSource : IContentRangeSource
    {
        public CopyContentRangeSource(string archive)
        {
            _archive = archive;
        }

        private readonly string _archive;
        private readonly List<string> _copied = new();
        private DateTime? _pinned;

        public IReadOnlyList<string> Copied => _copied;
        public int Requests { get; private set; }

        public async UniTask Fetch(IReadOnlyLifetime lifetime, ContentRange range, string path)
        {
            await UniTask.Yield(lifetime.Token);

            var bytes = new byte[range.Length];

            using (var source = File.OpenRead(_archive))
            {
                source.Position = range.Offset;

                if (source.Read(bytes, 0, bytes.Length) != bytes.Length)
                    throw new EndOfStreamException($"{_archive} ends before {range}");
            }

            using (var target = new FileStream(path, FileMode.Open, FileAccess.Write))
            {
                target.Position = range.Offset;
                target.Write(bytes, 0, bytes.Length);
            }

            if (_pinned.HasValue)
                File.SetLastWriteTimeUtc(path, _pinned.Value);

            Requests++;

            foreach (var file in range.Files)
                _copied.Add(file.Name);
        }

        public void Pin(string path)
        {
            _pinned = File.GetLastWriteTimeUtc(path);
        }
    }
}
