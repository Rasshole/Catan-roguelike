namespace CatanRoguelike.Core.UI
{
    public static class HudToastBuffer
    {
        public static void Push(GameState state, string message)
        {
            if (state == null || string.IsNullOrWhiteSpace(message))
                return;
            state.ToastMessage = message.Trim();
        }

        public static bool TryConsume(GameState state, out string message)
        {
            message = null;
            if (state == null || string.IsNullOrEmpty(state.ToastMessage))
                return false;
            message = state.ToastMessage;
            state.ToastMessage = null;
            return true;
        }
    }
}
