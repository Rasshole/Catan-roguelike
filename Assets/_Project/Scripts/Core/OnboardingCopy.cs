using CatanRoguelike.Core.Turn;

namespace CatanRoguelike.Core
{
    /// <summary>
    /// Pure IMGUI onboarding strings keyed by run phase — testable without Unity UI.
    /// </summary>
    public static class OnboardingCopy
    {
        public enum OnboardingBeat
        {
            None,
            MapSelect,
            LeaderSelect,
            DraftSelect,
            Setup,
            FirstNight,
            FirstDay,
            GameOver
        }

        public readonly struct Context
        {
            public GamePhase Phase { get; }
            public int DayNumber { get; }
            public bool TipsEnabled { get; }
            public bool HasWinner { get; }
            public int StarsEarnedThisRun { get; }

            public Context(
                GamePhase phase,
                int dayNumber = 0,
                bool tipsEnabled = true,
                bool hasWinner = false,
                int starsEarnedThisRun = 0)
            {
                Phase = phase;
                DayNumber = dayNumber;
                TipsEnabled = tipsEnabled;
                HasWinner = hasWinner;
                StarsEarnedThisRun = starsEarnedThisRun;
            }
        }

        public static OnboardingBeat ResolveBeat(GamePhase phase, int dayNumber, bool hasWinner)
        {
            if (hasWinner)
                return OnboardingBeat.GameOver;

            if (phase == GamePhase.RunSelectMap)
                return OnboardingBeat.MapSelect;
            if (phase == GamePhase.RunSelectLeader)
                return OnboardingBeat.LeaderSelect;
            if (phase == GamePhase.RunSelectDraft)
                return OnboardingBeat.DraftSelect;
            if (phase <= GamePhase.SetupPlayerRoad2)
                return OnboardingBeat.Setup;

            if (dayNumber == 1 && IsNightPhase(phase))
                return OnboardingBeat.FirstNight;
            if (dayNumber == 1 && phase == GamePhase.DayPlayerActions)
                return OnboardingBeat.FirstDay;

            return OnboardingBeat.None;
        }

        public static string ForPhase(OnboardingBeat beat)
        {
            return beat switch
            {
                OnboardingBeat.MapSelect =>
                    "Small map grows in later acts. Medium and Large unlock with stars.",
                OnboardingBeat.LeaderSelect =>
                    "Each leader changes shop, roads, or combat — pick one for this run.",
                OnboardingBeat.DraftSelect =>
                    "Draft unique buildings — they tweak night rolls and settlement bonuses.",
                OnboardingBeat.Setup =>
                    "Place 2 settlements and roads — your 2nd settlement grants adjacent resources.",
                OnboardingBeat.FirstNight =>
                    "Night: preview tomorrow's dice and weather, play one card or skip.",
                OnboardingBeat.FirstDay =>
                    "Shop, build, move the robber, then End Day — first to 10 VP wins.",
                OnboardingBeat.GameOver => null,
                _ => null
            };
        }

        public static string ForGameOver(int starsEarned)
        {
            if (starsEarned > 0)
                return $"+{starsEarned} stars earned — unlocks and stars persist between runs.";
            return "Earn stars from VP and wins — unlocks persist between runs.";
        }

        public const string FirstNightHybridHint =
            "Hybrid yield: hex pays when 2d6 matches its token AND weather roll > 0; dice match alone = min 1.";

        public static bool TryGetPhaseBanner(Context ctx, out string line)
        {
            line = null;
            if (!ctx.TipsEnabled)
                return false;

            var beat = ResolveBeat(ctx.Phase, ctx.DayNumber, ctx.HasWinner);
            if (beat == OnboardingBeat.GameOver)
            {
                line = ForGameOver(ctx.StarsEarnedThisRun);
                return true;
            }

            line = ForPhase(beat);
            return !string.IsNullOrEmpty(line);
        }

        public static bool TryGetFirstNightHybridHint(Context ctx, out string line)
        {
            line = null;
            if (!ctx.TipsEnabled)
                return false;

            if (ResolveBeat(ctx.Phase, ctx.DayNumber, ctx.HasWinner) != OnboardingBeat.FirstNight)
                return false;

            line = FirstNightHybridHint;
            return true;
        }

        private static bool IsNightPhase(GamePhase phase) =>
            phase == GamePhase.NightPlayCard
            || phase == GamePhase.NightRoll
            || phase == GamePhase.NightAiPlan;
    }
}
