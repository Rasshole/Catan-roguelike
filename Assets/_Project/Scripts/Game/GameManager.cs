using CatanRoguelike.Core;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Turn;
using UnityEngine;

namespace CatanRoguelike.Game
{
    public sealed class GameManager : MonoBehaviour
    {
        [SerializeField] private BoardView boardView;
        [SerializeField] private PlaceholderUI ui;
        [SerializeField] private BoardInputController boardInput;
        [SerializeField] private int randomSeed = 42;
        [SerializeField] private MapSize mapSize = MapSize.Small;

        public GameController Controller { get; private set; }

        private void Start()
        {
            Controller = new GameController(randomSeed, mapSize);
            Controller.OnStateChanged += HandleStateChanged;
            Controller.OnBoardRebuilt += HandleBoardRebuilt;

            boardView.Initialize(Controller);
            ui.Initialize(Controller, boardInput);

            if (boardInput != null)
                boardInput.Initialize(Controller, boardView);

            if (Controller.State.RunSetupComplete
                && Controller.State.Phase == GamePhase.SetupAiSettlement1)
                Controller.RunAiSetupStep();
        }

        private void OnDestroy()
        {
            if (Controller == null) return;
            Controller.OnStateChanged -= HandleStateChanged;
            Controller.OnBoardRebuilt -= HandleBoardRebuilt;
        }

        private void HandleStateChanged(GameState state)
        {
            boardView.Refresh(state);
            ui.Refresh(state);
        }

        private void HandleBoardRebuilt()
        {
            boardView.Rebuild(Controller);
        }
    }
}
