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
                return TableCameraFraming.ComputeBoardBoundingRadius(_tiles.Keys, hexScale);

            if (_controller != null)
                return TableCameraFraming.ComputeBoardBoundingRadius(_controller.State.Board, hexScale);

            return 0f;
        }

        private readonly Dictionary<HexCoord, HexTileView> _tiles = new();
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
            Destroy(target);
        }

        private void BuildBoard(BoardState board)
        {
            if (boardRoot == null) boardRoot = transform;

            DisableLegacySceneTable();

            foreach (var kvp in board.Tiles)
            {
                var tile = CreateHexTile(kvp.Value);
                _tiles[kvp.Key] = tile;
            }

            CreateTableSurface(board);
            CreatePortMarkers(board);
        }

        private void CreateTableSurface(BoardState board)
        {
            float boundingRadius = TableCameraFraming.ComputeBoardBoundingRadius(board, hexScale);

            if (!ColonistPresentation.IsActive)
            {
                CreateTableDisk(
                    "BoardSurface",
                    TableCameraFraming.TableSurfaceLocalY,
                    TableCameraFraming.ComputeTableDiskScale(boundingRadius),
                    BoardSurfaceMaterial.Create());
            }

            if (ColonistPresentation.IsActive)
                ColonistBoardSurfaces.CreateOceanAndCoast(boardRoot, boundingRadius);
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
                    port.IsGeneric,
                    port.SpecificResource,
                    port.IsGeneric
                        ? Color.white
                        : GetResourceColor(port.SpecificResource!.Value));
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

            var mesh = HexPrismMesh.Create(data.Coord, hexScale);
            var meshFilter = go.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;

            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = BuiltInMaterials.Create(BoardResourceColors.GetTileFace(data));

            var collider = go.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;

            if (ColonistPresentation.IsActive)
                ColonistHexTileIcon.Apply(go, data);
            else
                HexCartoonTileDecor.Apply(go, data.Coord, hexScale, data);

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
