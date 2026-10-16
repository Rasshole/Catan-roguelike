using System.Collections;
using System.Collections.Generic;
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
        private GameObject _tokenChip;
        private Renderer _chipRimRenderer;
        private Renderer _chipFaceRenderer;
        private TextMesh _tokenLabel;
        private GameObject _pipRoot;
        private readonly List<Renderer> _pipRenderers = new();

        public int PipChildCount => _pipRoot != null ? _pipRoot.transform.childCount : 0;

        private static readonly Color FamineTint = new(0.55f, 0.35f, 0.15f);
        private static readonly Color GoldRushTint = new(0.95f, 0.75f, 0.1f);
        private static readonly Color GoodHarvestTint = new(0.3f, 0.75f, 0.3f);
        private static readonly Color PortBlockadeTint = new(0.55f, 0.12f, 0.12f);
        private static readonly Color StormMarkerColor = new(0.1f, 0.15f, 0.55f);

        // Target world-space chip size (visible disc on the hex top).
        private const float WorldRimDiameter = 0.62f;
        private const float WorldFaceDiameter = 0.52f;
        private const float WorldChipThickness = 0.04f;

        // Hex prism mesh top is parent-local y=1; place chip/label above that surface.
        private const float ChipElevationY = 1.08f;
        private const float LabelElevationY = 1.18f;

        // Robber/storm sit above chip+label; compensate scale like EnsureTokenChip.
        private const float MarkerElevationY = 1.28f;
        private const float WorldRobberDiameter = 0.30f;
        private const float WorldStormExtent = 0.22f;
        private const float WorldStormHeight = 0.06f;
        private static readonly Vector3 RobberLocalOffset = new(0.20f, 0f, 0.18f);
        private static readonly Vector3 StormLocalOffset = new(-0.18f, 0f, -0.16f);

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
                ClearPipDots();
                SetTokenVisible(false);
                return;
            }

            int token = data.NumberToken.Value;
            EnsureTokenChip();
            EnsureTokenLabel();
            SetTokenVisible(true);
            ApplyTokenChipStyle(token);

            _tokenLabel.text = token.ToString();
            var labelRgb = NumberTokenVisualStyle.GetLabel(token);
            _tokenLabel.color = new Color(labelRgb.R, labelRgb.G, labelRgb.B);
            _tokenLabel.characterSize = NumberTokenVisualStyle.GetLabelCharacterSize(token);
            RefreshPipDots(token);
        }

        private void RefreshPipDots(int token)
        {
            ClearPipDots();
            int pipCount = NumberTokenPipLayout.GetPipCount(token);
            if (pipCount <= 0)
                return;

            EnsureTokenChip();
            EnsureTokenLabel();

            _pipRoot = new GameObject("TokenPips");
            _pipRoot.transform.SetParent(_tokenLabel.transform, false);
            _pipRoot.transform.localRotation = Quaternion.identity;

            var offsets = NumberTokenPipLayout.GetPipOffsetsX(pipCount);
            var labelRgb = NumberTokenVisualStyle.GetLabel(token);
            var pipColor = new Color(labelRgb.R, labelRgb.G, labelRgb.B);

            for (int i = 0; i < offsets.Length; i++)
            {
                var pip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                pip.name = $"Pip_{i}";
                pip.transform.SetParent(_pipRoot.transform, false);
                NumberTokenPipVisuals.PlacePipOnLabelRow(pip.transform, offsets[i], pipColor);
                Destroy(pip.GetComponent<Collider>());
                _pipRenderers.Add(pip.GetComponent<Renderer>());
            }
        }

        private void ClearPipDots()
        {
            _pipRenderers.Clear();
            if (_pipRoot == null)
                return;

            Destroy(_pipRoot);
            _pipRoot = null;
        }

        private void SetTokenVisible(bool visible)
        {
            if (_tokenChip != null)
                _tokenChip.SetActive(visible);
            if (_tokenLabel != null)
                _tokenLabel.gameObject.SetActive(visible);
            if (_pipRoot != null)
                _pipRoot.SetActive(visible);
        }

        private void EnsureTokenChip()
        {
            if (_tokenChip != null)
                return;

            _tokenChip = new GameObject("NumberTokenChip");
            _tokenChip.transform.SetParent(transform, false);
            _tokenChip.transform.localPosition = new Vector3(0f, ChipElevationY, 0f);

            // BoardView scales hex cylinders non-uniformly (wide XZ, short Y). Child localScale is
            // multiplied by that lossyScale, so divide target world sizes to keep a visible cream disc.
            var hexScale = transform.lossyScale;
            float localRimDiameter = WorldRimDiameter / hexScale.x;
            float localFaceDiameter = WorldFaceDiameter / hexScale.x;
            // Unity cylinder primitive spans 2 units in Y (-1..+1).
            float localRimHeight = WorldChipThickness / (2f * hexScale.y);
            float localFaceHeight = WorldChipThickness * 0.85f / (2f * hexScale.y);

            _chipRimRenderer = CreateCylinderChipDisc("Rim", localRimDiameter, localRimHeight, 0f);
            _chipFaceRenderer = CreateCylinderChipDisc(
                "Face",
                localFaceDiameter,
                localFaceHeight,
                localRimHeight + localFaceHeight * 0.5f);
        }

        private Renderer CreateCylinderChipDisc(string name, float diameter, float height, float localYOffset)
        {
            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = name;
            disc.transform.SetParent(_tokenChip.transform, false);
            disc.transform.localPosition = new Vector3(0f, localYOffset, 0f);
            disc.transform.localScale = new Vector3(diameter, height, diameter);
            Destroy(disc.GetComponent<Collider>());
            var renderer = disc.GetComponent<Renderer>();
            renderer.material = BuiltInMaterials.Create(Color.white);
            return renderer;
        }

        private void ApplyTokenChipStyle(int token)
        {
            var faceRgb = NumberTokenVisualStyle.GetChipFace(token);
            var rimRgb = NumberTokenVisualStyle.GetChipRim(token);
            _chipFaceRenderer.sharedMaterial.color = new Color(faceRgb.R, faceRgb.G, faceRgb.B);
            _chipRimRenderer.sharedMaterial.color = new Color(rimRgb.R, rimRgb.G, rimRgb.B);
        }

        private void EnsureTokenLabel()
        {
            if (_tokenLabel != null)
                return;

            var labelGo = new GameObject("NumberToken");
            labelGo.transform.SetParent(transform, false);
            labelGo.transform.localPosition = new Vector3(0f, LabelElevationY, 0f);
            labelGo.transform.localScale = Vector3.one * 0.08f;

            _tokenLabel = labelGo.AddComponent<TextMesh>();
            var facing = labelGo.AddComponent<FlatTokenLabelFacing>();

            var camera = Camera.main;
            if (camera != null)
                facing.ApplyFacing(camera);
            else
                labelGo.transform.rotation = Quaternion.Euler(NumberTokenLabelOrientation.FlatPitchDegrees, 180f, 0f);
            _tokenLabel.anchor = TextAnchor.MiddleCenter;
            _tokenLabel.alignment = TextAlignment.Center;
            _tokenLabel.fontSize = 48;
            _tokenLabel.characterSize = 0.5f;
        }

        public IEnumerator PulseProduction(float duration)
        {
            if (_renderer == null)
                yield break;

            var flash = Color.Lerp(_baseColor, Color.white, 0.45f);
            yield return SimpleTweens.PulseRendererColor(_renderer, flash, duration);
        }

        public void SetDimmed(bool dimmed)
        {
            if (_renderer == null)
                return;
            _renderer.sharedMaterial.color = dimmed
                ? Color.Lerp(_baseColor, Color.black, 0.45f)
                : _baseColor;
        }

        public void SetRobberVisible(bool visible)
        {
            if (visible)
                EnsureRobberMarker();
            if (_robberMarker != null)
                _robberMarker.SetActive(visible);
        }

        private void EnsureRobberMarker()
        {
            if (_robberMarker != null)
                return;

            var hexScale = transform.lossyScale;

            _robberMarker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _robberMarker.name = "Robber";
            _robberMarker.transform.SetParent(transform, false);
            _robberMarker.transform.localPosition = new Vector3(
                RobberLocalOffset.x, MarkerElevationY, RobberLocalOffset.z);
            // Non-uniform hex lossyScale squashes uniform localScale on Y; compensate per axis.
            _robberMarker.transform.localScale = new Vector3(
                WorldRobberDiameter / hexScale.x,
                WorldRobberDiameter / hexScale.y,
                WorldRobberDiameter / hexScale.z);
            _robberMarker.GetComponent<Renderer>().material.color = Color.black;
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
                case EventTileOverlayKind.PortBlockade:
                    _renderer.material.color = Color.Lerp(_baseColor, PortBlockadeTint, 0.45f);
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

            var hexScale = transform.lossyScale;
            float localExtent = WorldStormExtent / hexScale.x;
            float localHeight = WorldStormHeight / hexScale.y;

            _stormMarker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _stormMarker.name = "StormMarker";
            _stormMarker.transform.SetParent(transform, false);
            _stormMarker.transform.localPosition = new Vector3(
                StormLocalOffset.x, MarkerElevationY, StormLocalOffset.z);
            _stormMarker.transform.localScale = new Vector3(localExtent, localHeight, localExtent);
            _stormMarker.GetComponent<Renderer>().material.color = StormMarkerColor;
        }
    }
}
