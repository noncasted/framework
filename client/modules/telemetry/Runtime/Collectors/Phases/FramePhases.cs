using System;
using UnityEngine.PlayerLoop;

namespace Global.Telemetry
{
    // The top systems of the player loop a frame is cut into, in their order. Initialization and TimeUpdate are short and stay
    // out: with the time between the frames they are the rest of a frame (FrameSpikesTelemetryCollector names it "browser").
    public static class FramePhases
    {
        public const int Early = 0;
        public const int Fixed = 1;
        public const int Pre = 2;
        public const int Update = 3;
        public const int PreLate = 4;
        public const int PostLate = 5;
        public const int Count = 6;

        public static readonly string[] Names = { "early", "fixed", "pre", "update", "prelate", "postlate" };

        public static readonly Type[] LoopTypes =
        {
            typeof(EarlyUpdate), typeof(FixedUpdate), typeof(PreUpdate), typeof(UnityEngine.PlayerLoop.Update), typeof(PreLateUpdate),
            typeof(PostLateUpdate)
        };
    }
}
