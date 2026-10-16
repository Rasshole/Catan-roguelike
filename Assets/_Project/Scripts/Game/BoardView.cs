using System.Collections;
using System.Collections.Generic;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Events;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Shop;
using UnityEngine;
using UnityEngine.SceneManagement;
using Edge = CatanRoguelike.Core.Hex.HexMath.Edge;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;

namespace CatanRoguelike.Game
{
    public sealed class BoardView : MonoBehaviour
    {
        [SerializeField] private float hexScale = 1.2f;
        [SerializeField] private float tileHeight = 0.15f;
        [SerializeField] private Transform boardRoot;
        [SerializeField] private Transform buildingsRoot;

        public float HexScale => hexScale;
        public float TileHeight => tileHeight;

        public float GetBoardBoundingRadius()
        {
            if (_tiles.Count > 0)
                return TableCameraFraming.ComputeBoardBoundingRadius(GetLandTileCoords(), hexScale);

            if (_controller != null)
                return TableCameraFraming.ComputeBoardBoundingRadius(GetLandCoords(_controller.State.Board), hexScale);

            return 0f;
        }

        /// <summary>Radius from board center that includes sand ring and port badges (world units).</summary>
        public float GetColonistFramingExtentRadius()
        {
            float beachWidth = hexScale * ColonistCoastSandMesh.BeachWidthFactor;
            Vector3 center = GetBoardWorldCenter();
            float sandFromCenter = ColonistCoastSandMesh.SandSkirtEnabled
                ? ComputeFarthestSandDistanceFromCenter(center, beachWidth)
                : 0f;
            float badgeFromCenter = ComputeLivePortBadgeExtentFromCenter(center);
            float fishFromCenter = ComputeSeaFishGroundExtentFromCenter(center);
            badgeFromCenter = Mathf.Max(badgeFromCenter, fishFromCenter);
            if (badgeFromCenter <= 0f && _controller != null && _controller.State?.Board != null)
            {
                badgeFromCenter = PortBadgePlacement.ComputeMaxBadgeOuterDistanceFromCenter(
                    _controller.State.Board,
                    hexScale,
                    PortMarkerVisuals.BadgeDiscWorldRadius,
                    center);
            }

            if (sandFromCenter <= 0f && badgeFromCenter <= 0f)
            {
                float boardRadius = GetBoardBoundingRadius();
                return TableCameraFraming.ComputeColonistSandOuterRadius(boardRadius, hexScale, beachWidth);
            }

            return Mathf.Max(sandFromCenter, badgeFromCenter);
        }

        private float ComputeFarthestSandDistanceFromCenter(Vector3 center, float beachWidth)
        {
            if (_tiles.Count == 0)
                return 0f;

            var centerXZ = new Vector2(center.x, center.z);
            float max = 0f;
            foreach (var coord in _tiles.Keys)
            {
                for (int corner = 0; corner < HexPrismMesh.SideCount; corner++)
                {
                    var (x, z) = HexMath.VertexToWorldPosition(coord, corner, hexScale);
                    float dist = Vector2.Distance(centerXZ, new Vector2(x, z)) + beachWidth;
                    max = Mathf.Max(max, dist);
                }
            }

            return max;
        }

        private float ComputeLivePortBadgeExtentFromCenter(Vector3 center)
        {
            if (boardRoot == null)
                return 0f;

            var centerXZ = new Vector2(center.x, center.z);
            float max = 0f;
            foreach (var disc in boardRoot.GetComponentsInChildren<Transform>(true))
            {
                if (disc.name != "BadgeDisc")
                    continue;

                float radius = disc.lossyScale.x * 0.5f;
                var pos = disc.position;
                float dist = Vector2.Distance(centerXZ, new Vector2(pos.x, pos.z))
                    + radius * (1f + PortBadgePlacement.BadgeStackFramingPadFactor);
                max = Mathf.Max(max, dist);
            }

            return max;
        }

