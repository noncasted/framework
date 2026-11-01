using UnityEngine;
using UnityEngine.UI;

namespace Global.UI
{
    // From the middle of the edge of the current one in the direction, to the nearest point of a candidate: the
    // candidate is ahead (its centre past the edge), and the cheapest is the one nearest along the direction with
    // the sideways distance counted twice. The nearest point, not the centre (the rule of uGUI): a wide row right
    // under a small cell is the next one down, not a row further down with its centre nearer by the side.
    // A candidate that lies over the current one (the "Back" of the character panel over the bottom of the middle
    // card) is not ahead of its edge: when nothing is, the search is repeated from the centre of the current one.
    internal static class NearestSelectableFinder
    {
        private const float SidewaysWeight = 2f;

        public static Selectable Find(NavigationCandidates candidates, Selectable current, Vector2 direction)
        {
            var rect = (RectTransform)current.transform;
            var local = rect.rect;
            var edge = (Vector2)rect.TransformPoint(local.center + Vector2.Scale(local.size * 0.5f, direction));
            var best = Find(candidates, current, direction, edge);

            return best != null ? best : Find(candidates, current, direction, (Vector2)rect.TransformPoint(local.center));
        }

        private static Selectable Find(NavigationCandidates candidates, Selectable current, Vector2 direction, Vector2 from)
        {
            Selectable best = null;
            var bestCost = float.PositiveInfinity;

            foreach (var candidate in candidates)
            {
                if (candidate == current)
                    continue;

                var candidateRect = (RectTransform)candidate.transform;
                var bounds = candidateRect.rect;
                var min = (Vector2)candidateRect.TransformPoint(bounds.min);
                var max = (Vector2)candidateRect.TransformPoint(bounds.max);
                var centre = (min + max) * 0.5f;

                if (Vector2.Dot(direction, centre - from) <= 0f)
                    continue;

                var nearest = new Vector2(Mathf.Clamp(from.x, min.x, max.x), Mathf.Clamp(from.y, min.y, max.y));
                var offset = nearest - from;
                var along = Vector2.Dot(direction, offset);

                if (along < 0f)
                    continue;

                var sideways = (offset - direction * along).magnitude;
                var cost = along + sideways * SidewaysWeight;

                if (cost >= bestCost)
                    continue;

                best = candidate;
                bestCost = cost;
            }

            return best;
        }
    }
}
