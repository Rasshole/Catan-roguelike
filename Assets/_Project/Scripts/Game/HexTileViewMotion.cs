using CatanRoguelike.Core.Yield;
using CatanRoguelike.Core.Map;
using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>
    /// v146: the number-token pop (scale bounce + lift + warm glow when the dice hit this number) and the
    /// Fishermen lake's "2 3 / 11 12" token. Driven by <see cref="BoardView"/> on <see cref="HudMotion"/> time.
    /// </summary>
    public sealed partial class HexTileView
    {
        public const string LakeTokenText = "2  3\n11 12";
        public const string PopGlowName = "NumberPopGlow";

        /// <summary>Set by the board for pass &amp; play: the desert lake fishes on 2/3/11/12.</summary>
        public bool ShowLakeNumbers { get; set; }

        public bool IsLakeToken => ShowLakeNumbers && Data != null && Data.IsDesert && Data.IsFishTile;
        public bool HasVisibleToken => _tokenChip != null && _tokenChip.activeSelf;
        public Transform TokenChipTransform => _tokenChip != null ? _tokenChip.transform : null;
        public Transform TokenLabelTransform => _tokenLabel != null ? _tokenLabel.transform : null;
        public Transform RobberMarkerTransform => _robberMarker != null ? _robberMarker.transform : null;
        public bool IsPopping => _popActive;

        public Vector3 RobberRestingLocalPosition
        {
            get
            {
                var o = GetRobberLocalOffset();
                return new Vector3(o.x, HexPrismMesh.TopY + MarkerLiftLocal, o.z);
            }
        }

        private bool _popActive;
        private Vector3 _chipBaseScale, _chipBasePos, _labelBaseScale, _labelBasePos;
        private SpriteRenderer _popGlow;
        private static Sprite _glowSprite;

        private void RefreshLakeToken(HexTileData data)
        {
            EnsureTokenChip();
            EnsureTokenLabel();
            SetTokenVisible(true);
            ApplyTokenChipStyle(2, true);
            UpdateTokenChipGeometry();
            SyncLabelElevationAboveChip();
            _tokenLabel.text = LakeTokenText;
            var rgb = NumberTokenVisualStyle.GetLabel(2, true);
            _tokenLabel.color = new Color(rgb.R, rgb.G, rgb.B);
            _tokenLabel.characterSize = NumberTokenVisualStyle.GetLabelCharacterSize(2) * 0.6f;
            _tokenLabel.lineSpacing = 0.85f;
            ApplyTokenLabelFontMaterial();
            ClearPipDots();
        }

        /// <summary>Applies the pop at progress <paramref name="t"/> (0..1); t &gt;= 1 restores the token.</summary>
        public void ApplyNumberPop(float t)
        {
            if (_tokenChip == null || _tokenLabel == null)
                return;
            if (!_popActive)
            {
                if (t >= 1f)
                    return;
                _popActive = true;
                _chipBaseScale = _tokenChip.transform.localScale;
                _chipBasePos = _tokenChip.transform.localPosition;
                _labelBaseScale = _tokenLabel.transform.localScale;
                _labelBasePos = _tokenLabel.transform.localPosition;
            }

            if (t >= 1f)
            {
                EndNumberPop();
                return;
            }

            // Quick overshoot up to ~1.75x, a small rebound, then settle (ease-out elastic-ish).
            float bump = t < 0.28f
                ? SimpleTweens.EaseOutBack(t / 0.28f) * 0.75f
                : 0.75f * Mathf.Exp(-5.5f * (t - 0.28f)) * Mathf.Cos((t - 0.28f) * 13f);
            float s = 1f + bump;
            var lossy = transform.lossyScale;
            float liftLocal = Mathf.Max(0f, bump) * 0.22f / Mathf.Max(0.001f, lossy.y);
            _tokenChip.transform.localScale = new Vector3(_chipBaseScale.x * s, _chipBaseScale.y, _chipBaseScale.z * s);
            _tokenChip.transform.localPosition = _chipBasePos + new Vector3(0f, liftLocal, 0f);
            _tokenLabel.transform.localScale = _labelBaseScale * s;
            _tokenLabel.transform.localPosition = _labelBasePos + new Vector3(0f, liftLocal * 1.08f, 0f);

            EnsurePopGlow();
            float glowA = t < 0.15f ? t / 0.15f : Mathf.Clamp01(1f - (t - 0.15f) / 0.85f);
            _popGlow.color = new Color(1f, 0.84f, 0.32f, 0.85f * glowA);
            float d = TokenWorldFaceDiameter * (1.6f + 0.9f * Mathf.Clamp01(t * 1.6f));
            var parent = _popGlow.transform.parent;
            float ps = parent != null ? Mathf.Max(0.0001f, parent.lossyScale.x) : 1f;
            _popGlow.transform.localScale = Vector3.one * (d / ps);
            _popGlow.transform.position = new Vector3(transform.position.x, HexTopWorldY + 0.01f, transform.position.z);
        }

        public void EndNumberPop()
        {
            if (_popActive)
            {
                if (_tokenChip != null)
                {
                    _tokenChip.transform.localScale = _chipBaseScale;
                    _tokenChip.transform.localPosition = _chipBasePos;
                }

                if (_tokenLabel != null)
                {
                    _tokenLabel.transform.localScale = _labelBaseScale;
                    _tokenLabel.transform.localPosition = _labelBasePos;
                }
            }

            _popActive = false;
            if (_popGlow != null)
            {
                ColonistSceneDestroy.SafeDestroy(_popGlow.gameObject);
                _popGlow = null;
            }
        }

        private void EnsurePopGlow()
        {
            if (_popGlow != null)
                return;
            var go = new GameObject(PopGlowName);
            go.transform.SetParent(transform.parent, false);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            go.layer = gameObject.layer;
            _popGlow = go.AddComponent<SpriteRenderer>();
            _popGlow.sprite = GlowSprite();
            _popGlow.sortingOrder = 5;
        }

        private static Sprite GlowSprite()
        {
            if (_glowSprite != null)
                return _glowSprite;
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - r);
                a = a * a * (3f - 2f * a);
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }

            tex.SetPixels32(px);
            tex.Apply();
            _glowSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
            return _glowSprite;
        }
    }
}
