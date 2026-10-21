using Cysharp.Threading.Tasks;
using Internal;

namespace Global.Publisher
{
    public enum PlatformAuthType
    {
        // The guest saved between runs (Meta.IAuthentication: its id and secret): web, itch.
        Guest,

        // The signed Telegram.WebApp.initData: nothing of the guest is sent or saved.
        Telegram,

        // A Steam auth ticket for the Web API and the persona name: nothing of the guest is sent or saved.
        Steam
    }

    // A ticket of the Steam user for the server (hex, checked by the Steam Web API) and the name the user has in Steam.
    public readonly struct SteamAuthTicket
    {
        public SteamAuthTicket(string ticket, string name)
        {
            Ticket = ticket;
            Name = name;
        }

        public string Ticket { get; }
        public string Name { get; }
    }

    // Who the user is for the meta socket: MetaLoop puts it into the first frame (SharedBackendSocketAuth.Request).
    public interface IPlatformAuth
    {
        PlatformAuthType Type { get; }

        // Telegram.WebApp.initData as is (url-encoded fields). Only for PlatformAuthType.Telegram.
        string GetTelegramInitData();

        // A new ticket for each connect. Only for PlatformAuthType.Steam.
        UniTask<SteamAuthTicket> GetSteamTicket(IReadOnlyLifetime lifetime);
    }
}
