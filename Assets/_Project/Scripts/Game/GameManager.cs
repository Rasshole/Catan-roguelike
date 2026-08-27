using CatanRoguelike.Core;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Progression;
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
        public MetaProgression Meta { get; private set; }

        public void SaveMeta() => MetaProgressionFile.Save(Meta);

        public void SaveRun()
        {
            if (Controller != null)
                SaveGameFile.Save(Controller);
        }

        public bool TryLoadRun()
        {
            if (!SaveGameFile.TryLoad(out var loaded))
                return false;

            SwapController(loaded);
            return true;
        }

        private void SwapController(GameController newController)
        {
            if (Controller != null)
            {
                Controller.OnStateChanged -= HandleStateChanged;
                Controller.OnBoardRebuilt -= HandleBoardRebuilt;
            }

            Controller = newController;
            Controller.SetMeta(Meta);
            Controller.OnStateChanged += HandleStateChanged;
            Controller.OnBoardRebuilt += HandleBoardRebuilt;

            if (boardView != null)
                boardView.Initialize(Controller);
            if (ui != null)
                ui.Initialize(Controller, Meta, boardInput);
            if (boardInput != null)
                boardInput.Initialize(Controller, boardView);

            HandleBoardRebuilt();
            HandleStateChanged(Controller.State);
        }

        private void Start()
        {
            Meta = MetaProgressionFile.LoadOrCreate();
            Controller = new GameController(randomSeed, mapSize, Meta);
            Controller.OnStateChanged += HandleStateChanged;
            Controller.OnBoardRebuilt += HandleBoardRebuilt;

            boardView.Initialize(Controller);
            ui.Initialize(Controller, Meta, boardInput);

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

#if UNITY_EDITOR
        public int DebugSeed
        {
            get => randomSeed;
            set => randomSeed = value;
        }

        public MapSize DebugMapSize => mapSize;

        /// <summary>
        /// Editor-only replay: tear down the current controller and start a new
        /// seeded run. Stripped from player builds.
        /// </summary>
        public void DebugRestart(int seed)
        {
            randomSeed = seed;
            SwapController(new GameController(randomSeed, mapSize, Meta));
        }
#endif
    }
}
