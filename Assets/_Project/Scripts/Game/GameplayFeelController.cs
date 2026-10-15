using System.Collections;
using System.Collections.Generic;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Turn;
using CatanRoguelike.Core.Yield;
using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>Colonist-like roll, production pulse, and resource count tweens (no DOTween).</summary>
    public sealed class GameplayFeelController : MonoBehaviour
    {
        private const float DiceAnimSeconds = 0.5f;
        private const float HexPulseSeconds = 0.35f;
        private const float ResourceTickSeconds = 0.28f;

        private GameController _controller;
        private BoardView _boardView;
        private RunHudController _hud;
        private bool _sequenceRunning;
        private bool _rollQueued;

        public bool IsBlockingInput => _sequenceRunning;

        public void Initialize(GameController controller, BoardView boardView, RunHudController hud)
        {
            _controller = controller;
            _boardView = boardView;
            _hud = hud;
        }

        private void Update()
        {
            if (_controller == null || _sequenceRunning)
                return;

            if (_controller.State.Phase == GamePhase.DayProduction
                && (Input.GetKeyDown(KeyCode.Space) || _rollQueued))
            {
                _rollQueued = false;
                StartCoroutine(MorningRollSequence());
            }
        }

        public void RequestMorningRoll()
        {
            if (_controller == null || _sequenceRunning)
                return;
            if (_controller.State.Phase != GamePhase.DayProduction)
                return;
            _rollQueued = true;
        }

        private IEnumerator MorningRollSequence()
        {
            _sequenceRunning = true;
            var state = _controller.State;
            var rolls = new Dictionary<ResourceType, int>(state.TodayRolls);
            var preview = ProductionCalculator.CalculateForPlayer(state, PlayerId.Human, rolls);
            var hexes = ProductionHexResolver.GetProducingHexesForPlayer(state, PlayerId.Human, rolls);

            var before = state.PlayerInventory;
            int targetWood = before.Wood + preview.Wood;
            int targetBrick = before.Brick + preview.Brick;
            int targetWheat = before.Wheat + preview.Wheat;
            int targetSheep = before.Sheep + preview.Sheep;
            int targetStone = before.Stone + preview.Stone;

            yield return AnimateDice(state.TodayDiceRolls);

            var pulseRoutines = new List<Coroutine>();
            foreach (var hex in hexes)
            {
                if (_boardView != null)
                    pulseRoutines.Add(StartCoroutine(_boardView.PulseProductionHex(hex, HexPulseSeconds)));
            }

            foreach (var routine in pulseRoutines)
                yield return routine;

            if (_hud != null)
                _hud.BeginResourceAnimationLock();

            _controller.ResolveDayProduction();

            if (_hud != null)
            {
                yield return _hud.AnimateResourceCounts(
                    before,
                    _controller.State.PlayerInventory,
                    ResourceTickSeconds);
            }

            _sequenceRunning = false;
        }

        private IEnumerator AnimateDice(IReadOnlyList<int> finalRolls)
        {
            if (_hud == null || finalRolls == null || finalRolls.Count == 0)
            {
                yield return new WaitForSeconds(DiceAnimSeconds);
                yield break;
            }

            float elapsed = 0f;
            var rng = new System.Random(finalRolls.GetHashCode());
            while (elapsed < DiceAnimSeconds)
            {
                elapsed += Time.deltaTime;
                int fakeA = rng.Next(1, 7);
                int fakeB = rng.Next(1, 7);
                if (finalRolls.Count == 1)
                    _hud.SetDiceOverlay($"{fakeA}+{fakeB} = {finalRolls[0]}");
                else
                    _hud.SetDiceOverlay($"{fakeA}+{fakeB} · {fakeA}+{fakeB}");
                yield return null;
            }

            if (finalRolls.Count == 1)
                _hud.SetDiceOverlay($"🎲 {finalRolls[0]}");
            else
                _hud.SetDiceOverlay($"🎲 {string.Join(" · ", finalRolls)}");

            yield return new WaitForSeconds(0.12f);
            _hud.SetDiceOverlay(string.Empty);
        }
    }
}
