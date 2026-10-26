using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Match;
using UnityEngine;
using UnityEngine.UI;

namespace CatanRoguelike.Game
{
    /// <summary>
    /// v143 table feel for pass &amp; play: two tumbling dice that land on the rolled faces (and stay up), cards that
    /// fly bank → hand (or seat → seat, hand → bank when spending) in small staggered batches, and the cozy sound
    /// cues. Everything is derived by watching the match (command log + seat hands), so human and AI actions look
    /// the same and the rules code stays presentation-free. Lives on its own layer above the rebuilt table HUD.
    /// </summary>
    public sealed partial class RunHudController
    {
        public const float DiceRollSeconds = 1.0f;
        public const float CardFlySeconds = 0.5f;
        private const float CardFlyStagger = 0.06f;
        private const int MaxFlyPerBatch = 10;
        private const string FxRootName = "HotSeatFx";

        private RectTransform _fxRoot;
        private RectTransform _fxDiceRoot;
        private readonly Image[] _fxDie = new Image[2];
        private readonly Image[][] _fxPips = new Image[2][];
        private int _fxLogSeen;
        private ResourceBundle[] _fxHands;
        private int _fxRollAnimating;
        private bool _fxRolling;
        private int _fxLastTick = -1;
        private bool _fxOverSeen;
        private bool[] _fxQuestSeen;

        /// <summary>Tests: cards currently flying across the table.</summary>
        public int FlyingCardCountForTests => _fxRoot == null ? 0 : _fxRoot.Cast<Transform>().Count(t => t.name.StartsWith("FlyCard"));
        public bool DiceRollingForTests => _fxRolling;
        public bool DiceVisibleForTests => _fxDiceRoot != null && _fxDiceRoot.gameObject.activeSelf;
        public RectTransform DiceRootForTests => _fxDiceRoot;
        /// <summary>Faces currently shown on the two dice (0 = blank).</summary>
        public (int a, int b) DiceFacesForTests { get; private set; }

        private void ResetMatchFx()
        {
            _fxLogSeen = _hotSeat?.Match.CommandLog.Count ?? 0;
            _fxHands = _hotSeat?.State.Seats.Select(s => s.Hand).ToArray();
            _fxQuestSeen = _hotSeat?.State.Seats.Select(s => s.QuestDone).ToArray();
            _fxRolling = false;
            _fxLastTick = -1;
            _fxOverSeen = false;
            if (_fxRoot != null)
                ColonistSceneDestroy.SafeDestroy(_fxRoot.gameObject);
            _fxRoot = null;
            _fxDiceRoot = null;
        }

        private RectTransform EnsureFxRoot()
        {
            if (_fxRoot != null && _canvas != null && _fxRoot.parent == _canvas.transform)
                return _fxRoot;
            if (_canvas == null)
                return null;
            var go = new GameObject(FxRootName, typeof(RectTransform));
            go.layer = _canvas.gameObject.layer;
            _fxRoot = (RectTransform)go.transform;
            _fxRoot.SetParent(_canvas.transform, false);
            _fxRoot.anchorMin = Vector2.zero;
            _fxRoot.anchorMax = Vector2.one;
            _fxRoot.offsetMin = _fxRoot.offsetMax = Vector2.zero;
            BuildFxDice();
            return _fxRoot;
        }

        private void BuildFxDice()
        {
            var holder = new GameObject("Dice", typeof(RectTransform));
            holder.layer = _fxRoot.gameObject.layer;
            _fxDiceRoot = (RectTransform)holder.transform;
            _fxDiceRoot.SetParent(_fxRoot, false);
            // Left of the turn timer, clear of the seat strip and the build bar (bottom-right of the table).
            SetRect(_fxDiceRoot, new Vector2(1f, 0f), new Vector2(-385f, 285f), new Vector2(168f, 92f));
            var tray = CreateImage(_fxDiceRoot, "Tray", null);
            ColonistUiKit.Apply(tray, UiKitPiece.FrameMedium);
            tray.color = new Color(0.96f, 0.90f, 0.76f, 0.96f);
            tray.raycastTarget = false;
            Stretch(tray.rectTransform);
            for (int i = 0; i < 2; i++)
            {
                var die = CreateImage(_fxDiceRoot, "Die_" + i, null);
                die.color = new Color(1f, 0.95f, 0.84f, 1f);
                die.raycastTarget = false;
                SetRect(die.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(i == 0 ? -38f : 38f, 0f), new Vector2(64f, 64f));
                ColonistDiceFaceUi.Build(die.transform, out _fxPips[i]);
                _fxDie[i] = die;
            }

            SetDiceFaces(6, 6);
            _fxDiceRoot.gameObject.SetActive(false);
        }

