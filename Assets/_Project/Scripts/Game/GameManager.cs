using CatanRoguelike.Core;
using CatanRoguelike.Core.Turn;
using UnityEngine;

namespace CatanRoguelike.Game
{
    public sealed class GameManager : MonoBehaviour
    {
        [SerializeField] private BoardView boardView;
        [SerializeField] private PlaceholderUI ui;
        [SerializeField] private int randomSeed = 42;
        [SerializeField] private bool useThirteenHexMap;

        public GameController Controller { get; private set; }

        private void Start()
        {
            Controller = new GameController(randomSeed, useThirteenHexMap);
            Controller.OnStateChanged += HandleStateChanged;

            boardView.Initialize(Controller);
            ui.Initialize(Controller);

            if (Controller.State.Phase == GamePhase.SetupAiSettlement1)
                Controller.RunAiSetupStep();
        }

        private void OnDestroy()
        {
            if (Controller != null)
                Controller.OnStateChanged -= HandleStateChanged;
        }

        private void HandleStateChanged(GameState state)
        {
            boardView.Refresh(state);
            ui.Refresh(state);
        }
    }
}
