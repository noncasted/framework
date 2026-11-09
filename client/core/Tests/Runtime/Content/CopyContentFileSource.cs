using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;

namespace Internal.Tests
{
    // A source of the files of a content directory that copies them from its built folder, a frame later, and counts
    // the copies: the web fetch without a page.
    public sealed class CopyContentFileSource : IContentFileSource
    {
        public CopyContentFileSource(string folder)
        {
            _folder = folder;
        }

        private readonly string _folder;
        private readonly List<string> _copied = new();

        public IReadOnlyList<string> Copied => _copied;

        public async UniTask Fetch(IReadOnlyLifetime lifetime, ContentFile file, string path)
        {
            await UniTask.Yield(lifetime.Token);
            File.Copy(Path.Combine(_folder, file.Name), path, true);
            _copied.Add(file.Name);
        }
    }
}
