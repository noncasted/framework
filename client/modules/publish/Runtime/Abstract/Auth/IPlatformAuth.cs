using Cysharp.Threading.Tasks;
using Internal;

namespace Global.Publisher
{
    // Who the user is for the meta socket: MetaLoop puts it into the first frame (SharedBackendSocketAuth.Request).
    public interface IPlatformAuth
    {
        PlatformAuthType Type { get; }

        // New credentials for each connect.
        UniTask<PlatformCredentials> GetCredentials(IReadOnlyLifetime lifetime);
    }
}
