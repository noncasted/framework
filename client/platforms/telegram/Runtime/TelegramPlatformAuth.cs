using System;
using Cysharp.Threading.Tasks;
using Internal;

namespace Global.Publisher.Telegram
{
    public class TelegramPlatformAuth : IPlatformAuth
    {
        public TelegramPlatformAuth(ITelegramAPI api)
        {
            _api = api;
        }

        private readonly ITelegramAPI _api;

        public PlatformAuthType Type => PlatformAuthType.Telegram;

        public string GetTelegramInitData()
        {
            var initData = _api.GetInitData();

            // The template does not start the game outside Telegram; an empty value is a ?tgDebug=1 run without data.
            if (string.IsNullOrEmpty(initData) == true)
                throw new InvalidOperationException("[Telegram] Init data is empty: not inside Telegram");

            return initData;
        }

        public UniTask<SteamAuthTicket> GetSteamTicket(IReadOnlyLifetime lifetime)
        {
            throw new InvalidOperationException("[Telegram] A Steam ticket is requested in Telegram");
        }
    }
}