        private float ComputeSeaFishGroundExtentFromCenter(Vector3 center)
        {
            var board = _controller?.State?.Board;
            if (board == null || board.SeaFishGrounds.Count == 0)
                return 0f;

            var centerXZ = new Vector2(center.x, center.z);
            float max = 0f;
            foreach (var sea in board.SeaFishGrounds.Keys)
            {
                if (!SeaFrameSlotLayout.TryGetFishShoalPlacement(board, sea, hexScale, out FishShoalPlacement p))
                    continue;

                max = Mathf.Max(max, Vector2.Distance(centerXZ, new Vector2(p.CoastVertexA.X, p.CoastVertexA.Z)));
                max = Mathf.Max(max, Vector2.Distance(centerXZ, new Vector2(p.CoastVertexB.X, p.CoastVertexB.Z)));
                max = Mathf.Max(max, Vector2.Distance(centerXZ, new Vector2(p.SeaTip.X, p.SeaTip.Z)));
            }

            return max;
        }

        public Vector3 GetBoardWorldCenter()
        {
            if (_tiles.Count == 0)
                return transform.position;

            Vector3 sum = Vector3.zero;
            int count = 0;
            foreach (var tile in _tiles.Values)
            {
                if (IsSeaFishTile(tile.Data))
                    continue;
                sum += tile.transform.position;
                count++;
            }

            return count > 0 ? sum / count : transform.position;
        }

        /// <summary>World samples for colonist camera fit (land corners, sand ring, port badges).</summary>
        public void FillColonistFramingSamples(List<Vector3> samples)
        {
            if (samples == null)
                return;

            float beachWidth = hexScale * ColonistCoastSandMesh.BeachWidthFactor;
            float sampleY = 0f;
            foreach (var kvp in _tiles)
            {
                if (IsSeaFishTile(kvp.Value.Data))
                    continue;

                sampleY = GetHexTopWorldPoint(kvp.Value.transform).y;
                break;
            }

            if (_controller?.State?.Board != null)
            {
                ColonistCoastSandMesh.AppendLandHexTopCornerSamples(
                    _controller.State.Board,
                    hexScale,
                    sampleY,
                    samples);
            }
            else
            {
                foreach (var kvp in _tiles)
                {
                    if (IsSeaFishTile(kvp.Value.Data))
                        continue;

                    var coord = kvp.Key;
                    float y = GetHexTopWorldPoint(kvp.Value.transform).y;
                    for (int corner = 0; corner < HexPrismMesh.SideCount; corner++)
                    {
                        var (x, z) = HexMath.VertexToWorldPosition(coord, corner, hexScale);
                        samples.Add(new Vector3(x, y, z));
                    }
                }
            }

            if (boardRoot == null)
                return;

            foreach (var badgeRoot in boardRoot.GetComponentsInChildren<Transform>(true))
            {
                if (badgeRoot.name != "PortRateBadge")
                    continue;

                foreach (var renderer in badgeRoot.GetComponentsInChildren<Renderer>(true))
                    ColonistBoardFraming.AppendBoundsCorners(renderer.bounds, samples);
            }

            AppendSeaFishGroundCornerSamples(samples, sampleY);
        }

        /// <summary>Outer corners of inset sea-fish hex prisms (full hex footprint, not centre).</summary>
        public void AppendSeaFishGroundCornerSamples(List<Vector3> samples)
        {
            float sampleY = 0f;
            foreach (var kvp in _tiles)
            {
                if (!IsSeaFishTile(kvp.Value.Data))
                    continue;
                sampleY = GetHexTopWorldPoint(kvp.Value.transform).y;
                break;
            }

            AppendSeaFishGroundCornerSamples(samples, sampleY);
        }

        public void AppendSeaFishGroundCornerSamples(List<Vector3> samples, float sampleY)
        {
            if (samples == null)
                return;

            var board = _controller?.State?.Board;
            if (board == null || board.SeaFishGrounds.Count == 0)
                return;

            var xz = new List<MapPlaneXz>(board.SeaFishGrounds.Count * 3);
            SeaFrameSlotLayout.AppendFishShoalFramingSamples(board, hexScale, xz);
            foreach (var p in xz)
                samples.Add(new Vector3(p.X, sampleY, p.Z));
        }

