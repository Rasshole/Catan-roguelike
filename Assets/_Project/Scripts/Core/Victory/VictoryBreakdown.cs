using System.Text;

namespace CatanRoguelike.Core.Victory
{
    /// <summary>
    /// VP contribution per category for one player. Parts sum to <see cref="Total"/>.
    /// </summary>
    public readonly struct VictoryBreakdown
    {
        public int Settlements { get; }
        public int Cities { get; }
        public int Longest { get; }
        public int LongRoadBonus { get; }
        public int LargestArmy { get; }
        public int BonusVp { get; }

        public int Total => Settlements + Cities + Longest + LongRoadBonus + LargestArmy + BonusVp;

        public VictoryBreakdown(
            int settlements,
            int cities,
            int longest,
            int longRoadBonus,
            int largestArmy,
            int bonusVp)
        {
            Settlements = settlements;
            Cities = cities;
            Longest = longest;
            LongRoadBonus = longRoadBonus;
            LargestArmy = largestArmy;
            BonusVp = bonusVp;
        }

        /// <summary>One-line summary for IMGUI, e.g. "settlements 1 + cities 4 + longest 2".</summary>
        public string FormatLine()
        {
            var sb = new StringBuilder();
            bool any = false;

            void Add(int value, string label)
            {
                if (value <= 0)
                    return;
                if (any)
                    sb.Append(" + ");
                sb.Append(label).Append(' ').Append(value);
                any = true;
            }

            Add(Settlements, "settlements");
            Add(Cities, "cities");
            Add(Longest, "longest");
            Add(LongRoadBonus, "long road");
            Add(LargestArmy, "largest army");
            Add(BonusVp, "bonus");

            return any ? sb.ToString() : "0";
        }
    }
}
