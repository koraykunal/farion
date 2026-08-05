using System.Collections;
using Farion.Rendering.Celestial;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Farion.Tests.PlayMode
{
    public sealed class CelestialSurfacePatchPlayModeTests
    {
        [UnityTest]
        public IEnumerator ExpeditionHandsCollisionOffWithoutAnAuthorityGap()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync(
                "SC_Expedition",
                LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            while (!load.isDone)
            {
                yield return null;
            }

            yield return null;

            CelestialSurfacePatchSystem patchSystem =
                Object.FindAnyObjectByType<CelestialSurfacePatchSystem>();
            Assert.That(patchSystem, Is.Not.Null);

            CelestialBodyVisual bodyVisual =
                patchSystem.GetComponent<CelestialBodyVisual>();
            Assert.That(bodyVisual, Is.Not.Null);
            Assert.That(bodyVisual.Body, Is.Not.Null);
            Assert.That(bodyVisual.Body.SupportsNonConvexSurfaceCollider, Is.True);

            Camera camera = patchSystem.TargetCamera;
            Assert.That(camera, Is.Not.Null);
            DisableCameraDrivers(camera);

            Transform terrainMesh = patchSystem.transform.Find("Terrain Mesh");
            Assert.That(terrainMesh, Is.Not.Null);
            MeshRenderer globalRenderer = terrainMesh.GetComponent<MeshRenderer>();
            MeshCollider globalCollider = terrainMesh.GetComponent<MeshCollider>();
            Assert.That(globalRenderer, Is.Not.Null);
            Assert.That(globalCollider, Is.Not.Null);

            Rigidbody collisionObserver = patchSystem.CollisionObserverRigidbody;
            Assert.That(collisionObserver, Is.Not.Null);

            Vector3 surfaceDirection = patchSystem.transform.up;
            float outerRadius = bodyVisual.HasRenderRadiusRange
                ? bodyVisual.RenderRadiusMinMax.y
                : bodyVisual.Body.Radius;
            camera.transform.position =
                patchSystem.transform.position +
                surfaceDirection * (outerRadius + bodyVisual.Body.Radius * 1.1f);
            yield return null;
            yield return new WaitForEndOfFrame();

            Assert.That(patchSystem.SurfaceModeActive, Is.False);
            Assert.That(patchSystem.PatchTransitionPending, Is.False);
            Assert.That(
                patchSystem.CollisionAuthority,
                Is.EqualTo(CelestialSurfaceCollisionAuthority.GlobalFallback));
            Assert.That(globalRenderer.enabled, Is.True);
            Assert.That(globalCollider.enabled, Is.True);

            Vector3 preparationPosition =
                patchSystem.transform.position +
                surfaceDirection * (outerRadius + bodyVisual.Body.Radius * 0.5f);
            camera.transform.position = preparationPosition;
            collisionObserver.position = preparationPosition;
            collisionObserver.linearVelocity =
                bodyVisual.Body.GetVelocityAtPoint(preparationPosition);
            yield return null;

            int remainingFrames = 600;
            while (!patchSystem.SurfaceModeActive && remainingFrames-- > 0)
            {
                KeepObserverAtAltitude(
                    patchSystem,
                    bodyVisual,
                    camera,
                    collisionObserver,
                    surfaceDirection,
                    outerRadius + bodyVisual.Body.Radius * 0.5f);
                yield return null;
            }

            yield return new WaitForEndOfFrame();
            Assert.That(patchSystem.SurfaceModeActive, Is.True);
            Assert.That(
                patchSystem.CollisionAuthority,
                Is.EqualTo(CelestialSurfaceCollisionAuthority.GlobalFallback));
            Assert.That(patchSystem.ActiveColliderCount, Is.EqualTo(0));
            Assert.That(globalRenderer.enabled, Is.False);
            Assert.That(globalCollider.enabled, Is.True);

            Vector3 localSurfacePosition =
                patchSystem.transform.position +
                surfaceDirection * (outerRadius + 10f);
            camera.transform.position = localSurfacePosition;
            collisionObserver.position = localSurfacePosition;
            collisionObserver.linearVelocity =
                bodyVisual.Body.GetVelocityAtPoint(localSurfacePosition);
            yield return null;

            if (patchSystem.PatchTransitionPending)
            {
                Assert.That(globalCollider.enabled, Is.True);
                Assert.That(
                    patchSystem.CollisionAuthority,
                    Is.Not.EqualTo(CelestialSurfaceCollisionAuthority.LocalAuthoritative));
            }

            remainingFrames = 600;
            while ((!patchSystem.SurfaceModeActive ||
                    patchSystem.PatchTransitionPending ||
                    !patchSystem.LocalCollisionCoverageReady) &&
                   remainingFrames-- > 0)
            {
                KeepObserverAtAltitude(
                    patchSystem,
                    bodyVisual,
                    camera,
                    collisionObserver,
                    surfaceDirection,
                    outerRadius + 10f);
                yield return null;
            }

            yield return new WaitForEndOfFrame();

            Assert.That(patchSystem.SurfaceModeActive, Is.True);
            Assert.That(patchSystem.PatchTransitionPending, Is.False);
            Assert.That(patchSystem.ActivePatchCount, Is.GreaterThan(0));
            Assert.That(patchSystem.ActiveColliderCount, Is.GreaterThan(0));
            Assert.That(patchSystem.DeepestActiveLevel, Is.GreaterThan(0));
            Assert.That(
                patchSystem.CollisionAuthority,
                Is.EqualTo(CelestialSurfaceCollisionAuthority.LocalAuthoritative));
            Assert.That(patchSystem.LocalCollisionCoverageReady, Is.True);
            Assert.That(globalRenderer.enabled, Is.False);
            Assert.That(globalCollider.enabled, Is.False);

            Transform patchContainer =
                patchSystem.transform.Find("Adaptive Surface Patches");
            Assert.That(patchContainer, Is.Not.Null);
            MeshCollider[] patchColliders =
                patchContainer.GetComponentsInChildren<MeshCollider>(true);
            int enabledColliderCount = 0;
            for (int i = 0; i < patchColliders.Length; i++)
            {
                MeshCollider patchCollider = patchColliders[i];
                if (!patchCollider.enabled)
                {
                    continue;
                }

                enabledColliderCount++;
                MeshFilter patchFilter = patchCollider.GetComponent<MeshFilter>();
                Assert.That(patchFilter, Is.Not.Null);
                Assert.That(patchCollider.sharedMesh, Is.Not.Null);
                Assert.That(patchCollider.sharedMesh, Is.SameAs(patchFilter.sharedMesh));
            }

            Assert.That(
                enabledColliderCount,
                Is.EqualTo(patchSystem.ActiveColliderCount));

            Vector3 highSpeedDirection =
                Vector3.Cross(surfaceDirection, camera.transform.forward).normalized;
            if (highSpeedDirection.sqrMagnitude <= 0.0001f)
            {
                highSpeedDirection = camera.transform.right;
            }

            collisionObserver.linearVelocity =
                bodyVisual.Body.GetVelocityAtPoint(collisionObserver.position) +
                highSpeedDirection * 500f;
            yield return null;

            Assert.That(
                patchSystem.CollisionAuthority,
                Is.EqualTo(CelestialSurfaceCollisionAuthority.GlobalFallback));
            Assert.That(patchSystem.LocalCollisionCoverageReady, Is.False);
            Assert.That(patchSystem.ActiveColliderCount, Is.EqualTo(0));
            Assert.That(globalCollider.enabled, Is.True);

            camera.transform.position =
                patchSystem.transform.position +
                surfaceDirection * (outerRadius + bodyVisual.Body.Radius * 1.1f);

            yield return null;
            yield return new WaitForEndOfFrame();

            Assert.That(patchSystem.SurfaceModeActive, Is.False);
            Assert.That(
                patchSystem.CollisionAuthority,
                Is.EqualTo(CelestialSurfaceCollisionAuthority.GlobalFallback));
            Assert.That(globalRenderer.enabled, Is.True);
            Assert.That(globalCollider.enabled, Is.True);
            bodyVisual.SetLodLevel(0);
            Assert.That(
                globalCollider.sharedMesh,
                Is.SameAs(terrainMesh.GetComponent<MeshFilter>().sharedMesh));
        }

        static void DisableCameraDrivers(Camera camera)
        {
            MonoBehaviour[] behaviours = camera.GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour.GetType().FullName != "FMODUnity.StudioListener")
                {
                    behaviour.enabled = false;
                }
            }
        }

        static void KeepObserverAtAltitude(
            CelestialSurfacePatchSystem patchSystem,
            CelestialBodyVisual bodyVisual,
            Camera camera,
            Rigidbody collisionObserver,
            Vector3 surfaceDirection,
            float centerDistance)
        {
            Vector3 position =
                patchSystem.transform.position +
                surfaceDirection * centerDistance;
            camera.transform.position = position;
            collisionObserver.position = position;
            collisionObserver.linearVelocity =
                bodyVisual.Body.GetVelocityAtPoint(position);
        }
    }
}
