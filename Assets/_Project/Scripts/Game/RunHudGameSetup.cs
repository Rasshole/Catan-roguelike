using System;
using System.Linq;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Match;
using UnityEngine;
using UnityEngine.UI;

namespace CatanRoguelike.Game
{
    /// <summary>
    /// v143 front door. Pressing Play lands on a painted parchment main menu (title, a big "Start game", small
    /// Single-player run / Settings / Quit). "Start game" opens the setup screen: ANY map (every <see cref="MapSize"/>,
    /// no star unlocks needed), 2–5 seats as Human/AI, Fishermen of Catan, victory points, turn timer on/off + length,
    /// party rules with event cards and shenanigans toggles. "Leave match" always returns here.
    /// </summary>
    public sealed partial class RunHudController
    {
        public const string MainMenuStartName = "MainMenuStart";
        public const string MainMenuSoloName = "MainMenuSolo";
        public const string MainMenuSettingsName = "MainMenuSettings";
        public const string MainMenuQuitName = "MainMenuQuit";
        public const string SetupMapPrefix = "SetupMap_";

        public static readonly int[] TurnLengthSteps = { 30, 45, 60, 90, 120, 150, 180, 240, 300 };
        public const int MinSetupVictoryPoints = 3;
        public const int MaxSetupVictoryPoints = 20;

        private bool _mainMenuOpen = true;
        private MapSize? _lobbyMap;
        private bool _lobbyFishermen;
        private int _lobbyVp = MatchConfig.DefaultVictoryPoints;
        private bool _lobbyTimer = true;
        private int _lobbyTurnSeconds = (int)TurnClock.TurnSeconds;
        private bool _lobbyEvents = true;
        private bool _lobbyShenanigans = true;

        public bool IsMainMenuOpenForTests => _mainMenuOpen && !_hotSeatLobbyOpen && _hotSeat == null
                                              && _controller != null
                                              && _controller.State.Phase == Core.Turn.GamePhase.RunSelectMap;

        /// <summary>The map the setup screen will start (defaults to the recommendation for the seat count).</summary>
        public MapSize LobbyMap => _lobbyMap ?? MatchConfig.RecommendedMap(_lobbySeats.Count);

        public void SetLobbyMapForTests(MapSize size)
        {
            _lobbyMap = size;
            RebuildPreGame();
        }

        public void SetLobbyRulesForTests(bool fishermen, int vp, bool timer, int turnSeconds, bool events,
            bool shenanigans)
        {
            _lobbyFishermen = fishermen;
            _lobbyVp = vp;
            _lobbyTimer = timer;
            _lobbyTurnSeconds = turnSeconds;
            _lobbyEvents = events;
            _lobbyShenanigans = shenanigans;
            RebuildPreGame();
        }

        private string HotSeatLobbyKey() =>
            _hotSeatLobbyOpen
                ? "hs:" + string.Join(",", _lobbySeats.Select(s => s?.ToString() ?? "open")) +
                  (_lobbyPartyRules ? "|party" : "") +
                  $"|{LobbyMap}|{_lobbyFishermen}|{_lobbyVp}|{_lobbyTimer}|{_lobbyTurnSeconds}|{_lobbyEvents}|{_lobbyShenanigans}"
                : "";

        private static readonly System.Random MatchSeedSource = new System.Random();

        /// <summary>v146: tests can pin the next match seed; otherwise every Start game rolls a new one.</summary>
        public int? NextMatchSeedForTests { get; set; }

        public int NextMatchSeed()
        {
            if (NextMatchSeedForTests is int pinned)
                return pinned;
            lock (MatchSeedSource)
                return MatchSeedSource.Next(1, int.MaxValue);
        }

