using System.Collections.Generic;

namespace CatanRoguelike.Core.Progression
{
    public sealed class MetaSaveDocument
    {
        public int FormatVersion { get; set; }
        public int Stars { get; set; }
        public List<MetaUnlockId> UnlockedIds { get; set; } = new();
        public List<string> AwardedRunKeys { get; set; } = new();
    }
}
