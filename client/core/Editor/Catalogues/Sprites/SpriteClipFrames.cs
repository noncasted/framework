using System;
using System.Collections.Generic;

namespace Internal
{
    // The frames of a clip of a strip (SpriteCatalogClip.Frames): tokens split by commas, each a frame ("5") or a run of
    // frames up or down ("2-4", "8-0"), of the strip itself or of another strip of its folder by the name of its file
    // ("Close:3-0"); empty - every frame of the strip in order.
    internal static class SpriteClipFrames
    {
        // The frames of the strip itself; the counts of the strips by the name of the file, null - the strip itself, a
        // negative count - no such strip.
        internal static List<SpriteClipFrame> Parse(string frames, Func<string, int> count)
        {
            if (TryParse(frames, count, out var result, out var error) == false)
                throw new InvalidOperationException(error);

            return result;
        }

        // The inspector shows the error of a typed value instead of throwing on every repaint.
        internal static bool TryParse(string frames, Func<string, int> count, out List<SpriteClipFrame> result,
            out string error)
        {
            result = new List<SpriteClipFrame>();
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(frames))
            {
                var own = count(null);

                if (own <= 0)
                {
                    error = "The strip has no frames";
                    return false;
                }

                for (var i = 0; i < own; i++)
                    result.Add(new SpriteClipFrame(null, i));

                return true;
            }

            foreach (var raw in frames.Split(','))
            {
                var token = raw.Trim();
                string file = null;
                var colon = token.IndexOf(':');

                if (colon >= 0)
                {
                    file = token.Substring(0, colon).Trim();
                    token = token.Substring(colon + 1).Trim();

                    if (file.Length == 0)
                    {
                        error = $"'{raw.Trim()}' names no strip";
                        return false;
                    }
                }

                var frameCount = count(file);

                if (frameCount <= 0)
                {
                    error = file == null ? "The strip has no frames" : $"No strip {file} next to this one";
                    return false;
                }

                if (TryRun(token, frameCount, out var from, out var to, out error) == false)
                    return false;

                var step = to >= from ? 1 : -1;

                for (var i = from; i != to + step; i += step)
                    result.Add(new SpriteClipFrame(file, i));
            }

            return true;
        }

        private static bool TryRun(string token, int count, out int from, out int to, out string error)
        {
            to = 0;
            var dash = token.IndexOf('-');

            if (dash < 0)
            {
                var single = TryFrame(token, count, out from, out error);
                to = from;
                return single;
            }

            return TryFrame(token.Substring(0, dash).Trim(), count, out from, out error) &&
                   TryFrame(token.Substring(dash + 1).Trim(), count, out to, out error);
        }

        private static bool TryFrame(string token, int count, out int frame, out string error)
        {
            error = string.Empty;

            if (int.TryParse(token, out frame) == false)
            {
                error = $"'{token}' is not a frame";
                return false;
            }

            if (frame < 0 || frame >= count)
            {
                error = $"Frame {frame} is out of the strip of {count} frames";
                return false;
            }

            return true;
        }
    }
}