        public MatchConfig BuildLobbyConfig(int seed)
        {
            var config = new MatchConfig
            {
                Seed = seed,
                MapSize = LobbyMap,
                PartyRules = _lobbyPartyRules,
                VictoryPoints = Mathf.Clamp(_lobbyVp, MinSetupVictoryPoints, MaxSetupVictoryPoints),
                TurnTimer = _lobbyTimer,
                TurnSeconds = Mathf.Clamp(_lobbyTurnSeconds, MatchConfig.MinTurnSeconds, MatchConfig.MaxTurnSeconds),
                EventCards = _lobbyEvents,
                Shenanigans = _lobbyShenanigans,
                Fishermen = _lobbyFishermen
            };
            for (int i = 0; i < _lobbySeats.Count; i++)
            {
                var kind = _lobbySeats[i];
                config.Seats.Add(kind.HasValue
                    ? new SeatConfig(kind.Value, kind == SeatKind.Human ? $"Player {i + 1}" : $"AI {i + 1}")
                    : null);
            }

            return config;
        }

        private static string ShortMapName(MapSize size)
        {
            string name = MapPresets.GetDisplayName(size);
            int dash = name.IndexOf(" — ", StringComparison.Ordinal);
            return dash > 0 ? name.Substring(0, dash) : name;
        }

        private static string FormatTurnLength(int seconds) =>
            seconds % 60 == 0 ? $"{seconds / 60}:00" : $"{seconds / 60}:{seconds % 60:00}";

        // ---------- main menu ----------

