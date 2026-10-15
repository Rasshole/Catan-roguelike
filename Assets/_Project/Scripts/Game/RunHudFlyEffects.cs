using System.Collections;
using System.Collections.Generic;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.UI;
using UnityEngine;
using UnityEngine.UI;

namespace CatanRoguelike.Game
{
    public sealed partial class RunHudController
    {
        private RectTransform _flyLayer;
        private RectTransform _bankFlyTarget;

        public void SetBankFlyTarget(RectTransform bankStripPanel) => _bankFlyTarget = bankStripPanel;

        private void EnsureFlyLayer()
        {
            if (_flyLayer != null || _canvas == null)
                return;

            var go = new GameObject("ResourceFlyLayer");
            go.transform.SetParent(_canvas.transform, false);
            _flyLayer = go.AddComponent<RectTransform>();
            _flyLayer.anchorMin = Vector2.zero;
            _flyLayer.anchorMax = Vector2.one;
            _flyLayer.offsetMin = Vector2.zero;
            _flyLayer.offsetMax = Vector2.zero;
            _flyLayer.SetAsLastSibling();
        }

        public IEnumerator PlayProductionFlies(
            BoardView boardView,
            Dictionary<ResourceType, HexCoord> sources,
            float flySeconds)
        {
            if (_canvas == null || sources == null || sources.Count == 0)
                yield break;

            EnsureFlyLayer();
            var canvasRect = _canvas.transform as RectTransform;
            foreach (var pair in sources)
            {
                if (!TryGetResourceSwatchRect(pair.Key, out var targetRect))
                    continue;

                Vector2 targetLocal = GetRectCenterLocal(canvasRect, targetRect);
                Vector2 startLocal = targetLocal;
                if (boardView != null && boardView.TryGetHexWorldCenter(pair.Value, out var world))
                {
                    var screen = RectTransformUtility.WorldToScreenPoint(Camera.main, world);
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out startLocal);
                }

                yield return SpawnAndFlyIcon(pair.Key, startLocal, targetLocal, flySeconds);
            }
        }

        public IEnumerator PlaySpendToBankFlies(ResourceBundle paid, float flySeconds)
        {
            if (_canvas == null || paid.Total == 0)
                yield break;

            EnsureFlyLayer();
            var canvasRect = _canvas.transform as RectTransform;
            Vector2 bankLocal = _bankFlyTarget != null
                ? GetRectCenterLocal(canvasRect, _bankFlyTarget)
                : new Vector2(canvasRect.rect.width * 0.5f, canvasRect.rect.height * 0.5f);

            foreach (var pair in paid.EnumerateNonZero())
            {
                if (!TryGetResourceSwatchRect(pair.type, out var swatchRect))
                    continue;

                Vector2 startLocal = GetRectCenterLocal(canvasRect, swatchRect);
                yield return SpawnAndFlyIcon(pair.type, startLocal, bankLocal, flySeconds);
            }
        }

        private IEnumerator SpawnAndFlyIcon(ResourceType type, Vector2 from, Vector2 to, float duration)
        {
            var icon = CreateFlyIcon(type);
            icon.anchoredPosition = from;
            yield return SimpleTweens.FlyAnchoredPosition(icon, from, to, duration, arcHeight: 36f);
            yield return SimpleTweens.FadeGraphic(icon.GetComponent<Image>(), 1f, 0f, 0.12f);
            Destroy(icon.gameObject);
        }

        private RectTransform CreateFlyIcon(ResourceType type)
        {
            var go = new GameObject($"Fly_{type}");
            go.transform.SetParent(_flyLayer, false);
            var rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(28f, 28f);
            var image = go.AddComponent<Image>();
            image.color = BoardResourceColors.GetHudSwatch(type);
            image.raycastTarget = false;
            return rect;
        }

        private bool TryGetResourceSwatchRect(ResourceType type, out RectTransform rect)
        {
            int index = (int)type;
            if (index < 0 || index >= _resourceSwatches.Count)
            {
                rect = null;
                return false;
            }

            rect = _resourceSwatches[index].rectTransform;
            return rect != null;
        }

        private static Vector2 GetRectCenterLocal(RectTransform canvasRect, RectTransform target)
        {
            var world = target.TransformPoint(target.rect.center);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                RectTransformUtility.WorldToScreenPoint(null, world),
                null,
                out var local);
            return local;
        }
    }
}
