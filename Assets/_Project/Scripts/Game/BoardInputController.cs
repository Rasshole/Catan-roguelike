using System.Collections.Generic;
using System.Linq;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Turn;
using UnityEngine;
using Edge = CatanRoguelike.Core.Hex.HexMath.Edge;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;

namespace CatanRoguelike.Game
{
    /// <summary>Click vertices/edges on the 3D board to build (Catan-style corner placement).</summary>
    public sealed class BoardInputController : MonoBehaviour
    {
        [SerializeField] private BoardView boardView;
        [SerializeField] private float pickRadius = 0.35f;

        private GameController _controller;
        private Transform _highlightsRoot;
        private readonly List<GameObject> _highlights = new();
        private readonly List<GameObject> _setupSpots = new();
        private string _setupSpotsKey;
        private Vertex? _hoverVertex;
        private Edge? _hoverEdge;
        private BuildMode _mode = BuildMode.None;

        public event System.Action PlacementCancelled;

        private static readonly Color ValidVertexColor = new(0.2f, 1f, 0.3f, 0.55f);
        private static readonly Color ValidEdgeColor = new(1f, 0.85f, 0.1f, 0.55f);
        private static readonly Color CityColor = new(1f, 0.5f, 0.9f, 0.6f);

        private enum BuildMode { None, Auto, Settlement, Road, City }

        public void Initialize(GameController controller, BoardView view)
        {
            _controller = controller;
            boardView = view;
            if (_highlightsRoot == null)
            {
                var go = new GameObject("PlacementHighlights");
                go.transform.SetParent(transform, false);
                _highlightsRoot = go.transform;
            }
        }

        private void Update()
        {
            if (_controller == null || boardView == null) return;
            if (_controller.State.Winner.HasValue) return;

            if (!_controller.State.IsSetupPhase
                && _controller.State.Phase == GamePhase.DayPlayerActions
                && (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1)))
            {
                if (_mode != BuildMode.None)
                {
                    _mode = BuildMode.None;
                    ClearHighlights();
                    PlacementCancelled?.Invoke();
                }
            }

            if (_controller.State.Phase == GamePhase.DayPlaceRobberSeven)
            {
                UpdateRobberPlacementHighlights();
                HandleRobberPlacementClick();
                return;
            }

            if (!IsPlayerBuildPhase())
            {
                ClearSetupSpots();
                return;
            }

            RefreshSetupSpots();
            UpdateHighlights();
            HandleClick();
        }

        private HexCoord? _robberHoverHex;

        private void UpdateRobberPlacementHighlights()
        {
            ClearHighlights();
            _robberHoverHex = null;
            var hit = RaycastBoardPoint();
            if (!hit.HasValue || _controller == null)
                return;

            float scale = boardView.HexScale;
            HexCoord? nearest = null;
            float best = float.MaxValue;
            foreach (var coord in _controller.State.Board.Tiles.Keys)
            {
                var (x, z) = HexMath.ToWorldPosition(coord, scale);
                float d = (hit.Value.x - x) * (hit.Value.x - x) + (hit.Value.z - z) * (hit.Value.z - z);
                if (d < best)
                {
                    best = d;
                    nearest = coord;
                }
            }

            if (!nearest.HasValue)
                return;

            _robberHoverHex = nearest;
            var (hx, hz) = HexMath.ToWorldPosition(nearest.Value, scale);
            var marker = PlacementMarkerVisuals.CreateSettlementGhost(
                _highlightsRoot,
                new Vector3(hx, 0f, hz),
                new Color(0.2f, 0.85f, 1f, 0.65f),
                boardView.TileHeight);
            _highlights.Add(marker);
        }

        private void HandleRobberPlacementClick()
        {
            if (!Input.GetMouseButtonDown(0) || IsPointerOverUI())
                return;
            if (!_robberHoverHex.HasValue)
                return;
            _controller.PlaceRobberForSeven(_robberHoverHex.Value);
        }

        private bool IsPlayerBuildPhase()
        {
            if (!_controller.State.RunSetupComplete)
                return false;
            if (_controller.State.IsSetupPhase)
                return true;
            return _controller.State.Phase == GamePhase.DayPlayerActions && _mode != BuildMode.None;
        }

        private BuildMode ActiveMode =>
            _controller.State.IsSetupPhase ? BuildMode.Auto : _mode;

