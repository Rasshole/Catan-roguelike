using CatanRoguelike.Core;

namespace CatanRoguelike.Core.Trade
{
    /// <summary>Classic Catan bank stock (19 of each resource).</summary>
    public sealed class BankStock
    {
        public const int DefaultPerResource = 19;

        public int Wood = DefaultPerResource;
        public int Brick = DefaultPerResource;
        public int Wheat = DefaultPerResource;
        public int Sheep = DefaultPerResource;
        public int Stone = DefaultPerResource;

        public int Get(ResourceType type) => type switch
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

        public bool CanWithdraw(ResourceType type, int amount) => Get(type) >= amount;

        public bool TryWithdraw(ResourceType type, int amount)
        {
            if (!CanWithdraw(type, amount))
                return false;
            Set(type, Get(type) - amount);
            return true;
        }

        public void Deposit(ResourceType type, int amount) => Set(type, Get(type) + amount);
    }
}
