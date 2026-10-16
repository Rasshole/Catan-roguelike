namespace CatanRoguelike.Game
{
    internal static class ColonistBoardViewConstants
    {
        /// <summary>Colonist top-down camera yaw (matches stable a1d67f8 capture pose).</summary>
        public const float CameraYawDegrees = 90f;

        public const float BoardPlayAreaFill = 0.86f;

        /// <summary>Extra zoom so the hex cluster fits inside HUD chrome (~18% smaller than fill-only).</summary>
        public const float BoardZoomMultiplier = 1.83f;

        /// <summary>Extra play-area height reserved so the hex cluster clears the action bar.</summary>
        public const float BoardBottomBreathingRoomPixels = 40f;
    }
}
