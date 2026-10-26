using CatanRoguelike.Core;
using CatanRoguelike.Core.Match;
using UnityEngine;
using UnityEngine.UI;

namespace CatanRoguelike.Game
{
    /// <summary>
    /// v143/v146 Fishermen of Catan (setup-screen toggle) on the pass &amp; play table: a fish counter on the hand
    /// panel that opens the official spend menu, plus the old boot.
    /// </summary>
    public sealed partial class RunHudController
    {
        public const string FishChipName = "FishChip";
        public const string FishPickerName = "FishPicker";
        private bool _hsFishPick;

        public const string FishBootBadgeName = "OldBootBadge";
        private int _hsFishMode; // v146: 0 = menu, 3 = pick a victim, 4 = pick a card

        private static string FishSpendLabel(int cost) => cost switch
        {
            MatchController.FishSpendRobberOff => "Robber off",
            MatchController.FishSpendSteal => "Steal a card",
            MatchController.FishSpendCard => "Bank card",
            MatchController.FishSpendRoad => "Free road",
            _ => "Dev card",
        };

        /// <summary>
        /// v146 official Fishermen of Catan: the fish chip opens a spend menu (2 robber off, 3 steal, 4 card,
        /// 5 free road, 7 dev card) and, for the old boot's holder, a "pass the boot" row.
        /// </summary>
        private void BuildHotSeatFish(RectTransform panel, int seat)
        {
            var s = _hotSeat;
            if (s == null || !s.State.Config.Fishermen)
                return;
            var me = s.State.Seats[seat];
            var match = s.Match;
            bool mine = seat == s.State.ActingSeat && s.IsHandVisible(seat);
            bool anySpend = false;
            foreach (int cost in MatchController.FishSpendCosts)
                anySpend |= mine && match.CanSpendFish(seat, cost);
            bool holdsBoot = s.State.OldBootSeat == seat;
            bool canPass = false;
            if (mine && holdsBoot)
                for (int i = 0; i < s.State.Seats.Count; i++)
                    canPass |= match.CanPassOldBoot(seat, i);
            bool can = anySpend || canPass;
            if (!can)
            {
                _hsFishPick = false;
                _hsFishMode = 0;
            }

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
                _hsFishMode = 0;
                HsMark();
            });
            if (holdsBoot)
                BuildBootBadge(panel, new Vector2(0f, 1f), new Vector2(84f, -26f));
            if (!_hsFishPick || !can)
                return;

