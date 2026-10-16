using System.Collections.Generic;
using CatanRoguelike.Core;
using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>Kenney Board Game Icons (CC0) loaded from Resources/Art/Icons.</summary>
    public static class ColonistKenneyResourceIcons
    {
        private const string IconRoot = "Art/Icons";
        private static readonly Dictionary<ResourceType, Texture2D> Cache = new();

        public static Texture2D TryGet(ResourceType resource)
        {
            if (Cache.TryGetValue(resource, out var cached) && cached != null)
                return cached;

            string file = ResourceFileName(resource);
            if (file == null)
                return null;

            var tex = Resources.Load<Texture2D>($"{IconRoot}/{file}");
            if (tex != null)
                Cache[resource] = tex;
            return tex;
        }

        private static string ResourceFileName(ResourceType resource) => resource switch
        {
            ResourceType.Wood => "Wood",
            ResourceType.Brick => "Brick",
            ResourceType.Wheat => "Wheat",
            ResourceType.Sheep => "Sheep",
            ResourceType.Stone => "Stone",
            _ => null
        };
    }
}
