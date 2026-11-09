using Cysharp.Threading.Tasks;

namespace Internal
{
    // Brings bytes of the archive of a content directory to the local archive at path, written at their own offset over
    // what was there (zeros). Throws OperationCanceledException when the lifetime ends first and then writes nothing.
    internal interface IContentRangeSource
    {
        UniTask Fetch(IReadOnlyLifetime lifetime, ContentRange range, string path);

        // Called once the archive at path is ready for its registration: the engine compares the times of the archive
        // with the ones it saw on the mount and refuses a changed one, so the writes after this keep them.
        void Pin(string path);
    }
}