        /// <summary>World point on the top of the reference hex for capture colour sampling.</summary>
        public Vector3 GetCaptureReferenceWorldPoint()
        {
            var origin = new HexCoord(0, 0);
            if (_tiles.TryGetValue(origin, out var centerTile))
                return GetHexTopWorldPoint(centerTile.transform);

            foreach (var tile in _tiles.Values)
                return GetHexTopWorldPoint(tile.transform);

            return transform.position;
        }

        /// <summary>Land hex top offset toward a corner (fully off centre number token) for capture sampling.</summary>
        public Vector3 GetCaptureHexSampleWorldPoint()
        {
            HexTileView tile = null;
            if (_tiles.TryGetValue(new HexCoord(1, -1), out var preferred) && !preferred.Data.IsDesert)
                tile = preferred;

            if (tile == null)
            {
                foreach (var kvp in _tiles)
                {
                    if (IsSeaFishTile(kvp.Value.Data) || kvp.Value.Data.IsDesert)
                        continue;
                    tile = kvp.Value;
                    break;
                }
            }

            if (tile == null)
                return GetCaptureReferenceWorldPoint();

            var top = GetHexTopWorldPoint(tile.transform);
            float outer = hexScale * HexMath.OuterRadius;
            const float cornerInset = 0.72f;
            return new Vector3(
                top.x + outer * cornerInset * 0.8660254f,
                top.y + 0.002f,
                top.z + outer * cornerInset * 0.5f);
        }

        private static Vector3 GetHexTopWorldPoint(Transform tileRoot)
        {
            float topY = tileRoot.position.y + tileRoot.lossyScale.y * HexPrismMesh.TopY;
            return new Vector3(tileRoot.position.x, topY, tileRoot.position.z);
        }

        private readonly Dictionary<HexCoord, HexTileView> _tiles = new();
        private readonly Dictionary<HexCoord, HexTileView> _fishShoalViews = new();
        private readonly HashSet<HexCoord> _boardTileCoords = new();
        private GameController _controller;
        private int _lastBuildingVisualCount;


        public void Initialize(GameController controller)
        {
            _controller = controller;
            BuildBoard(controller.State.Board);
            Refresh(controller.State);
        }

        public void Rebuild(GameController controller)
        {
            _controller = controller;
            ClearBoard();
            BuildBoard(controller.State.Board);
            Refresh(controller.State);
        }

        private void ClearBoard()
        {
            if (boardRoot != null)
            {
                for (int i = boardRoot.childCount - 1; i >= 0; i--)
                    DestroySceneObject(boardRoot.GetChild(i).gameObject);
            }

            _tiles.Clear();
            _fishShoalViews.Clear();
            _boardTileCoords.Clear();
            ColonistPortBadgeSizing.ResetBoardSession();
        }

