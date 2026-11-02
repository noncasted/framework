using UnityEngine;

namespace Internal
{
    public class LatencyClock : ILatencyClock
    {
        public double Now => Time.realtimeSinceStartupAsDouble;
        public float FrameDelta => Time.unscaledDeltaTime;
    }
}
