using System.Collections.Generic;
using System.Linq;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Match;
using CatanRoguelike.Core.Turn;
using UnityEngine;
using UnityEngine.UI;

namespace CatanRoguelike.Game
{
    /// <summary>
    /// v134 Phase 2 (started): pass-and-play on one device. A lobby with 2–5 seats (any Human/AI mix, open seats
    /// filled with AI), then an N-player table HUD driven by <see cref="HotSeatSession"/>: seat strip, 1.5 min
    /// turn timer with a manual "+90 s" charge button whose number grows and pulses in the last 5 s, and a pass
    /// screen that hides hands while the device changes hands. v135: humans place by clicking the board
    /// (<see cref="HotSeatBoardInput"/>; the suggestion buttons stay as a shortcut), and the table has
    /// development cards, a trade board and a per-player discard on 7 (see RunHudMatchHotSeatPanels).
    /// </summary>
    public sealed partial class RunHudController
    {
        private bool _hotSeatLobbyOpen;
        private readonly List<SeatKind?> _lobbySeats = new() { SeatKind.Human, SeatKind.Human, SeatKind.Human, null };

        private HotSeatSession _hotSeat;
        private GameController _hotSeatDisplay;
        private RectTransform _hotSeatRoot;
        private bool _hotSeatDirty;
        private readonly List<CanvasGroup> _hotSeatHidden = new();
        private RectTransform _hsTimerRoot;
        private Text _hsTimerText;
        private Image _hsTimerDisc;
        private static Sprite _discSprite;
        private HotSeatBoardInput _hsBoardInput;

        public HotSeatBoardInput HotSeatBoardInputForTests => _hsBoardInput;

        public bool IsHotSeatActive => _hotSeat != null;
        public bool IsHotSeatLobbyOpenForTests => _hotSeatLobbyOpen;
        public HotSeatSession HotSeatSessionForTests => _hotSeat;
        public RectTransform HotSeatRootForTests => _hotSeatRoot;
        public IReadOnlyList<SeatKind?> HotSeatLobbySeatsForTests => _lobbySeats;

        /// <summary>Tests/captures: stop wall-clock ticking so a frozen timer state can be captured.</summary>
        public bool HotSeatClockFrozenForTests { get; set; }

        public static readonly Color TimerPulseRed = new(0.86f, 0.18f, 0.12f, 1f);

        // ---------- lobby (pre-game menu, map step) ----------

        public void OpenHotSeatLobbyForTests()
        {
            _hotSeatLobbyOpen = true;
            RebuildPreGame();
        }

        public void SetLobbySeatsForTests(params SeatKind?[] seats)
        {
            _lobbySeats.Clear();
            _lobbySeats.AddRange(seats);
            RebuildPreGame();
        }

        public void FillLobbyWithAi()
        {
            for (int i = 0; i < _lobbySeats.Count; i++)
                _lobbySeats[i] ??= SeatKind.Ai;
            RebuildPreGame();
        }

        private string HotSeatLobbyKey() =>
            _hotSeatLobbyOpen ? "hs:" + string.Join(",", _lobbySeats.Select(s => s?.ToString() ?? "open")) : "";

        public MatchConfig BuildLobbyConfig(int seed)
        {
            var config = new MatchConfig { Seed = seed, MapSize = MatchConfig.RecommendedMap(_lobbySeats.Count) };
            for (int i = 0; i < _lobbySeats.Count; i++)
            {
                var kind = _lobbySeats[i];
                config.Seats.Add(kind.HasValue
                    ? new SeatConfig(kind.Value, kind == SeatKind.Human ? $"Player {i + 1}" : $"AI {i + 1}")
                    : null);
            }

            return config;
        }

