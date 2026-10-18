using CatanRoguelike.Core;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Game;
using NUnit.Framework;

namespace CatanRoguelike.Tests
{
    public class ColonistCapturePieceSeedingTests
    {
        [Test]
        public void ForceDemoBoardPieces_MatchesExpectedCounts_OnLargeMapSeed42()
        {
            bool wasActive = ColonistPresentation.IsActive;
            ColonistPresentation.SetActive(true);
            try
            {
                var state = new GameState(MapPresets.CreateBoard(MapSize.Large, seed: 42));
                ColonistCapturePieceSeeding.ForceDemoBoardPiecesForCapture(state);
                Assert.IsTrue(
                    ColonistCapturePieceSeeding.MatchesExpectedDemoCounts(state.Board),
                    "Capture demo should place 2+1 human buildings and 2 AI settlements with 4+3 roads.");
            }
            finally
            {
                ColonistPresentation.SetActive(wasActive);
            }
        }
    }
}
