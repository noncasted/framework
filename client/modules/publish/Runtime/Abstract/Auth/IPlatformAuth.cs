using Cysharp.Threading.Tasks;
using Internal;

namespace Global.Publisher
{
    public enum PlatformAuthType
    {
        // The guest saved between runs (its id and secret): web, itch.
        Guest,

        // The signed Telegram.WebApp.initData: nothing of the guest is sent or saved.
        Telegram,

        // A Steam auth ticket for the Web API and the persona name: nothing of the guest is sent or saved.
        Steam
    }

    // What the platform says about the user. Ticket: nothing for a guest (the game has its own id and secret), the signed
    // initData for Telegram, the hex ticket of the Web API for Steam. Name: the name the user has in the platform, if any.
    public readonly struct PlatformCredentials
    {
        public PlatformCredentials(PlatformAuthType type, string ticket, string name)
        {
            Type = type;
            Ticket = ticket;
            Name = name;
        }

        public PlatformAuthType Type { get; }
        public string Ticket { get; }
        public string Name { get; }
    }

    // Who the user is for the meta socket: MetaLoop puts it into the first frame (SharedBackendSocketAuth.Request).
    public interface IPlatformAuth
    {
        PlatformAuthType Type { get; }

        // New credentials for each connect.
        UniTask<PlatformCredentials> GetCredentials(IReadOnlyLifetime lifetime);
    }
}
