namespace CatanRoguelike.Game
{
    public static class ColonistPresentation
    {
        public static bool IsActive { get; private set; }

        internal static void SetActive(bool active) => IsActive = active;
    }
}
