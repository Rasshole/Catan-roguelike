using System.Collections.Generic;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using UnityEngine;

namespace CatanRoguelike.Game
{
    public sealed class BoardView : MonoBehaviour
    {
        [SerializeField] private float hexScale = 1.2f;
        [SerializeField] private float tileHeight = 0.15f;
        [SerializeField] private Transform boardRoot;
        [SerializeField] private Transform buildingsRoot;

        private readonly Dictionary<HexCoord, HexTileView> _tiles = new();
        private GameController _controller;

        private static readonly Color WoodColor = new(0.2f, 0.55f, 0.2f);
        private static readonly Color BrickColor = new(0.7f, 0.35f, 0.2f);
        private static readonly Color WheatColor = new(0.9f, 0.85f, 0.2f);
        private static readonly Color SheepColor = new(0.5f, 0.85f, 0.5f);
        private static readonly Color StoneColor = new(0.55f, 0.55f, 0.6f);
        private static readonly Color RobberColor = new(0.15f, 0.15f, 0.15f);

        public void Initialize(GameController controller)
        {
            _controller = controller;
            BuildBoard(controller.State.Board);
            Refresh(controller.State);
        }

        private void BuildBoard(BoardState board)
        {
            if (boardRoot == null) boardRoot = transform;

            CreateTableSurface();

            foreach (var kvp in board.Tiles)
            {
                var tile = CreateHexTile(kvp.Value);
                _tiles[kvp.Key] = tile;
            }
        }

        private void CreateTableSurface()
        {
            var table = GameObject.CreatePrimitive(PrimitiveType.Cube);
            table.name = "BoardSurface";
            table.transform.SetParent(boardRoot, false);
            table.transform.localPosition = new Vector3(0f, -0.08f, 0f);
            table.transform.localScale = new Vector3(10f, 0.06f, 9f);

            var renderer = table.GetComponent<Renderer>();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard"));
            mat.color = new Color(0.76f, 0.65f, 0.45f);
            renderer.material = mat;
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
            renderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard"));
            renderer.material.color = GetResourceColor(data.Resource);

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

            // Clear and rebuild (placeholder — optimize later)
            for (int i = buildingsRoot.childCount - 1; i >= 0; i--)
                Destroy(buildingsRoot.GetChild(i).gameObject);

            foreach (var kvp in state.Board.VertexBuildings)
            {
                var (building, owner) = kvp.Value;
                var (x, z) = HexMath.ToWorldPosition(kvp.Key.Hex, hexScale);
                float y = building == BuildingType.City ? 0.5f : 0.35f;

                var go = GameObject.CreatePrimitive(
                    building == BuildingType.City ? PrimitiveType.Cube : PrimitiveType.Capsule);
                go.transform.SetParent(buildingsRoot, false);
                go.transform.localPosition = new Vector3(x, y, z);
                go.transform.localScale = Vector3.one * (building == BuildingType.City ? 0.35f : 0.25f);

                var renderer = go.GetComponent<Renderer>();
                renderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit")
                    ?? Shader.Find("Standard"));
                renderer.material.color = owner == PlayerId.Human
                    ? new Color(0.2f, 0.4f, 0.9f)
                    : new Color(0.9f, 0.25f, 0.2f);
            }

            foreach (var road in state.Board.Roads)
            {
                var midHex = road.Key.A.Hex;
                var (x, z) = HexMath.ToWorldPosition(midHex, hexScale);
                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.transform.SetParent(buildingsRoot, false);
                go.transform.localPosition = new Vector3(x, 0.12f, z);
                go.transform.localScale = new Vector3(0.08f, 0.04f, 0.4f);

                var renderer = go.GetComponent<Renderer>();
                renderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit")
                    ?? Shader.Find("Standard"));
                renderer.material.color = road.Value == PlayerId.Human
                    ? new Color(0.3f, 0.5f, 1f)
                    : new Color(1f, 0.3f, 0.3f);
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
