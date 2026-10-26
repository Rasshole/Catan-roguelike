using CatanRoguelike.Core;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Progression;
using CatanRoguelike.Core.Turn;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CatanRoguelike.Game
{
    public sealed class GameManager : MonoBehaviour
    {
        [SerializeField] private BoardView boardView;
        [SerializeField] private PlaceholderUI ui;
        [SerializeField] private BoardInputController boardInput;
        private RunHudController _runHud;
        private GameplayFeelController _feel;
        [SerializeField] private int randomSeed = 42;
        [SerializeField] private MapSize mapSize = MapSize.Large;

        public GameController Controller { get; private set; }
        public MetaProgression Meta { get; private set; }

        private void Awake()
        {
            if (GetComponent<ColonistPresentationMode>() != null)
                return;

            var sceneName = SceneManager.GetActiveScene().name;
            if (sceneName == "ColonistStylePrototype" || sceneName == "Game")
                ColonistPresentation.SetActive(true);
        }

        public void SaveMeta() => MetaProgressionFile.Save(Meta);

        public void SaveRun(int slotIndex = 0)
        {
            if (Controller != null)
                SaveGameFile.Save(Controller, slotIndex);
        }

        public bool TryLoadRun(int slotIndex = 0)
        {
            if (!SaveGameFile.TryLoad(out var loaded, slotIndex))
                return false;

            SwapController(loaded);
            return true;
        }

        public void AutosaveRun()
        {
            if (Controller != null)
                SaveGameFile.Autosave(Controller);
        }

        private void SwapController(GameController newController)
        {
            if (Controller != null)
            {
                Controller.OnStateChanged -= HandleStateChanged;
                Controller.OnBoardRebuilt -= HandleBoardRebuilt;
                Controller.OnAutosavePoint -= HandleAutosavePoint;
            }

            Controller = newController;
            Controller.SetMeta(Meta);
            Controller.UnlockAll = true; // v143: everything playable (loaded runs too)
            Controller.OnStateChanged += HandleStateChanged;
            Controller.OnBoardRebuilt += HandleBoardRebuilt;
            Controller.OnAutosavePoint += HandleAutosavePoint;

            if (boardView != null)
                boardView.Initialize(Controller);
            if (ui != null)
                ui.Initialize(Controller, Meta, boardInput);
            EnsureRunHud();
            EnsureFeel();
            _runHud.Initialize(Controller, Meta, boardInput);
            _feel.Initialize(Controller, boardView, _runHud);
            _runHud.BindFeel(_feel);
            if (boardInput != null)
                boardInput.Initialize(Controller, boardView);

            HandleBoardRebuilt();
            HandleStateChanged(Controller.State);
        }

        private int _menuReturns;
        private bool _returnQueued;

        /// <summary>
        /// v143: leave the current match (pass &amp; play or single player) and go back to the map-select menu with a
        /// fresh controller. Deferred to the next frame so it is safe to call from a button's onClick.
        /// </summary>
        public void ReturnToMainMenu()
        {
            if (_returnQueued)
                return;
            _returnQueued = true;
            StartCoroutine(ReturnNextFrame());
        }

        private System.Collections.IEnumerator ReturnNextFrame()
        {
            yield return null;
            _returnQueued = false;
            ReturnToMainMenuNow();
        }

        /// <summary>v143: immediate variant of <see cref="ReturnToMainMenu"/> (tests, non-UI callers).</summary>
        public void ReturnToMainMenuNow()
        {
            EnsureRunHud();
            _runHud.ResetForMainMenu();
            if (boardInput != null)
            {
                boardInput.CancelBuildMode();
                boardInput.enabled = true;
            }

            if (_feel != null)
                _feel.StopAllCoroutines();
            _menuReturns++;
            var bootMapSize = ColonistPresentation.IsActive ? MapSize.Large : mapSize;
            SwapController(new GameController(randomSeed + _menuReturns * 7919, bootMapSize, Meta)
            {
                CivilizationPickEnabled = true,
                UnlockAll = true
            });
        }

        private void EnsureRunHud()
        {
            _runHud = GetComponent<RunHudController>();
            if (_runHud == null)
                _runHud = gameObject.AddComponent<RunHudController>();
        }

        /// <summary>v143: the "Menu" popup (volume, mute, leave match).</summary>
        public GameSettingsMenu SettingsMenu { get; private set; }

        private void EnsureSettingsMenu()
        {
            SettingsMenu = GetComponent<GameSettingsMenu>();
            if (SettingsMenu == null)
                SettingsMenu = gameObject.AddComponent<GameSettingsMenu>();
            SettingsMenu.Bind(_runHud);
        }

        private void EnsureFeel()
        {
            _feel = GetComponent<GameplayFeelController>();
            if (_feel == null)
                _feel = gameObject.AddComponent<GameplayFeelController>();
        }

        private void Start()
        {
            Meta = MetaProgressionFile.LoadOrCreate();
            var bootMapSize = ColonistPresentation.IsActive ? MapSize.Large : mapSize;
            Controller = new GameController(randomSeed, bootMapSize, Meta) { CivilizationPickEnabled = true, UnlockAll = true };
            Controller.OnStateChanged += HandleStateChanged;
            Controller.OnBoardRebuilt += HandleBoardRebuilt;
            Controller.OnAutosavePoint += HandleAutosavePoint;

            boardView.Initialize(Controller);
            ui.Initialize(Controller, Meta, boardInput);
            EnsureRunHud();
            EnsureFeel();
            _runHud.Initialize(Controller, Meta, boardInput);
            _feel.Initialize(Controller, boardView, _runHud);
            _runHud.BindFeel(_feel);
            EnsureSettingsMenu();

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
            Controller.OnAutosavePoint -= HandleAutosavePoint;
        }

        private void HandleAutosavePoint() => AutosaveRun();

        private void HandleStateChanged(GameState state)
        {
            if (boardView != null)
                boardView.Refresh(state);
            if (ui != null)
                ui.Refresh(state);
            if (_runHud != null)
                _runHud.Refresh(state);
        }

        /// <summary>PlayMode/capture: ensure uGUI run HUD is built and synced to current state.</summary>
        public void EnsureRunHudReady()
        {
            EnsureRunHud();
            if (_runHud == null || Controller == null || Meta == null)
                return;
            _runHud.Initialize(Controller, Meta, boardInput);
            _runHud.Refresh(Controller.State);
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
            SwapController(new GameController(randomSeed, mapSize, Meta) { CivilizationPickEnabled = true, UnlockAll = true });
        }

        /// <summary>
        /// Editor/batchmode bootstrap without Play Mode. Mirrors <see cref="Start"/> so
        /// GameScenePlayHarness can drive setup while the scene stays in edit mode.
        /// </summary>
        public void EditorBootstrapForCapture()
        {
            if (Controller != null)
                return;

            Meta = MetaProgressionFile.LoadOrCreate();
            Controller = new GameController(randomSeed, mapSize, Meta) { CivilizationPickEnabled = true, UnlockAll = true };
            Controller.OnStateChanged += HandleStateChanged;
            Controller.OnBoardRebuilt += HandleBoardRebuilt;
            Controller.OnAutosavePoint += HandleAutosavePoint;

            if (boardView != null)
                boardView.Initialize(Controller);
            if (ui != null)
                ui.Initialize(Controller, Meta, boardInput);
            EnsureRunHud();
            _runHud.Initialize(Controller, Meta, boardInput);
            if (boardInput != null)
                boardInput.Initialize(Controller, boardView);
        }

        public RunHudController EditorRunHud => _runHud;

        public void EditorRefreshRunHud()
        {
            EnsureRunHud();
            if (_runHud != null && Controller != null)
                _runHud.Refresh(Controller.State);
        }
#endif
    }
}
