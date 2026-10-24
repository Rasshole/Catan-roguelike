using System.Linq;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Match;
using CatanRoguelike.Core.Trade;
using NUnit.Framework;

namespace CatanRoguelike.Tests
{
    /// <summary>
    /// v139: wildcard ("any resource") trade cards — "I'll give N of anything you choose" / "give me N of anything".
    /// Serializable, deterministic, validated against both hands; the AI answers them.
    /// </summary>
    public class MatchTradeWildcardV139Tests
    {
        private static MatchController AfterSetup(int seats, int humans, int seed)
        {
            var match = new MatchController(MatchConfig.Create(seats, humans, seed));
            while (match.IsSetup)
                Assert.IsTrue(match.Apply(MatchAi.ChooseCommand(match)).Ok);
            match.State.CurrentSeat = 0;
            match.State.Phase = MatchPhase.Main;
            return match;
        }

        private static ResourceBundle Cards(int wood = 0, int brick = 0, int wheat = 0, int sheep = 0, int stone = 0) =>
            new() { Wood = wood, Brick = brick, Wheat = wheat, Sheep = sheep, Stone = stone };

        [Test]
        public void Pool_ExcludesRequestedAndCommittedCards()
        {
            var pool = TradeWildcards.Pool(Cards(wood: 3, brick: 2, wheat: 1, sheep: 2), Cards(wood: 1), Cards(sheep: 1));
            Assert.AreEqual(2, pool.Wood, "the offered wood is committed");
            Assert.AreEqual(2, pool.Brick);
            Assert.AreEqual(1, pool.Wheat);
            Assert.AreEqual(0, pool.Sheep, "never a resource you ask for");
            Assert.IsTrue(TradeWildcards.Fits(Cards(brick: 2), pool, 2));
            Assert.IsFalse(TradeWildcards.Fits(Cards(sheep: 1), pool, 1));
            Assert.IsFalse(TradeWildcards.Fits(Cards(brick: 1), pool, 2), "the pick must be exactly N");
            Assert.IsFalse(TradeWildcards.Fits(Cards(brick: 3), pool, 3));
            Assert.AreEqual(1, TradeWildcards.PickForNeed(pool, 1, Cards(wood: 5, wheat: 5)).Brick,
                "deterministic: takes what it holds fewest of");
            var two = TradeWildcards.PickForNeed(pool, 2, Cards(brick: 5, wood: 5));
            Assert.AreEqual(1, two.Wheat);
            Assert.AreEqual(1, two.Wood, "ties go in board order");
            Assert.AreEqual(0, TradeWildcards.PickForNeed(pool, 6, Cards()).Total, "pool too small");
        }

        [Test]
        public void GiveAny_AccepterPicks_AndCardsSwap()
        {
            var match = AfterSetup(3, 3, 71);
            match.State.Seats[0].Hand = Cards(brick: 2, wheat: 1, sheep: 1);
            match.State.Seats[1].Hand = Cards(wood: 2);
            Assert.IsTrue(match.Apply(MatchCommand.Propose(0, Cards(), Cards(wood: 1), giveAny: 1)).Ok,
                "I want wood and give any one card");
            Assert.AreEqual(1, match.State.ActiveTrade.GiveAny);
            Assert.IsFalse(match.Apply(MatchCommand.AcceptWithChoice(1, Cards(wood: 1))).Ok, "they have no spare wood");
            Assert.IsFalse(match.Apply(MatchCommand.AcceptWithChoice(1, Cards(stone: 1))).Ok, "they hold no stone");
            Assert.IsFalse(match.Apply(MatchCommand.AcceptWithChoice(1, Cards(brick: 2))).Ok, "exactly one card");
            Assert.IsFalse(match.Apply(MatchCommand.Respond(1, TradeResponseKind.Accepted)).Ok, "a pick is required");
            Assert.IsTrue(match.Apply(MatchCommand.AcceptWithChoice(1, Cards(wheat: 1))).Ok);
            Assert.AreEqual(1, match.State.ActiveTrade.Responses[1].AnyChoice.Wheat);
            Assert.IsTrue(match.Apply(MatchCommand.Confirm(0, 1)).Ok);
            Assert.AreEqual(Cards(wood: 1, brick: 2, sheep: 1).Total, match.State.Seats[0].Hand.Total);
            Assert.AreEqual(1, match.State.Seats[0].Hand.Wood);
            Assert.AreEqual(0, match.State.Seats[0].Hand.Wheat);
            Assert.AreEqual(1, match.State.Seats[1].Hand.Wheat);
            Assert.AreEqual(1, match.State.Seats[1].Hand.Wood);
        }

