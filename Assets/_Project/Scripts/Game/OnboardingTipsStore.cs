using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>
    /// PlayerPrefs-backed toggle for IMGUI onboarding tips (outside save.json / meta.json).
    /// </summary>
    public static class OnboardingTipsStore
    {
        private const string PrefKey = "catan_onboarding_tips_enabled";

        public static bool TipsEnabled
        {
            get => PlayerPrefs.GetInt(PrefKey, 1) == 1;
            set
            {
                PlayerPrefs.SetInt(PrefKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }
    }
}
