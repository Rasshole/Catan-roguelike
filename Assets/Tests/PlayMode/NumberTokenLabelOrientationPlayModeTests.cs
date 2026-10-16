using System.Collections;
using System.IO;
using CatanRoguelike.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CatanRoguelike.Tests.PlayMode
{
    public class NumberTokenLabelOrientationPlayModeTests
    {
        private const string ColonistScenePath = "Assets/_Project/Scenes/ColonistStylePrototype.unity";
        private const string ColonistSceneName = "ColonistStylePrototype";

        [Timeout(60000)]
        [UnityTest]
        public IEnumerator NumberTokens_AreReadable_FromTableCameraPose()
        {
            if (!File.Exists(ColonistScenePath))
            {
                Assert.Inconclusive($"Colonist prototype not found at {ColonistScenePath}.");
                yield break;
            }

            var loadOp = SceneManager.LoadSceneAsync(ColonistSceneName, LoadSceneMode.Single);
            Assert.IsNotNull(loadOp);
            yield return loadOp;

            for (int i = 0; i < 6; i++)
                yield return null;

            var tableCamera = Object.FindFirstObjectByType<TableCamera>();
            Assert.IsNotNull(tableCamera, "Colonist prototype should include TableCamera.");

            var camera = Camera.main;
            Assert.IsNotNull(camera, "TableCamera should drive Camera.main.");

            NumberTokenLabelOrientation.RefreshAllLabelsForCamera(camera);

            var facings = Object.FindObjectsByType<FlatTokenLabelFacing>(FindObjectsSortMode.None);
            Assert.Greater(facings.Length, 0, "Board should spawn number token labels.");

            int upright = 0;
            for (int i = 0; i < facings.Length; i++)
            {
                var label = facings[i];
                if (NumberTokenLabelOrientation.IsLabelUprightForCamera(label.transform, camera))
                    upright++;
            }

            Assert.Greater(upright, facings.Length / 2,
                "Most number tokens should be upright vs colonist top-down camera.up.");
        }
    }
}
