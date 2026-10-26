using System.Collections.Generic;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Match;
using UnityEngine;
using Edge = CatanRoguelike.Core.Hex.HexMath.Edge;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;

namespace CatanRoguelike.Game
{
    /// <summary>
    /// v135: board clicks for pass-and-play. Highlights every spot the acting human can use right now (setup
    /// placements, affordable builds, free roads, robber tiles) and turns a click into the matching
    /// <see cref="MatchCommand"/> via <see cref="MatchBoardPick"/>. Hidden while a pass screen is up or an AI acts.
    /// </summary>
    public sealed class HotSeatBoardInput : MonoBehaviour
    {
        private HotSeatSession _session;
        private BoardView _view;
        private Transform _root;
        private readonly List<GameObject> _markers = new();
        private GameObject _hover;
        private string _targetsKey;

        private static readonly Color CityHoverColor = new(1f, 0.5f, 0.9f, 0.75f);
        private static readonly Color RobberColor = new(0.2f, 0.85f, 1f, 0.65f);

        public int MarkerCountForTests => _markers.Count;

        private static readonly Color ShipCore = new(0.55f, 0.85f, 1f, 1f);
        private static readonly Color MovableShipColor = new(1f, 0.82f, 0.25f, 1f);

        /// <summary>v145 Seafarers: what a board click builds (Auto = land pieces; Ship / MoveShip from the HUD buttons).</summary>
        public MatchBuildMode Mode { get; set; } = MatchBuildMode.Auto;

        /// <summary>v145: the ship picked for <see cref="MatchBuildMode.MoveShip"/> (null until one is picked).</summary>
        public Edge? MoveFrom { get; set; }

        private int _modeSeat = -1;
        private MatchPhase _modePhase;

        /// <summary>v145: back to land building when the turn or phase changes (or a ship has sailed).</summary>
        private void ResetModeIfStale()
        {
            var state = _session.State;
            if (_modeSeat != state.ActingSeat || _modePhase != state.Phase)
            {
                if (_modeSeat >= 0 && (Mode != MatchBuildMode.Auto || MoveFrom.HasValue))
                {
                    Mode = MatchBuildMode.Auto;
                    MoveFrom = null;
                }

                _modeSeat = state.ActingSeat;
                _modePhase = state.Phase;
            }

            if (Mode == MatchBuildMode.MoveShip && state.ShipMovedThisTurn)
            {
                Mode = MatchBuildMode.Auto;
                MoveFrom = null;
            }
        }

        /// <summary>Same as a click on the board at world x/z (tests and captures).</summary>
        public MatchCommand ClickForTests(float x, float z)
        {
            if (!HumanCanClick)
                return null;
            ResetModeIfStale();
            return HandleClick(x, z, apply: true);
        }

        private MatchCommand HandleClick(float x, float z, bool apply)
        {
            if (Mode == MatchBuildMode.MoveShip && !MoveFrom.HasValue)
            {
                var pick = MatchBoardPick.PickMovableShip(_session.Match, x, z, _view.HexScale);
                if (pick.HasValue && apply)
                {
                    MoveFrom = pick;
                    _targetsKey = null;
                }

                return null;
            }

            var command = MatchBoardPick.Pick(_session.Match, Mode, x, z, _view.HexScale, MoveFrom);
            if (command != null && apply && (Intercept == null || !Intercept(command)))
            {
                _session.Apply(command);
                if (command.Type == MatchCommandType.MoveShip)
                {
                    Mode = MatchBuildMode.Auto;
                    MoveFrom = null;
                }
            }

            return command;
        }

        /// <summary>v136: the HUD may take a clicked command (e.g. a robber move that needs a victim choice).</summary>
        public System.Func<MatchCommand, bool> Intercept { get; set; }

        /// <summary>v136: true while a HUD modal (victim picker, secret picks) owns the input.</summary>
        public System.Func<bool> Blocked { get; set; }

        public void Bind(HotSeatSession session, BoardView view)
        {
            _session = session;
            _view = view;
            if (_root == null)
            {
                // Board-space root: this component lives on the HUD (a canvas), so parenting under it would put the
                // ghosts in UI space where the board camera never sees them. Pieces are placed in the board view's
                // local space (like BoardView's own buildings), so the ghosts go under the view too.
                _root = new GameObject("HotSeatPlacementHighlights").transform;
            }

            if (view != null && _root.parent != view.transform)
                _root.SetParent(view.transform, false);

            Clear();
        }

