namespace Global.Publisher
{
    // Achievements of the user in the platform (Steam: the achievements of the Steamworks app, by their API names).
    // Steam only so far (Steam.SteamPlatformAchievements, registered on every platform).
    public interface IPlatformAchievements
    {
        // False where the platform has no achievements (not Steam): nothing else may be called then.
        bool IsSupported { get; }

        // Unlocks the achievement once and for good; an unlocked one stays as it is. An unknown name is an error.
        void Unlock(string name);
    }
}
