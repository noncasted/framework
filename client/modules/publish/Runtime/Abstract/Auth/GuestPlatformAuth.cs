using System;
using Cysharp.Threading.Tasks;
using Internal;

namespace Global.Publisher
{
    public class GuestPlatformAuth : IPlatformAuth
    {
        public PlatformAuthType Type => PlatformAuthType.Guest;

        public UniTask<PlatformCredentials> GetCredentials(IReadOnlyLifetime lifetime)
        {
            return UniTask.FromResult(new PlatformCredentials(Type, null, null));
        }
    }
}
