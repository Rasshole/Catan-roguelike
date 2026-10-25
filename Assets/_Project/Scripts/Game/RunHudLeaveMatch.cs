using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>
    /// v143: "Leave match" from anywhere (pass &amp; play or single player, mid-turn, with popups open) returns to the
    /// start menu with a fresh run controller and a freshly built HUD canvas, so a new game starts from a clean slate.
    /// </summary>
    public sealed partial class RunHudController
    {
        /// <summary>True while a match is being played (hot-seat table or a single-player run past the menus).</summary>
        public bool InMatchForMenu =>
            _hotSeat != null || (_controller != null && !PlaceholderHudLayout.IsPreGamePhase(_controller.State.Phase));

        /// <summary>Leave the current match and go back to the map-select menu (deferred one frame: safe from onClick).</summary>
        public void LeaveMatchToMenu()
        {
            var gm = GetComponent<GameManager>();
            if (gm == null)
                gm = Object.FindFirstObjectByType<GameManager>();
            if (gm != null)
                gm.ReturnToMainMenu();
            else
                ExitHotSeat();
        }

        /// <summary>Tear down every match/overlay UI; <see cref="Initialize"/> then rebuilds the canvas from scratch.</summary>
        internal void ResetForMainMenu()
        {
            StopAllCoroutines();
            ExitHotSeatSilently();
            _hotSeatLobbyOpen = false;
            _mainMenuOpen = true;
            _hotSeatDisplay = null;
            _hsTradeOpen = false;
            _hsPicker = null;
            _hsPickSeat = -1;
            _hsVictimCommand = null;
            _hsPickHidden = false;
            _civPickHidden = false;
            _spCivInfoOpen = false;
            _rollPulseActive = false;
            _resourceAnimLock = false;
            _unlockPanelOpen = false;
            _spPickOpen = false;
            _inRunHudHidden = false;
            _runRewardGranted = false;
            _preGameKey = null;
            _civPickOfferKey = null;
            if (_canvas != null)
            {
                // Immediate: the rebuild below must not see half-destroyed widgets from the old match.
                Object.DestroyImmediate(_canvas.gameObject);
            }

            _canvas = null;
            _colonistLayoutBuilt = false;
            _preGameRoot = null;
            _civPickRoot = null;
            _hotSeatRoot = null;
            PaintedPreGameMenusVisible = false;
        }

        /// <summary>Scene unload mid-match: undo the table's static board-layout overrides so the next scene is clean.</summary>
        private void OnDestroy()
        {
            if (_hotSeat == null)
                return;
            ColonistPlayAreaLayout.Override = null;
            ColonistBoardVisualScale.RobberScaleCap = float.MaxValue;
            ColonistBoardVisualScale.RobberOffsetScale = 1f;
            PlayerPieceVisuals.BuildingScaleMultiplier = 1f;
            PlayerPieceVisuals.RoadScaleMultiplier = 1f;
            _hotSeat = null;
        }

        private void ExitHotSeatSilently()
        {
            if (_hotSeat == null)
                return;
            var controller = _controller;
            _controller = null; // skip ExitHotSeat's menu refresh: the caller swaps in a fresh controller next
            try
            {
                ExitHotSeat();
            }
            finally
            {
                _controller = controller;
            }
        }
    }
}