        private void UpdateHighlights()
        {
            ClearHighlights();
            _hoverVertex = null;
            _hoverEdge = null;

            var hit = RaycastBoardPoint();
            if (!hit.HasValue) return;

            float scale = boardView.HexScale;
            var player = PlayerId.Human;

            var mode = ActiveMode;
            if (mode == BuildMode.City || (mode == BuildMode.Auto && IsCityOnlyContext()))
            {
                var cities = _controller.GetUpgradeableCities(player).ToList();
                _hoverVertex = FindNearestVertex(hit.Value, cities, scale);
                if (_hoverVertex.HasValue)
                    SpawnVertexMarker(_hoverVertex.Value, CityColor, scale, isCity: true);
                return;
            }

            if (mode == BuildMode.Settlement || ShouldPickSettlement())
            {
                var settlements = _controller.GetValidSettlements(player).ToList();
                _hoverVertex = FindNearestVertex(hit.Value, settlements, scale);
                if (_hoverVertex.HasValue)
                    SpawnVertexMarker(_hoverVertex.Value, ValidVertexColor, scale, isCity: false);
            }

            if (mode == BuildMode.Road || ShouldPickRoad())
            {
                var roads = _controller.GetValidRoads(player).ToList();
                _hoverEdge = FindNearestEdge(hit.Value, roads, scale);
                if (_hoverEdge.HasValue)
                    SpawnEdgeMarker(_hoverEdge.Value, ValidEdgeColor, scale);
            }

            if (mode == BuildMode.Auto && _controller.State.Phase == GamePhase.DayPlayerActions)
            {
                var settlements = _controller.GetValidSettlements(player).ToList();
                var roads = _controller.GetValidRoads(player).ToList();
                var nearV = FindNearestVertex(hit.Value, settlements, scale);
                var nearE = FindNearestEdge(hit.Value, roads, scale);
                float distV = nearV.HasValue ? Dist(hit.Value, nearV.Value, scale) : float.MaxValue;
                float distE = nearE.HasValue ? DistToEdge(hit.Value, nearE.Value, scale) : float.MaxValue;

                if (distV <= distE && nearV.HasValue)
                {
                    _hoverEdge = null;
                    _hoverVertex = nearV;
                }
                else if (nearE.HasValue)
                {
                    _hoverVertex = null;
                    _hoverEdge = nearE;
                }
            }
        }

        private bool IsCityOnlyContext() =>
            _controller.State.Phase == GamePhase.DayPlayerActions
            && _controller.GetUpgradeableCities(PlayerId.Human).Any()
            && !_controller.GetValidSettlements(PlayerId.Human).Any()
            && !_controller.GetValidRoads(PlayerId.Human).Any();

        private bool ShouldPickSettlement()
        {
            var mode = ActiveMode;
            if (mode == BuildMode.Road) return false;
            if (_controller.State.IsSetupPhase)
                return IsSettlementSetupPhase();
            return mode == BuildMode.Settlement;
        }

        private bool ShouldPickRoad()
        {
            var mode = ActiveMode;
            if (mode == BuildMode.Settlement) return false;
            if (_controller.State.IsSetupPhase)
                return IsRoadSetupPhase();
            return mode == BuildMode.Road;
        }

        private bool IsSettlementSetupPhase()
        {
            var p = _controller.State.Phase;
            return p == GamePhase.SetupPlayerSettlement1 || p == GamePhase.SetupPlayerSettlement2;
        }

        private bool IsRoadSetupPhase()
        {
            var p = _controller.State.Phase;
            return p == GamePhase.SetupPlayerRoad1 || p == GamePhase.SetupPlayerRoad2;
        }

        private void HandleClick()
        {
            if (!Input.GetMouseButtonDown(0)) return;
            if (IsPointerOverUI()) return;

            var player = PlayerId.Human;

            if (_hoverVertex.HasValue)
            {
                if (ActiveMode == BuildMode.City)
                    _controller.UpgradeCity(_hoverVertex.Value, player);
                else if (ShouldPickSettlement())
                    _controller.PlaceSettlement(_hoverVertex.Value, player);
            }
            else if (_hoverEdge.HasValue)
            {
                _controller.PlaceRoad(_hoverEdge.Value, player);
            }
        }

        private static bool IsPointerOverUI() =>
            UnityEngine.EventSystems.EventSystem.current != null
            && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();

        private Vector3? RaycastBoardPoint()
        {
            var cam = Camera.main;
            if (cam == null) return null;

            var ray = cam.ScreenPointToRay(Input.mousePosition);
            float planeY = BoardPickPlane.GetPickPlaneY(boardView.TileHeight);
            if (!BoardPickPlane.TryRaycast(ray, planeY, out var hit))
                return null;
            return hit;
        }

        private float Dist(Vector3 point, Vertex v, float scale)
        {
            var (x, z) = HexMath.VertexToWorldPosition(v, scale);
            return (point.x - x) * (point.x - x) + (point.z - z) * (point.z - z);
        }

        private float DistToEdge(Vector3 point, Edge e, float scale)
        {
            var (mx, mz) = HexMath.EdgeMidpoint(e, scale);
            return (point.x - mx) * (point.x - mx) + (point.z - mz) * (point.z - mz);
        }

        private Vertex? FindNearestVertex(Vector3 point, List<Vertex> candidates, float scale)
        {
            Vertex? best = null;
            float bestDist = float.MaxValue;
            float maxDist = pickRadius * scale;

            foreach (var v in candidates)
            {
                float dist = Dist(point, v, scale);
                if (dist < bestDist && dist <= maxDist * maxDist)
                {
                    bestDist = dist;
                    best = v;
                }
            }
            return best;
        }