        public void Unbind()
        {
            _session = null;
            Intercept = null;
            Blocked = null;
            Clear();
        }

        private void OnDestroy()
        {
            Clear();
            if (_root != null)
                ColonistSceneDestroy.SafeDestroy(_root.gameObject);
            _root = null;
        }

        public Transform HighlightsRootForTests => _root;

        private bool HumanCanClick =>
            _session != null && _view != null && !_session.IsOver && !_session.PassScreenPending && !_session.Acting.IsAi
            && (Blocked == null || !Blocked());

        /// <summary>Rebuild the static highlights when the situation changes (also used by tests/captures).</summary>
        public void RefreshTargets()
        {
            if (!HumanCanClick)
            {
                Clear();
                return;
            }

            var state = _session.State;
            ResetModeIfStale();
            string key = $"{state.ActingSeat}:{state.Phase}:{_session.Match.CommandLog.Count}:{state.ActiveTrade != null}:{Mode}:{MoveFrom}";
            if (key == _targetsKey)
                return;
            Clear();
            _targetsKey = key;
            float scale = _view.HexScale;
            float top = _view.TileHeight;
            // Ghosts in the acting seat's own colour (lightened) read clearly on every tile; cream/white ghosts
            // vanished into the cream hex borders.
            var seatColor = PlayerPieceVisuals.ColorForPlayer(_session.Acting.Owner);
            if (Mode == MatchBuildMode.MoveShip && MoveFrom.HasValue)
            {
                // Step two of a ship move: the picked ship ringed in gold, circles where it can sail.
                _markers.Add(ShipCircle(MoveFrom.Value, MovableShipColor, scale, top, 1.5f, pulse: false));
                foreach (var e in _session.Match.ShipMoveTargets(state.ActingSeat, MoveFrom.Value))
                    _markers.Add(ShipCircle(e, Color.Lerp(seatColor, Color.white, 0.12f), scale, top, 1f, true));
                return;
            }

            var (vertices, edges, _) = MatchBoardPick.Targets(_session.Match, Mode);
            bool shipEdges = Mode == MatchBuildMode.Ship || Mode == MatchBuildMode.MoveShip
                             || state.Phase == MatchPhase.RoadBuilding && MatchBoardPick.FreePieceShips(_session.Match, state.ActingSeat, Mode);
            // v143: small pulsing seat-colour circles / dashes instead of ghost houses and ghost roads.
            var markColor = Color.Lerp(seatColor, Color.white, 0.12f);
            foreach (var v in vertices)
            {
                var (x, z) = HexMath.VertexToWorldPosition(v, scale);
                _markers.Add(PlacementMarkerVisuals.CreateSpotCircle(_root, new Vector3(x, 0f, z), markColor, top, scale));
            }

            foreach (var e in edges)
                _markers.Add(shipEdges
                    ? ShipCircle(e, Mode == MatchBuildMode.MoveShip ? MovableShipColor : markColor, scale, top, 1f, true)
                    : RoadDash(e, markColor, scale, top, 1f));
        }

        private void Update()
        {
            if (!HumanCanClick)
            {
                if (_markers.Count > 0 || _hover != null)
                    Clear();
                return;
            }

            RefreshTargets();
            if (_hover != null)
            {
                ColonistSceneDestroy.SafeDestroy(_hover);
                _hover = null;
            }

            var cam = Camera.main;
            if (cam == null)
                return;
            var ray = cam.ScreenPointToRay(Input.mousePosition);
            if (!BoardPickPlane.TryRaycast(ray, BoardPickPlane.GetPickPlaneY(_view.TileHeight), out var hit))
                return;
            if (Mode == MatchBuildMode.MoveShip && !MoveFrom.HasValue)
            {
                var ship = MatchBoardPick.PickMovableShip(_session.Match, hit.x, hit.z, _view.HexScale);
                if (!ship.HasValue)
                    return;
                _hover = ShipCircle(ship.Value, MovableShipColor, _view.HexScale, _view.TileHeight, 1.5f, false);
                if (Input.GetMouseButtonDown(0) && !IsPointerOverUI())
                    HandleClick(hit.x, hit.z, apply: true);
                return;
            }

            var command = MatchBoardPick.Pick(_session.Match, Mode, hit.x, hit.z, _view.HexScale, MoveFrom);
            if (command == null)
                return;
            _hover = HoverMarker(command);
            if (Input.GetMouseButtonDown(0) && !IsPointerOverUI())
                HandleClick(hit.x, hit.z, apply: true);
        }

