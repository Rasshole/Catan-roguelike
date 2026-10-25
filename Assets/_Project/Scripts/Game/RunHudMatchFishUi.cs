using CatanRoguelike.Core;
using CatanRoguelike.Core.Match;
using UnityEngine;
using UnityEngine.UI;

namespace CatanRoguelike.Game
{
    /// <summary>
    /// v143 Fishermen (setup-screen toggle) on the pass &amp; play table: a fish counter on the hand panel that, with
    /// enough fish, opens a five-card picker to buy one card from the bank (<see cref="MatchController.FishPerCard"/>
    /// fish each).
    /// </summary>
    public sealed partial class RunHudController
    {
        public const string FishChipName = "FishChip";
        public const string FishPickerName = "FishPicker";
        private bool _hsFishPick;

        private void BuildHotSeatFish(RectTransform panel, int seat)
        {
            var s = _hotSeat;
            if (s == null || !s.State.Config.Fishermen)
                return;
            var me = s.State.Seats[seat];
            bool can = seat == s.State.ActingSeat && s.Match.CanFishForCard(seat) && s.IsHandVisible(seat);
            if (!can)
                _hsFishPick = false;
            var chip = RunHudUiFactory.CreateButton(panel, "", RunHudUiFactory.ButtonFill);
            chip.name = FishChipName;
            SetRect((RectTransform)chip.transform, new Vector2(1f, 1f), new Vector2(-64f, -28f), new Vector2(104f, 34f));
            ColonistKenneyHudArt.ApplyColonistWarmButton(chip.GetComponent<Image>(), primaryGreen: can);
            var label = chip.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.text = $"   {me.Fish}";
                label.fontSize = 18;
                label.fontStyle = FontStyle.Bold;
                label.color = can ? ColonistUiKit.Cream : ColonistUiKit.Ink;
            }

            var icon = CreateImage(chip.transform, "FishIcon", ColonistHudResourceSprites.GetFish());
            icon.raycastTarget = false;
            SetRect(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(26f, 0f), new Vector2(28f, 28f));
            chip.interactable = can;
            chip.onClick.AddListener(() =>
            {
                _hsFishPick = !_hsFishPick;
                HsMark();
            });
            if (!_hsFishPick || !can)
                return;

            var picker = CreateImage(panel, FishPickerName, null);
            picker.preserveAspect = false;
            ColonistUiKit.Apply(picker, UiKitPiece.FrameMedium);
            SetRect(picker.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 62f), new Vector2(560f, 104f));
            var title = RunHudUiFactory.CreateText(picker.rectTransform, "Title",
                $"Trade {MatchController.FishPerCard} fish for one card", 15, TextAnchor.MiddleCenter,
                ColonistUiKit.TextOn(UiKitPiece.FrameMedium), FontStyle.Bold);
            SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(500f, 20f));
            var all = MatchController.AllResources;
            for (int i = 0; i < all.Length; i++)
            {
                var r = all[i];
                var b = RunHudUiFactory.CreateButton(picker.rectTransform, "", RunHudUiFactory.ButtonFill);
                b.name = "FishFor_" + r;
                SetRect((RectTransform)b.transform, new Vector2(0.5f, 0.5f), new Vector2(-208f + i * 104f, -12f),
                    new Vector2(64f, 56f));
                ColonistKenneyHudArt.ApplyColonistWarmButton(b.GetComponent<Image>(), primaryGreen: false);
                var ri = CreateImage(b.transform, "Icon", ColonistHudResourceSprites.Get(r));
                ri.raycastTarget = false;
                SetRect(ri.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(44f, 44f));
                b.onClick.AddListener(() =>
                {
                    if (_hotSeat != null && _hotSeat.Apply(MatchCommand.Fish(seat, r)).Ok)
                        _hsFishPick = false;
                    HsMark();
                });
            }
        }
    }
}
