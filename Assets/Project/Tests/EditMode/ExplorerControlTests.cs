using Farion.Core.Physics;
using Farion.Gameplay.Character;
using Farion.Gameplay.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Utils;

namespace Farion.Tests.EditMode
{
    public sealed class ExplorerControlTests
    {
        [TestCase(30)]
        [TestCase(60)]
        [TestCase(120)]
        public void FullStickTurnsTheAuthoredDegreesPerSecondAtAnyFrameRate(int framesPerSecond)
        {
            float deltaTime = 1f / framesPerSecond;
            float turned = 0f;
            for (int i = 0; i < framesPerSecond; i++)
            {
                turned += FarionInputActions.ScaleLookDegrees(
                    Vector2.right,
                    mouse: false,
                    0.06f,
                    150f,
                    deltaTime).x;
            }

            Assert.That(turned, Is.EqualTo(150f).Within(0.01f));
        }

        [Test]
        public void MouseCountsTurnFixedDegreesRegardlessOfFrameTime()
        {
            Vector2 slow = FarionInputActions.ScaleLookDegrees(new Vector2(10f, -4f), true, 0.06f, 150f, 1f / 30f);
            Vector2 fast = FarionInputActions.ScaleLookDegrees(new Vector2(10f, -4f), true, 0.06f, 150f, 1f / 120f);
            Assert.That(slow, Is.EqualTo(new Vector2(0.6f, -0.24f)).Using(Vector2EqualityComparer.Instance));
            Assert.That(fast, Is.EqualTo(slow).Using(Vector2EqualityComparer.Instance));
        }

        [Test]
        public void CameraClearanceStopsInFrontOfObstaclesAndNeverGoesNegative()
        {
            GameObject wall = new("Wall") { layer = FarionLayers.CelestialSurface };
            try
            {
                wall.transform.position = new Vector3(0f, 0f, 2f);
                wall.AddComponent<BoxCollider>();
                Physics.SyncTransforms();
                PhysicsScene physics = Physics.defaultPhysicsScene;

                float blocked = ExplorerCameraRig.ClearDistance(
                    physics, Vector3.zero, Vector3.forward, 3.2f, 0.25f, 0.05f);
                float clear = ExplorerCameraRig.ClearDistance(
                    physics, Vector3.zero, Vector3.back, 3.2f, 0.25f, 0.05f);
                float touching = ExplorerCameraRig.ClearDistance(
                    physics, new Vector3(0f, 0f, 1.22f), Vector3.forward, 3.2f, 0.25f, 0.05f);

                Assert.That(blocked, Is.EqualTo(1.2f).Within(0.02f));
                Assert.That(clear, Is.EqualTo(3.2f));
                Assert.That(touching, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(wall);
            }
        }
    }
}