        private GameObject HoverMarker(MatchCommand command)
        {
            float scale = _view.HexScale;
            float top = _view.TileHeight;
            switch (command.Type)
            {
                case MatchCommandType.PlaceSetupRoad:
                case MatchCommandType.BuildRoad:
                    return RoadDash(command.Edge, HoverTint(), scale, top, 1.45f);
                case MatchCommandType.BuildShip:
                    return ShipCircle(command.Edge, HoverTint(), scale, top, 1.55f, false);
                case MatchCommandType.MoveShip:
                    return ShipCircle(command.ToEdge, HoverTint(), scale, top, 1.55f, false);
                case MatchCommandType.MovePirate:
                {
                    var (hx, hz) = HexMath.ToWorldPosition(command.Hex, scale);
                    return ShipPieceVisuals.CreatePirate(_root, new Vector3(hx, 0f, hz), top + 0.01f, scale);
                }
                case MatchCommandType.MoveRobber:
                {
                    var (hx, hz) = HexMath.ToWorldPosition(command.Hex, scale);
                    return PlacementMarkerVisuals.CreateSettlementGhost(_root, new Vector3(hx, 0f, hz), RobberColor, top);
                }
                case MatchCommandType.BuildCity:
                {
                    var (x, z) = HexMath.VertexToWorldPosition(command.Vertex, scale);
                    return PlacementMarkerVisuals.CreateCityGhost(_root, new Vector3(x, 0f, z), CityHoverColor, top);
                }
                default:
                {
                    var (x, z) = HexMath.VertexToWorldPosition(command.Vertex, scale);
                    return PlacementMarkerVisuals.CreateSpotCircle(_root, new Vector3(x, 0f, z), HoverTint(), top, scale,
                        1.55f, pulse: false);
                }
            }
        }

        private Color HoverTint() =>
            Color.Lerp(PlayerPieceVisuals.ColorForPlayer(_session.Acting.Owner), Color.white, 0.45f);

        private GameObject RoadDash(Edge e, Color color, float scale, float top, float sizeMul)
        {
            var (mx, mz) = HexMath.EdgeMidpoint(e, scale);
            var (ax, az) = HexMath.VertexToWorldPosition(e.A, scale);
            var (bx, bz) = HexMath.VertexToWorldPosition(e.B, scale);
            float length = Vector3.Distance(new Vector3(ax, 0, az), new Vector3(bx, 0, bz));
            return PlacementMarkerVisuals.CreateRoadDash(_root, new Vector3(mx, 0f, mz), HexMath.EdgeAngle(e), length,
                color, top, scale, sizeMul, pulse: sizeMul <= 1f);
        }

        /// <summary>v145: ship spots are circles on the edge midpoint (a light-blue core), unlike the road dashes.</summary>
        private GameObject ShipCircle(Edge e, Color color, float scale, float top, float sizeMul, bool pulse)
        {
            var (mx, mz) = HexMath.EdgeMidpoint(e, scale);
            var go = PlacementMarkerVisuals.CreateSpotCircle(_root, new Vector3(mx, 0f, mz), color, top, scale, sizeMul, pulse);
            go.name = ShipSpotName;
            var core = go.transform.Find("Core");
            if (core != null && core.TryGetComponent<MeshRenderer>(out var mr))
                mr.sharedMaterial = _shipCoreMat != null ? _shipCoreMat : _shipCoreMat = BuiltInMaterials.CreateUnlit(ShipCore);
            return go;
        }

        public const string ShipSpotName = "ShipSpot";
        private static Material _shipCoreMat;

        private void Clear()
        {
            foreach (var m in _markers)
                if (m != null)
                    ColonistSceneDestroy.SafeDestroy(m);
            _markers.Clear();
            if (_hover != null)
                ColonistSceneDestroy.SafeDestroy(_hover);
            _hover = null;
            _targetsKey = null;
        }

        private static bool IsPointerOverUI() =>
            UnityEngine.EventSystems.EventSystem.current != null
            && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
    }
}
