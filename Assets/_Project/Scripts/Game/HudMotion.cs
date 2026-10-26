using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>
    /// v146: one clock for the table's time-stamped animations (piece drops, robber/pirate slides, number pops,
    /// panel fades, VP punches). The pass &amp; play HUD is rebuilt on every change, so animations are keyed to a
    /// start time instead of living on the (short-lived) objects. Captures snap everything to its resting state
    /// unless a test asks to freeze mid-motion.
    /// </summary>
    public static class HudMotion
    {
        private static float _offset;

        public static float Now => Time.unscaledTime + _offset;

        /// <summary>When true (default) a HUD capture first completes every running animation.</summary>
        public static bool SnapOnCapture = true;

        /// <summary>Jumps the motion clock forward so every running animation reaches its end state.</summary>
        public static void Snap() => _offset += 30f;

        public static float Progress(float startedAt, float duration) =>
            duration <= 0f ? 1f : Mathf.Clamp01((Now - startedAt) / duration);

        public static float EaseOutBounce(float t)
        {
            const float n1 = 7.5625f, d1 = 2.75f;
            if (t < 1f / d1) return n1 * t * t;
            if (t < 2f / d1) { t -= 1.5f / d1; return n1 * t * t + 0.75f; }
            if (t < 2.5f / d1) { t -= 2.25f / d1; return n1 * t * t + 0.9375f; }
            t -= 2.625f / d1;
            return n1 * t * t + 0.984375f;
        }
    }
}
