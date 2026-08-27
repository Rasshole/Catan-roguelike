using System;
using System.Globalization;
using System.IO;

namespace CatanRoguelike.Core.Save
{
    /// <summary>
    /// Path-agnostic multi-slot file IO for run saves. Slot 0 uses legacy <c>save.json</c>; slot 1 uses <c>save_1.json</c>.
    /// </summary>
    public static class SaveGameSlotStore
    {
        public const int SlotCount = 2;
        public const int AutosaveSlotIndex = 0;

        public const string LegacySlot0FileName = "save.json";
        public const string Slot1FileName = "save_1.json";

        public static string GetSlotFileName(int slotIndex)
        {
            return slotIndex switch
            {
                0 => LegacySlot0FileName,
                1 => Slot1FileName,
                _ => throw new ArgumentOutOfRangeException(nameof(slotIndex), slotIndex, "Only slots 0 and 1 are supported.")
            };
        }

        public static string GetSlotPath(string baseDirectory, int slotIndex) =>
            Path.Combine(baseDirectory, GetSlotFileName(slotIndex));

        public static void WriteSlot(string baseDirectory, int slotIndex, string json)
        {
            Directory.CreateDirectory(baseDirectory);
            File.WriteAllText(GetSlotPath(baseDirectory, slotIndex), json);
        }

        public static bool TryReadSlot(string baseDirectory, int slotIndex, out string json)
        {
            json = null;
            var path = GetSlotPath(baseDirectory, slotIndex);
            if (!File.Exists(path))
                return false;

            json = File.ReadAllText(path);
            return true;
        }

        public static bool SlotExists(string baseDirectory, int slotIndex) =>
            File.Exists(GetSlotPath(baseDirectory, slotIndex));

        public static DateTime? GetLastAutosaveUtc(string baseDirectory)
        {
            if (!TryReadSlot(baseDirectory, AutosaveSlotIndex, out var json))
                return null;

            try
            {
                var doc = SaveGame.Parse(json);
                if (doc.IsAutosave
                    && !string.IsNullOrEmpty(doc.SavedAtUtc)
                    && DateTime.TryParse(doc.SavedAtUtc, null, DateTimeStyles.RoundtripKind, out var savedAt))
                    return savedAt.ToUniversalTime();
            }
            catch (InvalidOperationException)
            {
                // Fall through to file timestamp.
            }

            return File.GetLastWriteTimeUtc(GetSlotPath(baseDirectory, AutosaveSlotIndex));
        }
    }
}
