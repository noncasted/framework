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

        public UniTask<PlatformCredentials> GetCredentials(IReadOnlyLifetime lifetime)
        {
            var initData = _api.GetInitData();

            // The template does not start the game outside Telegram; an empty value is a ?tgDebug=1 run without data.
            if (string.IsNullOrEmpty(initData) == true)
                throw new InvalidOperationException("[Telegram] Init data is empty: not inside Telegram");

            return UniTask.FromResult(new PlatformCredentials(Type, initData, null));
        }
    }
}