        private void SetDiceFaces(int a, int b)
        {
            DiceFacesForTests = (a, b);
            ColonistKenneyDiceUi.ApplyFace(_fxDie[0], _fxPips[0], a);
            ColonistKenneyDiceUi.ApplyFace(_fxDie[1], _fxPips[1], b);
        }

        /// <summary>Called every table tick: plays sounds/animations for everything that happened since last time.</summary>
        private void ObserveMatchFx()
        {
            if (_hotSeat == null)
                return;
            var match = _hotSeat.Match;
            var state = match.State;
            if (_fxHands == null || _fxHands.Length != state.Seats.Count || _fxLogSeen > match.CommandLog.Count)
                ResetMatchFx();
            var root = EnsureFxRoot();
            if (root == null)
                return;
            root.SetAsLastSibling();

            bool rolled = false;
            for (int i = _fxLogSeen; i < match.CommandLog.Count; i++)
            {
                var c = match.CommandLog[i];
                if (c.Type == MatchCommandType.RollDice)
                    rolled = true;
                PlayCommandSfx(c, state);
            }

            _fxLogSeen = match.CommandLog.Count;
            if (rolled && state.LastDieA > 0)
                StartCoroutine(RollDiceRoutine(state.LastDieA, state.LastDieB));

            FlyHandChanges(state);

            for (int i = 0; i < state.Seats.Count && _fxQuestSeen != null; i++)
            {
                if (state.Seats[i].QuestDone && !_fxQuestSeen[i])
                    GameAudio.Play(Sfx.QuestDone);
                _fxQuestSeen[i] = state.Seats[i].QuestDone;
            }

            if (_hotSeat.IsOver && !_fxOverSeen)
                GameAudio.Play(Sfx.Victory);
            _fxOverSeen = _hotSeat.IsOver;

            var clock = _hotSeat.Clock;
            if (clock.Enabled && clock.ShouldPulse && !_hotSeat.Acting.IsAi && !_hotSeat.PassScreenPending)
            {
                if (clock.DisplaySeconds != _fxLastTick)
                    GameAudio.Play(Sfx.TimerTick);
                _fxLastTick = clock.DisplaySeconds;
            }
            else
            {
                _fxLastTick = -1;
            }
        }