            var ink = ColonistUiKit.TextOn(UiKitPiece.FrameMedium);
            bool bootRow = canPass && _hsFishMode == 0;
            float h = _hsFishMode == 0 ? (bootRow ? 196f : 128f) : 120f;
            var picker = CreateImage(panel, FishPickerName, null);
            picker.preserveAspect = false;
            ColonistUiKit.Apply(picker, UiKitPiece.FrameMedium);
            SetRect(picker.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 10f + h / 2f), new Vector2(580f, h));
            string head = _hsFishMode switch
            {
                MatchController.FishSpendSteal => $"Steal a card for {MatchController.FishSpendSteal} fish: whom?",
                MatchController.FishSpendCard => $"Pick a card for {MatchController.FishSpendCard} fish",
                _ => $"SPEND FISH  ·  you have {me.Fish}",
            };
            var title = RunHudUiFactory.CreateText(picker.rectTransform, "Title", head, 15, TextAnchor.MiddleCenter,
                ink, FontStyle.Bold);
            SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(520f, 20f));
            if (_hsFishMode != 0)
            {
                var back = RunHudUiFactory.CreateButton(picker.rectTransform, "Back", RunHudUiFactory.ButtonFill);
                back.name = "FishBack";
                SetRect((RectTransform)back.transform, new Vector2(0f, 1f), new Vector2(48f, -20f), new Vector2(72f, 26f));
                ColonistKenneyHudArt.ApplyColonistWarmButton(back.GetComponent<Image>(), primaryGreen: false);
                back.onClick.AddListener(() =>
                {
                    _hsFishMode = 0;
                    HsMark();
                });
            }

            if (_hsFishMode == MatchController.FishSpendCard)
            {
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
                    b.onClick.AddListener(() => ApplyFish(MatchCommand.Fish(seat, r)));
                }

                return;
            }

            if (_hsFishMode == MatchController.FishSpendSteal)
            {
                var victims = new System.Collections.Generic.List<int>();
                for (int i = 0; i < s.State.Seats.Count; i++)
                    if (i != seat && s.State.Seats[i].Hand.Total > 0)
                        victims.Add(i);
                float w = Mathf.Min(170f, 540f / Mathf.Max(1, victims.Count));
                for (int k = 0; k < victims.Count; k++)
                {
                    int v = victims[k];
                    var vs = s.State.Seats[v];
                    var b = RunHudUiFactory.CreateButton(picker.rectTransform, $"{vs.Name}  ·  {vs.Hand.Total}",
                        RunHudUiFactory.ButtonFill);
                    b.name = "FishSteal_" + v;
                    SetRect((RectTransform)b.transform, new Vector2(0.5f, 0.5f),
                        new Vector2((k - (victims.Count - 1) / 2f) * w, -12f), new Vector2(w - 12f, 52f));
                    ColonistKenneyHudArt.ApplyColonistWarmButton(b.GetComponent<Image>(), primaryGreen: false);
                    b.onClick.AddListener(() => ApplyFish(MatchCommand.SpendFish(seat, MatchController.FishSpendSteal, v)));
                }

                return;
            }

            var costs = MatchController.FishSpendCosts;
            for (int i = 0; i < costs.Length; i++)
            {
                int cost = costs[i];
                bool ok = match.CanSpendFish(seat, cost);
                var b = RunHudUiFactory.CreateButton(picker.rectTransform, "", RunHudUiFactory.ButtonFill);
                b.name = "FishSpend_" + cost;
                SetRect((RectTransform)b.transform, new Vector2(0.5f, 1f), new Vector2(-224f + i * 112f, -80f),
                    new Vector2(104f, 72f));
                ColonistKenneyHudArt.ApplyColonistWarmButton(b.GetComponent<Image>(), primaryGreen: ok);
                b.interactable = ok;
                var txt = b.GetComponentInChildren<Text>();
                if (txt != null)
                    txt.text = "";
                var num = RunHudUiFactory.CreateText(b.transform, "Cost", cost.ToString(), 26, TextAnchor.MiddleCenter,
                    ok ? ColonistUiKit.Cream : ColonistUiKit.InkMuted, FontStyle.Bold);
                num.raycastTarget = false;
                SetRect(num.rectTransform, new Vector2(0.5f, 1f), new Vector2(8f, -20f), new Vector2(60f, 30f));
                var fi = CreateImage(b.transform, "Fish", ColonistHudResourceSprites.GetFish());
                fi.raycastTarget = false;
                SetRect(fi.rectTransform, new Vector2(0.5f, 1f), new Vector2(-22f, -20f), new Vector2(22f, 22f));
                var cap = RunHudUiFactory.CreateText(b.transform, "Caption", FishSpendLabel(cost), 13,
                    TextAnchor.MiddleCenter, ok ? ColonistUiKit.Cream : ColonistUiKit.InkMuted, FontStyle.Bold);
                cap.raycastTarget = false;
                SetRect(cap.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(100f, 20f));
                b.onClick.AddListener(() =>
                {
                    if (cost == MatchController.FishSpendSteal || cost == MatchController.FishSpendCard)
                    {
                        _hsFishMode = cost;
                        HsMark();
                        return;
                    }

                    ApplyFish(MatchCommand.SpendFish(seat, cost));
                });
            }

            if (!bootRow)
                return;
            var bootLabel = RunHudUiFactory.CreateText(picker.rectTransform, "BootTitle",
                "Pass the old boot (+1 VP to win) to:", 14, TextAnchor.MiddleLeft, ink, FontStyle.Bold);
            SetRect(bootLabel.rectTransform, new Vector2(0f, 0f), new Vector2(150f, 34f), new Vector2(260f, 24f));
            float x = 300f;
            for (int i = 0; i < s.State.Seats.Count; i++)
            {
                if (!match.CanPassOldBoot(seat, i))
                    continue;
                int to = i;
                var b = RunHudUiFactory.CreateButton(picker.rectTransform, s.State.Seats[i].Name,
                    RunHudUiFactory.ButtonFill);
                b.name = "BootTo_" + i;
                SetRect((RectTransform)b.transform, new Vector2(0f, 0f), new Vector2(x + 60f, 34f), new Vector2(112f, 36f));
                ColonistKenneyHudArt.ApplyColonistWarmButton(b.GetComponent<Image>(), primaryGreen: true);
                b.onClick.AddListener(() => ApplyFish(MatchCommand.PassBoot(seat, to)));
                x += 120f;
            }
        }

        private void ApplyFish(MatchCommand command)
        {
            if (_hotSeat != null && _hotSeat.Apply(command).Ok)
            {
                _hsFishPick = false;
                _hsFishMode = 0;
            }

            HsMark();
        }

        /// <summary>v146: the old boot marker (its holder needs one more VP to win).</summary>
        /// <summary>The spend menu opens above the hand; lift it over the quest card and dev-card panel.</summary>
        private void LiftFishPickerToTop()
        {
            var picker = _hotSeatRoot != null ? _hotSeatRoot.Find("Hand/" + FishPickerName) : null;
            if (picker == null)
                return;
            picker.SetParent(_hotSeatRoot, true);
            picker.SetAsLastSibling();
        }

        private void BuildBootBadge(Transform parent, Vector2 anchor, Vector2 pos)
        {
            var badge = CreateImage(parent, FishBootBadgeName, null);
            badge.color = new Color(0.36f, 0.24f, 0.14f, 0.95f);
            badge.raycastTarget = false;
            SetRect(badge.rectTransform, anchor, pos, new Vector2(96f, 28f));
            var t = RunHudUiFactory.CreateText(badge.rectTransform, "Text", "OLD BOOT +1", 12, TextAnchor.MiddleCenter,
                ColonistUiKit.Cream, FontStyle.Bold);
            t.raycastTarget = false;
            SetRect(t.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(96f, 28f));
        }
    }
}
