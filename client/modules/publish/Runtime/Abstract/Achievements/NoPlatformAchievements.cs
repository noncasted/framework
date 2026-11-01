using System;

namespace Global.Publisher
{
    public class NoPlatformAchievements : IPlatformAchievements
    {
        public bool IsSupported => false;

        public void Unlock(string name)
        {
            throw new InvalidOperationException($"[Publisher] Achievement {name} is unlocked on a platform without achievements");
        }
    }
}
