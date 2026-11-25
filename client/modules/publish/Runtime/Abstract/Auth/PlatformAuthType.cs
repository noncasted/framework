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
}