        private Edge? FindNearestEdge(Vector3 point, List<Edge> candidates, float scale)
        {
            Edge? best = null;
            float bestDist = float.MaxValue;
            float maxDist = pickRadius * scale;

            foreach (var e in candidates)
            {
                float dist = DistToEdge(point, e, scale);
                if (dist < bestDist && dist <= maxDist * maxDist)
                {
                    bestDist = dist;
                    best = e;
                }
            }
            return best;
        }

        /// <summary>
        /// v143: during setup every valid corner (or road edge) shows a small pulsing circle/dash in the human's colour
        /// so the choices read at a glance without a field of ghost houses.
        /// </summary>
        private void RefreshSetupSpots()
        {
            if (!_controller.State.IsSetupPhase)
            {
                ClearSetupSpots();
                return;
            }

            bool road = IsRoadSetupPhase();
            bool settlement = IsSettlementSetupPhase();
            string key = _controller.State.Phase.ToString() + (road ? _controller.GetValidRoads(PlayerId.Human).Count()
                : settlement ? _controller.GetValidSettlements(PlayerId.Human).Count() : 0);
            if (key == _setupSpotsKey && _setupSpots.Count > 0)
                return;
            ClearSetupSpots();
            _setupSpotsKey = key;
            float scale = boardView.HexScale;
            float top = boardView.TileHeight;
            var color = Color.Lerp(PlayerPieceVisuals.ColorForPlayer(PlayerId.Human), Color.white, 0.12f);
            if (settlement)
            {
                foreach (var v in _controller.GetValidSettlements(PlayerId.Human))
                {
                    var (x, z) = HexMath.VertexToWorldPosition(v, scale);
                    _setupSpots.Add(PlacementMarkerVisuals.CreateSpotCircle(_highlightsRoot, new Vector3(x, 0f, z), color,
                        top, scale));
                }
            }
            else if (road)
            {
                foreach (var e in _controller.GetValidRoads(PlayerId.Human))
                {
                    var (mx, mz) = HexMath.EdgeMidpoint(e, scale);
                    var (ax, az) = HexMath.VertexToWorldPosition(e.A, scale);
                    var (bx, bz) = HexMath.VertexToWorldPosition(e.B, scale);
                    float length = Vector3.Distance(new Vector3(ax, 0, az), new Vector3(bx, 0, bz));
                    _setupSpots.Add(PlacementMarkerVisuals.CreateRoadDash(_highlightsRoot, new Vector3(mx, 0f, mz),
                        HexMath.EdgeAngle(e), length, color, top, scale));
                }
            }
        }

        private void ClearSetupSpots()
        {
            foreach (var h in _setupSpots)
                if (h != null) ColonistSceneDestroy.SafeDestroy(h);
            _setupSpots.Clear();
            _setupSpotsKey = null;
        }

        public int SetupSpotCountForTests => _setupSpots.Count;

        private Color HoverSpotColor() =>
            Color.Lerp(PlayerPieceVisuals.ColorForPlayer(PlayerId.Human), Color.white, 0.45f);

        private void SpawnVertexMarker(Vertex v, Color color, float scale, bool isCity)
        {
            var (x, z) = HexMath.VertexToWorldPosition(v, scale);
            var position = new Vector3(x, 0f, z);
            float hexTopY = boardView.TileHeight;

            // v143: a settlement spot is a larger circle (not a ghost house); city upgrades keep the city preview.
            GameObject go = isCity
                ? PlacementMarkerVisuals.CreateCityGhost(_highlightsRoot, position, color, hexTopY)
                : PlacementMarkerVisuals.CreateSpotCircle(_highlightsRoot, position, HoverSpotColor(), hexTopY, scale,
                    1.55f, pulse: false);
            _highlights.Add(go);
        }

        private void SpawnEdgeMarker(Edge e, Color color, float scale)
        {
            var (mx, mz) = HexMath.EdgeMidpoint(e, scale);
            var (ax, az) = HexMath.VertexToWorldPosition(e.A, scale);
            var (bx, bz) = HexMath.VertexToWorldPosition(e.B, scale);
            float length = Vector3.Distance(new Vector3(ax, 0, az), new Vector3(bx, 0, bz));

            var go = PlacementMarkerVisuals.CreateRoadDash(
                _highlightsRoot,
                new Vector3(mx, 0f, mz),
                HexMath.EdgeAngle(e),
                length,
                HoverSpotColor(),
                boardView.TileHeight,
                scale,
                1.45f,
                pulse: false);
            _highlights.Add(go);
        }

        private void ClearHighlights()
        {
            foreach (var h in _highlights)
                if (h != null) ColonistSceneDestroy.SafeDestroy(h);
            _highlights.Clear();
        }

        public void SetBuildModeSettlement() => _mode = BuildMode.Settlement;
        public void SetBuildModeRoad() => _mode = BuildMode.Road;
        public void SetBuildModeCity() => _mode = BuildMode.City;
        public void SetBuildModeAuto() => _mode = BuildMode.Auto;
        public void CancelBuildMode() => _mode = BuildMode.None;

        public bool IsPlacementModeActive => _mode != BuildMode.None;
    }
}
