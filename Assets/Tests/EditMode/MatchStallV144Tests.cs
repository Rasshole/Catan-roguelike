using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Match;
using NUnit.Framework;

namespace CatanRoguelike.Tests
{
    /// <summary>
    /// v144: the 5-player games that timed out in the ecbab92 party sim (seeds 746910 Extended, 746205 Huge) replay
    /// to a winner. The sim's timeout is wall-clock, so a stalled box could cut games short; this pins that the
    /// games themselves always finish (no AI loop on the Harbour Pilot pick or the quest roads).
    /// </summary>
    public class MatchStallV144Tests
    {
        private const int CommandCap = 5000;

        [TestCase(746910, MapSize.Extended)]
        [TestCase(746205, MapSize.Huge)]
        public void FivePlayerPartyGame_FromTheStalledSimSeeds_ReachesAWinner(int seed, MapSize map)
        {
            var match = new MatchController(MatchConfig.Create(5, 0, seed, map, partyRules: true));
            int commands = 0;
            while (!match.IsOver)
            {
                Assert.Less(commands++, CommandCap, $"no winner after {CommandCap} commands (phase {match.State.Phase})");
                var command = MatchRunner.PendingAiResponse(match) ?? MatchAi.ChooseCommand(match);
                Assert.IsNotNull(command, $"no command in phase {match.State.Phase}");
                if (command.Type == MatchCommandType.PickQuest)
                {
                    // The sim's --quest-pick random policy, so the replay matches the sim game exactly.
                    var offer = match.State.Seats[command.Seat].QuestOffer;
                    var pick = offer[(int)((uint)(seed * 31 + command.Seat * 7919) % (uint)offer.Count)];
                    command = MatchCommand.PickQuestTrait(command.Seat, pick, (MatchTraitId)command.Pick2);
                }

                var result = match.Apply(command);
                Assert.IsTrue(result.Ok, $"{command}: {result.Error}");
            }

            Assert.IsTrue(match.IsOver);
            Assert.IsTrue(match.State.Winner.HasValue, "game over without a winner");
        }
    }
}