        private static void DestroySceneObject(GameObject target)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEngine.Object.DestroyImmediate(target);
                return;
            }
#endif
            ColonistSceneDestroy.SafeDestroy(target);
        }

        private void BuildBoard(BoardState board)
        {
            if (boardRoot == null) boardRoot = transform;

            DisableLegacySceneTable();

            _boardTileCoords.Clear();
            foreach (var kvp in board.Tiles)
            {
                if (IsLandHexCoord(kvp.Value))
                    _boardTileCoords.Add(kvp.Key);
            }

            foreach (var kvp in board.Tiles)
            {
                var tile = CreateHexTile(kvp.Value);
                _tiles[kvp.Key] = tile;
            }

            BuildCoastalFishShoals(board);

            if (ColonistPresentation.IsActive)
            {
                ColonistBoardVisualScale.UpdateFromBoard(board, hexScale);
                ColonistPortBadgeSizing.ConfigureFromTiles(_tiles.Values);
            }

            CreateTableSurface(board);
            CreatePortMarkers(board);
        }

        private void BuildCoastalFishShoals(BoardState board)
        {
            if (!ColonistPresentation.IsActive || board == null)
                return;

            foreach (var kvp in board.SeaFishGrounds)
            {
                if (!SeaFrameSlotLayout.TryGetFishShoalPlacement(
                        board, kvp.Key, hexScale, out FishShoalPlacement placement))
                    continue;

                float topY = ResolveCoastalFishShoalTopY(board, kvp.Key, placement);
                var view = ColonistCoastalFishShoalVisual.Build(
                    boardRoot, kvp.Value, placement, hexScale, topY);
                _fishShoalViews[kvp.Key] = view;
            }
        }

        private float ResolveCoastalFishShoalTopY(
            BoardState board,
            HexCoord seaCoord,
            FishShoalPlacement placement)
        {
            foreach (var kvp in _tiles)
            {
                for (int d = 0; d < 6; d++)
                {
                    if (HexMath.GetNeighbor(kvp.Key, d) != seaCoord)
                        continue;

                    return GetHexTopWorldPoint(kvp.Value.transform).y;
                }
            }

            float height = ColonistPresentation.IsActive ? tileHeight * 0.25f : tileHeight;
            return height * HexPrismMesh.TopY;
        }

        private void CreateTableSurface(BoardState board)
        {
            float boundingRadius = ColonistPresentation.IsActive
                ? TableCameraFraming.ComputeBoardBoundingRadius(GetLandCoords(board), hexScale)
                : TableCameraFraming.ComputeBoardBoundingRadius(board, hexScale);

            if (!ColonistPresentation.IsActive)
            {
                CreateTableDisk(
                    "BoardSurface",
                    TableCameraFraming.TableSurfaceLocalY,
                    TableCameraFraming.ComputeTableDiskScale(boundingRadius),
                    BoardSurfaceMaterial.Create());
            }

            if (ColonistPresentation.IsActive)
            {
                float colonistHexHeight = tileHeight * 0.25f;
                ColonistBoardSurfaces.CreateOceanAndCoast(boardRoot, board, boundingRadius, colonistHexHeight, hexScale);
            }
            else
            {
                CreateTableDisk(
                    "WaterSurface",
                    TableCameraFraming.WaterSurfaceLocalY,
                    TableCameraFraming.ComputeWaterDiskScale(boundingRadius),
                    BoardWaterMaterial.Create());
            }

            if (!ColonistPresentation.IsActive)
            {
                CreateTableDisk(
                    "FeltSurface",
                    TableCameraFraming.FeltSurfaceLocalY,
                    TableCameraFraming.ComputeFeltDiskScale(boundingRadius),
                    BoardFeltMaterial.Create());
            }
        }

        private void CreateTableDisk(string name, float localY, Vector3 localScale, Material material)
        {
            var disk = new GameObject(name);
            disk.transform.SetParent(boardRoot, false);
            disk.transform.SetAsFirstSibling();
            disk.transform.localPosition = new Vector3(0f, localY, 0f);
            disk.transform.localScale = localScale;

            var meshFilter = disk.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = ThinDiskMesh.Shared;

            var renderer = disk.AddComponent<MeshRenderer>();
            renderer.material = material;
        }

        private static void DisableLegacySceneTable()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
                return;

            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == "Table")
                    root.SetActive(false);
            }
        }

        private void CreatePortMarkers(BoardState board)
        {
            var ports = PortAccess.DiscoverPorts(board);
            foreach (var port in ports)
            {
                PortMarkerVisuals.Create(
                    boardRoot,
                    port.Vertex,
                    hexScale,
                    tileHeight,
                    board,
                    port.IsGeneric,
                    port.SpecificResource,
                    port.IsGeneric
                        ? Color.white
                        : GetResourceColor(port.SpecificResource!.Value));
            }
        }

        private static bool IsSeaFishTile(HexTileData data) =>
            BoardState.IsSeaFishGround(data);

        private static bool IsLandHexCoord(HexTileData data) =>
            !BoardState.IsSeaFishGround(data);

        private IEnumerable<HexCoord> GetLandTileCoords()
        {
            foreach (var kvp in _tiles)
            {
                if (!IsSeaFishTile(kvp.Value.Data))
                    yield return kvp.Key;
            }
        }

        private static IEnumerable<HexCoord> GetLandCoords(BoardState board)
        {
            foreach (var kvp in board.Tiles)
            {
                if (!IsSeaFishTile(kvp.Value))
                    yield return kvp.Key;
            }
        }

        private HexTileView CreateHexTile(HexTileData data)
        {
            var go = new GameObject($"Hex_{data.Coord.Q}_{data.Coord.R}");
            go.transform.SetParent(boardRoot, false);

            var (x, z) = HexMath.ToWorldPosition(data.Coord, hexScale);
            go.transform.localPosition = new Vector3(x, 0f, z);
            float height = ColonistPresentation.IsActive ? tileHeight * 0.25f : tileHeight;
            go.transform.localScale = new Vector3(1f, height, 1f);

            Renderer renderer;
            {
                var mesh = HexPrismMesh.Create(data.Coord, hexScale);
                var meshFilter = go.AddComponent<MeshFilter>();
                meshFilter.sharedMesh = mesh;

                renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = ColonistPresentation.IsActive
                    ? BoardTerrainMaterialSlots.GetTileMaterialInstance(data)
                    : BuiltInMaterials.Create(BoardResourceColors.GetTileFace(data));

                var collider = go.AddComponent<MeshCollider>();
                collider.sharedMesh = mesh;
            }

            HexCartoonTileDecor.ApplyOutline(go, data.Coord, hexScale, _boardTileCoords);
            if (ColonistPresentation.IsActive)
            {
                if (!data.IsDesert && !IsSeaFishTile(data))
                    ColonistHexTileIcon.Apply(go, data, hexScale);
                if (data.IsDesert && !data.IsFishTile)
                    ColonistDesertDuneDecor.Apply(go, hexScale);
                if (data.IsDesert && data.IsFishTile)
                    ColonistDesertLakeVisual.Apply(go, hexScale);
                if (data.IsFishTile)
                    ColonistFishTileVisual.Apply(go, data, hexScale);
            }
            else
                HexCartoonTileDecor.ApplyTopCap(go, data, hexScale, go.transform.lossyScale);

            var view = go.AddComponent<HexTileView>();
            view.Initialize(data, renderer);
            view.RefreshNumberToken(data);
            return view;
        }

        public void Refresh(GameState state)
        {
            foreach (var kvp in state.Board.Tiles)
            {
                if (_tiles.TryGetValue(kvp.Key, out var view))
                {
                    view.Refresh(kvp.Value);
                    view.RefreshNumberToken(kvp.Value);
                }
            }

            foreach (var kvp in state.Board.SeaFishGrounds)
            {
                if (_fishShoalViews.TryGetValue(kvp.Key, out var view))
                {
                    view.Refresh(kvp.Value);
                    view.RefreshNumberToken(kvp.Value);
                }
            }

            RefreshBuildings(state);
            RefreshRobber(state);
            RefreshEventOverlays(state);
        }

        private void RefreshRobber(GameState state)
        {
            foreach (var tile in _tiles.Values)
                tile.SetRobberVisible(false);

            if (!state.Board.RobberTile.HasValue) return;
            var coord = state.Board.RobberTile.Value;
            if (_tiles.TryGetValue(coord, out var robberView))
                robberView.SetRobberVisible(true);
        }

        private void RefreshEventOverlays(GameState state)
        {
            foreach (var kvp in _tiles)
            {
                var overlay = EventBoardVisual.TryGetOverlay(state, kvp.Key, out var kind)
                    ? kind
                    : EventTileOverlayKind.None;
                kvp.Value.SetEventOverlay(overlay);
            }
        }

        public IEnumerator PulseProductionHex(HexCoord coord, float duration)
        {
            if (!_tiles.TryGetValue(coord, out var view))
                yield break;
            yield return view.PulseProduction(duration);
        }

        public IEnumerator PlayProductionHighlight(IReadOnlyList<HexCoord> producing, float duration)
        {
            var producingSet = new HashSet<HexCoord>(producing);
            foreach (var kvp in _tiles)
                kvp.Value.SetDimmed(!producingSet.Contains(kvp.Key));

            foreach (var hex in producing)
            {
                if (_tiles.TryGetValue(hex, out var view))
                    yield return view.PulseProduction(duration);
            }

            foreach (var view in _tiles.Values)
                view.SetDimmed(false);
        }

        public void SetHexDimAll(bool dimmed)
        {
            foreach (var view in _tiles.Values)
                view.SetDimmed(dimmed);
        }

        public bool TryGetHexWorldCenter(HexCoord coord, out Vector3 worldCenter)
        {
            if (_tiles.TryGetValue(coord, out var view))
            {
                worldCenter = view.transform.position + Vector3.up * (tileHeight * 0.5f);
                return true;
            }

            var (x, z) = HexMath.ToWorldPosition(coord, hexScale);
            var local = new Vector3(x, tileHeight, z);
            if (boardRoot != null)
            {
                worldCenter = boardRoot.TransformPoint(local);
                return true;
            }

            worldCenter = transform.TransformPoint(local);
            return true;
        }

        private void RefreshBuildings(GameState state)
        {
            if (buildingsRoot == null)
            {
                var root = new GameObject("Buildings");
                root.transform.SetParent(transform, false);
                buildingsRoot = root.transform;
            }

            int priorCount = buildingsRoot.childCount;
            for (int i = buildingsRoot.childCount - 1; i >= 0; i--)
                DestroySceneObject(buildingsRoot.GetChild(i).gameObject);

            foreach (var kvp in state.Board.VertexBuildings)
            {
                var (building, owner) = kvp.Value;
                var (x, z) = HexMath.VertexToWorldPosition(kvp.Key, hexScale);
                var position = new Vector3(x, 0f, z);
                var color = PlayerPieceVisuals.ColorForPlayer(owner);

                if (building == BuildingType.City)
                    PlayerPieceVisuals.CreateCity(buildingsRoot, position, color, tileHeight);
                else
                    PlayerPieceVisuals.CreateSettlement(buildingsRoot, position, color, tileHeight);
            }

            foreach (var road in state.Board.Roads)
            {
                var (mx, mz) = HexMath.EdgeMidpoint(road.Key, hexScale);
                float angle = HexMath.EdgeAngle(road.Key);
                var (ax, az) = HexMath.VertexToWorldPosition(road.Key.A, hexScale);
                var (bx, bz) = HexMath.VertexToWorldPosition(road.Key.B, hexScale);
                float length = Vector3.Distance(new Vector3(ax, 0, az), new Vector3(bx, 0, bz));

                bool disabled = state.Board.DisabledRoads.Contains(road.Key);
                var color = disabled ? Color.gray : PlayerPieceVisuals.ColorForPlayer(road.Value);
                PlayerPieceVisuals.CreateRoad(
                    buildingsRoot,
                    new Vector3(mx, 0f, mz),
                    angle,
                    length,
                    color,
                    tileHeight);
            }

            if (buildingsRoot.childCount > priorCount && buildingsRoot.childCount > _lastBuildingVisualCount)
            {
                var placed = buildingsRoot.GetChild(buildingsRoot.childCount - 1);
                StartCoroutine(SimpleTweens.ScalePop(placed, 0.26f, 1.14f));
            }

            _lastBuildingVisualCount = buildingsRoot.childCount;
        }

        private static Color GetResourceColor(ResourceType resource) =>
            BoardResourceColors.GetHudSwatch(resource);
    }
}
