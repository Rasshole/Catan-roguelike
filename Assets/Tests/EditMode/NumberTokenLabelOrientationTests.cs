using CatanRoguelike.Game;
using NUnit.Framework;
using UnityEngine;

namespace CatanRoguelike.Tests
{
    public class NumberTokenLabelOrientationTests
    {
        private static readonly Vector3 LabelPosition = new(1.2f, 1.18f, -0.4f);

        [Test]
        public void ComputeWorldRotation_CameraOnNegativeZ_LabelUpPointsTowardCamera()
        {
            var cameraPosition = LabelPosition + new Vector3(0f, 5f, -8f);

            var rotation = NumberTokenLabelOrientation.ComputeWorldRotation(LabelPosition, cameraPosition);
            var toCamera = PlanarToCamera(LabelPosition, cameraPosition);

            AssertLabelFacesCamera(rotation, toCamera);
        }

        [Test]
        public void ComputeWorldRotation_CameraOnPositiveZ_LabelUpPointsTowardCamera()
        {
            var cameraPosition = LabelPosition + new Vector3(0f, 5f, 8f);

            var rotation = NumberTokenLabelOrientation.ComputeWorldRotation(LabelPosition, cameraPosition);
            var toCamera = PlanarToCamera(LabelPosition, cameraPosition);

            AssertLabelFacesCamera(rotation, toCamera);
        }

        [Test]
        public void ComputeWorldRotation_CameraOrbitsEast_LabelUpPointsTowardCamera()
        {
            var cameraPosition = LabelPosition + new Vector3(9f, 4f, 0f);

            var rotation = NumberTokenLabelOrientation.ComputeWorldRotation(LabelPosition, cameraPosition);
            var toCamera = PlanarToCamera(LabelPosition, cameraPosition);

            AssertLabelFacesCamera(rotation, toCamera);
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
        public void ComputeWorldRotation_DefaultTableSide_IsReadableNotBaseOrientation()
        {
            var cameraPosition = LabelPosition + new Vector3(0f, 5f, -8f);
            var baseRotation = Quaternion.Euler(NumberTokenLabelOrientation.FlatPitchDegrees, 0f, 0f);
            var orientedRotation = NumberTokenLabelOrientation.ComputeWorldRotation(LabelPosition, cameraPosition);
            var toCamera = PlanarToCamera(LabelPosition, cameraPosition);

            Assert.That(Vector3.Dot(baseRotation * Vector3.up, toCamera), Is.LessThan(0f));
            AssertLabelFacesCamera(orientedRotation, toCamera);
        }

        private static Vector3 PlanarToCamera(Vector3 labelPosition, Vector3 cameraPosition)
        {
            var toCamera = cameraPosition - labelPosition;
            toCamera.y = 0f;
            return toCamera.normalized;
        }

        private static void AssertLabelFacesCamera(Quaternion rotation, Vector3 planarToCamera)
        {
            var labelUp = rotation * Vector3.up;

            Assert.That(Mathf.Abs(labelUp.y), Is.LessThan(0.01f));
            Assert.That(Vector3.Dot(labelUp.normalized, planarToCamera), Is.GreaterThan(0.99f));
        }
    }
}
