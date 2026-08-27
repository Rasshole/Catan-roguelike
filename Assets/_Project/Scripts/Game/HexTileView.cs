using CatanRoguelike.Core.Events;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Yield;
using UnityEngine;

namespace CatanRoguelike.Game
{
    public sealed class HexTileView : MonoBehaviour
    {
        public HexTileData Data { get; private set; }
        private Renderer _renderer;
        private Color _baseColor;
        private GameObject _robberMarker;
        private GameObject _stormMarker;
        private TextMesh _tokenLabel;

        private static readonly Color FamineTint = new(0.55f, 0.35f, 0.15f);
        private static readonly Color GoldRushTint = new(0.95f, 0.75f, 0.1f);
        private static readonly Color GoodHarvestTint = new(0.3f, 0.75f, 0.3f);
        private static readonly Color StormMarkerColor = new(0.1f, 0.15f, 0.55f);

        public void Initialize(HexTileData data, Renderer renderer)
        {
            Data = data;
            _renderer = renderer;
            _baseColor = renderer.material.color;
        }

        public void Refresh(HexTileData data)
        {
            Data = data;
            if (_robberMarker != null)
                _robberMarker.SetActive(data.HasRobber);
            RefreshNumberToken(data);
        }

        public void RefreshNumberToken(HexTileData data)
        {
            if (data.IsDesert || !data.NumberToken.HasValue)
            {
                if (_tokenLabel != null)
                    _tokenLabel.gameObject.SetActive(false);
                return;
            }

            EnsureTokenLabel();
            _tokenLabel.gameObject.SetActive(true);
            _tokenLabel.text = data.NumberToken.Value.ToString();
            _tokenLabel.color = NumberTokenLibrary.IsRedNumber(data.NumberToken.Value)
                ? Color.red
                : Color.black;
        }

        private void EnsureTokenLabel()
        {
            if (_tokenLabel != null)
                return;

            var labelGo = new GameObject("NumberToken");
            labelGo.transform.SetParent(transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 0.52f, 0f);
            labelGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            labelGo.transform.localScale = Vector3.one * 0.08f;

            _tokenLabel = labelGo.AddComponent<TextMesh>();
            _tokenLabel.anchor = TextAnchor.MiddleCenter;
            _tokenLabel.alignment = TextAlignment.Center;
            _tokenLabel.fontSize = 48;
            _tokenLabel.characterSize = 0.5f;
        }

        public void SetRobberVisible(bool visible)
        {
            if (_robberMarker == null)
            {
                _robberMarker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                _robberMarker.name = "Robber";
                _robberMarker.transform.SetParent(transform, false);
                _robberMarker.transform.localPosition = new Vector3(0f, 0.5f, 0f);
                _robberMarker.transform.localScale = Vector3.one * 0.35f;
                _robberMarker.GetComponent<Renderer>().material.color = Color.black;
            }
            _robberMarker.SetActive(visible);
        }

        public void SetEventOverlay(EventTileOverlayKind kind)
        {
            if (kind == EventTileOverlayKind.Storm)
            {
                ApplyBaseColor();
                EnsureStormMarker();
                _stormMarker.SetActive(true);
                return;
            }

            if (_stormMarker != null)
                _stormMarker.SetActive(false);

            switch (kind)
            {
                case EventTileOverlayKind.Famine:
                    _renderer.material.color = Color.Lerp(_baseColor, FamineTint, 0.45f);
                    break;
                case EventTileOverlayKind.GoldRush:
                    _renderer.material.color = Color.Lerp(_baseColor, GoldRushTint, 0.45f);
                    break;
                case EventTileOverlayKind.GoodHarvest:
                    _renderer.material.color = Color.Lerp(_baseColor, GoodHarvestTint, 0.25f);
                    break;
                default:
                    ApplyBaseColor();
                    break;
            }
        }

        private void ApplyBaseColor()
        {
            if (_renderer != null)
                _renderer.material.color = _baseColor;
        }

        private void EnsureStormMarker()
        {
            if (_stormMarker != null)
                return;

            _stormMarker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _stormMarker.name = "StormMarker";
            _stormMarker.transform.SetParent(transform, false);
            _stormMarker.transform.localPosition = new Vector3(0f, 0.42f, 0f);
            _stormMarker.transform.localScale = new Vector3(0.28f, 0.12f, 0.28f);
            _stormMarker.GetComponent<Renderer>().material.color = StormMarkerColor;
        }
    }
}