        private void BuildHotSeatLobby(RectTransform root)
        {
            var dim = new GameObject("HotSeatLobbyDim").AddComponent<Image>();
            dim.transform.SetParent(root, false);
            dim.color = new Color(0.04f, 0.03f, 0.02f, 0.62f);
            dim.raycastTarget = true;
            Stretch(dim.rectTransform);

            var card = CreateCardFrame(root, "HotSeatLobby", wide: false);
            var cardImg = card.GetComponent<Image>();
            var sliced = ColonistRoguelikeArt.GetCardFrameSliced(false);
            if (sliced != null)
            {
                cardImg.sprite = sliced;
                cardImg.type = Image.Type.Sliced;
                cardImg.preserveAspect = false;
            }

            float h = 330f + _lobbySeats.Count * 78f;
            SetRect(card, new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(820f, h));

            var kicker = RunHudUiFactory.CreateText(card, "Kicker", "PASS & PLAY  ·  ONE DEVICE", 16,
                TextAnchor.MiddleCenter, ColonistUiKit.InkGold, FontStyle.Bold);
            SetRect(kicker.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -46f), new Vector2(640f, 24f));
            var title = RunHudUiFactory.CreateText(card, "Title", "Seat your table", 36, TextAnchor.MiddleCenter,
                ColonistUiKit.Ink, FontStyle.Bold);
            SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -84f), new Vector2(640f, 46f));

            // Seat count stepper.
            var countLabel = RunHudUiFactory.CreateText(card, "SeatCount", $"{_lobbySeats.Count} seats", 24,
                TextAnchor.MiddleCenter, ColonistUiKit.Ink, FontStyle.Bold);
            SetRect(countLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -134f), new Vector2(200f, 40f));
            AddLobbyButton(card, "SeatMinus", "−", new Vector2(-150f, -134f), new Vector2(64f, 46f),
                _lobbySeats.Count > MatchConfig.MinSeats, false, () =>
                {
                    _lobbySeats.RemoveAt(_lobbySeats.Count - 1);
                    RebuildPreGame();
                });
            AddLobbyButton(card, "SeatPlus", "+", new Vector2(150f, -134f), new Vector2(64f, 46f),
                _lobbySeats.Count < MatchConfig.MaxSeats, false, () =>
                {
                    _lobbySeats.Add(null);
                    RebuildPreGame();
                });

            for (int i = 0; i < _lobbySeats.Count; i++)
            {
                int seat = i;
                float y = -200f - i * 78f;
                var disc = CreateDisc(card, "SeatDisc_" + i, PlayerPieceVisuals.ColorForSeat(i), 52f);
                SetRect(disc.rectTransform, new Vector2(0.5f, 1f), new Vector2(-250f, y), new Vector2(52f, 52f));
                var num = RunHudUiFactory.CreateText(disc.rectTransform, "N", (i + 1).ToString(), 24,
                    TextAnchor.MiddleCenter, SeatInk(i), FontStyle.Bold);
                Stretch(num.rectTransform);
                var label = RunHudUiFactory.CreateText(card, "SeatLabel_" + i, $"Seat {i + 1}", 24,
                    TextAnchor.MiddleLeft, ColonistUiKit.Ink, FontStyle.Bold);
                SetRect(label.rectTransform, new Vector2(0.5f, 1f), new Vector2(-120f, y), new Vector2(170f, 40f));
                var kind = _lobbySeats[i];
                string text = kind == SeatKind.Human ? "Human" : kind == SeatKind.Ai ? "AI player" : "Open seat";
                AddLobbyButton(card, "SeatKind_" + i, text, new Vector2(140f, y), new Vector2(250f, 54f), true,
                    kind == SeatKind.Human, () =>
                    {
                        _lobbySeats[seat] = _lobbySeats[seat] switch
                        {
                            SeatKind.Human => SeatKind.Ai,
                            SeatKind.Ai => null,
                            _ => SeatKind.Human
                        };
                        RebuildPreGame();
                    });
            }

            int humans = _lobbySeats.Count(s => s == SeatKind.Human);
            int ai = _lobbySeats.Count(s => s == SeatKind.Ai);
            int open = _lobbySeats.Count(s => s == null);
            string map = MatchConfig.RecommendedMap(_lobbySeats.Count) == Core.Data.MapSize.Extended
                ? "Extended map (30 hexes)"
                : "Base map (19 hexes)";
            string summary = $"{humans} human{(humans == 1 ? "" : "s")} · {ai} AI · {open} open  ·  {map}";
            var info = RunHudUiFactory.CreateText(card, "Summary", summary, 19, TextAnchor.MiddleCenter,
                ColonistUiKit.InkMuted, FontStyle.Italic);
            SetRect(info.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 118f), new Vector2(680f, 28f));

            AddLobbyButton(card, "FillAi", "Fill open seats with AI", new Vector2(-185f, 62f), new Vector2(290f, 56f),
                open > 0, false, FillLobbyWithAi, anchorBottom: true);
            AddLobbyButton(card, "StartMatch", open > 0 ? "Fill open seats first" : "Start match",
                new Vector2(135f, 62f), new Vector2(260f, 56f), open == 0, true, () =>
                {
                    var config = BuildLobbyConfig((_controller?.RunSeed ?? 1) * 31 + 7);
                    StartHotSeat(config);
                }, anchorBottom: true);
            AddLobbyButton(card, "CloseLobby", "✕", new Vector2(330f, 62f), new Vector2(64f, 56f), true, false, () =>
            {
                _hotSeatLobbyOpen = false;
                RebuildPreGame();
            }, anchorBottom: true);
        }

        private static void AddLobbyButton(RectTransform parent, string name, string label, Vector2 pos, Vector2 size,
            bool enabled, bool green, System.Action click, bool anchorBottom = false)
        {
            var button = RunHudUiFactory.CreateButton(parent, label, RunHudUiFactory.ButtonFill);
            button.name = name;
            SetRect((RectTransform)button.transform, new Vector2(0.5f, anchorBottom ? 0f : 1f), pos, size);
            ColonistKenneyHudArt.ApplyColonistWarmButton(button.GetComponent<Image>(), primaryGreen: green);
            var text = button.GetComponentInChildren<Text>();
            if (text != null)
            {
                text.color = green ? ColonistUiKit.Cream : ColonistUiKit.Ink;
                text.fontSize = 20;
            }

            button.interactable = enabled;
            button.onClick.AddListener(() => click());
        }

        // ---------- the table ----------

        public void StartHotSeat(MatchConfig config)
        {
            config.Validate();
            EnsureCanvasBuilt();
            _hotSeatLobbyOpen = false;
            _hotSeat = new HotSeatSession(config);
            _hotSeat.Changed += () => _hotSeatDirty = true;

            _hotSeatDisplay = new GameController(config.Seed, config.MapSize);
            _hotSeatDisplay.State.ResetForNewMap(_hotSeat.State.Board, config.MapSize);
            _hotSeatDisplay.State.Phase = GamePhase.DayPlayerActions;
            var boardView = Object.FindFirstObjectByType<BoardView>();
            if (boardView != null)
                boardView.Rebuild(_hotSeatDisplay);
            if (_boardInput != null)
                _boardInput.enabled = false;
            _hsTradeOpen = false;
            _hsPicker = null;
            _hsCounterSeat = -1;
            _hsDiscardSeat = -1;
            _hsGive = _hsGet = _hsDiscard = ResourceBundle.Zero;
            _hsBoardInput = GetComponent<HotSeatBoardInput>();
            if (_hsBoardInput == null)
                _hsBoardInput = gameObject.AddComponent<HotSeatBoardInput>();
            _hsBoardInput.Bind(_hotSeat, boardView);

            if (_preGameRoot != null)
                _preGameRoot.gameObject.SetActive(false);
            PaintedPreGameMenusVisible = true;
            PlaceholderHudLayout.SetActivePanelWidth(380f);
            _canvas.gameObject.SetActive(true);

            _hotSeat.Tick(0f); // AI seats that act first
            RebuildHotSeat();
        }

        public void ExitHotSeat()
        {
            if (_hotSeat == null)
                return;
            _hotSeat = null;
            if (_hsBoardInput != null)
                _hsBoardInput.Unbind();
            if (_hotSeatRoot != null)
                ColonistSceneDestroy.SafeDestroy(_hotSeatRoot.gameObject);
            _hotSeatRoot = null;
            foreach (var group in _hotSeatHidden.Where(g => g != null))
            {
                group.alpha = 1f;
                group.blocksRaycasts = true;
                group.interactable = true;
            }

            _hotSeatHidden.Clear();
            if (_boardInput != null)
                _boardInput.enabled = true;
            var boardView = Object.FindFirstObjectByType<BoardView>();
            if (boardView != null && _controller != null)
                boardView.Rebuild(_controller);
            _preGameKey = null;
            if (_controller != null)
                Refresh(_controller.State);
        }

        /// <summary>Rebuilds the table HUD now (tests call this after driving the session directly).</summary>
        public void RefreshHotSeatNow()
        {
            if (_hotSeat == null)
                return;
            _hotSeatDirty = false;
            RebuildHotSeat();
            var boardView = Object.FindFirstObjectByType<BoardView>();
            if (boardView != null)
                boardView.Refresh(_hotSeatDisplay.State);
            UpdateHotSeatTimer();
        }

        private void TickHotSeat()
        {
            if (_hotSeat == null)
                return;
            if (_hotSeatRoot == null || _canvas == null || !_canvas.gameObject.activeSelf)
                _hotSeatDirty = true; // the canvas was rebuilt or hidden under us: re-attach the table
            if (!HotSeatClockFrozenForTests)
                _hotSeat.Tick(Time.deltaTime);
            if (_hotSeatDirty)
                RefreshHotSeatNow();
            UpdateHotSeatTimer();
        }

        private void HideRunHudForHotSeat()
        {
            foreach (Transform child in _canvas.transform)
            {
                if (child == _hotSeatRoot || child.name == "HotSeatTable")
                    continue;
                var group = child.GetComponent<CanvasGroup>();
                if (group == null)
                    group = child.gameObject.AddComponent<CanvasGroup>();
                if (group.alpha > 0f && !_hotSeatHidden.Contains(group))
                    _hotSeatHidden.Add(group);
                group.alpha = 0f;
                group.blocksRaycasts = false;
                group.interactable = false;
            }
        }

        private void RebuildHotSeat()
        {
            EnsureCanvasBuilt();
            if (!_canvas.gameObject.activeSelf)
                _canvas.gameObject.SetActive(true);
            if (_hotSeatRoot != null)
            {
                _hotSeatRoot.gameObject.SetActive(false);
                ColonistSceneDestroy.SafeDestroy(_hotSeatRoot.gameObject);
            }

            var go = new GameObject("HotSeatTable");
            go.transform.SetParent(_canvas.transform, false);
            _hotSeatRoot = go.AddComponent<RectTransform>();
            Stretch(_hotSeatRoot);
            _hotSeatRoot.SetAsLastSibling();

            var s = _hotSeat;
            var state = s.State;
            BuildHotSeatBanner(state);
            BuildSeatStrip(state);
            BuildTimerAndCharge();
            BuildHotSeatHand();
            BuildHotSeatDevCards();
            BuildHotSeatActions();
            if (!s.IsOver && !s.PassScreenPending)
            {
                BuildTradeBoard();
                BuildDiscardPicker();
                BuildDevCardPicker();
            }

            if (s.IsOver)
                BuildGameOver(state);
            else if (s.PassScreenPending)
                BuildPassScreen(state.Acting);

            SetLayerRecursively(_hotSeatRoot.gameObject, HudCaptureUiLayer);
            HideRunHudForHotSeat();
        }

        private void BuildHotSeatBanner(MatchState state)
        {
            var banner = CreateImage(_hotSeatRoot, "Banner", null);
            banner.preserveAspect = false;
            ColonistUiKit.Apply(banner, UiKitPiece.Badge, 0.8f);
            SetRect(banner.rectTransform, new Vector2(0.5f, 1f), new Vector2(-190f, -52f), new Vector2(1000f, 76f));
            string phase = state.Phase switch
            {
                MatchPhase.SetupSettlement => "place a settlement",
                MatchPhase.SetupRoad => "place a road",
                MatchPhase.Roll => "roll the dice",
                MatchPhase.MoveRobber => "move the robber (click a tile)",
                MatchPhase.Discard => $"{state.Acting.Name} discards half (rolled 7)",
                MatchPhase.RoadBuilding => $"place {state.FreeRoadsLeft} free road{(state.FreeRoadsLeft == 1 ? "" : "s")}",
                MatchPhase.Main => state.LastRoll > 0 ? $"rolled {state.LastRoll} · build or end turn" : "build or end turn",
                _ => "game over"
            };
            bool setup = state.Phase == MatchPhase.SetupSettlement || state.Phase == MatchPhase.SetupRoad;
            string round = setup ? "SETUP" : $"ROUND {state.Round + 1}";
            var ink = ColonistUiKit.TextOn(UiKitPiece.Badge);
            var top = RunHudUiFactory.CreateText(banner.rectTransform, "Title",
                $"PASS & PLAY  ·  {round}  ·  {state.Current.Name}: {phase}", 22, TextAnchor.MiddleCenter, ink,
                FontStyle.Bold);
            SetRect(top.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(960f, 30f));
            string last = !string.IsNullOrEmpty(_hotSeat.LastMessage) ? _hotSeat.LastMessage
                : state.Log.Count > 0 ? state.Log[state.Log.Count - 1] : "";
            var sub = RunHudUiFactory.CreateText(banner.rectTransform, "Log", last, 17, TextAnchor.MiddleCenter,
                ink, FontStyle.Italic);
            SetRect(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -52f), new Vector2(960f, 24f));

            var leave = RunHudUiFactory.CreateButton(_hotSeatRoot, "Leave match", RunHudUiFactory.ButtonSecondary);
            leave.name = "LeaveMatch";
            SetRect((RectTransform)leave.transform, new Vector2(0f, 1f), new Vector2(110f, -52f), new Vector2(170f, 50f));
            ColonistKenneyHudArt.ApplyColonistWarmButton(leave.GetComponent<Image>(), primaryGreen: false);
            leave.onClick.AddListener(ExitHotSeat);
        }

        private void BuildSeatStrip(MatchState state)
        {
            int n = state.Seats.Count;
            const float rowH = 104f, width = 360f;
            var panel = CreateImage(_hotSeatRoot, "SeatStrip", null);
            panel.preserveAspect = false;
            ColonistUiKit.Apply(panel, UiKitPiece.FrameMedium);
            float h = 64f + n * rowH;
            SetRect(panel.rectTransform, new Vector2(1f, 1f), new Vector2(-width / 2f - 20f, -24f - h / 2f),
                new Vector2(width, h));
            var ink = ColonistUiKit.TextOn(UiKitPiece.FrameMedium);
            var head = RunHudUiFactory.CreateText(panel.rectTransform, "Head", $"{n} PLAYERS  ·  FIRST TO {state.Config.VictoryPoints} VP",
                16, TextAnchor.MiddleCenter, ink, FontStyle.Bold);
            SetRect(head.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(width - 40f, 24f));

            for (int i = 0; i < n; i++)
            {
                var seat = state.Seats[i];
                bool active = i == state.CurrentSeat && !_hotSeat.IsOver;
                float y = -64f - rowH / 2f - i * rowH;
                var row = CreateImage(panel.rectTransform, "Seat_" + i, null);
                row.preserveAspect = false;
                row.color = active ? new Color(1f, 0.82f, 0.36f, 0.55f) : new Color(0f, 0f, 0f, 0.08f);
                SetRect(row.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(width - 36f, rowH - 10f));

                var disc = CreateDisc(row.rectTransform, "Disc", PlayerPieceVisuals.ColorForSeat(i), 62f);
                SetRect(disc.rectTransform, new Vector2(0f, 0.5f), new Vector2(44f, 0f), new Vector2(62f, 62f));
                var num = RunHudUiFactory.CreateText(disc.rectTransform, "N", seat.IsAi ? "AI" : (i + 1).ToString(),
                    seat.IsAi ? 20 : 28, TextAnchor.MiddleCenter, SeatInk(i), FontStyle.Bold);
                Stretch(num.rectTransform);

                var name = RunHudUiFactory.CreateText(row.rectTransform, "Name", seat.Name, 22, TextAnchor.MiddleLeft,
                    ink, FontStyle.Bold);
                SetRect(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(170f, 18f), new Vector2(150f, 30f));
                string detail = $"{seat.CardCount} cards · {seat.DevCardCount} dev" +
                                (seat.KnightsPlayed > 0 ? $" · {seat.KnightsPlayed} kn" : "");
                var det = RunHudUiFactory.CreateText(row.rectTransform, "Detail", detail, 16, TextAnchor.MiddleLeft,
                    ColonistUiKit.InkMuted, FontStyle.Normal);
                SetRect(det.rectTransform, new Vector2(0f, 0.5f), new Vector2(180f, -8f), new Vector2(170f, 24f));

                // Extra-time charges as small gold pips.
                for (int c = 0; c < Mathf.Min(seat.ExtraTimeCharges, 6); c++)
                {
                    var pip = CreateDisc(row.rectTransform, "Charge_" + c, new Color(0.93f, 0.72f, 0.22f, 1f), 14f);
                    SetRect(pip.rectTransform, new Vector2(0f, 0.5f), new Vector2(102f + c * 18f, -32f), new Vector2(14f, 14f));
                }

                // Victory-point cards stay hidden: everyone sees public points only.
                var vp = RunHudUiFactory.CreateText(row.rectTransform, "VP", seat.PublicVictoryPoints.ToString(), 36,
                    TextAnchor.MiddleCenter, ink, FontStyle.Bold);
                SetRect(vp.rectTransform, new Vector2(1f, 0.5f), new Vector2(-40f, 8f), new Vector2(64f, 44f));
                var vpl = RunHudUiFactory.CreateText(row.rectTransform, "VPLabel",
                    "VP" + (state.LongestRoadSeat == i ? " · road" : "") + (state.LargestArmySeat == i ? " · army" : ""), 13, TextAnchor.MiddleCenter, ColonistUiKit.InkMuted,
                    FontStyle.Bold);
                SetRect(vpl.rectTransform, new Vector2(1f, 0.5f), new Vector2(-40f, -24f), new Vector2(80f, 18f));
            }
        }

        private void BuildTimerAndCharge()
        {
            var s = _hotSeat;
            var holder = new GameObject("Timer").AddComponent<RectTransform>();
            holder.SetParent(_hotSeatRoot, false);
            SetRect(holder, new Vector2(0.5f, 0f), new Vector2(-190f, 196f), new Vector2(150f, 150f));
            _hsTimerRoot = holder;
            var ring = CreateDisc(holder, "Ring", new Color(0.93f, 0.72f, 0.22f, 1f), 150f);
            Stretch(ring.rectTransform);
            _hsTimerDisc = CreateDisc(holder, "Disc", new Color(0.16f, 0.11f, 0.07f, 0.96f), 136f);
            SetRect(_hsTimerDisc.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(136f, 136f));
            var label = RunHudUiFactory.CreateText(holder, "Label", s.Acting.IsAi ? "AI TURN" : "TURN TIME", 13,
                TextAnchor.MiddleCenter, ColonistUiKit.Cream, FontStyle.Bold);
            SetRect(label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 38f), new Vector2(130f, 18f));
            _hsTimerText = RunHudUiFactory.CreateText(holder, "Seconds", "", 46, TextAnchor.MiddleCenter,
                ColonistUiKit.Cream, FontStyle.Bold);
            _hsTimerText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _hsTimerText.verticalOverflow = VerticalWrapMode.Overflow;
            SetRect(_hsTimerText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -6f), new Vector2(130f, 60f));

            var seat = s.Current;
            string chargeText = s.Match.IsSetup ? "+90 s (after setup)"
                : seat.ExtraTimeUsedThisTurn ? "+90 s used this turn"
                : $"+90 s  ·  {seat.ExtraTimeCharges} charge{(seat.ExtraTimeCharges == 1 ? "" : "s")}";
            var charge = RunHudUiFactory.CreateButton(_hotSeatRoot, chargeText, RunHudUiFactory.ButtonFill);
            charge.name = "ExtraTimeCharge";
            SetRect((RectTransform)charge.transform, new Vector2(0.5f, 0f), new Vector2(60f, 220f), new Vector2(290f, 62f));
            bool usable = s.CanUseExtraTime;
            ColonistKenneyHudArt.ApplyColonistWarmButton(charge.GetComponent<Image>(), primaryGreen: usable);
            var ct = charge.GetComponentInChildren<Text>();
            if (ct != null)
            {
                ct.color = usable ? ColonistUiKit.Cream : ColonistUiKit.Ink;
                ct.fontSize = 20;
            }

            charge.interactable = usable;
            charge.onClick.AddListener(() => _hotSeat?.UseExtraTime());
            var hint = RunHudUiFactory.CreateText(_hotSeatRoot, "ChargeHint",
                "1 charge at start · +1 every 5 rounds", 15, TextAnchor.MiddleCenter, ColonistUiKit.Cream,
                FontStyle.Italic);
            SetRect(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(60f, 176f), new Vector2(320f, 22f));
            AddShadow(hint.gameObject, 1.2f);
        }

        private void UpdateHotSeatTimer()
        {
            if (_hotSeat == null || _hsTimerText == null)
                return;
            var clock = _hotSeat.Clock;
            if (_hotSeat.Acting.IsAi || _hotSeat.IsOver)
            {
                _hsTimerText.text = "—";
                _hsTimerRoot.localScale = Vector3.one;
                return;
            }

            int secs = clock.DisplaySeconds;
            _hsTimerText.text = clock.ShouldPulse ? secs.ToString() : $"{secs / 60}:{secs % 60:00}";
            float scale = clock.PulseScale(Time.unscaledTime);
            _hsTimerText.rectTransform.localScale = new Vector3(scale, scale, 1f);
            _hsTimerText.color = clock.ShouldPulse ? new Color(1f, 0.92f, 0.86f, 1f) : ColonistUiKit.Cream;
            _hsTimerText.fontSize = clock.ShouldPulse ? 58 : 46;
            _hsTimerDisc.color = clock.ShouldPulse
                ? Color.Lerp(TimerPulseRed, new Color(0.16f, 0.11f, 0.07f, 0.96f), 0.15f)
                : new Color(0.16f, 0.11f, 0.07f, 0.96f);
            float ringScale = clock.ShouldPulse ? 1f + (scale - 1f) * 0.25f : 1f;
            _hsTimerRoot.localScale = new Vector3(ringScale, ringScale, 1f);
        }

        private void BuildHotSeatHand()
        {
            var s = _hotSeat;
            int seat = s.State.ActingSeat;
            var panel = CreateImage(_hotSeatRoot, "Hand", null);
            panel.preserveAspect = false;
            ColonistUiKit.Apply(panel, UiKitPiece.FrameMedium);
            SetRect(panel.rectTransform, new Vector2(0f, 0f), new Vector2(330f, 120f), new Vector2(580f, 170f));
            var ink = ColonistUiKit.TextOn(UiKitPiece.FrameMedium);
            bool visible = s.IsHandVisible(seat);
            string title = visible ? $"YOUR HAND  ·  {s.Acting.Name}" : s.Acting.IsAi ? "AI IS PLAYING" : "HAND HIDDEN";
            var t = RunHudUiFactory.CreateText(panel.rectTransform, "Title", title, 16, TextAnchor.MiddleCenter, ink,
                FontStyle.Bold);
            SetRect(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(520f, 22f));
            if (!visible)
            {
                var hidden = RunHudUiFactory.CreateText(panel.rectTransform, "Hidden",
                    "Cards stay face-down until their owner takes the device.", 17, TextAnchor.MiddleCenter,
                    ColonistUiKit.InkMuted, FontStyle.Italic);
                SetRect(hidden.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -12f), new Vector2(520f, 40f));
                return;
            }

            var hand = s.Acting.Hand;
            var resources = MatchController.AllResources;
            for (int i = 0; i < resources.Length; i++)
            {
                var r = resources[i];
                float x = -208f + i * 104f;
                var icon = CreateImage(panel.rectTransform, "Res_" + r, ColonistHudResourceSprites.Get(r));
                SetRect(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(x, -4f), new Vector2(70f, 70f));
                var count = RunHudUiFactory.CreateText(panel.rectTransform, "Count_" + r, hand[r].ToString(), 26,
                    TextAnchor.MiddleCenter, ink, FontStyle.Bold);
                SetRect(count.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(x, -58f), new Vector2(70f, 30f));
            }
        }

        private void BuildHotSeatActions()
        {
            var s = _hotSeat;
            var state = s.State;
            bool human = !s.Acting.IsAi && !s.IsOver && !s.PassScreenPending;
            bool free = state.ActiveTrade == null;
            var buttons = new List<(string label, bool green, bool enabled, System.Action click)>();
            string hint = null;
            switch (state.Phase)
            {
                case MatchPhase.SetupSettlement:
                    buttons.Add(("Best spot", true, human, () => s.ApplySuggested()));
                    hint = "Click a glowing corner to place your settlement";
                    break;
                case MatchPhase.SetupRoad:
                    buttons.Add(("Best road", true, human, () => s.ApplySuggested()));
                    hint = "Click a glowing edge next to it for your road";
                    break;
                case MatchPhase.Roll:
                    buttons.Add(("Roll dice", true, human, () => s.Roll()));
                    break;
                case MatchPhase.MoveRobber:
                    buttons.Add(("Best tile", false, human, () => s.ApplySuggested()));
                    hint = "Click a tile to move the robber (robs the richest neighbour)";
                    break;
                case MatchPhase.RoadBuilding:
                    buttons.Add(("Best road", true, human, () => s.ApplySuggested()));
                    hint = "Road Building: click glowing edges for your free roads";
                    break;
                case MatchPhase.Main:
                    buttons.Add((_hsTradeOpen && free ? "Close trade" : "Trade", false, human && free, () =>
                    {
                        _hsTradeOpen = !_hsTradeOpen;
                        _hsPicker = null;
                    }));
                    buttons.Add(("Buy card", false, human && s.Match.CanBuyDevCard(state.CurrentSeat) && free,
                        () => s.Apply(MatchCommand.Simple(MatchCommandType.BuyDevCard, state.CurrentSeat))));
                    buttons.Add(("Auto-build", false, human && free, () => s.AutoBuild()));
                    buttons.Add(("End turn", true, human && free, () => s.EndTurn()));
                    if (human && free)
                        hint = "Click glowing spots on the board to build";
                    break;
            }

            if (hint != null && human)
            {
                var ht = RunHudUiFactory.CreateText(_hotSeatRoot, "BoardHint", hint, 17, TextAnchor.MiddleCenter,
                    ColonistUiKit.Cream, FontStyle.Italic);
                SetRect(ht.rectTransform, new Vector2(0.5f, 0f), new Vector2(150f, 112f), new Vector2(760f, 24f));
                AddShadow(ht.gameObject, 1.2f);
            }

            const float h = 60f, gap = 14f;
            float w = buttons.Count > 2 ? 190f : 250f;
            float total = buttons.Count * w + (buttons.Count - 1) * gap;
            // Left edge clear of the hand panel (screen x 40..620 of 1920), centre-relative.
            float x = Mathf.Max(70f - total / 2f, 640f - 960f) + w / 2f;
            for (int i = 0; i < buttons.Count; i++)
            {
                var (label, green, enabled, click) = buttons[i];
                var b = RunHudUiFactory.CreateButton(_hotSeatRoot, label, RunHudUiFactory.ButtonFill);
                b.name = "Action_" + i;
                SetRect((RectTransform)b.transform, new Vector2(0.5f, 0f), new Vector2(x + i * (w + gap), 62f),
                    new Vector2(w, h));
                ColonistKenneyHudArt.ApplyColonistWarmButton(b.GetComponent<Image>(), primaryGreen: green);
                var text = b.GetComponentInChildren<Text>();
                if (text != null)
                {
                    text.color = green ? ColonistUiKit.Cream : ColonistUiKit.Ink;
                    text.fontSize = 20;
                }

                b.interactable = enabled;
                b.onClick.AddListener(() =>
                {
                    click();
                    _hotSeatDirty = true;
                });
            }
        }

        private void BuildPassScreen(MatchSeatState seat)
        {
            var dim = CreateImage(_hotSeatRoot, "PassScreen", null);
            dim.preserveAspect = false;
            dim.color = new Color(0.05f, 0.04f, 0.03f, 0.97f);
            dim.raycastTarget = true;
            Stretch(dim.rectTransform);

            var card = CreateCardFrame(dim.rectTransform, "PassCard", wide: true);
            SetRect(card, new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(760f, 470f));
            var disc = CreateDisc(card, "SeatDisc", PlayerPieceVisuals.ColorForSeat(seat.Index), 120f);
            SetRect(disc.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(120f, 120f));
            var num = RunHudUiFactory.CreateText(disc.rectTransform, "N", (seat.Index + 1).ToString(), 56,
                TextAnchor.MiddleCenter, SeatInk(seat.Index), FontStyle.Bold);
            Stretch(num.rectTransform);
            var kicker = RunHudUiFactory.CreateText(card, "Kicker", "PASS THE DEVICE TO", 18, TextAnchor.MiddleCenter,
                ColonistUiKit.InkGold, FontStyle.Bold);
            SetRect(kicker.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -196f), new Vector2(640f, 26f));
            var name = RunHudUiFactory.CreateText(card, "Name", seat.Name, 50, TextAnchor.MiddleCenter, ColonistUiKit.Ink,
                FontStyle.Bold);
            SetRect(name.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -244f), new Vector2(640f, 62f));
            bool discard = _hotSeat.State.Phase == MatchPhase.Discard && seat.Index != _hotSeat.State.CurrentSeat;
            string why = discard && _hotSeat.State.PendingDiscards.TryGetValue(seat.Index, out int owed)
                ? $"A 7 was rolled: {seat.Name} must discard {owed}. Everyone else, look away."
                : $"Everyone else, look away. {seat.Name}'s hand stays hidden until they tap below; the turn timer starts then.";
            var body = RunHudUiFactory.CreateText(card, "Body", why,
                19, TextAnchor.MiddleCenter, ColonistUiKit.InkMuted, FontStyle.Italic);
            SetRect(body.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -304f), new Vector2(600f, 56f));
            AddLobbyButton(card, "ConfirmPass", discard ? $"I'm {seat.Name} · discard" : $"I'm {seat.Name} · start my turn",
                new Vector2(0f, 74f),
                new Vector2(380f, 62f), true, true, () => _hotSeat?.ConfirmPass(), anchorBottom: true);
        }

        private void BuildGameOver(MatchState state)
        {
            var dim = CreateImage(_hotSeatRoot, "GameOver", null);
            dim.preserveAspect = false;
            dim.color = new Color(0.05f, 0.04f, 0.03f, 0.72f);
            dim.raycastTarget = true;
            Stretch(dim.rectTransform);
            var card = CreateCardFrame(dim.rectTransform, "GameOverCard", wide: true);
            float h = 260f + state.Seats.Count * 40f;
            SetRect(card, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700f, h));
            int winner = state.Winner ?? 0;
            var title = RunHudUiFactory.CreateText(card, "Title", $"{state.Seats[winner].Name} wins!", 44,
                TextAnchor.MiddleCenter, ColonistUiKit.Ink, FontStyle.Bold);
            SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(620f, 56f));
            var ranked = state.Seats.OrderByDescending(x => x.VictoryPoints).ToList();
            for (int i = 0; i < ranked.Count; i++)
            {
                var line = RunHudUiFactory.CreateText(card, "Rank_" + i,
                    $"{i + 1}.  {ranked[i].Name}  —  {ranked[i].VictoryPoints} VP", 22, TextAnchor.MiddleCenter,
                    ColonistUiKit.Ink, i == 0 ? FontStyle.Bold : FontStyle.Normal);
                SetRect(line.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -130f - i * 40f), new Vector2(600f, 34f));
            }

            AddLobbyButton(card, "BackToMenu", "Back to menu", new Vector2(0f, 60f), new Vector2(260f, 58f), true,
                true, ExitHotSeat, anchorBottom: true);
        }

        // ---------- helpers ----------

        private static Color SeatInk(int seat) => seat == 3 ? ColonistUiKit.Ink : ColonistUiKit.Cream;

        private static Image CreateDisc(Transform parent, string name, Color color, float size)
        {
            var img = CreateImage(parent, name, DiscSprite());
            img.color = color;
            img.preserveAspect = true;
            ((RectTransform)img.transform).sizeDelta = new Vector2(size, size);
            return img;
        }

        private static Sprite DiscSprite()
        {
            if (_discSprite != null)
                return _discSprite;
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            float c = (n - 1) / 2f, r = n / 2f - 1f;
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                byte a = (byte)(Mathf.Clamp01(r - d + 0.5f) * 255f);
                px[y * n + x] = new Color32(255, 255, 255, a);
            }

            tex.SetPixels32(px);
            tex.Apply();
            _discSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            return _discSprite;
        }
    }
}
