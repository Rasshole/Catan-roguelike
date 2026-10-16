using CatanRoguelike.Game;
using NUnit.Framework;
using UnityEngine;

namespace CatanRoguelike.Tests
{
    public class NumberTokenLabelOrientationTests
    {
        private static readonly Vector3 LabelPosition = new(1.2f, 1.18f, -0.4f);

        [Test]
        public void ComputeWorldRotation_CameraOnNegativeZ_IsReadableFromCamera()
        {
            var cameraPosition = LabelPosition + new Vector3(0f, 5f, -8f);
            AssertReadableForCamera(LabelPosition, cameraPosition);
        }

        [Test]
        public void ComputeWorldRotation_CameraOnPositiveZ_IsReadableFromCamera()
        {
            var cameraPosition = LabelPosition + new Vector3(0f, 5f, 8f);
            AssertReadableForCamera(LabelPosition, cameraPosition);
        }

        [Test]
        public void ComputeWorldRotation_CameraOrbitsEast_IsReadableFromCamera()
        {
            var cameraPosition = LabelPosition + new Vector3(9f, 4f, 0f);
            AssertReadableForCamera(LabelPosition, cameraPosition);
        }

        [Test]
        public void ComputeWorldRotation_StaysFlatOnChip()
        {
            var cameraPosition = LabelPosition + new Vector3(2f, 6f, -7f);
            var rotation = NumberTokenLabelOrientation.ComputeWorldRotation(LabelPosition, cameraPosition);
            var up = rotation * Vector3.up;
            var forward = rotation * Vector3.forward;

            Assert.That(Mathf.Abs(up.y), Is.LessThan(0.01f));
            Assert.That(Mathf.Abs(forward.y), Is.GreaterThan(0.99f));
        }

        [Test]
        public void ComputeWorldRotation_DefaultTableSide_BeatsUnorientedBase()
        {
            var cameraPosition = LabelPosition + new Vector3(0f, 5f, -8f);
            var baseRotation = Quaternion.Euler(NumberTokenLabelOrientation.FlatPitchDegrees, 0f, 0f);
            var orientedRotation = NumberTokenLabelOrientation.ComputeWorldRotation(LabelPosition, cameraPosition);

            float baseScore = NumberTokenLabelOrientation.GetReadableAlignmentScore(
                baseRotation, LabelPosition, cameraPosition);
            float orientedScore = NumberTokenLabelOrientation.GetReadableAlignmentScore(
                orientedRotation, LabelPosition, cameraPosition);

            Assert.Greater(orientedScore, baseScore);
            Assert.IsTrue(NumberTokenLabelOrientation.IsReadableFromCamera(
                orientedRotation, LabelPosition, cameraPosition));
        }

        [Test]
        public void ComputeWorldRotation_TableOrbitCamera_MatchesNegativeZSide()
        {
            var cameraPosition = new Vector3(0f, 6.5f, -7.5f);
            AssertReadableForCamera(LabelPosition, cameraPosition);
            Assert.Less(cameraPosition.z, LabelPosition.z);
        }

        private static void AssertReadableForCamera(Vector3 labelPosition, Vector3 cameraPosition)
        {
            var rotation = NumberTokenLabelOrientation.ComputeWorldRotation(labelPosition, cameraPosition);
            Assert.IsTrue(NumberTokenLabelOrientation.IsReadableFromCamera(
                rotation, labelPosition, cameraPosition));
        }
    }
}
