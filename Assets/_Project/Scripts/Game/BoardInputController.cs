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
        private Vertex? _hoverVertex;
        private Edge? _hoverEdge;
        private BuildMode _mode = BuildMode.Auto;

        private static readonly Color ValidVertexColor = new(0.2f, 1f, 0.3f, 0.55f);
        private static readonly Color ValidEdgeColor = new(1f, 0.85f, 0.1f, 0.55f);
        private static readonly Color CityColor = new(1f, 0.5f, 0.9f, 0.6f);

        private enum BuildMode { Auto, Settlement, Road, City }

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
            if (!IsPlayerBuildPhase()) return;

            UpdateHighlights();
            HandleClick();
        }

        private bool IsPlayerBuildPhase() =>
            _controller.State.RunSetupComplete
            && (_controller.State.IsSetupPhase
                || _controller.State.Phase == GamePhase.DayPlayerActions);

        private void UpdateHighlights()
        {
            ClearHighlights();
            _hoverVertex = null;
            _hoverEdge = null;

            var hit = RaycastBoardPoint();
            if (!hit.HasValue) return;

            float scale = boardView.HexScale;
            var player = PlayerId.Human;

            if (_mode == BuildMode.City || (_mode == BuildMode.Auto && IsCityOnlyContext()))
            {
                var cities = _controller.GetUpgradeableCities(player).ToList();
                _hoverVertex = FindNearestVertex(hit.Value, cities, scale);
                if (_hoverVertex.HasValue)
                    SpawnVertexMarker(_hoverVertex.Value, CityColor, 0.2f, scale);
                return;
            }

            if (_mode == BuildMode.Settlement || ShouldPickSettlement())
            {
                var settlements = _controller.GetValidSettlements(player).ToList();
                _hoverVertex = FindNearestVertex(hit.Value, settlements, scale);
                if (_hoverVertex.HasValue)
                    SpawnVertexMarker(_hoverVertex.Value, ValidVertexColor, 0.18f, scale);
            }

            if (_mode == BuildMode.Road || ShouldPickRoad())
            {
                var roads = _controller.GetValidRoads(player).ToList();
                _hoverEdge = FindNearestEdge(hit.Value, roads, scale);
                if (_hoverEdge.HasValue)
                    SpawnEdgeMarker(_hoverEdge.Value, ValidEdgeColor, scale);
            }

            if (_mode == BuildMode.Auto && _controller.State.Phase == GamePhase.DayPlayerActions)
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
            if (_mode == BuildMode.Road) return false;
            if (_controller.State.IsSetupPhase)
                return IsSettlementSetupPhase();
            return _mode == BuildMode.Settlement;
        }

        private bool ShouldPickRoad()
        {
            if (_mode == BuildMode.Settlement) return false;
            if (_controller.State.IsSetupPhase)
                return IsRoadSetupPhase();
            return _mode == BuildMode.Road;
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
                if (_mode == BuildMode.City)
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
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (!plane.Raycast(ray, out float enter)) return null;
            return ray.GetPoint(enter);
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

        private void SpawnVertexMarker(Vertex v, Color color, float size, float scale)
        {
            var (x, z) = HexMath.VertexToWorldPosition(v, scale);
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.transform.SetParent(_highlightsRoot, false);
            go.transform.position = new Vector3(x, 0.22f, z);
            go.transform.localScale = Vector3.one * size;
            Destroy(go.GetComponent<Collider>());
            var r = go.GetComponent<Renderer>();
            r.material = BuiltInMaterials.Create(color);
            _highlights.Add(go);
        }

        private void SpawnEdgeMarker(Edge e, Color color, float scale)
        {
            var (mx, mz) = HexMath.EdgeMidpoint(e, scale);
            var (ax, az) = HexMath.VertexToWorldPosition(e.A, scale);
            var (bx, bz) = HexMath.VertexToWorldPosition(e.B, scale);
            float length = Vector3.Distance(new Vector3(ax, 0, az), new Vector3(bx, 0, bz));

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.SetParent(_highlightsRoot, false);
            go.transform.position = new Vector3(mx, 0.18f, mz);
            go.transform.rotation = Quaternion.Euler(0f, HexMath.EdgeAngle(e), 0f);
            go.transform.localScale = new Vector3(0.1f, 0.06f, length * 0.95f);
            Destroy(go.GetComponent<Collider>());
            var r = go.GetComponent<Renderer>();
            r.material = BuiltInMaterials.Create(color);
            _highlights.Add(go);
        }

        private void ClearHighlights()
        {
            foreach (var h in _highlights)
                if (h != null) Destroy(h);
            _highlights.Clear();
        }

        public void SetBuildModeSettlement() => _mode = BuildMode.Settlement;
        public void SetBuildModeRoad() => _mode = BuildMode.Road;
        public void SetBuildModeCity() => _mode = BuildMode.City;
        public void SetBuildModeAuto() => _mode = BuildMode.Auto;
    }
}