        private static void PlayCommandSfx(MatchCommand c, MatchState state)
        {
            switch (c.Type)
            {
                case MatchCommandType.RollDice: GameAudio.Play(Sfx.Dice); break;
                case MatchCommandType.PlaceSetupRoad:
                case MatchCommandType.BuildRoad: GameAudio.Play(Sfx.Road); break;
                case MatchCommandType.PlaceSetupSettlement:
                case MatchCommandType.BuildSettlement: GameAudio.Play(Sfx.Settlement); break;
                case MatchCommandType.BuildCity: GameAudio.Play(Sfx.City); break;
                case MatchCommandType.BuyDevCard: GameAudio.Play(Sfx.DevBuy); break;
                case MatchCommandType.PlayKnight:
                case MatchCommandType.PlayRoadBuilding:
                case MatchCommandType.PlayYearOfPlenty:
                case MatchCommandType.PlayMonopoly: GameAudio.Play(Sfx.DevPlay); break;
                case MatchCommandType.MoveRobber:
                    GameAudio.Play(Sfx.Robber);
                    if (c.Victim >= 0)
                        GameAudio.Play(Sfx.Steal, 0.8f);
                    break;
                case MatchCommandType.RespondTrade:
                    GameAudio.Play(c.Response == TradeResponseKind.Declined ? Sfx.TradeDeclined : Sfx.TradeAccepted, 0.8f);
                    break;
                case MatchCommandType.ConfirmTrade:
                case MatchCommandType.BankTrade:
                case MatchCommandType.BankTradeBatch:
                case MatchCommandType.ChooseHarbourPilotResource: GameAudio.Play(Sfx.TradeAccepted); break;
                case MatchCommandType.CancelTrade: GameAudio.Play(Sfx.TradeDeclined, 0.7f); break;
                case MatchCommandType.CastVote: GameAudio.Play(Sfx.Vote); break;
                case MatchCommandType.PlaceBid: GameAudio.Play(Sfx.Auction); break;
                case MatchCommandType.BuySabotage:
                case MatchCommandType.PlaySabotage:
                case MatchCommandType.GuessSaboteur: GameAudio.Play(Sfx.Sabotage); break;
                case MatchCommandType.EndTurn: GameAudio.Play(Sfx.TurnStart, 0.8f); break;
            }
        }

