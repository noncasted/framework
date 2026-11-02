using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Internal
{
    // The plan of the running prefab batch: it lives in SessionState, the domain reloads between the phases.
    internal static class HierarchyBindingsPrefabPlans
    {
        private const string PlanKey = "Internal.HierarchyBindings.PrefabBatch";

        // The base before the variants, children before parents inside an asset.
        public static HierarchyBindingsPrefabPlan Build(List<string> assets, List<HierarchyBindingsPrefabItem> items)
        {
            var plan = new HierarchyBindingsPrefabPlan { Assets = assets };

            foreach (var group in items
                         .GroupBy(item => (item.VariantDepth, item.HierarchyDepth))
                         .OrderBy(group => group.Key.VariantDepth)
                         .ThenByDescending(group => group.Key.HierarchyDepth))
            {
                var phase = new HierarchyBindingsPrefabPlan.Phase();
                phase.Requests.AddRange(group.Select(item => item.Request));
                plan.Phases.Add(phase);
            }

            return plan;
        }

        public static HierarchyBindingsPrefabPlan Load()
        {
            var json = SessionState.GetString(PlanKey, string.Empty);
            return string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<HierarchyBindingsPrefabPlan>(json);
        }

        public static void Save(HierarchyBindingsPrefabPlan plan)
        {
            SessionState.SetString(PlanKey, JsonUtility.ToJson(plan));
        }

        public static void Clear()
        {
            SessionState.EraseString(PlanKey);
        }
    }
}
