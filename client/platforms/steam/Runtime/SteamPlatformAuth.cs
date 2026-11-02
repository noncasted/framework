using System;
using Cysharp.Threading.Tasks;
using Internal;
#if UNITY_STANDALONE
using System.Text;
using Steamworks;
#endif

namespace Global.Publisher.Steam
{
    // The Steam user: a ticket for the Web API (the server checks it with Steam and takes the SteamID from the answer)
    // and the persona name, which becomes the name of the user.
    public class SteamPlatformAuth : IPlatformAuth
    {
        public SteamPlatformAuth(SteamOptions options)
        {
            _options = options;
        }

        private readonly SteamOptions _options;

#if UNITY_STANDALONE
        private HAuthTicket _ticket = HAuthTicket.Invalid;
#endif

        public PlatformAuthType Type => PlatformAuthType.Steam;

        public async UniTask<PlatformCredentials> GetCredentials(IReadOnlyLifetime lifetime)
        {
#if UNITY_STANDALONE
            // The ticket of the previous connect has been checked by the server already.
            if (_ticket != HAuthTicket.Invalid)
                SteamUser.CancelAuthTicket(_ticket);

            var completion = new UniTaskCompletionSource<string>();
            var handle = HAuthTicket.Invalid;

            using var callback = Callback<GetTicketForWebApiResponse_t>.Create(OnTicket);

            handle = SteamUser.GetAuthTicketForWebApi(_options.WebApiIdentity);

            if (handle == HAuthTicket.Invalid)
                throw new InvalidOperationException("[Steam] GetAuthTicketForWebApi returned no ticket");

            _ticket = handle;

            var ticket = await completion.Task.AttachExternalCancellation(lifetime.Token);

            return new PlatformCredentials(Type, ticket, SteamFriends.GetPersonaName());

            void OnTicket(GetTicketForWebApiResponse_t response)
            {
                if (response.m_hAuthTicket != handle)
                    return;

                if (response.m_eResult != EResult.k_EResultOK)
                {
                    completion.TrySetException(
                        new InvalidOperationException($"[Steam] The Web API ticket is refused: {response.m_eResult}"));
                    return;
                }

                var hex = new StringBuilder(response.m_cubTicket * 2);

                for (var index = 0; index < response.m_cubTicket; index++)
                    hex.Append(response.m_rgubTicket[index].ToString("X2"));

                completion.TrySetResult(hex.ToString());
            }
#else
            await UniTask.CompletedTask;
            throw new PlatformNotSupportedException("[Steam] Steamworks is only in standalone builds");
#endif
        }
    }
}
