using System;

namespace CatanRoguelike.Core.Match
{
    /// <summary>
    /// v134 (DECIDED): 1.5-minute turns. Each player starts with 1 "extra turn time" charge, activated manually
    /// for +90 s on one turn, and gains +1 charge every 5 rounds. In the last 5 seconds the HUD number pulses.
    /// Pure time math only; charges live on <see cref="MatchSeatState"/> and are spent through a command.
    /// </summary>
    public sealed class TurnClock
    {
        public const float TurnSeconds = 90f;
        public const float ExtraTimeSeconds = 90f;
        public const float PulseThresholdSeconds = 5f;
        public const int StartingCharges = 1;
        public const int RoundsPerNewCharge = 5;

        public float Elapsed { get; private set; }
        public float Budget { get; private set; } = TurnSeconds;
        public bool Paused { get; set; }

        public float Remaining => Math.Max(0f, Budget - Elapsed);
        public bool Expired => Remaining <= 0f;

        /// <summary>True in the last 5 seconds: the HUD grows/pulses the number so you know to use a charge.</summary>
        public bool ShouldPulse => !Expired && Remaining <= PulseThresholdSeconds;

        /// <summary>Whole seconds for display, rounded up (so "1" shows until the very end).</summary>
        public int DisplaySeconds => (int)Math.Ceiling(Remaining);

        public void StartTurn()
        {
            Elapsed = 0f;
            Budget = TurnSeconds;
            Paused = false;
        }

        public void Tick(float deltaSeconds)
        {
            if (Paused || deltaSeconds <= 0f)
                return;
            Elapsed += deltaSeconds;
        }

        public void AddExtraTime() => Budget += ExtraTimeSeconds;

        /// <summary>Scale 1 normally; in the last 5 s pulses between ~1.15 and ~1.6 (grows as time runs out).</summary>
        public float PulseScale(float timeSeconds)
        {
            if (!ShouldPulse)
                return 1f;
            float urgency = 1f - Remaining / PulseThresholdSeconds;
            float wave = 0.5f + 0.5f * (float)Math.Sin(timeSeconds * Math.PI * 4.0);
            return 1.15f + 0.25f * urgency + 0.2f * wave;
        }

        /// <summary>Charges a player has after <paramref name="completedRounds"/> full rounds if none were spent.</summary>
        public static int ChargesEarned(int completedRounds) =>
            StartingCharges + Math.Max(0, completedRounds) / RoundsPerNewCharge;
    }
}
