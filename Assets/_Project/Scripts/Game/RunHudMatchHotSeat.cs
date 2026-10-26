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
            _hotSeatDisplay.State.FishermenOfCatanEnabled = config.Fishermen; // v143: draw the fishing grounds
            var boardView = Object.FindFirstObjectByType<BoardView>();
            // v136: the table chrome frames the board (timer bottom-right), and the robber stays token-sized.
            ColonistPlayAreaLayout.Override = HotSeatPlayArea;
            ColonistBoardVisualScale.RobberScaleCap = HotSeatRobberScaleCap;
            ColonistBoardVisualScale.RobberOffsetScale = HotSeatRobberOffsetScale;
            PlayerPieceVisuals.BuildingScaleMultiplier = HotSeatBuildingScale;
            PlayerPieceVisuals.RoadScaleMultiplier = HotSeatRoadScale;
            if (boardView != null)
                boardView.Rebuild(_hotSeatDisplay);
            if (_boardInput != null)
                _boardInput.enabled = false;
            _hsVictimCommand = null;
            _hsPickSeat = -1;
            _hsTradeOpen = false;
            _hsFishPick = false;
            ResetShenanigansUi();
            _hsPicker = null;
            _hsCounterSeat = -1;
            _hsDiscardSeat = -1;
            _hsGive = _hsGet = _hsDiscard = ResourceBundle.Zero;
            _hsGiveAny = _hsGetAny = 0;
            CloseWildPicker();
            _hsBoardInput = GetComponent<HotSeatBoardInput>();
            if (_hsBoardInput == null)
                _hsBoardInput = gameObject.AddComponent<HotSeatBoardInput>();
            _hsBoardInput.Bind(_hotSeat, boardView);
            _hsBoardInput.Intercept = InterceptHotSeatBoardCommand;
            _hsBoardInput.Blocked = HotSeatBoardBlocked;

            if (_preGameRoot != null)
                _preGameRoot.gameObject.SetActive(false);
            PaintedPreGameMenusVisible = true;
            PlaceholderHudLayout.SetActivePanelWidth(380f);
            _canvas.gameObject.SetActive(true);

            ResetMatchFx();
            _hotSeat.Tick(0f); // AI seats that act first
            RebuildHotSeat();
        }

        public void ExitHotSeat()
        {
            if (_hotSeat == null)
                return;
            _hotSeat = null;
            ResetMatchFx();
            _mainMenuOpen = true; // v143: leaving (or finishing) a table lands on the main menu
            _hsVictimCommand = null;
            ColonistPlayAreaLayout.Override = null;
            ColonistBoardVisualScale.RobberScaleCap = float.MaxValue;
            ColonistBoardVisualScale.RobberOffsetScale = 1f;
            PlayerPieceVisuals.BuildingScaleMultiplier = 1f;
            PlayerPieceVisuals.RoadScaleMultiplier = 1f;
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
            ObserveMatchFx();
        }

        private void HideRunHudForHotSeat()
        {
            foreach (Transform child in _canvas.transform)
            {
                if (child == _hotSeatRoot || child.name == "HotSeatTable" || child.name == FxRootName)
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
            BuildEventCard(state);
            BuildHotSeatActions();
            if (!s.IsOver && !s.PassScreenPending)
            {
                BuildTradeBoard();
                BuildDiscardPicker();
                BuildDevCardPicker();
                BuildHarbourPilotPicker();
                BuildVictimPicker();
                BuildPartyPickers();
                BuildShenanigans();
            }

            if (s.IsOver)
                BuildGameOver(state);
            else if (s.PassScreenPending)
                BuildPassScreen(state.Acting);
            BuildHotSeatInfoPopup(state);

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
                MatchPhase.CivPick => "choose a civilization (secret)",
                MatchPhase.QuestPick => "choose a secret quest + trait",
                MatchPhase.SetupSettlement => "place a settlement",
                MatchPhase.SetupRoad => "place a road",
                MatchPhase.Roll => "roll the dice",
                MatchPhase.MoveRobber => _hotSeat.Match.SeafarersOn ? "move the robber or the pirate" : "move the robber (click a tile)",
                MatchPhase.Discard => $"{state.Acting.Name} discards half (rolled 7)",
                MatchPhase.RoadBuilding => _hotSeat.Match.SeafarersOn
                    ? $"place {state.FreeRoadsLeft} free road{(state.FreeRoadsLeft == 1 ? "" : "s")} or ship{(state.FreeRoadsLeft == 1 ? "" : "s")}"
                    : $"place {state.FreeRoadsLeft} free road{(state.FreeRoadsLeft == 1 ? "" : "s")}",
                MatchPhase.Vote => $"secret ballot · {state.Acting.Name} votes",
                MatchPhase.Auction => $"Night Market · {state.Acting.Name} bids in secret",
                MatchPhase.Main => state.LastRoll > 0 ? $"rolled {state.LastRoll} · build or end turn" : "build or end turn",
                _ => "game over"
            };
            bool setup = state.Phase == MatchPhase.SetupSettlement || state.Phase == MatchPhase.SetupRoad;
            string round = state.IsPicking ? "PICKS" : setup ? "SETUP" : $"ROUND {state.Round + 1}";
            var ink = ColonistUiKit.TextOn(UiKitPiece.Badge);
            var top = RunHudUiFactory.CreateText(banner.rectTransform, "Title",
                $"PASS & PLAY  ·  {round}  ·  {state.Current.Name}: {phase}", 22, TextAnchor.MiddleCenter, ink,
                FontStyle.Bold);
            SetRect(top.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(960f, 30f));
            string last = !string.IsNullOrEmpty(_hotSeat.LastMessage) ? _hotSeat.LastMessage
                : state.Log.Count > 0 ? state.Log[state.Log.Count - 1] : "";
            // v140: never keep a stale "offers a trade" once the offer is gone.
            if (state.ActiveTrade == null && last.EndsWith("offers a trade."))
                last = "";
            var sub = RunHudUiFactory.CreateText(banner.rectTransform, "Log", last, 17, TextAnchor.MiddleCenter,
                ink, FontStyle.Italic);
            SetRect(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -52f), new Vector2(960f, 24f));

            var leave = RunHudUiFactory.CreateButton(_hotSeatRoot, "Leave match", RunHudUiFactory.ButtonSecondary);
            leave.name = "LeaveMatch";
            SetRect((RectTransform)leave.transform, new Vector2(0f, 1f), new Vector2(110f, -52f), new Vector2(170f, 50f));
            ColonistKenneyHudArt.ApplyColonistWarmButton(leave.GetComponent<Image>(), primaryGreen: false);
            leave.onClick.AddListener(LeaveMatchToMenu); // v143: clean return to the start menu
        }

        private void BuildSeatStrip(MatchState state)
        {
            int n = state.Seats.Count;
            const float rowH = 104f, width = 360f;
            var panel = CreateImage(_hotSeatRoot, "SeatStrip", null);
            panel.preserveAspect = false;
            ColonistUiKit.Apply(panel, UiKitPiece.FrameMedium);
            float h = 64f + n * rowH + 14f; // bottom padding: the last row's pips/civ name clear the frame
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

                const float shift = 0f;
                string civLabel = null;
                if (state.Config.PartyRules)
                {
                    // v137: with party rules the seat's own leader fills the seat-coloured ring (a neutral
                    // silhouette until every civ is revealed); the seat number / AI tag moves to a small badge.
                    bool revealed = SeatCivRevealed(state, seat);
                    var ring = CreateDisc(row.rectTransform, "Disc", PlayerPieceVisuals.ColorForSeat(i), 78f);
                    SetRect(ring.rectTransform, new Vector2(0f, 0.5f), new Vector2(46f, 0f), new Vector2(78f, 78f));
                    var face = CreateImage(row.rectTransform, "Civ",
                        revealed ? ColonistRoguelikeArt.GetCivilizationPortrait(seat.Civilization) : NeutralLeaderSprite());
                    face.preserveAspect = true;
                    SetRect(face.rectTransform, new Vector2(0f, 0.5f), new Vector2(46f, 0f), new Vector2(68f, 68f));
                    if (revealed)
                        MakeSeatPortraitClickable(face, i); // v138: opens the civ info popup beside the strip
                    if (revealed && _hsInfoSeat == i)
                    {
                        var sel = CreateDisc(row.rectTransform, "Selected", new Color(1f, 0.93f, 0.6f, 1f), 88f);
                        SetRect(sel.rectTransform, new Vector2(0f, 0.5f), new Vector2(46f, 0f), new Vector2(88f, 88f));
                        sel.transform.SetSiblingIndex(ring.transform.GetSiblingIndex());
                    }
                    var badge = CreateDisc(row.rectTransform, "Badge", PlayerPieceVisuals.ColorForSeat(i), 30f);
                    SetRect(badge.rectTransform, new Vector2(0f, 0.5f), new Vector2(76f, -28f), new Vector2(30f, 30f));
                    var tag = RunHudUiFactory.CreateText(badge.rectTransform, "N", seat.IsAi ? "AI" : (i + 1).ToString(),
                        seat.IsAi ? 12 : 16, TextAnchor.MiddleCenter, SeatInk(i), FontStyle.Bold);
                    Stretch(tag.rectTransform);
                    if (revealed && Core.Civilizations.CivilizationLibrary.TryGet(seat.Civilization, out var civDef))
                        civLabel = civDef.Name;
                }
                else
                {
                    var disc = CreateDisc(row.rectTransform, "Disc", PlayerPieceVisuals.ColorForSeat(i), 62f);
                    SetRect(disc.rectTransform, new Vector2(0f, 0.5f), new Vector2(44f, 0f), new Vector2(62f, 62f));
                    var num = RunHudUiFactory.CreateText(disc.rectTransform, "N", seat.IsAi ? "AI" : (i + 1).ToString(),
                        seat.IsAi ? 20 : 28, TextAnchor.MiddleCenter, SeatInk(i), FontStyle.Bold);
                    Stretch(num.rectTransform);
                }

                var name = RunHudUiFactory.CreateText(row.rectTransform, "Name", seat.Name, 22, TextAnchor.MiddleLeft,
                    ink, FontStyle.Bold);
                SetRect(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(170f + shift, 18f), new Vector2(150f, 30f));
                string detail = $"{seat.CardCount} cards · {seat.DevCardCount} dev" +
                                (seat.KnightsPlayed > 0 ? $" · {seat.KnightsPlayed} kn" : "");
                var det = RunHudUiFactory.CreateText(row.rectTransform, "Detail", detail, 16, TextAnchor.MiddleLeft,
                    ColonistUiKit.InkMuted, FontStyle.Normal);
                float detW = 160f;
                SetRect(det.rectTransform, new Vector2(0f, 0.5f), new Vector2(95f + shift + detW / 2f, -8f), new Vector2(detW, 24f));
                det.resizeTextForBestFit = true;
                det.resizeTextMinSize = 11;
                det.resizeTextMaxSize = 16;

                // Extra-time charges as small gold pips.
                for (int c = 0; c < Mathf.Min(seat.ExtraTimeCharges, 6); c++)
                {
                    var pip = CreateDisc(row.rectTransform, "Charge_" + c, new Color(0.93f, 0.72f, 0.22f, 1f), 14f);
                    SetRect(pip.rectTransform, new Vector2(0f, 0.5f), new Vector2(102f + shift + c * 18f, -32f), new Vector2(14f, 14f));
                }

                if (civLabel != null)
                {
                    float x0 = 95f + Mathf.Min(seat.ExtraTimeCharges, 6) * 18f + 6f;
                    var civText = RunHudUiFactory.CreateText(row.rectTransform, "CivName", civLabel, 13, TextAnchor.MiddleLeft,
                        ColonistUiKit.InkMuted, FontStyle.Italic);
                    float w = 252f - x0;
                    SetRect(civText.rectTransform, new Vector2(0f, 0.5f), new Vector2(x0 + w / 2f, -32f), new Vector2(w, 18f));
                    civText.resizeTextForBestFit = true;
                    civText.resizeTextMinSize = 9;
                    civText.resizeTextMaxSize = 13;
                }

                // Victory-point cards stay hidden: everyone sees public points only.
                var vp = RunHudUiFactory.CreateText(row.rectTransform, "VP", seat.PublicVictoryPoints.ToString(), 36,
                    TextAnchor.MiddleCenter, ink, FontStyle.Bold);
                SetRect(vp.rectTransform, new Vector2(1f, 0.5f), new Vector2(-40f, 8f), new Vector2(64f, 44f));
                BuildSeatShenaniganMarks(row.rectTransform, state, i); // v141: bounty + face-down card marks
                var vpl = RunHudUiFactory.CreateText(row.rectTransform, "VPLabel",
                    "VP" + (state.LongestRoadSeat == i ? (state.Board.IsSeafarers ? " · route" : " · road") : "") + (state.LargestArmySeat == i ? " · army" : ""), 13, TextAnchor.MiddleCenter, ColonistUiKit.InkMuted,
                    FontStyle.Bold);
                SetRect(vpl.rectTransform, new Vector2(1f, 0.5f), new Vector2(-40f, -24f), new Vector2(80f, 18f));
            }
        }

        private void BuildTimerAndCharge()
        {
            var s = _hotSeat;
            var holder = new GameObject("Timer").AddComponent<RectTransform>();
            holder.SetParent(_hotSeatRoot, false);
            SetRect(holder, new Vector2(1f, 0f), new Vector2(-200f, 276f), new Vector2(150f, 150f));
            _hsTimerRoot = holder;
            var ring = CreateDisc(holder, "Ring", new Color(0.93f, 0.72f, 0.22f, 1f), 150f);
            Stretch(ring.rectTransform);
            _hsTimerDisc = CreateDisc(holder, "Disc", new Color(0.16f, 0.11f, 0.07f, 0.96f), 136f);
            SetRect(_hsTimerDisc.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(136f, 136f));
            var label = RunHudUiFactory.CreateText(holder, "Label", s.Acting.IsAi ? "AI TURN" : s.Clock.Enabled ? "TURN TIME" : "UNTIMED", 13,
                TextAnchor.MiddleCenter, ColonistUiKit.Cream, FontStyle.Bold);
            SetRect(label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 38f), new Vector2(130f, 18f));
            _hsTimerText = RunHudUiFactory.CreateText(holder, "Seconds", "", 46, TextAnchor.MiddleCenter,
                ColonistUiKit.Cream, FontStyle.Bold);
            _hsTimerText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _hsTimerText.verticalOverflow = VerticalWrapMode.Overflow;
            SetRect(_hsTimerText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -6f), new Vector2(130f, 60f));

            var seat = s.Current;
            string chargeText = !s.Clock.Enabled ? "No turn timer"
                : s.Match.IsPregame ? "+90 s (after setup)"
                : seat.ExtraTimeUsedThisTurn ? "+90 s used this turn"
                : $"+90 s  ·  {seat.ExtraTimeCharges} charge{(seat.ExtraTimeCharges == 1 ? "" : "s")}";
            var charge = RunHudUiFactory.CreateButton(_hotSeatRoot, chargeText, RunHudUiFactory.ButtonFill);
            charge.name = "ExtraTimeCharge";
            SetRect((RectTransform)charge.transform, new Vector2(1f, 0f), new Vector2(-200f, 136f), new Vector2(290f, 62f));
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
                s.Clock.Enabled ? "1 charge at start · +1 every 5 rounds" : "Turns are untimed (setup screen)", 15, TextAnchor.MiddleCenter, ColonistUiKit.Cream,
                FontStyle.Italic);
            SetRect(hint.rectTransform, new Vector2(1f, 0f), new Vector2(-200f, 90f), new Vector2(320f, 22f));
            AddShadow(hint.gameObject, 1.2f);
        }

        private void UpdateHotSeatTimer()
        {
            if (_hotSeat == null || _hsTimerText == null)
                return;
            var clock = _hotSeat.Clock;
            if (_hotSeat.Acting.IsAi || _hotSeat.IsOver || !clock.Enabled)
            {
                _hsTimerText.text = "—";
                _hsTimerText.rectTransform.localScale = Vector3.one;
                _hsTimerText.fontSize = 46;
                _hsTimerText.color = ColonistUiKit.Cream;
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

        /// <summary>
        /// The seat whose hand to show while an AI's trade offer waits for an answer, when exactly one human sits at
        /// the table (nobody else could see it); -1 otherwise.
        /// </summary>
        private int HotSeatSoleHumanAnsweringAi()
        {
            var s = _hotSeat;
            if (s == null || s.IsOver || s.PassScreenPending || !MatchAi.AwaitingHumanAnswer(s.Match))
                return -1;
            int human = -1;
            for (int i = 0; i < s.State.Seats.Count; i++)
            {
                if (s.State.Seats[i].IsAi)
                    continue;
                if (human >= 0)
                    return -1;
                human = i;
            }

            var trade = s.State.ActiveTrade;
            return human >= 0 && trade.Responses[human].Kind == TradeResponseKind.Pending ? human : -1;
        }

        private void BuildHotSeatHand()
        {
            var s = _hotSeat;
            int seat = s.State.ActingSeat;
            // v142: an AI's offer waiting on the only human at the table shows that human's hand to answer with.
            int answering = HotSeatSoleHumanAnsweringAi();
            if (answering >= 0)
                seat = answering;
            var panel = CreateImage(_hotSeatRoot, "Hand", null);
            panel.preserveAspect = false;
            ColonistUiKit.Apply(panel, UiKitPiece.FrameMedium);
            SetRect(panel.rectTransform, new Vector2(0f, 0f), new Vector2(330f, 120f), new Vector2(580f, 170f));
            var ink = ColonistUiKit.TextOn(UiKitPiece.FrameMedium);
            bool visible = answering >= 0 || s.IsHandVisible(seat);
            var owner = s.State.Seats[seat];
            string title = visible ? $"YOUR HAND  ·  {owner.Name}" : s.Acting.IsAi ? "AI IS PLAYING" : "HAND HIDDEN";
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

            var hand = owner.Hand;
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

            BuildHotSeatFish(panel.rectTransform, seat);
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
                    hint = s.Match.SeafarersOn
                        ? "Click a land tile for the robber or the sea for the pirate (it robs ships)"
                        : "Click a tile to move the robber; pick whom to rob if several players touch it";
                    break;
                case MatchPhase.RoadBuilding:
                    buttons.Add(("Best road", true, human, () => s.ApplySuggested()));
                    hint = "Road Building: click glowing edges for your free roads";
                    if (s.Match.SeafarersOn)
                    {
                        // v145: the two free pieces may be roads or ships, mixed.
                        bool ships = _hsBoardInput != null && _hsBoardInput.Mode == MatchBuildMode.Ship;
                        buttons.Add((ships ? "Roads" : "Ships", false, human && s.Match.ValidShips(state.CurrentSeat).Any(),
                            () => SetHotSeatBoardMode(ships ? MatchBuildMode.Auto : MatchBuildMode.Ship)));
                        hint = ships
                            ? "Road Building: click glowing sea circles for free ships"
                            : "Road Building: free roads or ships (any mix)";
                    }
                    break;
                case MatchPhase.Main:
                    buttons.Add((_hsTradeOpen && free ? "Close trade" : "Trade", false, human && free, () =>
                    {
                        _hsTradeOpen = !_hsTradeOpen;
                        _hsPicker = null;
                        if (_hsTradeOpen)
                            OpenScheme(HotSeatScheme.None);
                    }));
                    buttons.Add(("Buy card", false, human && s.Match.CanBuyDevCard(state.CurrentSeat) && free,
                        () => s.Apply(MatchCommand.Simple(MatchCommandType.BuyDevCard, state.CurrentSeat))));
                    if (state.Config.PartyRules)
                        buttons.Add((_hsScheme != HotSeatScheme.None && free ? "Close schemes" : "Schemes", false, human && free,
                            () => OpenScheme(_hsScheme == HotSeatScheme.None ? HotSeatScheme.Bounty : HotSeatScheme.None)));
                    if (s.Match.SeafarersOn)
                    {
                        // v145 Seafarers: build mode toggles for ships and the one ship move per turn.
                        var mode = _hsBoardInput != null ? _hsBoardInput.Mode : MatchBuildMode.Auto;
                        int cs = state.CurrentSeat;
                        bool canShip = state.Seats[cs].Hand.CanAfford(s.Match.ShipCostFor(cs)) && s.Match.ValidShips(cs).Any();
                        buttons.Add((mode == MatchBuildMode.Ship ? "Land" : "Ship", mode == MatchBuildMode.Ship,
                            human && free && (canShip || mode == MatchBuildMode.Ship),
                            () => SetHotSeatBoardMode(mode == MatchBuildMode.Ship ? MatchBuildMode.Auto : MatchBuildMode.Ship)));
                        buttons.Add((mode == MatchBuildMode.MoveShip ? "Stay" : "Move ship", mode == MatchBuildMode.MoveShip,
                            human && free && (s.Match.MovableShips(cs).Any() || mode == MatchBuildMode.MoveShip),
                            () => SetHotSeatBoardMode(mode == MatchBuildMode.MoveShip ? MatchBuildMode.Auto : MatchBuildMode.MoveShip)));
                        if (human && free && mode == MatchBuildMode.Ship)
                            hint = "Ship (1 lumber + 1 wool): click a glowing sea circle";
                        else if (human && free && mode == MatchBuildMode.MoveShip)
                            hint = _hsBoardInput.MoveFrom.HasValue
                                ? "Click a circle to sail the ship there"
                                : "Click your open ship (gold) to move it";
                    }

                    buttons.Add(("Auto-build", false, human && free, () => s.AutoBuild()));
                    buttons.Add(("End turn", true, human && free, () => s.EndTurn()));
                    if (human && free && hint == null)
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

            const float h = 60f;
            float gap = buttons.Count > 5 ? 10f : 14f;
            float w = buttons.Count > 5 ? 140f : buttons.Count > 4 ? 162f : buttons.Count > 2 ? 190f : 250f;
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

        /// <summary>v145: switch what board clicks build (land, ships, ship move) and redraw the highlights.</summary>
        private void SetHotSeatBoardMode(MatchBuildMode mode)
        {
            if (_hsBoardInput == null)
                return;
            _hsBoardInput.Mode = mode;
            _hsBoardInput.MoveFrom = null;
            _hsBoardInput.RefreshTargets();
            GameAudio.Play(Sfx.Click);
        }

        public void SetHotSeatBoardModeForTests(MatchBuildMode mode) => SetHotSeatBoardMode(mode);

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
            var phase = _hotSeat.State.Phase;
            bool picking = _hotSeat.State.IsPicking;
            string why = discard && _hotSeat.State.PendingDiscards.TryGetValue(seat.Index, out int owed)
                ? $"A 7 was rolled: {seat.Name} must discard {owed}. Everyone else, look away."
                : phase == MatchPhase.CivPick
                    ? $"{seat.Name} picks a civilization in secret. Everyone else, look away; all picks are revealed together."
                    : phase == MatchPhase.Vote
                        ? $"{seat.Name} votes on the decree in secret. Everyone else, look away: only the totals are revealed."
                    : phase == MatchPhase.Auction
                        ? $"{seat.Name} seals a bid at the Night Market. Everyone else, look away: bids stay sealed."
                    : phase == MatchPhase.QuestPick
                        ? $"{seat.Name} picks a secret quest and a trait. Everyone else, look away: quests stay hidden until the game ends."
                        : $"Everyone else, look away. {seat.Name}'s hand stays hidden until they tap below; the turn timer starts then.";
            var body = RunHudUiFactory.CreateText(card, "Body", why,
                19, TextAnchor.MiddleCenter, ColonistUiKit.InkMuted, FontStyle.Italic);
            SetRect(body.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -304f), new Vector2(600f, 56f));
            AddLobbyButton(card, "ConfirmPass",
                discard ? $"I'm {seat.Name} · discard" : phase == MatchPhase.Vote ? $"I'm {seat.Name} · vote in secret"
                    : phase == MatchPhase.Auction ? $"I'm {seat.Name} · seal my bid" : picking ? $"I'm {seat.Name} · pick in secret" : $"I'm {seat.Name} · start my turn",
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
            bool party = state.Config.PartyRules;
            var sliced = party ? ColonistRoguelikeArt.GetCardFrameSliced(true) : null;
            if (sliced != null)
            {
                // The reveal card is taller than the frame art: nine-slice it like the other modals.
                var img = card.GetComponent<Image>();
                img.sprite = sliced;
                img.type = Image.Type.Sliced;
                img.preserveAspect = false;
            }

            float h = party ? 330f + state.Seats.Count * 84f : 260f + state.Seats.Count * 40f;
            SetRect(card, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(party ? 960f : 700f, h));
            int winner = state.Winner ?? 0;
            var title = RunHudUiFactory.CreateText(card, "Title", $"{state.Seats[winner].Name} wins!", 44,
                TextAnchor.MiddleCenter, ColonistUiKit.Ink, FontStyle.Bold);
            SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, party ? -96f : -70f), new Vector2(620f, 56f));
            if (party)
            {
                var kicker = RunHudUiFactory.CreateText(card, "Reveal", "SECRET QUESTS REVEALED", 16,
                    TextAnchor.MiddleCenter, ColonistUiKit.InkGold, FontStyle.Bold);
                SetRect(kicker.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -138f), new Vector2(620f, 22f));
                BuildPartyGameOverRows(card, state);
            }

            var ranked = state.Seats.OrderByDescending(x => x.VictoryPoints).ToList();
            for (int i = 0; i < ranked.Count && !party; i++)
            {
                var line = RunHudUiFactory.CreateText(card, "Rank_" + i,
                    $"{i + 1}.  {ranked[i].Name}  —  {ranked[i].VictoryPoints} VP", 22, TextAnchor.MiddleCenter,
                    ColonistUiKit.Ink, i == 0 ? FontStyle.Bold : FontStyle.Normal);
                SetRect(line.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -130f - i * 40f), new Vector2(600f, 34f));
            }

            AddLobbyButton(card, "BackToMenu", "Back to menu", new Vector2(0f, 60f), new Vector2(260f, 58f), true,
                true, LeaveMatchToMenu, anchorBottom: true);
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
