#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Buildings;
using CatanRoguelike.Core.Cards;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Events;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Progression;
using CatanRoguelike.Core.Turn;
using CatanRoguelike.Core.Victory;
using CatanRoguelike.Game;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace CatanRoguelike.Editor
{
    public static class DebugHooks
    {
        private const string Menu = "Catan Roguelike/Debug/";

        [MenuItem(Menu + "Open Debug Panel %#&p")]
        public static void OpenPanel() => DebugPanelWindow.ShowWindow();

        [Shortcut("Catan Roguelike/Debug/Open Panel", KeyCode.P, ShortcutModifiers.Alt | ShortcutModifiers.Shift)]
        private static void OpenPanelShortcut() => OpenPanel();

        [MenuItem(Menu + "Fast-Forward 1 Day %#&f")]
        public static void FastForwardOne() => FastForwardDays(1);

        [Shortcut("Catan Roguelike/Debug/Fast-Forward 1 Day", KeyCode.F, ShortcutModifiers.Alt | ShortcutModifiers.Shift)]
        private static void FastForwardOneShortcut() => FastForwardDays(1);

        [MenuItem(Menu + "Fast-Forward 5 Days")]
        public static void FastForwardFive() => FastForwardDays(5);

        [MenuItem(Menu + "Give 50 of Each Resource %#&r")]
        public static void GiveResources()
        {
            if (!TryGetController(out var game)) return;
            var inv = game.State.PlayerInventory;
            inv.Add(ResourceType.Wood, 50);
            inv.Add(ResourceType.Brick, 50);
            inv.Add(ResourceType.Wheat, 50);
            inv.Add(ResourceType.Sheep, 50);
            inv.Add(ResourceType.Stone, 50);
            game.State.PlayerInventory = inv;
            game.State.StatusMessage = "Debug: +50 of each resource.";
            game.NotifyChanged();
        }

        [MenuItem(Menu + "Set Player VP to 9")]
        public static void SetVp9() => SetPlayerVictoryPoints(9);

        [MenuItem(Menu + "Force Win")]
        public static void ForceWin()
        {
            if (!TryGetController(out var game)) return;
            SetPlayerVictoryPoints(BalanceConfig.VictoryPointGoal);
            game.State.Winner = PlayerId.Human;
            game.State.Phase = GamePhase.GameOver;
            game.State.StatusMessage = "Debug: forced win.";
            game.NotifyChanged();
        }

        [MenuItem(Menu + "Force Rolls/All 1s")]
        public static void ForceRollsOnes() => SetAllRolls(1);

        [MenuItem(Menu + "Force Rolls/All 2s")]
        public static void ForceRollsTwos() => SetAllRolls(2);

        [MenuItem(Menu + "Force Card/Draw Knight")]
        public static void DrawKnight() => DrawCard(CardId.Knight);

        [MenuItem(Menu + "Force Card/Draw Harbor Charter")]
        public static void DrawHarborCharter() => DrawCard(CardId.HarborCharter);

        [MenuItem(Menu + "Force Card/Draw Year of Plenty")]
        public static void DrawYearOfPlenty() => DrawCard(CardId.YearOfPlenty);

        [MenuItem(Menu + "Force Event/Storm")]
        public static void ForceStorm() => ForceEvent(EventId.Storm);

        [MenuItem(Menu + "Force Event/Famine")]
        public static void ForceFamine() => ForceEvent(EventId.Famine);

        [MenuItem(Menu + "Force Event/Gold Rush")]
        public static void ForceGoldRush() => ForceEvent(EventId.GoldRush);

        [MenuItem(Menu + "Force Event/Market Day")]
        public static void ForceMarketDay() => ForceEvent(EventId.MarketDay);

        [MenuItem(Menu + "Force Event/Good Harvest")]
        public static void ForceGoodHarvest() => ForceEvent(EventId.GoodHarvest);

        [MenuItem(Menu + "Force Event/Bandit Raid")]
        public static void ForceBanditRaid() => ForceEvent(EventId.BanditRaid);

        [MenuItem(Menu + "Skip Night Card")]
        public static void SkipNight()
        {
            if (!TryGetController(out var game)) return;
            EnsureTodayRolls(game);
            if (game.State.Phase == GamePhase.NightPlayCard)
                game.SkipNightCard();
        }

        [MenuItem(Menu + "Auto-Play One Day")]
        public static void AutoPlayOneDay() => FastForwardDays(1);

        [MenuItem(Menu + "Replay Seed 42")]
        public static void Replay42() => ReplaySeed(42);

        [MenuItem(Menu + "Fast-Forward 1 Day %#&f", true)]
        [MenuItem(Menu + "Fast-Forward 5 Days", true)]
        [MenuItem(Menu + "Give 50 of Each Resource %#&r", true)]
        [MenuItem(Menu + "Set Player VP to 9", true)]
        [MenuItem(Menu + "Force Win", true)]
        [MenuItem(Menu + "Force Rolls/All 1s", true)]
        [MenuItem(Menu + "Force Rolls/All 2s", true)]
        [MenuItem(Menu + "Force Card/Draw Knight", true)]
        [MenuItem(Menu + "Force Card/Draw Harbor Charter", true)]
        [MenuItem(Menu + "Force Card/Draw Year of Plenty", true)]
        [MenuItem(Menu + "Force Event/Storm", true)]
        [MenuItem(Menu + "Force Event/Famine", true)]
        [MenuItem(Menu + "Force Event/Gold Rush", true)]
        [MenuItem(Menu + "Force Event/Market Day", true)]
        [MenuItem(Menu + "Force Event/Good Harvest", true)]
        [MenuItem(Menu + "Force Event/Bandit Raid", true)]
        [MenuItem(Menu + "Skip Night Card", true)]
        [MenuItem(Menu + "Auto-Play One Day", true)]
        [MenuItem(Menu + "Replay Seed 42", true)]
        public static bool ValidatePlayMode() => Application.isPlaying;

        public static void FastForwardDays(int days)
        {
            if (!TryGetController(out var game)) return;
            int guard = days * 8 + 8;
            int advanced = 0;
            while (advanced < days && guard-- > 0 && game.State.Phase != GamePhase.GameOver)
            {
                int dayBefore = game.State.Board.DayNumber;
                StepOnce(game);
                if (game.State.Board.DayNumber > dayBefore)
                    advanced++;
            }
            game.State.StatusMessage = $"Debug: fast-forwarded {advanced} day(s).";
            game.NotifyChanged();
        }

        public static void ReplaySeed(int seed)
        {
            if (!TryGetManager(out var manager)) return;
            EditorPrefs.SetInt("CatanRoguelike.DebugSeed", seed);
            manager.DebugRestart(seed);
            Debug.Log($"Catan Roguelike: replay seed {seed}");
        }

        public static void SetPlayerVictoryPoints(int target)
        {
            if (!TryGetController(out var game)) return;
            int boardVp = VictoryCalculator.CalculateVictoryPoints(game.State.Board, PlayerId.Human);
            game.State.PlayerBonusVictoryPoints = Math.Max(0, target - boardVp);
            VictoryCalculator.RefreshVictoryPoints(game.State);
            game.State.StatusMessage = $"Debug: player VP set to {game.State.PlayerVictoryPoints}.";
            game.NotifyChanged();
        }

        public static bool TryGetController(out GameController game)
        {
            game = null;
            if (!Application.isPlaying)
            {
                Debug.LogWarning("Catan Roguelike debug hooks require Play Mode.");
                return false;
            }

            if (!TryGetManager(out var manager) || manager.Controller == null)
            {
                Debug.LogWarning("Catan Roguelike: no GameManager/Controller in the loaded scene.");
                return false;
            }

            game = manager.Controller;
            return true;
        }

        public static bool TryGetManager(out GameManager manager)
        {
            manager = UnityEngine.Object.FindFirstObjectByType<GameManager>();
            return manager != null;
        }

        private static void SetAllRolls(int value)
        {
            if (!TryGetController(out var game)) return;
            value = Math.Clamp(value, 0, 2);
            EnsureRollDict(game.State.TomorrowRolls, value);
            EnsureRollDict(game.State.TodayRolls, value);
            game.State.StatusMessage = $"Debug: all rolls set to {value}.";
            game.NotifyChanged();
        }

        private static void EnsureRollDict(Dictionary<ResourceType, int> rolls, int value)
        {
            foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
                rolls[type] = value;
        }

        private static void DrawCard(CardId card)
        {
            if (!TryGetController(out var game)) return;
            if (game.State.PlayerHand.Count >= BalanceConfig.MaxHandSize)
                game.State.PlayerHand.RemoveAt(0);
            game.State.PlayerHand.Add(card);
            game.State.StatusMessage = $"Debug: drew {CardLibrary.Get(card).Name}.";
            game.NotifyChanged();
        }

        private static void ForceEvent(EventId eventId)
        {
            if (!TryGetController(out var game)) return;
            game.Events.ApplyEvent(game.State, eventId);
            game.NotifyChanged();
        }

        private static void EnsureTodayRolls(GameController game)
        {
            if (game.State.TodayRolls.Count == 0 && game.State.TomorrowRolls.Count > 0)
                game.State.TodayRolls = new Dictionary<ResourceType, int>(game.State.TomorrowRolls);
        }

        /// <summary>
        /// One safe step. Avoids PlaceSettlement / EndPlayerDay / AI placement —
        /// those stall in VertexGraph.VertexDistance. Setup is skipped into night.
        /// </summary>
        private static void StepOnce(GameController game)
        {
            var phase = game.State.Phase;
            if (phase == GamePhase.RunSelectMap)
            {
                game.SelectMap(game.State.MapSize);
                return;
            }

            if (phase == GamePhase.RunSelectLeader)
            {
                game.SelectLeader(game.State.Leader);
                return;
            }

            if (phase == GamePhase.RunSelectDraft)
            {
                if (game.State.DraftedUniques.Count < RunProgression.DraftPickCount)
                {
                    foreach (UniqueBuildingId id in Enum.GetValues(typeof(UniqueBuildingId)))
                    {
                        if (!game.State.DraftedUniques.Contains(id))
                            game.ToggleDraftUnique(id);
                        if (game.State.DraftedUniques.Count >= RunProgression.DraftPickCount)
                            break;
                    }
                }
                game.ConfirmRunSetup();
                return;
            }

            if (phase <= GamePhase.SetupPlayerRoad2)
            {
                game.BeginNight();
                return;
            }

            if (phase == GamePhase.NightPlayCard)
            {
                EnsureTodayRolls(game);
                game.SkipNightCard();
                return;
            }

            if (phase == GamePhase.DayPlayerActions)
            {
                SafeEndDay(game);
                return;
            }

            if (phase == GamePhase.LevelUpChoice)
            {
                if (game.State.PendingLevelUpChoices.Count > 0)
                    game.ChooseLevelUpPerk(game.State.PendingLevelUpChoices[0]);
                else
                    SafeEndDay(game);
            }
        }

        private static void SafeEndDay(GameController game)
        {
            var state = game.State;
            game.Events.ClearDailyEventEffects(state);
            state.Board.DisabledRoads.Clear();

            if (state.AiEmbargoDaysLeft > 0)
            {
                state.AiEmbargoDaysLeft--;
                if (state.AiEmbargoDaysLeft <= 0) state.AiShopEmbargo = null;
            }
            else
                state.AiShopEmbargo = null;

            state.Board.DayNumber++;

            if (RunProgression.ShouldOfferLevelUp(state))
            {
                state.PendingLevelUpChoices.Clear();
                state.PendingLevelUpChoices.AddRange(
                    RunProgression.GenerateLevelUpChoices(state, new System.Random(state.Board.DayNumber + 17)));
                if (state.PendingLevelUpChoices.Count > 0)
                {
                    state.Phase = GamePhase.LevelUpChoice;
                    state.StatusMessage = "Level up! Choose a perk.";
                    game.NotifyChanged();
                    return;
                }
            }

            var winner = VictoryCalculator.CheckWinner(state);
            if (winner.HasValue)
            {
                state.Winner = winner;
                state.Phase = GamePhase.GameOver;
                state.StatusMessage = winner == PlayerId.Human ? "You win!" : "AI wins!";
                game.NotifyChanged();
                return;
            }

            game.BeginNight();
        }
    }

    public sealed class DebugPanelWindow : EditorWindow
    {
        private int _seed = 42;
        private int _days = 5;
        private int _vp = 9;

        
        public static void ShowWindow()
        {
            var window = GetWindow<DebugPanelWindow>("Catan Debug");
            window.minSize = new Vector2(280, 360);
            window._seed = EditorPrefs.GetInt("CatanRoguelike.DebugSeed", 42);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Catan Roguelike — Debug", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Play Mode only. Fast-forward skips AI placement (VertexDistance stall). Player builds are unaffected (#if UNITY_EDITOR + Editor asmdef).",
                MessageType.Info);

            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Seed + replay", EditorStyles.boldLabel);
                _seed = EditorGUILayout.IntField("Seed", _seed);
                if (GUILayout.Button("Replay this seed"))
                    DebugHooks.ReplaySeed(_seed);

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Time", EditorStyles.boldLabel);
                _days = EditorGUILayout.IntSlider("Days", _days, 1, 20);
                if (GUILayout.Button($"Fast-forward {_days} day(s)"))
                    DebugHooks.FastForwardDays(_days);
                if (GUILayout.Button("Auto-play 1 day"))
                    DebugHooks.FastForwardDays(1);

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Resources / VP", EditorStyles.boldLabel);
                if (GUILayout.Button("Give 50 of each resource"))
                    DebugHooks.GiveResources();
                _vp = EditorGUILayout.IntSlider("Target VP", _vp, 0, 12);
                if (GUILayout.Button($"Set player VP to {_vp}"))
                    DebugHooks.SetPlayerVictoryPoints(_vp);
                if (GUILayout.Button("Force win"))
                    DebugHooks.ForceWin();

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Force", EditorStyles.boldLabel);
                if (GUILayout.Button("Rolls = 1"))
                    DebugHooks.ForceRollsOnes();
                if (GUILayout.Button("Draw Knight"))
                    DebugHooks.DrawKnight();
                if (GUILayout.Button("Event: Storm"))
                    DebugHooks.ForceStorm();
                if (GUILayout.Button("Skip night card"))
                    DebugHooks.SkipNight();
            }

            if (Application.isPlaying && DebugHooks.TryGetController(out var game))
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField(
                    $"Day {game.State.Board.DayNumber}  {game.State.Phase}  VP {game.State.PlayerVictoryPoints}/{game.State.AiVictoryPoints}");
            }
        }
    }
}
#endif
