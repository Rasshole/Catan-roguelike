using System.Collections.Generic;
using CatanRoguelike.Core;

namespace CatanRoguelike.Core.UI
{
    public readonly struct GameLogEntry
    {
        public readonly int DayNumber;
        public readonly string Message;

        public GameLogEntry(int dayNumber, string message)
        {
            DayNumber = dayNumber;
            Message = message;
        }
    }

    public static class GameLog
    {
        public const int MaxEntries = 64;

        public static void Append(GameState state, string message)
        {
            if (state == null || string.IsNullOrWhiteSpace(message))
                return;

            state.Log.Add(new GameLogEntry(state.Board.DayNumber, message.Trim()));
            if (state.Log.Count > MaxEntries)
                state.Log.RemoveAt(0);
        }

        public static string FormatEntry(GameLogEntry entry) => $"D{entry.DayNumber}: {entry.Message}";
    }
}
