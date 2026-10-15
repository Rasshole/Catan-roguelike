using System.Collections;
using System.Collections.Generic;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Ai;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Turn;
using CatanRoguelike.Core.UI;
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
        private bool _aiPacingRunning;

        public bool IsBlockingInput => _sequenceRunning || _aiPacingRunning;

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

            if (_controller.State.Phase == GamePhase.DayAiTurn && !_aiPacingRunning)
                StartCoroutine(AiPacingSequence());
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
            bool isSeven = SevenRollRules.RolledSeven(state.TodayDiceRolls);
            var hexes = isSeven
                ? new List<HexCoord>()
                : ProductionHexResolver.GetProducingHexesForPlayer(state, PlayerId.Human, rolls);

            var before = state.PlayerInventory;

            yield return AnimateDice(state.TodayDiceRolls, isSeven);

            if (_boardView != null)
            {
                if (isSeven)
                    _boardView.SetHexDimAll(true);
                else
                    yield return _boardView.PlayProductionHighlight(hexes, HexPulseSeconds);
            }

            if (_hud != null)
                _hud.BeginResourceAnimationLock();

            _controller.ResolveDayProduction();

            if (!isSeven && _hud != null)
                yield return _hud.AnimateResourceCounts(before, _controller.State.PlayerInventory, ResourceTickSeconds);

            if (_boardView != null)
                _boardView.SetHexDimAll(false);

            _sequenceRunning = false;
        }

        private IEnumerator AiPacingSequence()
        {
            _aiPacingRunning = true;
            var plan = AiPacingPlanner.PlanDayActions(_controller.State);
            foreach (var step in plan)
            {
                GameLog.Append(_controller.State, $"AI: {step}");
                if (_hud != null)
                    _hud.Refresh(_controller.State);
                yield return new WaitForSeconds(Random.Range(1f, 3f));
            }

            _controller.CompleteAiDayTurn();
            _aiPacingRunning = false;
        }

        private IEnumerator AnimateDice(IReadOnlyList<int> finalRolls, bool glowSeven)
        {
            if (_hud != null)
                _hud.SetRollPromptActive(true);

            if (_hud == null || finalRolls == null || finalRolls.Count == 0)
            {
                yield return new WaitForSeconds(DiceAnimSeconds);
                if (_hud != null)
                    _hud.SetRollPromptActive(false);
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
                _hud.SetDiceOverlay(glowSeven ? $"🎲 7" : $"🎲 {finalRolls[0]}");
            else
                _hud.SetDiceOverlay($"🎲 {string.Join(" · ", finalRolls)}");

            yield return new WaitForSeconds(0.12f);
            _hud.SetDiceOverlay(string.Empty);
            _hud.SetRollPromptActive(false);
        }
    }
}
