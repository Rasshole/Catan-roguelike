using System;
using System.IO;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Save;
using UnityEngine;

namespace CatanRoguelike.Game
{
    public static class SaveGameFile
    {
        public const string DefaultFileName = SaveGameSlotStore.LegacySlot0FileName;

        public static string DefaultPath => GetSlotPath(0);

        public static string GetSlotPath(int slotIndex) =>
            SaveGameSlotStore.GetSlotPath(Application.persistentDataPath, slotIndex);

        public static void Save(GameController controller, int slotIndex = 0)
        {
            var json = SaveGame.Serialize(controller, SaveWriteOptions.Manual);
            SaveGameSlotStore.WriteSlot(Application.persistentDataPath, slotIndex, json);
        }

        public static void Autosave(GameController controller)
        {
            var json = SaveGame.Serialize(controller, SaveWriteOptions.Autosave(DateTime.UtcNow));
            SaveGameSlotStore.WriteSlot(Application.persistentDataPath, SaveGameSlotStore.AutosaveSlotIndex, json);
        }

        public static bool TryLoad(out GameController controller, int slotIndex = 0)
        {
            if (!SaveGameSlotStore.TryReadSlot(Application.persistentDataPath, slotIndex, out var json))
            {
                controller = null;
                return false;
            }

            controller = SaveGame.LoadGame(json);
            return true;
        }

        public static DateTime? LastAutosaveUtc =>
            SaveGameSlotStore.GetLastAutosaveUtc(Application.persistentDataPath);
    }
}
