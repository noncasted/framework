using Internal;
using UnityEngine;

namespace Global.FrameRate
{
    // The last decision of the automatic frame rate in the local storage of the device: the next start begins with it. A rate
    // belongs to the device, not to the player, so it is not in the saves that follow the player.
    public class AutoFrameRateMemory
    {
        private const string Key = "FrameRate.AutoLow";

        public bool IsLow => PlayerPrefs.GetInt(LocalStorageKeys.Get(Key), 0) == 1;

        public void Remember(bool isLow)
        {
            PlayerPrefs.SetInt(LocalStorageKeys.Get(Key), isLow == true ? 1 : 0);
            PlayerPrefs.Save();
        }

        public void Forget()
        {
            PlayerPrefs.DeleteKey(LocalStorageKeys.Get(Key));
            PlayerPrefs.Save();
        }
    }
}