        private void BuildMainMenu(RectTransform root)
        {
            var card = CreateParchment(root, "MainMenu", new Vector2(0f, 8f), new Vector2(1000f, 720f));

            BuildHexCluster(card, MapSize.Large, new Vector2(0f, -150f), new Vector2(300f, 170f), true);
            var title = RunHudUiFactory.CreateText(card, "GameTitle", "Catan Roguelike", 78, TextAnchor.MiddleCenter,
                ColonistUiKit.Ink, FontStyle.Bold);
            SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -300f), new Vector2(900f, 96f));
            var shadow = title.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(1f, 0.95f, 0.8f, 0.6f);
            shadow.effectDistance = new Vector2(2f, -2f);
            var sub = RunHudUiFactory.CreateText(card, "Tagline",
                "Settle the island, trade by lamplight, and outwit your friends.", 22, TextAnchor.MiddleCenter,
                ColonistUiKit.InkMuted, FontStyle.Italic);
            SetRect(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -364f), new Vector2(860f, 32f));

            var start = AddMenuButton(card, MainMenuStartName, "Start game", new Vector2(0f, -470f),
                new Vector2(470f, 104f), true, 38, () =>
                {
                    _mainMenuOpen = false;
                    _hotSeatLobbyOpen = true;
                    _unlockPanelOpen = false;
                    RebuildPreGame();
                });
            start.transform.localScale = Vector3.one;

            AddMenuButton(card, MainMenuSoloName, "Single-player run", new Vector2(-250f, -596f),
                new Vector2(230f, 54f), false, 19, () =>
                {
                    _mainMenuOpen = false;
                    RebuildPreGame();
                });
            AddMenuButton(card, MainMenuSettingsName, "Settings & volume", new Vector2(0f, -596f),
                new Vector2(230f, 54f), false, 19, () =>
                {
                    var gm = GetComponent<GameManager>();
                    if (gm == null)
                        gm = FindFirstObjectByType<GameManager>();
                    if (gm != null && gm.SettingsMenu != null)
                        gm.SettingsMenu.Toggle();
                });
            AddMenuButton(card, MainMenuQuitName, "Quit", new Vector2(250f, -596f), new Vector2(230f, 54f), false, 19,
                QuitGame);
        }

        private static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ---------- setup screen ----------

        private void BuildGameSetup(RectTransform root)
        {
            var card = CreateParchment(root, "GameSetup", new Vector2(0f, 0f), new Vector2(1600f, 980f));
            card.name = "HotSeatLobby";

            var kicker = RunHudUiFactory.CreateText(card, "Kicker", "NEW GAME  ·  PASS & PLAY OR AGAINST AI", 16,
                TextAnchor.MiddleCenter, ColonistUiKit.InkGold, FontStyle.Bold);
            SetRect(kicker.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -58f), new Vector2(900f, 24f));
            var title = RunHudUiFactory.CreateText(card, "Title", "Game setup", 44, TextAnchor.MiddleCenter,
                ColonistUiKit.Ink, FontStyle.Bold);
            SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(900f, 54f));

            // Map row: every map size, all unlocked.
            SectionLabel(card, "MapHeader", "MAP", new Vector2(0f, -152f));
            var sizes = (MapSize[])Enum.GetValues(typeof(MapSize));
            // v145: six maps (Islands added) still fit the 1600-wide card.
            float tileW = sizes.Length > 5 ? 228f : 250f, gap = sizes.Length > 5 ? 20f : 22f;
            float x0 = -(sizes.Length - 1) * (tileW + gap) / 2f;
            for (int i = 0; i < sizes.Length; i++)
            {
                var size = sizes[i];
                bool selected = LobbyMap == size;
                var tile = AddMenuButton(card, SetupMapPrefix + size, "", new Vector2(x0 + i * (tileW + gap), -268f),
                    new Vector2(tileW, 196f), selected, 18, () =>
                    {
                        _lobbyMap = size;
                        RebuildPreGame();
                    });
                var rect = (RectTransform)tile.transform;
                BuildHexCluster(rect, size, new Vector2(0f, -66f), new Vector2(190f, 104f), true);
                var name = RunHudUiFactory.CreateText(rect, "Name", ShortMapName(size), 22,
                    TextAnchor.MiddleCenter, selected ? ColonistUiKit.Cream : ColonistUiKit.Ink, FontStyle.Bold);
                SetRect(name.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 46f), new Vector2(tileW - 16f, 28f));
                bool recommended = size == MatchConfig.RecommendedMap(_lobbySeats.Count);
                var info = RunHudUiFactory.CreateText(rect, "Info",
                    $"{MapPresets.GetHexCount(size)} hexes{(recommended ? "  ·  suggested" : "")}", 15,
                    TextAnchor.MiddleCenter, selected ? ColonistUiKit.Cream : ColonistUiKit.InkMuted, FontStyle.Italic);
                SetRect(info.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(tileW - 16f, 22f));
            }

            // Left column: seats.
            const float leftX = -390f, rightX = 390f, top = -414f;
            SectionLabel(card, "SeatsHeader", "PLAYERS", new Vector2(leftX, top));
            var countLabel = RunHudUiFactory.CreateText(card, "SeatCount", $"{_lobbySeats.Count} seats", 24,
                TextAnchor.MiddleCenter, ColonistUiKit.Ink, FontStyle.Bold);
            SetRect(countLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(leftX, top - 50f), new Vector2(200f, 40f));
            AddLobbyButton(card, "SeatMinus", "−", new Vector2(leftX - 150f, top - 50f), new Vector2(64f, 46f),
                _lobbySeats.Count > MatchConfig.MinSeats, false, () =>
                {
                    _lobbySeats.RemoveAt(_lobbySeats.Count - 1);
                    RebuildPreGame();
                });
            AddLobbyButton(card, "SeatPlus", "+", new Vector2(leftX + 150f, top - 50f), new Vector2(64f, 46f),
                _lobbySeats.Count < MatchConfig.MaxSeats, false, () =>
                {
                    _lobbySeats.Add(null);
                    RebuildPreGame();
                });
            for (int i = 0; i < _lobbySeats.Count; i++)
            {
                int seat = i;
                float y = top - 112f - i * 62f;
                var disc = CreateDisc(card, "SeatDisc_" + i, PlayerPieceVisuals.ColorForSeat(i), 46f);
                SetRect(disc.rectTransform, new Vector2(0.5f, 1f), new Vector2(leftX - 230f, y), new Vector2(46f, 46f));
                var num = RunHudUiFactory.CreateText(disc.rectTransform, "N", (i + 1).ToString(), 22,
                    TextAnchor.MiddleCenter, SeatInk(i), FontStyle.Bold);
                Stretch(num.rectTransform);
                var label = RunHudUiFactory.CreateText(card, "SeatLabel_" + i, $"Seat {i + 1}", 22,
                    TextAnchor.MiddleLeft, ColonistUiKit.Ink, FontStyle.Bold);
                SetRect(label.rectTransform, new Vector2(0.5f, 1f), new Vector2(leftX - 110f, y), new Vector2(160f, 40f));
                var kind = _lobbySeats[i];
                string text = kind == SeatKind.Human ? "Human" : kind == SeatKind.Ai ? "AI player" : "Open seat";
                AddLobbyButton(card, "SeatKind_" + i, text, new Vector2(leftX + 130f, y), new Vector2(250f, 50f), true,
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

            // Right column: rules.
            SectionLabel(card, "RulesHeader", "RULES", new Vector2(rightX, top));
            float row = top - 50f;
            ToggleRow(card, "Fishermen", "Fishermen of Catan", _lobbyFishermen, true, new Vector2(rightX, row), () =>
                _lobbyFishermen = !_lobbyFishermen);
            row -= 62f;
            StepperRow(card, "VictoryPoints", "Victory points", _lobbyVp.ToString(), new Vector2(rightX, row),
                _lobbyVp > MinSetupVictoryPoints, _lobbyVp < MaxSetupVictoryPoints, d => _lobbyVp += d, null);
            row -= 62f;
            int step = Array.IndexOf(TurnLengthSteps, _lobbyTurnSeconds);
            if (step < 0)
                step = Array.IndexOf(TurnLengthSteps, 90);
            StepperRow(card, "TurnTimer", "Turn timer", _lobbyTimer ? FormatTurnLength(_lobbyTurnSeconds) : "Off",
                new Vector2(rightX, row), _lobbyTimer && step > 0, _lobbyTimer && step < TurnLengthSteps.Length - 1,
                d => _lobbyTurnSeconds = TurnLengthSteps[Mathf.Clamp(step + d, 0, TurnLengthSteps.Length - 1)],
                () => _lobbyTimer = !_lobbyTimer);
            row -= 62f;
            ToggleRow(card, "PartyRules", "Civs & secret quests", _lobbyPartyRules, true, new Vector2(rightX, row), () =>
                _lobbyPartyRules = !_lobbyPartyRules);
            row -= 62f;
            ToggleRow(card, "EventCards", "Event cards", _lobbyPartyRules && _lobbyEvents, _lobbyPartyRules,
                new Vector2(rightX, row), () => _lobbyEvents = !_lobbyEvents);
            row -= 62f;
            ToggleRow(card, "Shenanigans", "Shenanigans (sabotage, votes)", _lobbyPartyRules && _lobbyShenanigans,
                _lobbyPartyRules, new Vector2(rightX, row), () => _lobbyShenanigans = !_lobbyShenanigans);

            int humans = _lobbySeats.Count(s => s == SeatKind.Human);
            int ai = _lobbySeats.Count(s => s == SeatKind.Ai);
            int open = _lobbySeats.Count(s => s == null);
            string summary = $"{humans} human{(humans == 1 ? "" : "s")} · {ai} AI · {open} open  ·  " +
                             $"{ShortMapName(LobbyMap)} map ({MapPresets.GetHexCount(LobbyMap)} hexes)  ·  " +
                             $"first to {_lobbyVp} VP";
            var infoLine = RunHudUiFactory.CreateText(card, "Summary", summary, 19, TextAnchor.MiddleCenter,
                ColonistUiKit.InkMuted, FontStyle.Italic);
            SetRect(infoLine.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 128f), new Vector2(1200f, 28f));

            AddLobbyButton(card, "CloseLobby", "Back", new Vector2(-470f, 70f), new Vector2(200f, 58f), true, false, () =>
            {
                _hotSeatLobbyOpen = false;
                _mainMenuOpen = true;
                RebuildPreGame();
            }, anchorBottom: true);
            AddLobbyButton(card, "FillAi", "Fill open seats with AI", new Vector2(-170f, 70f), new Vector2(300f, 58f),
                open > 0, false, FillLobbyWithAi, anchorBottom: true);
            AddLobbyButton(card, "StartMatch", open > 0 ? "Fill open seats first" : "Start game",
                new Vector2(250f, 70f), new Vector2(380f, 64f), open == 0, true, () =>
                {
                    // v146: a fresh seed every match → new board, harbours, numbers and pick offers each time.
                    var config = BuildLobbyConfig(NextMatchSeed());
                    StartHotSeat(config);
                }, anchorBottom: true);
        }

        // ---------- small builders ----------

        private RectTransform CreateParchment(RectTransform root, string name, Vector2 pos, Vector2 size)
        {
            var card = CreateCardFrame(root, name, wide: true);
            var img = card.GetComponent<Image>();
            var sliced = ColonistRoguelikeArt.GetCardFrameSliced(true) ?? ColonistRoguelikeArt.GetCardFrameSliced(false);
            if (sliced != null)
            {
                img.sprite = sliced;
                img.type = Image.Type.Sliced;
                img.preserveAspect = false;
            }

            SetRect(card, new Vector2(0.5f, 0.5f), pos, size);
            return card;
        }

        private static void SectionLabel(RectTransform card, string name, string text, Vector2 pos)
        {
            var label = RunHudUiFactory.CreateText(card, name, text, 17, TextAnchor.MiddleCenter, ColonistUiKit.InkGold,
                FontStyle.Bold);
            SetRect(label.rectTransform, new Vector2(0.5f, 1f), pos, new Vector2(400f, 24f));
        }

        private static Button AddMenuButton(RectTransform parent, string name, string label, Vector2 pos, Vector2 size,
            bool green, int fontSize, Action click)
        {
            var button = RunHudUiFactory.CreateButton(parent, label, RunHudUiFactory.ButtonFill);
            button.name = name;
            SetRect((RectTransform)button.transform, new Vector2(0.5f, 1f), pos, size);
            ColonistKenneyHudArt.ApplyColonistWarmButton(button.GetComponent<Image>(), primaryGreen: green);
            var text = button.GetComponentInChildren<Text>();
            if (text != null)
            {
                text.color = green ? ColonistUiKit.Cream : ColonistUiKit.Ink;
                text.fontSize = fontSize;
                text.fontStyle = FontStyle.Bold;
            }

            button.onClick.AddListener(() => click());
            return button;
        }

        private void RowLabel(RectTransform card, string name, string text, Vector2 pos, bool enabled)
        {
            var label = RunHudUiFactory.CreateText(card, name + "Label", text, 21, TextAnchor.MiddleLeft,
                enabled ? ColonistUiKit.Ink : ColonistUiKit.InkMuted, FontStyle.Bold);
            SetRect(label.rectTransform, new Vector2(0.5f, 1f), new Vector2(pos.x - 120f, pos.y), new Vector2(340f, 40f));
        }

        private void ToggleRow(RectTransform card, string name, string text, bool on, bool enabled, Vector2 pos,
            Action toggle)
        {
            RowLabel(card, name, text, pos, enabled);
            AddLobbyButton(card, name, on ? "On" : "Off", new Vector2(pos.x + 200f, pos.y), new Vector2(180f, 50f),
                enabled, on, () =>
                {
                    toggle();
                    RebuildPreGame();
                });
        }

        private void StepperRow(RectTransform card, string name, string text, string value, Vector2 pos, bool canDown,
            bool canUp, Action<int> step, Action valueClick)
        {
            RowLabel(card, name, text, pos, true);
            AddLobbyButton(card, name + "Minus", "−", new Vector2(pos.x + 92f, pos.y), new Vector2(50f, 46f), canDown,
                false, () =>
                {
                    step(-1);
                    RebuildPreGame();
                });
            if (valueClick != null)
            {
                AddLobbyButton(card, name, value, new Vector2(pos.x + 200f, pos.y), new Vector2(150f, 50f), true,
                    value != "Off", () =>
                    {
                        valueClick();
                        RebuildPreGame();
                    });
            }
            else
            {
                var v = RunHudUiFactory.CreateText(card, name + "Value", value, 24, TextAnchor.MiddleCenter,
                    ColonistUiKit.Ink, FontStyle.Bold);
                v.name = name;
                SetRect(v.rectTransform, new Vector2(0.5f, 1f), new Vector2(pos.x + 200f, pos.y), new Vector2(150f, 44f));
            }

            AddLobbyButton(card, name + "Plus", "+", new Vector2(pos.x + 308f, pos.y), new Vector2(50f, 46f), canUp,
                false, () =>
                {
                    step(1);
                    RebuildPreGame();
                });
        }
    }
}
