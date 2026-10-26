using System.Collections.Generic;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Match;
using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>
    /// v146 pass &amp; play board motion: new pieces drop in with a bounce, the robber and the pirate slide in an
    /// arc to their new hex, and rolled number tokens pop. Pieces are rebuilt on every refresh, so each motion
    /// is keyed (piece identity / start time on <see cref="HudMotion"/>) and re-applied to the fresh objects.
    /// </summary>
    public sealed partial class BoardView
    {
        /// <summary>Pass &amp; play turns these on (the classic single-player board keeps its old look).</summary>
        public static bool MatchMotion;
        public static bool MatchLakeNumbers;

        public const float PieceDropSeconds = 0.5f;
        public const float SlideSeconds = 0.55f;
        public const float NumberPopSeconds = 0.9f;
        private const float DropHeight = 1.1f;

        private readonly Dictionary<(int, object, int, int), float> _pieceBorn = new();
        private readonly HashSet<(int, object, int, int)> _pieceSeen = new();
        private readonly List<(Transform t, Vector3 pos, Vector3 scale, float born)> _drops = new();
        private bool _piecesPrimed;

        private HexCoord? _robberAt;
        private Vector3 _robberFrom;
        private float _robberMovedAt = -100f;
        private bool _robberSliding;

        private HexCoord? _pirateAt;
        private Transform _pirate;
        private Vector3 _pirateBase, _pirateFrom;
        private float _pirateMovedAt = -100f;

        private readonly Dictionary<HexTileView, float> _pops = new();

        public int ActiveDropCountForTests => _drops.Count;
        public bool RobberSlidingForTests => HudMotion.Now - _robberMovedAt < SlideSeconds;
        public int PoppingTokenCountForTests
        {
            get
            {
                int n = 0;
                foreach (var kv in _pops)
                    if (kv.Key != null && kv.Key.IsPopping)
                        n++;
                return n;
            }
        }

        private void ResetMotion()
        {
            foreach (var kv in _pops)
                if (kv.Key != null)
                    kv.Key.EndNumberPop();
            _pops.Clear();
            _pieceBorn.Clear();
            _pieceSeen.Clear();
            _drops.Clear();
            _piecesPrimed = false;
            _robberAt = null;
            _robberMovedAt = -100f;
            _pirateAt = null;
            _pirate = null;
            _pirateMovedAt = -100f;
        }

        private void BeginPieceTracking()
        {
            _drops.Clear();
            _pieceSeen.Clear();
        }

        private void TrackPiece(GameObject piece, (int, object, int, int) key)
        {
            _pieceSeen.Add(key);
            if (!MatchMotion || piece == null)
                return;
            if (!_pieceBorn.TryGetValue(key, out float born))
            {
                born = _piecesPrimed ? HudMotion.Now : -100f;
                _pieceBorn[key] = born;
            }

            if (HudMotion.Now - born < PieceDropSeconds)
                _drops.Add((piece.transform, piece.transform.localPosition, piece.transform.localScale, born));
        }

        private void EndPieceTracking()
        {
            if (_pieceBorn.Count > _pieceSeen.Count)
            {
                var gone = new List<(int, object, int, int)>();
                foreach (var k in _pieceBorn.Keys)
                    if (!_pieceSeen.Contains(k))
                        gone.Add(k);
                foreach (var k in gone)
                    _pieceBorn.Remove(k);
            }

            _piecesPrimed = true;
            ApplyMotion();
        }

        private void TrackPirate(GameObject pirate, HexCoord? at)
        {
            _pirate = pirate != null ? pirate.transform : null;
            if (_pirate != null)
                _pirateBase = _pirate.localPosition;
            if (MatchMotion && at.HasValue && _pirateAt.HasValue && !at.Value.Equals(_pirateAt.Value))
            {
                var (fx, fz) = HexMath.ToWorldPosition(_pirateAt.Value, hexScale);
                _pirateFrom = new Vector3(fx, _pirateBase.y, fz);
                _pirateMovedAt = HudMotion.Now;
            }

            _pirateAt = at;
        }

        private void NoteRobberMove(HexCoord? to)
        {
            if (MatchMotion && to.HasValue && _robberAt.HasValue && !to.Value.Equals(_robberAt.Value)
                && _tiles.TryGetValue(_robberAt.Value, out var from) && from.RobberMarkerTransform != null)
            {
                _robberFrom = from.RobberMarkerTransform.position;
                _robberMovedAt = HudMotion.Now;
            }

            _robberAt = to;
        }

        /// <summary>v146: pops every number token the dice just hit (tiles, sea fishing grounds, the lake).</summary>
        public int PopNumberTokens(int roll)
        {
            int n = 0;
            float now = HudMotion.Now;
            foreach (var view in _tiles.Values)
            {
                if (view == null || view.Data == null || !view.HasVisibleToken)
                    continue;
                bool hit = view.IsLakeToken
                    ? System.Array.IndexOf(MatchController.LakeNumbers, roll) >= 0
                    : !view.Data.IsDesert && view.Data.NumberToken == roll;
                if (!hit)
                    continue;
                _pops[view] = now;
                n++;
            }

            foreach (var view in _fishShoalViews.Values)
            {
                if (view == null || view.Data == null || !view.HasVisibleToken || view.Data.NumberToken != roll)
                    continue;
                _pops[view] = now;
                n++;
            }

            ApplyMotion();
            return n;
        }

        private void LateUpdate() => ApplyMotion();

        /// <summary>Applies every running board motion at the current <see cref="HudMotion.Now"/>.</summary>
        public void ApplyMotion()
        {
            float now = HudMotion.Now;
            for (int i = _drops.Count - 1; i >= 0; i--)
            {
                var d = _drops[i];
                if (d.t == null)
                {
                    _drops.RemoveAt(i);
                    continue;
                }

                float t = Mathf.Clamp01((now - d.born) / PieceDropSeconds);
                if (t >= 1f)
                {
                    d.t.localPosition = d.pos;
                    d.t.localScale = d.scale;
                    _drops.RemoveAt(i);
                    continue;
                }

                float fall = 1f - HudMotion.EaseOutBounce(t);
                d.t.localPosition = d.pos + Vector3.up * (DropHeight * fall);
                float squash = t > 0.35f ? 1f + 0.12f * Mathf.Sin((t - 0.35f) / 0.65f * Mathf.PI) * (1f - t) : 1f;
                d.t.localScale = new Vector3(d.scale.x * squash, d.scale.y / squash, d.scale.z * squash) *
                                 Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(t * 3f));
            }

            float rt = (now - _robberMovedAt) / SlideSeconds;
            if (rt >= 0f && rt < 1f && _robberAt.HasValue && _tiles.TryGetValue(_robberAt.Value, out var rv)
                && rv.RobberMarkerTransform != null)
            {
                var marker = rv.RobberMarkerTransform;
                var target = marker.parent.TransformPoint(rv.RobberRestingLocalPosition);
                _robberSliding = true;
                float e = SimpleTweens.EaseOutQuad(rt);
                marker.position = Vector3.Lerp(_robberFrom, target, e) + Vector3.up * (Mathf.Sin(rt * Mathf.PI) * 0.9f);
            }
            else if (_robberSliding)
            {
                _robberSliding = false;
                if (_robberAt.HasValue && _tiles.TryGetValue(_robberAt.Value, out var rv2) && rv2.RobberMarkerTransform != null)
                    rv2.RobberMarkerTransform.localPosition = rv2.RobberRestingLocalPosition;
            }

            if (_pirate != null)
            {
                float pt = (now - _pirateMovedAt) / SlideSeconds;
                if (pt >= 0f && pt < 1f)
                {
                    float e = SimpleTweens.EaseOutQuad(pt);
                    _pirate.localPosition = Vector3.Lerp(_pirateFrom, _pirateBase, e) +
                                            Vector3.up * (Mathf.Sin(pt * Mathf.PI) * 0.5f);
                }
                else
                    _pirate.localPosition = _pirateBase;
            }

            if (_pops.Count > 0)
            {
                List<HexTileView> done = null;
                foreach (var kv in _pops)
                {
                    if (kv.Key == null)
                    {
                        (done ??= new List<HexTileView>()).Add(kv.Key);
                        continue;
                    }

                    float t = Mathf.Clamp01((now - kv.Value) / NumberPopSeconds);
                    kv.Key.ApplyNumberPop(t);
                    if (t >= 1f)
                        (done ??= new List<HexTileView>()).Add(kv.Key);
                }

                if (done != null)
                    foreach (var v in done)
                        _pops.Remove(v);
            }
        }
    }
}
