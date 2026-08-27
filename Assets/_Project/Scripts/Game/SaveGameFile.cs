using System.IO;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Save;
using UnityEngine;

namespace CatanRoguelike.Game
{
    public static class SaveGameFile
    {
        public const string DefaultFileName = "save.json";

        public static string DefaultPath => Path.Combine(Application.persistentDataPath, DefaultFileName);

        public static void Save(GameController controller)
        {
            File.WriteAllText(DefaultPath, SaveGame.Serialize(controller));
        }

        public static bool TryLoad(out GameController controller)
        {
            if (!File.Exists(DefaultPath))
            {
                controller = null;
                return false;
            }

            controller = SaveGame.LoadGame(File.ReadAllText(DefaultPath));
            return true;
        }
    }
}
