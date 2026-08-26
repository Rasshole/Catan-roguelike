using System.Collections.Generic;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Shop;
using UnityEngine;
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

        private readonly Dictionary<HexCoord, HexTileView> _tiles = new();
        private GameController _controller;
        private int _tileCount;

        private static readonly Color WoodColor = new(0.2f, 0.55f, 0.2f);
        private static readonly Color BrickColor = new(0.7f, 0.35f, 0.2f);
        private static readonly Color WheatColor = new(0.9f, 0.85f, 0.2f);
        private static readonly Color SheepColor = new(0.5f, 0.85f, 0.5f);
        private static readonly Color StoneColor = new(0.55f, 0.55f, 0.6f);

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
                    Destroy(boardRoot.GetChild(i).gameObject);
            }

            _tiles.Clear();
        }

        private void BuildBoard(BoardState board)
        {
            if (boardRoot == null) boardRoot = transform;

            _tileCount = board.Tiles.Count;
            CreateTableSurface();

            foreach (var kvp in board.Tiles)
            {
                var tile = CreateHexTile(kvp.Value);
                _tiles[kvp.Key] = tile;
            }

            CreatePortMarkers(board);
        }

        private void CreateTableSurface()
        {
            var table = GameObject.CreatePrimitive(PrimitiveType.Cube);
            table.name = "BoardSurface";
            table.transform.SetParent(boardRoot, false);
            table.transform.localPosition = new Vector3(0f, -0.08f, 0f);
            float scale = Mathf.Sqrt(_tileCount / 7f) * hexScale;
            table.transform.localScale = new Vector3(10f * scale / hexScale, 0.06f, 9f * scale / hexScale);

            var renderer = table.GetComponent<Renderer>();
            renderer.material = BuiltInMaterials.Create(new Color(0.76f, 0.65f, 0.45f));
        }

        private void CreatePortMarkers(BoardState board)
        {
            var ports = PortAccess.DiscoverPorts(board);
            foreach (var port in ports)
            {
                var (x, z) = HexMath.VertexToWorldPosition(port.Vertex, hexScale);
                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.name = port.IsGeneric ? "Port_Generic" : $"Port_{port.SpecificResource}";
                go.transform.SetParent(boardRoot, false);
                go.transform.position = new Vector3(x, 0.05f, z);
                go.transform.localScale = new Vector3(0.2f, 0.04f, 0.2f);

                var renderer = go.GetComponent<Renderer>();
                renderer.material = BuiltInMaterials.Create(port.IsGeneric
                    ? new Color(0.9f, 0.9f, 0.95f)
                    : GetResourceColor(port.SpecificResource!.Value) * 0.7f + Color.white * 0.3f);
                Destroy(go.GetComponent<Collider>());
            }
        }

        private HexTileView CreateHexTile(HexTileData data)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = $"Hex_{data.Coord.Q}_{data.Coord.R}";
            go.transform.SetParent(boardRoot, false);

            var (x, z) = HexMath.ToWorldPosition(data.Coord, hexScale);
            go.transform.localPosition = new Vector3(x, 0f, z);
            go.transform.localScale = new Vector3(hexScale * 1.05f, tileHeight, hexScale * 1.05f);

            var renderer = go.GetComponent<Renderer>();
            renderer.material = BuiltInMaterials.Create(GetResourceColor(data.Resource));

            var view = go.AddComponent<HexTileView>();
            view.Initialize(data, renderer);
            return view;
        }

        public void Refresh(GameState state)
        {
            foreach (var kvp in state.Board.Tiles)
            {
                if (_tiles.TryGetValue(kvp.Key, out var view))
                    view.Refresh(kvp.Value);
            }

            RefreshBuildings(state);
            RefreshRobber(state);
        }

        private void RefreshRobber(GameState state)
        {
            foreach (var tile in _tiles.Values)
                tile.SetRobberVisible(false);

            if (!state.Board.RobberTile.HasValue) return;
            var coord = state.Board.RobberTile.Value;
            if (_tiles.TryGetValue(coord, out var tile))
                tile.SetRobberVisible(true);
        }

        private void RefreshBuildings(GameState state)
        {
            if (buildingsRoot == null)
            {
                var root = new GameObject("Buildings");
                root.transform.SetParent(transform, false);
                buildingsRoot = root.transform;
            }

            for (int i = buildingsRoot.childCount - 1; i >= 0; i--)
                Destroy(buildingsRoot.GetChild(i).gameObject);

            foreach (var kvp in state.Board.VertexBuildings)
            {
                var (building, owner) = kvp.Value;
                var (x, z) = HexMath.VertexToWorldPosition(kvp.Key, hexScale);
                float y = building == BuildingType.City ? 0.45f : 0.32f;
                float size = building == BuildingType.City ? 0.22f : 0.16f;

                var go = GameObject.CreatePrimitive(
                    building == BuildingType.City ? PrimitiveType.Cube : PrimitiveType.Cylinder);
                go.transform.SetParent(buildingsRoot, false);
                go.transform.position = new Vector3(x, y, z);
                go.transform.localScale = new Vector3(size, size * 1.2f, size);

                var renderer = go.GetComponent<Renderer>();
                renderer.material = BuiltInMaterials.Create(owner == PlayerId.Human
                    ? new Color(0.2f, 0.4f, 0.9f)
                    : new Color(0.9f, 0.25f, 0.2f));
                Destroy(go.GetComponent<Collider>());
            }

            foreach (var road in state.Board.Roads)
            {
                var (mx, mz) = HexMath.EdgeMidpoint(road.Key, hexScale);
                float angle = HexMath.EdgeAngle(road.Key);
                var (ax, az) = HexMath.VertexToWorldPosition(road.Key.A, hexScale);
                var (bx, bz) = HexMath.VertexToWorldPosition(road.Key.B, hexScale);
                float length = Vector3.Distance(new Vector3(ax, 0, az), new Vector3(bx, 0, bz));

                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.transform.SetParent(buildingsRoot, false);
                go.transform.position = new Vector3(mx, 0.14f, mz);
                go.transform.rotation = Quaternion.Euler(0f, angle, 0f);
                go.transform.localScale = new Vector3(0.07f, 0.05f, length * 0.92f);

                bool disabled = state.Board.DisabledRoads.Contains(road.Key);
                var renderer = go.GetComponent<Renderer>();
                renderer.material = BuiltInMaterials.Create(disabled
                    ? Color.gray
                    : road.Value == PlayerId.Human
                        ? new Color(0.3f, 0.5f, 1f)
                        : new Color(1f, 0.3f, 0.3f));
                Destroy(go.GetComponent<Collider>());
            }
        }

        private static Color GetResourceColor(ResourceType resource) => resource switch
        {
            ResourceType.Wood => WoodColor,
            ResourceType.Brick => BrickColor,
            ResourceType.Wheat => WheatColor,
            ResourceType.Sheep => SheepColor,
            ResourceType.Stone => StoneColor,
            _ => Color.gray
        };
    }
}
