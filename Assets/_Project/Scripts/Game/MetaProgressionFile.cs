using System.IO;
using CatanRoguelike.Core.Progression;
using UnityEngine;

namespace CatanRoguelike.Game
{
    public static class MetaProgressionFile
    {
        public const string DefaultFileName = "meta.json";

        public static string DefaultPath => Path.Combine(Application.persistentDataPath, DefaultFileName);

        public static MetaProgression LoadOrCreate()
        {
            if (!File.Exists(DefaultPath))
                return MetaProgression.CreateFresh();

            return MetaSave.Load(File.ReadAllText(DefaultPath));
        }

        public static void Save(MetaProgression progression)
        {
            File.WriteAllText(DefaultPath, MetaSave.Serialize(progression));
        }
    }
}
