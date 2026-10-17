namespace CatanRoguelike.Core.Data
{
    /// <summary>Hex tile counts for selectable boards (enum value = land hex count).</summary>
    public enum MapSize
    {
        Small = 7,
        Medium = 13,
        /// <summary>Classic base Catan — 3–4 players (19 land hexes).</summary>
        Large = 19,
        /// <summary>Colonist-style extended — 5–6 players (30 land hexes).</summary>
        Extended = 30,
        /// <summary>Full radius-3 hexagon (37 land hexes) — large custom tables.</summary>
        Huge = 37
    }
}