        [Test]
        public void GetAny_OffererPicksOnConfirm_FromWhatThePartnerCanSpare()
        {
            var match = AfterSetup(3, 3, 72);
            match.State.Seats[0].Hand = Cards(wood: 2);
            match.State.Seats[1].Hand = Cards(wood: 1, brick: 1, stone: 2);
            Assert.IsTrue(match.Apply(MatchCommand.Propose(0, Cards(wood: 2), Cards(), getAny: 2)).Ok);
            Assert.IsTrue(match.Apply(MatchCommand.Respond(1, TradeResponseKind.Accepted)).Ok);
            Assert.IsFalse(match.Apply(MatchCommand.Confirm(0, 1)).Ok, "the offerer must pick");
            Assert.IsFalse(match.Apply(MatchCommand.Confirm(0, 1, Cards(wood: 1, stone: 1))).Ok,
                "never the resource the offerer gives");
            Assert.IsFalse(match.Apply(MatchCommand.Confirm(0, 1, Cards(brick: 2))).Ok, "they hold one brick");
            Assert.IsTrue(match.Apply(MatchCommand.Confirm(0, 1, Cards(brick: 1, stone: 1))).Ok);
            Assert.AreEqual(0, match.State.Seats[0].Hand.Wood);
            Assert.AreEqual(1, match.State.Seats[0].Hand.Brick);
            Assert.AreEqual(1, match.State.Seats[0].Hand.Stone);
            Assert.AreEqual(3, match.State.Seats[1].Hand.Wood);
            Assert.AreEqual(1, match.State.Seats[1].Hand.Stone);
        }

        [Test]
        public void IllegalWildcardOffers_AreRejected()
        {
            var match = AfterSetup(3, 3, 73);
            match.State.Seats[0].Hand = Cards(wood: 2, brick: 1);
            Assert.IsFalse(match.Apply(MatchCommand.Propose(0, Cards(), Cards(wood: 1), giveAny: 2)).Ok,
                "only one card outside the requested wood");
            Assert.IsFalse(match.Apply(MatchCommand.Propose(0, Cards(brick: 1), Cards(wood: 1), giveAny: 1)).Ok,
                "the brick is already offered and wood is asked for");
            Assert.IsFalse(match.Apply(MatchCommand.Propose(0, Cards(), Cards(), giveAny: 1)).Ok, "ask for something");
            Assert.IsFalse(match.Apply(MatchCommand.Propose(0, Cards(), Cards(stone: 1), giveAny: 1, getAny: 1)).Ok,
                "wildcards on one side only");
            Assert.IsFalse(match.Apply(MatchCommand.Propose(0, Cards(), Cards(stone: 1), giveAny: -1)).Ok);
            Assert.IsFalse(match.Apply(MatchCommand.Propose(0, Cards(), Cards(stone: 1), giveAny: 10)).Ok);
            Assert.IsTrue(match.Apply(MatchCommand.Propose(0, Cards(), Cards(stone: 1), giveAny: 3)).Ok);

            // Hands change after the accept: confirm re-checks the backing.
            match.State.Seats[1].Hand = Cards(stone: 1);
            Assert.IsTrue(match.Apply(MatchCommand.AcceptWithChoice(1, Cards(wood: 2, brick: 1))).Ok);
            match.State.Seats[0].Hand = Cards(wood: 1, brick: 1);
            Assert.IsFalse(match.Apply(MatchCommand.Confirm(0, 1)).Ok, "no illegal trade after the hand shrank");
            Assert.IsFalse(match.Apply(MatchCommand.Confirm(0, 1, Cards(brick: 1))).Ok, "no pick on a give wildcard");
        }

