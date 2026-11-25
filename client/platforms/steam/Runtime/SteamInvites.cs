using System;
using Internal;
#if UNITY_STANDALONE
using Steamworks;
#endif

namespace Global.Publisher.Steam
{
    // Invites through Steam without a Steam lobby: the lobby of the game travels as a connect string. The user in a
    // lobby has it in the rich presence ("connect"), so the friends list offers "Join game"; the overlay dialog sends
    // invites with it. A friend that accepts gets GameRichPresenceJoinRequested_t while the game runs, or Steam starts
    // the game with the connect string on its command line.
    public class SteamInvites : IPlatformInvites, IScopeSetup
    {
        public SteamInvites(SteamOptions options, ILog<SteamInvites> log)
        {
            _connectKey = options.InviteConnectKey;
            _log = log;
        }

        // The connect string is "<key> <code>" (SteamOptions.InviteConnectKey): two words of the command line when
        // Steam starts the game.
        private readonly string _connectKey;

        private readonly ILog _log;

        private readonly ViewableProperty<string> _joinRequest = new(null);

        public bool IsSupported => true;
        public IViewableProperty<string> JoinRequest => _joinRequest;

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            var launchCode = FindCode(Environment.GetCommandLineArgs(), _connectKey);

            if (launchCode != null)
                _joinRequest.Set(launchCode);

#if UNITY_STANDALONE
            var callback = Callback<GameRichPresenceJoinRequested_t>.Create(OnJoinRequested);
            lifetime.Listen(callback.Dispose);
#endif
        }

        public void ConsumeJoinRequest()
        {
            if (_joinRequest.Value == null)
                throw new InvalidOperationException("[Steam] There is no join request to consume");

            _joinRequest.Set(null);
        }

        public void ShowInLobby(IReadOnlyLifetime lifetime, string lobbyCode)
        {
#if UNITY_STANDALONE
            if (SteamFriends.SetRichPresence("connect", $"{_connectKey} {lobbyCode}") == false)
                throw new InvalidOperationException("[Steam] The rich presence is not set");

            lifetime.Listen(SteamFriends.ClearRichPresence);
#else
            throw new PlatformNotSupportedException("[Steam] Steamworks is only in standalone builds");
#endif
        }

        public void OpenInviteDialog(string lobbyCode)
        {
            if (string.IsNullOrEmpty(lobbyCode) == true)
                throw new InvalidOperationException($"[Steam] An invite into a lobby without a code: '{lobbyCode}'");

#if UNITY_STANDALONE
            SteamFriends.ActivateGameOverlayInviteDialogConnectString($"{_connectKey} {lobbyCode}");
#else
            throw new PlatformNotSupportedException("[Steam] Steamworks is only in standalone builds");
#endif
        }

        // The code after ConnectKey, or null when the words have none.
        public static string FindCode(string[] words, string connectKey)
        {
            for (var index = 0; index < words.Length - 1; index++)
            {
                if (words[index] == connectKey)
                    return words[index + 1];
            }

            return null;
        }

#if UNITY_STANDALONE
        private void OnJoinRequested(GameRichPresenceJoinRequested_t request)
        {
            var code = FindCode(request.m_rgchConnect.Split(' ', StringSplitOptions.RemoveEmptyEntries), _connectKey);

            if (code == null)
                throw new InvalidOperationException($"[Steam] A join request without a lobby: '{request.m_rgchConnect}'");

            _log.Info("A join request into the lobby {Code}", code);
            _joinRequest.Set(code);
        }
#endif
    }
}
