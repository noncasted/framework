using System;
using Cysharp.Threading.Tasks;
using Internal;

namespace Global.Publisher
{
    public class GuestPlatformAuth : IPlatformAuth
    {
        public PlatformAuthType Type => PlatformAuthType.Guest;

        public string GetTelegramInitData()
        {
            throw new InvalidOperationException("[Publisher] Telegram init data is requested on a guest platform");
        }

        public UniTask<SteamAuthTicket> GetSteamTicket(IReadOnlyLifetime lifetime)
        {
            throw new InvalidOperationException("[Publisher] A Steam ticket is requested on a guest platform");
        }
    }
}
