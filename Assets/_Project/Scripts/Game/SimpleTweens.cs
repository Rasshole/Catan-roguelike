using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace CatanRoguelike.Game
{
    public static class SimpleTweens
    {
        public static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        public static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);

        public static IEnumerator ScalePop(Transform target, float duration, float peakScale = 1.18f)
        {
            if (target == null)
                yield break;

            Vector3 baseScale = target.localScale;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = EaseOutBack(t);
                float scale = Mathf.Lerp(0.35f, peakScale, eased);
                if (t > 0.65f)
                    scale = Mathf.Lerp(peakScale, 1f, EaseOutQuad((t - 0.65f) / 0.35f));
                target.localScale = baseScale * scale;
                yield return null;
            }

            target.localScale = baseScale;
        }

        public static IEnumerator PulseRendererColor(Renderer renderer, Color flash, float duration)
        {
            if (renderer == null)
                yield break;

            Color start = renderer.sharedMaterial.color;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float wave = Mathf.Sin(t * Mathf.PI);
                renderer.sharedMaterial.color = Color.Lerp(start, flash, wave * 0.55f);
                yield return null;
            }

            renderer.sharedMaterial.color = start;
        }

        public static IEnumerator AnimateInt(int from, int to, float duration, System.Action<int> onStep)
        {
            if (duration <= 0f)
            {
                onStep?.Invoke(to);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = EaseOutQuad(Mathf.Clamp01(elapsed / duration));
                int value = Mathf.RoundToInt(Mathf.Lerp(from, to, t));
                onStep?.Invoke(value);
                yield return null;
            }

            onStep?.Invoke(to);
        }

        public static IEnumerator FlyAnchoredPosition(
            RectTransform target,
            Vector2 from,
            Vector2 to,
            float duration,
            float arcHeight = 48f)
        {
            if (target == null)
                yield break;

            if (duration <= 0f)
            {
                target.anchoredPosition = to;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = EaseOutQuad(Mathf.Clamp01(elapsed / duration));
                Vector2 pos = Vector2.Lerp(from, to, t);
                pos.y += Mathf.Sin(t * Mathf.PI) * arcHeight;
                target.anchoredPosition = pos;
                yield return null;
            }

            target.anchoredPosition = to;
        }

        public static IEnumerator FadeGraphic(Graphic graphic, float fromAlpha, float toAlpha, float duration)
        {
            if (graphic == null)
                yield break;

            if (duration <= 0f)
            {
                var c = graphic.color;
                c.a = toAlpha;
                graphic.color = c;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = EaseOutQuad(Mathf.Clamp01(elapsed / duration));
                var c = graphic.color;
                c.a = Mathf.Lerp(fromAlpha, toAlpha, t);
                graphic.color = c;
                yield return null;
            }

            var end = graphic.color;
            end.a = toAlpha;
            graphic.color = end;
        }
    }
}
