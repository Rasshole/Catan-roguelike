using System;

namespace CatanRoguelike.Core.Save
{
    /// <summary>Optional metadata written alongside run state.</summary>
    public sealed class SaveWriteOptions
    {
        public static SaveWriteOptions Manual { get; } = new();

        public DateTime? SavedAtUtc { get; set; }
        public bool IsAutosave { get; set; }

        public static SaveWriteOptions Autosave(DateTime savedAtUtc) => new()
        {
            SavedAtUtc = savedAtUtc,
            IsAutosave = true
        };
    }
}
