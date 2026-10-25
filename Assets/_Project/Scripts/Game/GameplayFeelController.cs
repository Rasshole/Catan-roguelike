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
        private const float ResourceFlySeconds = 0.42f;

        public bool IsBlockingInput => _sequenceRunning || _aiPacingRunning;

        public void Initialize(GameController controller, BoardView boardView, RunHudController hud)
        {
            if (_controller != null)
                _controller.HumanBuildCostPaid -= HandleHumanBuildCostPaid;

            _controller = controller;
            _boardView = boardView;
            _hud = hud;

            if (_controller != null)
                _controller.HumanBuildCostPaid += HandleHumanBuildCostPaid;
        }

        private void OnDestroy()
        {
            if (_controller != null)
                _controller.HumanBuildCostPaid -= HandleHumanBuildCostPaid;
        }

        private void HandleHumanBuildCostPaid(ResourceBundle paid)
        {
            if (paid.Total <= 0)
                return;
            // v143 sounds for the single-player run too (cost shape tells what was bought).
            GameAudio.Play(paid.Wheat == 2 && paid.Stone == 3 ? Sfx.City
                : paid.Wood == 1 && paid.Brick == 1 && paid.Total == 2 ? Sfx.Road
                : paid.Wood == 1 && paid.Brick == 1 && paid.Wheat == 1 && paid.Sheep == 1 ? Sfx.Settlement
                : Sfx.DevBuy);
            if (_hud == null)
                return;
            StartCoroutine(_hud.PlaySpendToBankFlies(paid, ResourceFlySeconds));
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

            GameAudio.Play(Sfx.Dice);
            yield return AnimateDice(state.TodayDiceRolls, isSeven);
            if (isSeven)
                GameAudio.Play(Sfx.Robber);

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
            {
                var gained = SubtractBundles(_controller.State.PlayerInventory, before);
                var flySources = ProductionFlySources.MapGainedResourcesToHexes(
                    _controller.State,
                    PlayerId.Human,
                    rolls,
                    gained);
                if (flySources.Count > 0)
                    GameAudio.Play(Sfx.CardDraw);
                if (flySources.Count > 0)
                    yield return _hud.PlayProductionFlies(_boardView, flySources, ResourceFlySeconds);
                yield return _hud.AnimateResourceCounts(before, _controller.State.PlayerInventory, ResourceTickSeconds);
            }

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

        private static ResourceBundle SubtractBundles(ResourceBundle after, ResourceBundle before)
        {
            var result = ResourceBundle.Zero;
            result.Wood = Mathf.Max(0, after.Wood - before.Wood);
            result.Brick = Mathf.Max(0, after.Brick - before.Brick);
            result.Wheat = Mathf.Max(0, after.Wheat - before.Wheat);
            result.Sheep = Mathf.Max(0, after.Sheep - before.Sheep);
            result.Stone = Mathf.Max(0, after.Stone - before.Stone);
            return result;
        }
    }
}
