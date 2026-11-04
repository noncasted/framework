using System;
using System.Collections.Generic;
using System.Diagnostics;
using Internal;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;

namespace Global.FrameRate
{
    // The work of the last frame without the wait for the screen: from the start of EarlyUpdate to the end of PostLateUpdate
    // (the rendering included), by two markers of the player loop, put in for a lifetime. The markers of the telemetry
    // (FramePhaseLoop) keep these: each edit takes out only its own type. Stopwatch only, nothing allocated in a frame.
    public class FrameWorkMeter
    {
        public FrameWorkMeter()
        {
            _begin = Begin;
            _end = End;
        }

        private static readonly double _msPerTick = 1000d / Stopwatch.Frequency;

        private readonly PlayerLoopSystem.UpdateFunction _begin;
        private readonly PlayerLoopSystem.UpdateFunction _end;

        private long _started;

        public double LastWorkMs { get; private set; }

        public void Run(IReadOnlyLifetime lifetime)
        {
            LastWorkMs = 0d;
            PlayerLoop.SetPlayerLoop(With(PlayerLoop.GetCurrentPlayerLoop()));
            lifetime.Listen(() => PlayerLoop.SetPlayerLoop(Without(PlayerLoop.GetCurrentPlayerLoop())));
        }

        private void Begin()
        {
            _started = Stopwatch.GetTimestamp();
        }

        private void End()
        {
            LastWorkMs = (Stopwatch.GetTimestamp() - _started) * _msPerTick;
        }

        private PlayerLoopSystem With(PlayerLoopSystem root)
        {
            root = Without(root);
            var phases = root.subSystemList;

            for (var i = 0; i < phases.Length; i++)
            {
                if (phases[i].type == typeof(EarlyUpdate))
                    phases[i].subSystemList = Insert(phases[i].subSystemList, 0, _begin);
                else if (phases[i].type == typeof(PostLateUpdate))
                    phases[i].subSystemList = Insert(phases[i].subSystemList, -1, _end);
            }

            return root;
        }

        private static PlayerLoopSystem[] Insert(PlayerLoopSystem[] systems, int index, PlayerLoopSystem.UpdateFunction update)
        {
            var list = new List<PlayerLoopSystem>(systems ?? Array.Empty<PlayerLoopSystem>());
            var marker = new PlayerLoopSystem { type = typeof(FrameWorkMeter), updateDelegate = update };

            if (index < 0)
                list.Add(marker);
            else
                list.Insert(index, marker);

            return list.ToArray();
        }

        private static PlayerLoopSystem Without(PlayerLoopSystem root)
        {
            var phases = (PlayerLoopSystem[])root.subSystemList.Clone();

            for (var i = 0; i < phases.Length; i++)
            {
                if (phases[i].subSystemList != null)
                    phases[i].subSystemList = Array.FindAll(phases[i].subSystemList, system => system.type != typeof(FrameWorkMeter));
            }

            root.subSystemList = phases;
            return root;
        }
    }
}
