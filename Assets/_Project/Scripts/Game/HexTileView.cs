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

        public float HexTopWorldY => transform.position.y + transform.lossyScale.y * HexPrismMesh.TopY;

        public float ChipTopWorldY
        {
            get
            {
                if (_chipFaceRenderer != null)
                    return _chipFaceRenderer.bounds.max.y;
                if (_chipRimRenderer != null)
                    return _chipRimRenderer.bounds.max.y;
                return HexTopWorldY + WorldChipThickness;
            }
        }

        public Vector3 GetChipFaceWorldCenter() =>
            _chipFaceRenderer != null ? _chipFaceRenderer.bounds.center : transform.position;

        public Renderer TokenFaceRenderer => _chipFaceRenderer;

        public Renderer TokenRimRenderer => _chipRimRenderer;

        public float LabelWorldY => _tokenLabel != null ? _tokenLabel.transform.position.y : transform.position.y;

        public bool HasOpaqueTokenChip =>
            _chipFaceRenderer != null
            && _chipFaceRenderer.sharedMaterial != null
            && _chipFaceRenderer.sharedMaterial.color.a >= 0.98f;

        public bool ChipUsesUnlitMaterial =>
            BuiltInMaterials.IsUnlitMaterial(_chipFaceRenderer != null ? _chipFaceRenderer.sharedMaterial : null);

        public float ChipWorldDiameter =>
            _chipFaceRenderer != null
                ? Mathf.Max(_chipFaceRenderer.bounds.size.x, _chipFaceRenderer.bounds.size.z)
                : 0f;

        public float ChipWorldHeight
        {
            get
            {
                if (_chipRimRenderer == null)
                    return 0f;
                float rim = _chipRimRenderer.bounds.size.y;
                float face = _chipFaceRenderer != null ? _chipFaceRenderer.bounds.size.y : 0f;
                return Mathf.Max(rim, face);
            }
        }

        public bool IsChipFlat =>
            ChipWorldDiameter > 0.01f && ChipWorldHeight / ChipWorldDiameter < 0.22f;

        public string TokenLabelText => _tokenLabel != null ? _tokenLabel.text : string.Empty;

        public float TokenFaceWorldRadius => WorldFaceDiameter * 0.5f;

        public float TokenRimOuterWorldRadius => TokenFaceWorldRadius * RimOuterRadiusFactor;

        public Vector3 RobberWorldPosition =>
            _robberMarker != null ? _robberMarker.transform.position : transform.position;

        public Bounds RobberWorldBounds
        {
            get
            {
                if (_robberMarker == null)
                    return new Bounds(transform.position, Vector3.zero);
                var renderers = _robberMarker.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0)
                    return new Bounds(_robberMarker.transform.position, Vector3.zero);
                Bounds bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    bounds.Encapsulate(renderers[i].bounds);
                return bounds;
            }
        }

        private static readonly Color FamineTint = new(0.55f, 0.35f, 0.15f);
        private static readonly Color GoldRushTint = new(0.95f, 0.75f, 0.1f);
        private static readonly Color GoodHarvestTint = new(0.3f, 0.75f, 0.3f);
        private static readonly Color PortBlockadeTint = new(0.55f, 0.12f, 0.12f);
        private static readonly Color StormMarkerColor = new(0.1f, 0.15f, 0.55f);

        // Target world-space chip size (visible disc on the hex top).
        private const float WorldFaceDiameter = 0.62f;
        private const float RimOuterRadiusFactor = 1.18f;
        private const float WorldChipThickness = 0.04f;

        // Tile-local Y offsets from hex prism top (TopY = 1).
        private const float ChipBaseLiftLocal = 0.02f;
        private const float FaceAboveRimGapLocal = 0.004f;
        private static bool _loggedTokenChipRimDiagnostics;
        private const float LabelLiftLocal = 0.20f;
        private const float MarkerLiftLocal = 0.24f;
        private const float WorldRobberFootprint = 0.34f;
        private const float WorldRobberBaseHeight = 0.08f;
        private const float WorldRobberBodyHeight = 0.14f;
        private const float WorldRobberShoulderDiameter = 0.38f;
        private const float WorldRobberShoulderHeight = 0.10f;
        private const float WorldRobberHeadDiameter = 0.10f;
        private const float WorldRobberTopIconDiameter = 0.40f;
        private const string RobberShoulderPartName = "RobberShoulder";
        private const string RobberTopIconPartName = "RobberTopIcon";
        private const float WorldStormExtent = 0.22f;
        private const float WorldStormHeight = 0.06f;
        private static readonly Vector3 RobberLocalOffset = new(0.46f, 0f, -0.46f);
        private static readonly Color RobberColor = new(0.28f, 0.28f, 0.32f, 1f);
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
            if (data.HasRobber)
                EnsureRobberMarker();
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
            UpdateTokenChipGeometry();
            SyncLabelElevationAboveChip();
            LogTokenChipRimDiagnosticsOnce(token);

            _tokenLabel.text = token.ToString();
            var labelRgb = NumberTokenVisualStyle.GetLabel(token);
            _tokenLabel.color = new Color(labelRgb.R, labelRgb.G, labelRgb.B);
            _tokenLabel.characterSize = NumberTokenVisualStyle.GetLabelCharacterSize(token);
            ClearPipDots();
        }

        private void ClearPipDots()
        {
            _pipRenderers.Clear();
            if (_pipRoot == null)
                return;

            ColonistSceneDestroy.SafeDestroy(_pipRoot);
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

            // BoardView scales hex cylinders non-uniformly (wide XZ, short Y). Child localScale is
            // multiplied by that lossyScale, so divide target world sizes to keep a visible cream disc.
            var hexScale = transform.lossyScale;
            float localFaceDiameter = WorldFaceDiameter / hexScale.x;
            float localFaceHeight = WorldChipThickness / (ThinDiskMesh.MeshHeight * hexScale.y);
            float localRimHeight = WorldChipThickness * 0.55f / (ThinDiskMesh.MeshHeight * hexScale.y);

            float localRimDiameter = localFaceDiameter * RimOuterRadiusFactor;
            _chipRimRenderer = CreateFlatChipDisc("Rim", localRimDiameter, localRimHeight, localRimHeight, 3110);
            _chipFaceRenderer = CreateFlatChipDisc("Face", localFaceDiameter, localFaceHeight, 0f, 3120);
            UpdateTokenChipGeometry();
        }

        private void SyncLabelElevationAboveChip()
        {
            if (_tokenLabel == null)
                return;

            float labelWorldY = ChipTopWorldY + 0.012f;
            var local = transform.InverseTransformPoint(
                new Vector3(transform.position.x, labelWorldY, transform.position.z));
            _tokenLabel.transform.localPosition = new Vector3(0f, local.y, 0f);
        }

        private void UpdateTokenChipGeometry()
        {
            if (_tokenChip == null || _chipRimRenderer == null || _chipFaceRenderer == null)
                return;

            var hexScale = transform.lossyScale;
            float localFaceDiameter = WorldFaceDiameter / hexScale.x;
            float localFaceHeight = WorldChipThickness / (ThinDiskMesh.MeshHeight * hexScale.y);
            float localRimHeight = WorldChipThickness * 0.55f / (ThinDiskMesh.MeshHeight * hexScale.y);

            float localRimDiameter = localFaceDiameter * RimOuterRadiusFactor;
            float rimStackTop = localRimHeight * 2f;
            float faceCenterY = rimStackTop + FaceAboveRimGapLocal + localFaceHeight;

            _tokenChip.transform.localPosition = new Vector3(0f, HexPrismMesh.TopY + ChipBaseLiftLocal, 0f);
            _chipRimRenderer.transform.localScale = new Vector3(localRimDiameter, localRimHeight, localRimDiameter);
            _chipRimRenderer.transform.localPosition = new Vector3(0f, localRimHeight, 0f);
            _chipFaceRenderer.transform.localScale = new Vector3(localFaceDiameter, localFaceHeight, localFaceDiameter);
            _chipFaceRenderer.transform.localPosition = new Vector3(0f, faceCenterY, 0f);
        }

        private Renderer CreateFlatChipDisc(string name, float diameter, float height, float localCenterY, int renderQueue)
        {
            var disc = new GameObject(name);
            disc.transform.SetParent(_tokenChip.transform, false);
            disc.transform.localPosition = new Vector3(0f, localCenterY, 0f);
            disc.transform.localScale = new Vector3(diameter, height, diameter);

            var meshFilter = disc.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = ThinDiskMesh.Shared;
            var renderer = disc.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = BuiltInMaterials.CreateUnlit(Color.white);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.sharedMaterial.renderQueue = renderQueue;
            return renderer;
        }

        private void LogTokenChipRimDiagnosticsOnce(int token)
        {
            if (_loggedTokenChipRimDiagnostics || _chipRimRenderer == null || _chipFaceRenderer == null)
                return;

            _loggedTokenChipRimDiagnostics = true;
            var meshFilter = _chipRimRenderer.GetComponent<MeshFilter>();
            var mesh = meshFilter != null ? meshFilter.sharedMesh : null;
            int vertCount = mesh != null ? mesh.vertexCount : 0;
            Vector3 avgNormal = Vector3.zero;
            if (mesh != null && mesh.normals != null && mesh.normals.Length > 0)
            {
                foreach (var n in mesh.normals)
                    avgNormal += n;
                avgNormal /= mesh.normals.Length;
            }

            var mat = _chipRimRenderer.sharedMaterial;
            var cam = Camera.main;
            int layer = _chipRimRenderer.gameObject.layer;
            int cullingMask = cam != null ? cam.cullingMask : -1;
            bool layerVisible = (cullingMask & (1 << layer)) != 0;
            var rimBounds = _chipRimRenderer.bounds;
            var faceBounds = _chipFaceRenderer.bounds;
            float rimWidth = Mathf.Max(rimBounds.size.x, rimBounds.size.z);
            float faceWidth = Mathf.Max(faceBounds.size.x, faceBounds.size.z);

            Debug.Log(
                $"TokenChipRimDiagnostics token={token} rimEnabled={_chipRimRenderer.enabled} " +
                $"meshVerts={vertCount} rimBounds={rimBounds} faceBounds={faceBounds} " +
                $"rimW={rimWidth:F4} faceW={faceWidth:F4} rimWider={rimWidth > faceWidth} " +
                $"shader={(mat != null ? mat.shader.name : "null")} queue={(mat != null ? mat.renderQueue : -1)} " +
                $"mat={BuiltInMaterials.FormatMaterialColorForLog(mat)} layer={layer} cullingMask={cullingMask} " +
                $"layerVisible={layerVisible} meshAvgNormal={avgNormal}");
        }

        private void ApplyTokenChipStyle(int token)
        {
            var faceRgb = NumberTokenVisualStyle.GetChipFace(token);
            var rimRgb = NumberTokenVisualStyle.GetChipRim(token);
            _chipFaceRenderer.sharedMaterial = BuiltInMaterials.CreateUnlit(
                new Color(faceRgb.R, faceRgb.G, faceRgb.B, 1f));
            _chipRimRenderer.sharedMaterial = BuiltInMaterials.CreateUnlit(
                new Color(rimRgb.R, rimRgb.G, rimRgb.B, 1f));
            _chipRimRenderer.sharedMaterial.renderQueue = 3110;
            _chipFaceRenderer.sharedMaterial.renderQueue = 3120;
        }

        private void EnsureTokenLabel()
        {
            if (_tokenLabel != null)
                return;

            var labelGo = new GameObject("NumberToken");
            labelGo.transform.SetParent(transform, false);
            labelGo.transform.localPosition = new Vector3(0f, HexPrismMesh.TopY + LabelLiftLocal, 0f);
            labelGo.transform.localScale = Vector3.one * 0.08f;

            _tokenLabel = labelGo.AddComponent<TextMesh>();
            var facing = labelGo.AddComponent<FlatTokenLabelFacing>();

            var camera = Camera.main;
            labelGo.transform.rotation = camera != null
                ? NumberTokenLabelOrientation.ComputeColonistTopDownRotation(camera)
                : Quaternion.Euler(
                    NumberTokenLabelOrientation.FlatPitchDegrees,
                    ColonistBoardViewConstants.CameraYawDegrees,
                    0f);
            if (camera != null)
                facing.ApplyFacing(camera);

            _tokenLabel.anchor = TextAnchor.MiddleCenter;
            _tokenLabel.alignment = TextAlignment.Center;
            _tokenLabel.fontSize = 58;
            _tokenLabel.characterSize = 0.6f;
            _tokenLabel.color = Color.black;

            var labelRenderer = labelGo.GetComponent<MeshRenderer>();
            if (labelRenderer != null)
                labelRenderer.sharedMaterial.renderQueue = 3300;
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
            PurgeLegacyRobberVisuals();

            if (_robberMarker != null
                && _robberMarker.transform.Find(RobberShoulderPartName) != null
                && _robberMarker.transform.Find(RobberTopIconPartName) != null)
            {
                ApplyRobberTopIconPlacement();
                LogRobberPawnRenderers();
                return;
            }

            if (_robberMarker != null)
            {
                ColonistSceneDestroy.SafeDestroy(_robberMarker);
                _robberMarker = null;
            }

            var lossy = transform.lossyScale;
            _robberMarker = new GameObject("RobberPawn");
            _robberMarker.transform.SetParent(transform, false);
            _robberMarker.transform.localPosition = new Vector3(
                RobberLocalOffset.x, HexPrismMesh.TopY + MarkerLiftLocal, RobberLocalOffset.z);

            float sx = Mathf.Max(lossy.x, 0.001f);
            float sy = Mathf.Max(lossy.y, 0.001f);
            float sz = Mathf.Max(lossy.z, 0.001f);
            float stackLocalY = 0f;

            stackLocalY = AddRobberCylinder(
                "RobberBase",
                stackLocalY,
                WorldRobberFootprint,
                WorldRobberFootprint,
                WorldRobberBaseHeight,
                sx,
                sy,
                sz);
            stackLocalY = AddRobberCylinder(
                "RobberBody",
                stackLocalY,
                WorldRobberFootprint * 0.68f,
                WorldRobberFootprint * 0.68f,
                WorldRobberBodyHeight,
                sx,
                sy,
                sz);
            stackLocalY = AddRobberCylinder(
                RobberShoulderPartName,
                stackLocalY,
                WorldRobberShoulderDiameter,
                WorldRobberShoulderDiameter,
                WorldRobberShoulderHeight,
                sx,
                sy,
                sz);
            stackLocalY = AddRobberCylinder(
                "RobberHead",
                stackLocalY,
                WorldRobberHeadDiameter,
                WorldRobberHeadDiameter,
                WorldRobberHeadDiameter,
                sx,
                sy,
                sz,
                PrimitiveType.Sphere);

            float iconLocalY = stackLocalY + 0.01f / sy;
            Renderer pawnTopRenderer = null;
            float pawnTopY = float.NegativeInfinity;
            foreach (var partRenderer in _robberMarker.GetComponentsInChildren<Renderer>())
            {
                if (partRenderer.bounds.max.y > pawnTopY)
                {
                    pawnTopY = partRenderer.bounds.max.y;
                    pawnTopRenderer = partRenderer;
                }
            }

            var iconGo = ColonistFlatBoardQuad.Create(
                _robberMarker.transform,
                RobberTopIconPartName,
                new Vector3(0f, iconLocalY, 0f),
                WorldRobberTopIconDiameter,
                WorldRobberTopIconDiameter,
                ColonistRobberTopIconTextures.Get(),
                3360,
                pawnTopRenderer,
                clearanceAbove: 0.015f,
                zWrite: true);
            ApplyRobberTopIconPlacement();
            LogRobberPawnRenderers();

            Debug.Log(
                $"HexTileView RobberPawn tile={Data.Coord} boundsY={RobberWorldBounds.size.y:F3} " +
                $"footprint={RobberWorldBounds.size.x:F3} pos={RobberWorldPosition}");
        }

        private void PurgeLegacyRobberVisuals()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child == null || (_robberMarker != null && child == _robberMarker.transform))
                    continue;

                string name = child.name;
                if (name.Contains("RobberMarker")
                    || name.Contains("Thief")
                    || name.Contains("thief")
                    || (name.Contains("Robber") && name != "RobberPawn"))
                    ColonistSceneDestroy.SafeDestroy(child.gameObject);
            }
        }

        private void ApplyRobberTopIconPlacement()
        {
            if (_robberMarker == null)
                return;

            var icon = _robberMarker.transform.Find(RobberTopIconPartName);
            if (icon == null)
                return;

            ColonistFlatBoardQuad.AlignToColonistCamera(icon);
            ColonistFlatBoardQuad.ApplyWorldSize(
                icon,
                _robberMarker.transform,
                WorldRobberTopIconDiameter,
                WorldRobberTopIconDiameter);
            ColonistFlatBoardQuad.PlaceWorldYAboveAll(icon, _robberMarker.transform, 0.015f);

            var iconRenderer = icon.GetComponent<Renderer>();
            if (iconRenderer != null && iconRenderer.sharedMaterial != null)
                iconRenderer.sharedMaterial.renderQueue = 3360;
        }

        private void LogRobberPawnRenderers()
        {
            if (_robberMarker == null)
                return;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"HexTileView RobberPawn renderers tile={Data.Coord}:");
            foreach (var renderer in _robberMarker.GetComponentsInChildren<Renderer>())
            {
                string matInfo = BuiltInMaterials.FormatMaterialColorForLog(renderer.sharedMaterial);
                sb.AppendLine(
                    $"  {renderer.gameObject.name} maxY={renderer.bounds.max.y:F4} " +
                    $"bounds={renderer.bounds.size} queue={renderer.sharedMaterial?.renderQueue} mat={matInfo}");
            }

            Debug.Log(sb.ToString());
        }

        private float AddRobberCylinder(
            string name,
            float stackLocalY,
            float worldWidthX,
            float worldWidthZ,
            float worldHeight,
            float sx,
            float sy,
            float sz,
            PrimitiveType primitive = PrimitiveType.Cylinder)
        {
            float localHeight = worldHeight / sy;
            float centerY = stackLocalY + localHeight * 0.5f;
            CreateRobberPart(
                name,
                primitive,
                new Vector3(0f, centerY, 0f),
                new Vector3(worldWidthX / sx, worldHeight / sy, worldWidthZ / sz));
            return stackLocalY + localHeight;
        }

        private void CreateRobberPart(string name, PrimitiveType primitive, Vector3 localPos, Vector3 localScale)
        {
            var part = GameObject.CreatePrimitive(primitive);
            part.name = name;
            part.transform.SetParent(_robberMarker.transform, false);
            part.transform.localPosition = localPos;
            part.transform.localScale = localScale;
            var renderer = part.GetComponent<Renderer>();
            renderer.sharedMaterial = BuiltInMaterials.CreateUnlit(RobberColor);
            renderer.sharedMaterial.renderQueue = 3180;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            ColonistSceneDestroy.SafeDestroy(part.GetComponent<Collider>());
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
                StormLocalOffset.x, HexPrismMesh.TopY + MarkerLiftLocal, StormLocalOffset.z);
            _stormMarker.transform.localScale = new Vector3(localExtent, localHeight, localExtent);
            _stormMarker.GetComponent<Renderer>().material.color = StormMarkerColor;
        }
    }
}
