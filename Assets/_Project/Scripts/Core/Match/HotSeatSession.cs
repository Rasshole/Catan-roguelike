using System;

namespace CatanRoguelike.Core.Match
{
    /// <summary>
    /// v134 Phase 2: pass-and-play driver around a <see cref="MatchController"/>. It owns the turn clock
    /// (1.5 min turns, manual +90 s charge, pulsing last 5 s), runs AI seats, resolves human timeouts, and
    /// gates each human turn behind a pass screen so the previous player's hand is hidden while the device
    /// changes hands. Pure C#: the HUD only renders it and forwards button presses as commands.
    /// </summary>
    public sealed class HotSeatSession
    {
        public MatchController Match { get; }
        public TurnClock Clock { get; } = new();

        /// <summary>True while the "pass the device to X" screen covers the table (clock paused, hands hidden).</summary>
        public bool PassScreenPending { get; private set; }

        /// <summary>Seat that last saw its hand; a pass screen appears whenever a different human takes over.</summary>
        public int LastRevealedHumanSeat { get; private set; } = -1;

        public string LastMessage { get; private set; } = "";

        public event Action Changed;

        private (int seat, int turn, int step, MatchPhase phase) _turnKey;

        public HotSeatSession(MatchConfig config)
        {
            Match = new MatchController(config);
            _turnKey = Key();
            BeginTurn(force: true);
        }

        public MatchState State => Match.State;
        public bool IsOver => Match.IsOver;
        public MatchSeatState Current => Match.State.Current;
        public int HumanCount => Match.State.Config.HumanCount;

        /// <summary>A seat's hand may be shown only to itself, on its own turn, after the pass screen.</summary>
        public bool IsHandVisible(int seat) =>
            !PassScreenPending && !Match.State.Seats[seat].IsAi && seat == Match.State.CurrentSeat && !IsOver;

        public bool CanUseExtraTime
        {
            get
            {
                var s = Current;
                return !IsOver && !s.IsAi && !PassScreenPending && !Match.IsSetup
                       && s.ExtraTimeCharges > 0 && !s.ExtraTimeUsedThisTurn;
            }
        }

        public void ConfirmPass()
        {
            if (!PassScreenPending)
                return;
            PassScreenPending = false;
            LastRevealedHumanSeat = Match.State.CurrentSeat;
            Clock.StartTurn();
            Changed?.Invoke();
        }

        /// <summary>Advances time. AI seats act immediately; an expired human turn is resolved by the AI policy.</summary>
        public void Tick(float deltaSeconds)
        {
            if (IsOver)
                return;
            if (Current.IsAi)
            {
                RunAi();
                return;
            }

            if (PassScreenPending)
                return;
            Clock.Tick(deltaSeconds);
            if (Clock.Expired)
            {
                LastMessage = $"{Current.Name} ran out of time.";
                MatchRunner.ResolveTimeout(Match);
                AfterChange();
                RunAi();
            }
        }

        public MatchCommandResult Apply(MatchCommand command)
        {
            if (PassScreenPending)
                return MatchCommandResult.Rejected("Pass the device first.");
            var result = Match.Apply(command);
            if (!result.Ok)
            {
                LastMessage = result.Error;
                Changed?.Invoke();
                return result;
            }

            if (command.Type == MatchCommandType.UseExtraTime)
                Clock.AddExtraTime();
            AfterChange();
            return result;
        }

        public MatchCommandResult UseExtraTime() =>
            Apply(MatchCommand.Simple(MatchCommandType.UseExtraTime, Match.State.CurrentSeat));

        public MatchCommandResult EndTurn() =>
            Apply(MatchCommand.Simple(MatchCommandType.EndTurn, Match.State.CurrentSeat));

        public MatchCommandResult Roll() =>
            Apply(MatchCommand.Simple(MatchCommandType.RollDice, Match.State.CurrentSeat));

        /// <summary>One suggested action for the current human (the AI policy), e.g. for setup placement.</summary>
        public MatchCommandResult ApplySuggested()
        {
            if (IsOver || Current.IsAi || PassScreenPending)
                return MatchCommandResult.Rejected("Not a human turn.");
            return Apply(MatchAi.ChooseCommand(Match));
        }

        /// <summary>Applies suggestions until the policy would end the turn (it never ends the turn itself).</summary>
        public int AutoBuild(int maxCommands = 32)
        {
            int seat = Match.State.CurrentSeat;
            int turn = Match.State.TurnNumber;
            int applied = 0;
            while (applied < maxCommands && !IsOver && !PassScreenPending
                   && Match.State.CurrentSeat == seat && Match.State.TurnNumber == turn)
            {
                var command = MatchAi.ChooseCommand(Match);
                if (command.Type == MatchCommandType.EndTurn)
                    break;
                if (!Apply(command).Ok)
                    break;
                applied++;
                if (Match.IsSetup)
                    break;
            }

            return applied;
        }

        private void RunAi()
        {
            if (!Current.IsAi || IsOver)
                return;
            MatchRunner.AdvanceAi(Match);
            AfterChange();
        }

        private void AfterChange()
        {
            var key = Key();
            if (key.seat != _turnKey.seat || key.turn != _turnKey.turn)
            {
                _turnKey = key;
                BeginTurn(force: false);
            }
            else
            {
                _turnKey = key;
            }

            if (IsOver)
                LastMessage = $"{Match.State.Seats[Match.State.Winner ?? 0].Name} wins!";
            Changed?.Invoke();
        }

        private void BeginTurn(bool force)
        {
            Clock.StartTurn();
            if (IsOver || Current.IsAi)
            {
                PassScreenPending = false;
                return;
            }

            // A pass screen whenever a different human takes the device (always for the first human turn
            // in a game with 2+ humans; never in a 1-human game, where nobody else is looking).
            int seat = Match.State.CurrentSeat;
            PassScreenPending = HumanCount >= 2 && seat != LastRevealedHumanSeat;
            if (!PassScreenPending)
                LastRevealedHumanSeat = seat;
            Clock.Paused = PassScreenPending;
            _ = force;
        }

        private (int seat, int turn, int step, MatchPhase phase) Key() =>
            (Match.State.CurrentSeat, Match.State.TurnNumber, Match.State.SetupStep, Match.State.Phase);
    }
}
