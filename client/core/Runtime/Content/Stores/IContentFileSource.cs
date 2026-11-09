using Cysharp.Threading.Tasks;

namespace Internal
{
    // Brings one file of a content directory to a local path, over what was there (zeros of its size). Throws
    // OperationCanceledException when the lifetime ends first and then writes nothing.
    internal interface IContentFileSource
    {
        UniTask Fetch(IReadOnlyLifetime lifetime, ContentFile file, string path);
    }
}
