using UnityEngine;

namespace CatanRoguelike.Game
{
    public sealed partial class RunHudController
    {
        private bool _rollPulseActive;

        private void Update()
        {
            if (!ColonistPresentation.IsActive)
                return;

            TickColonistTurnTimer();
            TickColonistRollPulse();
        }

        private void TickColonistRollPulse()
        {
            if (!_rollPulseActive || _rollButtonGlowRing == null)
                return;

            float pulse = (Mathf.Sin(Time.unscaledTime * 5.5f) + 1f) * 0.5f;
            _rollButtonGlowRing.color = new Color(1f, 0.88f, 0.28f, Mathf.Lerp(0.22f, 0.78f, pulse));
            var rect = _rollButtonGlowRing.rectTransform;
            float scale = 1f + pulse * 0.1f;
            rect.localScale = new Vector3(scale, scale, 1f);

            if (_rollButtonGlow != null)
            {
                var baseColor = RunHudUiFactory.ButtonFill;
                _rollButtonGlow.color = Color.Lerp(baseColor, new Color(1f, 0.9f, 0.35f, 1f), 0.25f + pulse * 0.35f);
            }
        }

        internal void SetRollPulseActive(bool active) => _rollPulseActive = active;
    }
}
