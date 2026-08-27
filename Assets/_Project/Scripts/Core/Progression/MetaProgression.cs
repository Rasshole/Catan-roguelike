using System;
using System.Collections.Generic;
using System.Linq;
using CatanRoguelike.Core.Buildings;
using CatanRoguelike.Core.Cards;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Leaders;
using CatanRoguelike.Core.Map;

namespace CatanRoguelike.Core.Progression
{
    /// <summary>
    /// Meta-currency and permanent unlocks that survive between runs (stored in meta.json).
    /// </summary>
    public sealed class MetaProgression
    {
        public int Stars { get; private set; }
        public IReadOnlyCollection<MetaUnlockId> UnlockedIds => _unlocked;
        public IReadOnlyCollection<string> AwardedRunKeys => _awardedRunKeys;

        private readonly HashSet<MetaUnlockId> _unlocked = new();
        private readonly HashSet<string> _awardedRunKeys = new(StringComparer.Ordinal);

        public static MetaProgression CreateFresh()
        {
            var meta = new MetaProgression();
            foreach (var id in MetaCatalog.DefaultUnlocked)
                meta._unlocked.Add(id);
            return meta;
        }

        internal static MetaProgression FromSave(MetaSaveDocument doc)
        {
            var meta = CreateFresh();
            meta.Stars = doc.Stars;
            meta._unlocked.Clear();
            foreach (var id in MetaCatalog.DefaultUnlocked)
                meta._unlocked.Add(id);
            foreach (var id in doc.UnlockedIds ?? Enumerable.Empty<MetaUnlockId>())
                meta._unlocked.Add(id);
            meta._awardedRunKeys.Clear();
            foreach (var key in doc.AwardedRunKeys ?? Enumerable.Empty<string>())
                meta._awardedRunKeys.Add(key);
            return meta;
        }

        public bool IsUnlocked(MetaUnlockId id) => _unlocked.Contains(id);

        public bool IsMapAvailable(MapSize size)
        {
            if (MetaCatalog.IsMapAlwaysAvailable(size))
                return true;
            var unlock = MetaCatalog.MapUnlockFor(size);
            return unlock.HasValue && IsUnlocked(unlock.Value);
        }

        public bool IsLeaderAvailable(LeaderId leader)
        {
            if (MetaCatalog.IsLeaderAlwaysAvailable(leader))
                return true;
            var unlock = MetaCatalog.LeaderUnlockFor(leader);
            return unlock.HasValue && IsUnlocked(unlock.Value);
        }

        public bool IsUniqueAvailable(UniqueBuildingId id)
        {
            if (MetaCatalog.IsUniqueAlwaysAvailable(id))
                return true;
            var unlock = MetaCatalog.UniqueUnlockFor(id);
            return unlock.HasValue && IsUnlocked(unlock.Value);
        }

        public bool IsCardAvailable(CardId id)
        {
            if (MetaCatalog.IsCardAlwaysAvailable(id))
                return true;
            var unlock = MetaCatalog.CardUnlockFor(id);
            return unlock.HasValue && IsUnlocked(unlock.Value);
        }

        public IEnumerable<MapSize> GetAvailableMapSizes()
        {
            foreach (MapSize size in Enum.GetValues(typeof(MapSize)))
            {
                if (IsMapAvailable(size))
                    yield return size;
            }
        }

        public IEnumerable<LeaderId> GetAvailableLeaders()
        {
            foreach (LeaderId leader in Enum.GetValues(typeof(LeaderId)))
            {
                if (IsLeaderAvailable(leader))
                    yield return leader;
            }
        }

        public IEnumerable<UniqueBuildingId> GetDraftPool()
        {
            foreach (UniqueBuildingId id in Enum.GetValues(typeof(UniqueBuildingId)))
            {
                if (IsUniqueAvailable(id))
                    yield return id;
            }
        }

        public IEnumerable<CardId> GetCardPool()
        {
            foreach (var id in CardLibrary.AllCards)
            {
                if (IsCardAvailable(id))
                    yield return id;
            }
        }

        public int GetDraftPickCount()
        {
            int requested = IsUnlocked(MetaUnlockId.ExtraDraftPick)
                ? RunProgression.DraftPickCount + 1
                : RunProgression.DraftPickCount;
            int available = GetDraftPool().Count();
            return Math.Min(requested, available);
        }

        public bool HasStartWheatBonus() => IsUnlocked(MetaUnlockId.StartBonusWheat);

        public CardId? GetStartBonusCard() =>
            IsUnlocked(MetaUnlockId.StartBonusCard) ? MetaCatalog.StartBonusCard : null;

        public static int CalculateStarsEarned(int humanVp, int dayNumber, PlayerId winner)
        {
            int stars = humanVp + dayNumber / 2;
            if (winner == PlayerId.Human)
                stars += MetaCatalog.WinBonusStars;
            return stars;
        }

        public static string BuildRunAwardKey(int runSeed, int humanVp, int dayNumber, PlayerId winner) =>
            $"{runSeed}:{humanVp}:{dayNumber}:{winner}";

        public bool TryAwardRun(int runSeed, int humanVp, int dayNumber, PlayerId winner, out int starsEarned)
        {
            var key = BuildRunAwardKey(runSeed, humanVp, dayNumber, winner);
            if (!_awardedRunKeys.Add(key))
            {
                starsEarned = 0;
                return false;
            }

            starsEarned = CalculateStarsEarned(humanVp, dayNumber, winner);
            Stars += starsEarned;
            return true;
        }

        public bool CanPurchase(MetaUnlockId id)
        {
            if (IsUnlocked(id))
                return false;
            return Stars >= MetaCatalog.Get(id).Cost;
        }

        public bool TryPurchase(MetaUnlockId id)
        {
            if (IsUnlocked(id))
                return false;

            var def = MetaCatalog.Get(id);
            if (Stars < def.Cost)
                return false;

            Stars -= def.Cost;
            _unlocked.Add(id);
            return true;
        }

        public IEnumerable<MetaUnlockId> GetPurchasableUnlocks() =>
            MetaCatalog.All.Keys.Where(id => !IsUnlocked(id));

        public void AddStarsForTesting(int amount) => Stars += amount;
    }
}
