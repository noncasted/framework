using System;
using System.Collections.Generic;
using UnityEngine.LowLevel;

namespace Global.Telemetry
{
    // The markers around the systems of each phase: the first and the last of its list, so they cover whatever others put there
    // (the runners of UniTask included). A phase looped by the engine (FixedUpdate, once per step) calls its markers each time.
    public static class FramePhaseLoopEdit
    {
        public static PlayerLoopSystem With(PlayerLoopSystem root, PlayerLoopSystem.UpdateFunction[] begins, PlayerLoopSystem.UpdateFunction[] ends)
        {
            root = Without(root);
            var phases = root.subSystemList;

            for (var i = 0; i < phases.Length; i++)
            {
                var phase = Array.IndexOf(FramePhases.LoopTypes, phases[i].type);

                if (phase < 0)
                    continue;

                var systems = new List<PlayerLoopSystem>(phases[i].subSystemList ?? Array.Empty<PlayerLoopSystem>());
                systems.Insert(0, Marker(begins[phase]));
                systems.Add(Marker(ends[phase]));
                phases[i].subSystemList = systems.ToArray();
            }

            return root;
        }

        public static PlayerLoopSystem Without(PlayerLoopSystem root)
        {
            var phases = (PlayerLoopSystem[])root.subSystemList.Clone();

            for (var i = 0; i < phases.Length; i++)
            {
                if (phases[i].subSystemList != null)
                    phases[i].subSystemList = Array.FindAll(phases[i].subSystemList, system => system.type != typeof(FramePhaseMarker));
            }

            root.subSystemList = phases;
            return root;
        }

        public static int CountMarkers(PlayerLoopSystem root)
        {
            var count = 0;

            foreach (var phase in root.subSystemList)
                count += phase.subSystemList == null ? 0 : Array.FindAll(phase.subSystemList, system => system.type == typeof(FramePhaseMarker)).Length;

            return count;
        }

        private static PlayerLoopSystem Marker(PlayerLoopSystem.UpdateFunction update)
        {
            return new PlayerLoopSystem { type = typeof(FramePhaseMarker), updateDelegate = update };
        }
    }
}