        [Test]
        public void GetAny_ResponderNeedsSpareCards()
        {
            var match = AfterSetup(3, 3, 74);
            match.State.Seats[0].Hand = Cards(wheat: 1);
            match.State.Seats[1].Hand = Cards(wheat: 3);
            match.State.Seats[2].Hand = Cards(sheep: 1);
            Assert.IsTrue(match.Apply(MatchCommand.Propose(0, Cards(wheat: 1), Cards(), getAny: 1)).Ok);
            Assert.IsFalse(match.Apply(MatchCommand.Respond(1, TradeResponseKind.Accepted)).Ok,
                "wheat only — the offerer gives wheat, so nothing to spare");
            Assert.IsTrue(match.Apply(MatchCommand.Respond(2, TradeResponseKind.Accepted)).Ok);
            Assert.IsFalse(match.Apply(MatchCommand.Respond(2, TradeResponseKind.Countered, Cards(wheat: 1), Cards())).Ok,
                "counters carry plain cards on both sides");
        }

        [Test]
        public void Ai_AnswersWildcards_Sensibly()
        {
            var match = AfterSetup(3, 3, 75);
            match.State.Seats[0].Hand = Cards(brick: 1, sheep: 2);
            match.State.Seats[1].Hand = Cards(wood: 2, sheep: 3);
            match.State.Seats[2].Hand = Cards(brick: 2);
            Assert.IsTrue(match.Apply(MatchCommand.Propose(0, Cards(), Cards(wood: 1), giveAny: 1)).Ok);
            var ai1 = MatchAi.ChooseTradeResponse(match, 1);
            Assert.AreEqual(TradeResponseKind.Accepted, ai1.Response);
            Assert.AreEqual(1, ai1.Choice.Total);
            Assert.AreEqual(1, ai1.Choice.Brick, "it holds no brick, so it takes the brick over more sheep");
            Assert.IsTrue(match.Apply(ai1).Ok);
            Assert.AreEqual(TradeResponseKind.Declined, MatchAi.ChooseTradeResponse(match, 2).Response, "no wood");
            Assert.IsTrue(match.Apply(MatchCommand.Simple(MatchCommandType.CancelTrade, 0)).Ok);

            Assert.IsTrue(match.Apply(MatchCommand.Propose(0, Cards(sheep: 1), Cards(), getAny: 2)).Ok);
            Assert.AreEqual(TradeResponseKind.Declined, MatchAi.ChooseTradeResponse(match, 1).Response,
                "1 card for 2 is a bad deal");
            Assert.IsTrue(match.Apply(MatchCommand.Simple(MatchCommandType.CancelTrade, 0)).Ok);

            Assert.IsTrue(match.Apply(MatchCommand.Propose(0, Cards(sheep: 2), Cards(), getAny: 2)).Ok);
            Assert.AreEqual(TradeResponseKind.Accepted, MatchAi.ChooseTradeResponse(match, 1).Response);
            Assert.AreEqual(TradeResponseKind.Accepted, MatchAi.ChooseTradeResponse(match, 2).Response);
            Assert.IsTrue(match.Apply(MatchAi.ChooseTradeResponse(match, 1)).Ok);
            var pick = MatchAi.ChooseConfirmWildcards(match, 1);
            Assert.AreEqual(2, pick.Wood, "sheep is offered, so only wood can be taken");
            Assert.IsTrue(match.Apply(MatchCommand.Confirm(0, 1, pick)).Ok);
        }

