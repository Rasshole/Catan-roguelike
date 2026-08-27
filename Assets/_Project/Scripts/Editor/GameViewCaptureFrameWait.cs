using System;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("CatanRoguelike.Tests")]

namespace CatanRoguelike.Editor
{
    /// <summary>
    /// Frame-wait helpers for batchmode Play Mode capture. Extracted for unit tests.
    /// </summary>
    internal static class GameViewCaptureFrameWait
    {
        public const double PhaseTimeoutSeconds = 120.0;
        public const double OverallTimeoutSeconds = 180.0;

        public static double ComputeDeadline(double timeSinceStartup) =>
            timeSinceStartup + PhaseTimeoutSeconds;

        public static double ComputeOverallDeadline(double timeSinceStartup) =>
            timeSinceStartup + OverallTimeoutSeconds;

        public static DateTime ComputeOverallDeadlineUtc(DateTime startedUtc) =>
            startedUtc.AddSeconds(OverallTimeoutSeconds);

        public static bool IsTimedOut(double timeSinceStartup, double deadline) =>
            timeSinceStartup > deadline;

        public static bool IsOverallTimedOutUtc(DateTime nowUtc, DateTime deadlineUtc) =>
            nowUtc > deadlineUtc;

        public static bool HasReachedTarget(int currentFrame, int targetFrame) =>
            currentFrame >= targetFrame;
    }
}