        private IEnumerator RollDiceRoutine(int a, int b)
        {
            int token = ++_fxRollAnimating;
            _fxRolling = true;
            _fxDiceRoot.gameObject.SetActive(true);
            var rng = new System.Random(a * 31 + b * 7 + Time.frameCount);
            float t = 0f, nextFace = 0f;
            var d0 = _fxDie[0].rectTransform;
            var d1 = _fxDie[1].rectTransform;
            while (t < DiceRollSeconds)
            {
                if (token != _fxRollAnimating || _fxDiceRoot == null)
                    yield break;
                float k = t / DiceRollSeconds;
                if (t >= nextFace)
                {
                    SetDiceFaces(rng.Next(1, 7), rng.Next(1, 7));
                    nextFace = t + Mathf.Lerp(0.05f, 0.16f, k); // tumbles slow down before landing
                }

                float hop = Mathf.Abs(Mathf.Sin(k * Mathf.PI * 3f)) * (1f - k) * 22f;
                d0.anchoredPosition = new Vector2(-38f, hop);
                d1.anchoredPosition = new Vector2(38f, Mathf.Abs(Mathf.Sin(k * Mathf.PI * 3f + 1.1f)) * (1f - k) * 22f);
                d0.localRotation = Quaternion.Euler(0f, 0f, (1f - k) * 540f);
                d1.localRotation = Quaternion.Euler(0f, 0f, -(1f - k) * 470f);
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            d0.anchoredPosition = new Vector2(-38f, 0f);
            d1.anchoredPosition = new Vector2(38f, 0f);
            d0.localRotation = d1.localRotation = Quaternion.identity;
            SetDiceFaces(a, b);
            _fxRolling = false;
            StartCoroutine(SimpleTweens.ScalePop(_fxDiceRoot, 0.22f, 1.12f));
        }

        // ---------- cards ----------

        private void FlyHandChanges(MatchState state)
        {
            var before = _fxHands;
            var after = state.Seats.Select(s => s.Hand).ToArray();
            _fxHands = after;
            if (before == null)
                return;
            var gains = new List<(int seat, ResourceType r, int n)>();
            var losses = new List<(int seat, ResourceType r, int n)>();
            for (int s = 0; s < after.Length; s++)
            {
                foreach (var r in MatchController.AllResources)
                {
                    int d = after[s][r] - before[s][r];
                    if (d > 0) gains.Add((s, r, d));
                    else if (d < 0) losses.Add((s, r, -d));
                }
            }

            if (gains.Count == 0 && losses.Count == 0)
                return;
            int flown = 0;
            // A gain matched by another seat's loss of the same card is a steal / trade / monopoly: seat → seat.
            foreach (var (seat, r, n) in gains)
            {
                int left = n;
                for (int j = 0; j < losses.Count && left > 0; j++)
                {
                    var l = losses[j];
                    if (l.seat == seat || l.r != r || l.n == 0)
                        continue;
                    int m = Mathf.Min(left, l.n);
                    for (int k = 0; k < m && flown < MaxFlyPerBatch; k++, flown++)
                        LaunchCard(SeatAnchor(l.seat, r), SeatAnchor(seat, r), r, Reveal(state, seat, l.seat), flown);
                    losses[j] = (l.seat, l.r, l.n - m);
                    left -= m;
                }

                for (int k = 0; k < left && flown < MaxFlyPerBatch; k++, flown++)
                    LaunchCard(BankAnchor(), SeatAnchor(seat, r), r, true, flown);
            }

            if (gains.Count > 0)
                GameAudio.Play(Sfx.CardDraw);
            // Spending: what's left of the losses goes back to the bank.
            foreach (var (seat, r, n) in losses)
                for (int k = 0; k < n && flown < MaxFlyPerBatch; k++, flown++)
                    LaunchCard(SeatAnchor(seat, r), BankAnchor(), r, true, flown);
        }

        private bool Reveal(MatchState state, int to, int from) =>
            _hotSeat.IsHandVisible(to) || _hotSeat.IsHandVisible(from)
            || (state.Seats[to].IsAi && state.Seats[from].IsAi && _hotSeat.HumanCount == 0);

        private Vector3 BankAnchor()
        {
            // The bank sits under the banner, top-centre.
            var canvasRt = (RectTransform)_canvas.transform;
            return canvasRt.TransformPoint(new Vector3(0f, canvasRt.rect.height * 0.5f - 130f, 0f));
        }

        private Vector3 SeatAnchor(int seat, ResourceType r)
        {
            if (_hotSeatRoot != null)
            {
                if (_hotSeat.IsHandVisible(seat))
                {
                    var slot = _hotSeatRoot.Find("Hand/Res_" + r);
                    if (slot != null)
                        return slot.position;
                }

                var row = _hotSeatRoot.Find("SeatStrip/Seat_" + seat);
                if (row != null)
                    return row.position;
            }

            var canvasRt = (RectTransform)_canvas.transform;
            return canvasRt.TransformPoint(new Vector3(canvasRt.rect.width * 0.5f - 200f, canvasRt.rect.height * 0.5f - 120f - seat * 90f, 0f));
        }

        private void LaunchCard(Vector3 from, Vector3 to, ResourceType r, bool faceUp, int index)
        {
            if (_fxRoot == null)
                return;
            var sprite = faceUp ? ColonistHudResourceSprites.Get(r) : ColonistKenneyDiceSprites.TryGetCardBack();
            var card = CreateImage(_fxRoot, "FlyCard_" + index, sprite);
            card.raycastTarget = false;
            card.preserveAspect = true;
            var rt = card.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(58f, 58f);
            rt.position = from;
            card.gameObject.SetActive(false);
            StartCoroutine(FlyCardRoutine(rt, from, to, index * CardFlyStagger));
        }

        private static IEnumerator FlyCardRoutine(RectTransform rt, Vector3 from, Vector3 to, float delay)
        {
            float wait = 0f;
            while (wait < delay)
            {
                wait += Time.unscaledDeltaTime;
                yield return null;
            }

            if (rt == null)
                yield break;
            rt.gameObject.SetActive(true);
            float t = 0f;
            var mid = (from + to) * 0.5f + new Vector3(0f, 90f, 0f);
            while (t < CardFlySeconds)
            {
                if (rt == null)
                    yield break;
                float k = SimpleTweens.EaseOutQuad(t / CardFlySeconds);
                var a = Vector3.Lerp(from, mid, k);
                var b = Vector3.Lerp(mid, to, k);
                rt.position = Vector3.Lerp(a, b, k);
                float s = 1f + 0.25f * Mathf.Sin(k * Mathf.PI);
                rt.localScale = new Vector3(s, s, 1f);
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            if (rt != null)
                ColonistSceneDestroy.SafeDestroy(rt.gameObject);
        }
    }
}