        [Test]
        public void WildcardCommands_RoundTripJson_AndReplayDeterministically()
        {
            var a = AfterSetup(3, 3, 76);
            var b = AfterSetup(3, 3, 76);
            foreach (var m in new[] { a, b })
            {
                m.State.Seats[0].Hand = Cards(wood: 1, brick: 2);
                m.State.Seats[1].Hand = Cards(stone: 1);
            }

            var cmds = new[]
            {
                MatchCommand.Propose(0, Cards(), Cards(stone: 1), giveAny: 2),
                MatchCommand.AcceptWithChoice(1, Cards(wood: 1, brick: 1)),
                MatchCommand.Confirm(0, 1)
            };
            var json = MatchCommand.LogToJson(cmds);
            var back = MatchCommand.LogFromJson(json);
            Assert.AreEqual(cmds.Length, back.Count);
            for (int i = 0; i < cmds.Length; i++)
                Assert.AreEqual(cmds[i], back[i]);
            Assert.AreEqual(2, back[0].GiveAny);
            Assert.AreEqual(1, back[1].Choice.Brick);
            Assert.AreNotEqual(cmds[1], MatchCommand.AcceptWithChoice(1, Cards(brick: 2)), "Choice is part of equality");

            Assert.IsTrue(a.Apply(cmds[0]).Ok);
            Assert.IsTrue(b.Apply(back[0]).Ok);
            Assert.AreEqual(a.State.ComputeHash(), b.State.ComputeHash());
            Assert.IsTrue(a.Apply(cmds[1]).Ok);
            var mid = a.State.ComputeHash();
            Assert.IsTrue(b.Apply(MatchCommand.AcceptWithChoice(1, Cards(brick: 2))).Ok);
            Assert.AreNotEqual(mid, b.State.ComputeHash(), "the pick is part of the state hash");
            Assert.IsTrue(a.Apply(cmds[2]).Ok);
            Assert.AreEqual(1, a.State.Seats[0].Hand.Stone);
            Assert.AreEqual(1, a.State.Seats[0].Hand.Brick);
            Assert.AreEqual(1, a.State.Seats[1].Hand.Wood);
        }

        [Test]
        public void SinglePlayer_WildcardOffer_AiPicks_AndHumanPicksOnReceive()
        {
            var state = new GameController(9, MapSize.Small).State;
            state.PlayerInventory = Cards(brick: 1, sheep: 2);
            state.AiInventory = Cards(wood: 1, wheat: 2);
            var giveAny = new PlayerTradeOffer(Cards(), Cards(wood: 1), giveAny: 1);
            Assert.IsTrue(PlayerTradeResolver.IsValidOffer(state, giveAny));
            var r = PlayerTradeResolver.EvaluateAiResponses(state, giveAny).Single();
            Assert.IsTrue(r.Accept);
            Assert.AreEqual(1, r.Choice.Total);
            Assert.AreEqual(1, r.Choice.Brick, "AI takes the brick it lacks");
            Assert.IsFalse(PlayerTradeResolver.TryExecute(state, PlayerId.Ai, giveAny, Cards(wood: 1), Cards()));
            Assert.IsTrue(PlayerTradeResolver.TryExecute(state, PlayerId.Ai, giveAny, r.Choice, Cards()));
            Assert.AreEqual(1, state.PlayerInventory.Wood);
            Assert.AreEqual(0, state.PlayerInventory.Brick);

            var getAny = new PlayerTradeOffer(Cards(sheep: 1), Cards(), receiveAny: 1);
            Assert.IsTrue(PlayerTradeResolver.EvaluateAiResponses(state, getAny).Single().Accept);
            Assert.IsFalse(PlayerTradeResolver.TryExecute(state, PlayerId.Ai, getAny, Cards(), Cards()), "pick required");
            Assert.IsFalse(PlayerTradeResolver.TryExecute(state, PlayerId.Ai, getAny, Cards(), Cards(sheep: 1)));
            Assert.IsTrue(PlayerTradeResolver.TryExecute(state, PlayerId.Ai, getAny, Cards(), Cards(wheat: 1)));
            Assert.AreEqual(1, state.PlayerInventory.Wheat);

            Assert.IsFalse(PlayerTradeResolver.IsValidOffer(state, new PlayerTradeOffer(Cards(), Cards(sheep: 1), giveAny: 3)),
                "only 2 cards outside sheep");
        }
    }
}
