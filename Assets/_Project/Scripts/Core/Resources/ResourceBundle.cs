using System;
using System.Collections.Generic;

namespace CatanRoguelike.Core
{
    [Serializable]
    public struct ResourceBundle
    {
        public int Wood;
        public int Brick;
        public int Wheat;
        public int Sheep;
        public int Stone;

        public static ResourceBundle Zero => default;

        public int this[ResourceType type] => type switch
        {
            ResourceType.Wood => Wood,
            ResourceType.Brick => Brick,
            ResourceType.Wheat => Wheat,
            ResourceType.Sheep => Sheep,
            ResourceType.Stone => Stone,
            _ => 0
        };

        public void Set(ResourceType type, int value)
        {
            switch (type)
            {
                case ResourceType.Wood: Wood = value; break;
                case ResourceType.Brick: Brick = value; break;
                case ResourceType.Wheat: Wheat = value; break;
                case ResourceType.Sheep: Sheep = value; break;
                case ResourceType.Stone: Stone = value; break;
            }
        }

        public void Add(ResourceType type, int amount)
        {
            switch (type)
            {
                case ResourceType.Wood: Wood += amount; break;
                case ResourceType.Brick: Brick += amount; break;
                case ResourceType.Wheat: Wheat += amount; break;
                case ResourceType.Sheep: Sheep += amount; break;
                case ResourceType.Stone: Stone += amount; break;
            }
        }

        public void Add(ResourceBundle other)
        {
            Wood += other.Wood;
            Brick += other.Brick;
            Wheat += other.Wheat;
            Sheep += other.Sheep;
            Stone += other.Stone;
        }

        public bool CanAfford(ResourceBundle cost)
        {
            return Wood >= cost.Wood
                && Brick >= cost.Brick
                && Wheat >= cost.Wheat
                && Sheep >= cost.Sheep
                && Stone >= cost.Stone;
        }

        public void Pay(ResourceBundle cost)
        {
            Wood -= cost.Wood;
            Brick -= cost.Brick;
            Wheat -= cost.Wheat;
            Sheep -= cost.Sheep;
            Stone -= cost.Stone;
        }

        public int Total => Wood + Brick + Wheat + Sheep + Stone;

        public IEnumerable<(ResourceType type, int amount)> EnumerateNonZero()
        {
            if (Wood > 0) yield return (ResourceType.Wood, Wood);
            if (Brick > 0) yield return (ResourceType.Brick, Brick);
            if (Wheat > 0) yield return (ResourceType.Wheat, Wheat);
            if (Sheep > 0) yield return (ResourceType.Sheep, Sheep);
            if (Stone > 0) yield return (ResourceType.Stone, Stone);
        }
    }
}
