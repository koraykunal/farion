using System.Collections;
using System.Collections.Generic;
using Farion.Rendering.Celestial;
using Farion.Simulation.Physics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Farion.Tests.PlayMode
{
    public sealed class CelestialSurfacePatchPlayModeTests
    {
        [UnityTest]
        public IEnumerator GameplayShellHandsNonReferenceSurfaceCollisionOffWithoutAnAuthorityGap()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync(
                "SC_GameplayShell",
                LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            while (!load.isDone)
            {
                yield return null;
            }

            float zoneLoadDeadline = Time.realtimeSinceStartup + 90f;
            while (Time.realtimeSinceStartup < zoneLoadDeadline &&
                   !SceneManager.GetSceneByName("SC_WorldZone").isLoaded)
            {
                yield return null;
            }

            Assert.That(SceneManager.GetSceneByName("SC_WorldZone").isLoaded, Is.True);
            yield return null;

            GravitySimulation simulation =
                Object.FindAnyObjectByType<GravitySimulation>();
            Assert.That(simulation, Is.Not.Null);

            CelestialSurfacePatchSystem patchSystem = null;
            CelestialSurfacePatchSystem[] patchSystems =
                Object.FindObjectsByType<CelestialSurfacePatchSystem>();
            for (int i = 0; i < patchSystems.Length; i++)
            {
                CelestialBodyVisual candidateVisual =
                    patchSystems[i].GetComponent<CelestialBodyVisual>();
                if (candidateVisual != null &&
                    candidateVisual.Body != simulation.PhysicsReferenceBody)
                {
                    patchSystem = patchSystems[i];
                    break;
                }
            }

            Assert.That(patchSystem, Is.Not.Null);

            CelestialBodyVisual bodyVisual =
                patchSystem.GetComponent<CelestialBodyVisual>();
            Assert.That(bodyVisual, Is.Not.Null);
            Assert.That(bodyVisual.Body, Is.Not.Null);
            Assert.That(bodyVisual.Body.SupportsNonConvexSurfaceCollider, Is.True);
            Assert.That(bodyVisual.Body, Is.Not.SameAs(simulation.PhysicsReferenceBody));

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
            Vector3 farPosition =
                patchSystem.transform.position +
                surfaceDirection * (outerRadius + bodyVisual.Body.Radius * 1.1f);
            camera.transform.position = farPosition;
            collisionObserver.position = farPosition;
            collisionObserver.linearVelocity =
                bodyVisual.Body.GetVelocityAtPoint(farPosition);
            yield return null;
            for (int frame = 0;
                 frame < 60 &&
                 (patchSystem.SurfaceModeActive || patchSystem.PatchTransitionPending);
                 frame++)
            {
                yield return null;
            }

            yield return null;
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

            yield return null;
            Assert.That(patchSystem.SurfaceModeActive, Is.True);
            AssertSurfaceContractHolds(patchSystem, globalRenderer, globalCollider);

            Vector3 localSurfacePosition =
                patchSystem.transform.position +
                surfaceDirection * (outerRadius + 10f);
            camera.transform.position = localSurfacePosition;
            collisionObserver.position = localSurfacePosition;
            collisionObserver.linearVelocity =
                bodyVisual.Body.GetVelocityAtPoint(localSurfacePosition) +
                camera.transform.right * 500f;
            yield return null;

            AssertSurfaceContractHolds(patchSystem, globalRenderer, globalCollider);

            remainingFrames = 600;
            while (remainingFrames-- > 0)
            {
                KeepObserverAtAltitude(
                    patchSystem,
                    bodyVisual,
                    camera,
                    collisionObserver,
                    surfaceDirection,
                    outerRadius + 10f);
                yield return null;

                if (patchSystem.SurfaceModeActive &&
                    !patchSystem.PatchTransitionPending &&
                    patchSystem.LocalCollisionCoverageReady &&
                    patchSystem.ActiveColliderCount > 0)
                {
                    break;
                }
            }

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
                AssertGeomorphOffsetsAreCrackSafe(patchFilter.sharedMesh);
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
                Is.EqualTo(CelestialSurfaceCollisionAuthority.LocalAuthoritative));
            Assert.That(patchSystem.LocalCollisionCoverageReady, Is.True);
            Assert.That(patchSystem.ActiveColliderCount, Is.GreaterThan(0));
            Assert.That(globalCollider.enabled, Is.False);

            Vector3 farCameraPosition =
                patchSystem.transform.position +
                surfaceDirection * (outerRadius + bodyVisual.Body.Radius * 1.1f);

            remainingFrames = 60;
            while (patchSystem.SurfaceModeActive && remainingFrames-- > 0)
            {
                camera.transform.position = farCameraPosition;
                collisionObserver.position = farCameraPosition;
                collisionObserver.linearVelocity =
                    bodyVisual.Body.GetVelocityAtPoint(farCameraPosition);
                yield return null;
            }

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

        static void AssertGeomorphOffsetsAreCrackSafe(Mesh mesh)
        {
            List<Vector4> morphOffsets = new();
            List<Vector4> morphNormals = new();
            mesh.GetUVs(1, morphOffsets);
            mesh.GetUVs(2, morphNormals);
            Assert.That(
                morphOffsets.Count,
                Is.EqualTo(mesh.vertexCount),
                "patch meshes must carry one geomorph offset per vertex");
            Assert.That(
                morphNormals.Count,
                Is.EqualTo(mesh.vertexCount),
                "patch meshes must carry one geomorph normal per vertex");

            int rowSize = Mathf.RoundToInt(Mathf.Sqrt(mesh.vertexCount));
            Assert.That(rowSize * rowSize, Is.EqualTo(mesh.vertexCount));

            bool anyOffset = false;
            for (int y = 0; y < rowSize; y++)
            {
                for (int x = 0; x < rowSize; x++)
                {
                    Vector4 offset = morphOffsets[y * rowSize + x];
                    if ((x & 1) == 0 && (y & 1) == 0)
                    {
                        Assert.That(
                            new Vector3(offset.x, offset.y, offset.z).magnitude,
                            Is.LessThan(0.0001f),
                            "coarse grid vertices must not move while morphing");
                        continue;
                    }

                    anyOffset |= new Vector3(offset.x, offset.y, offset.z).sqrMagnitude > 0f;
                }
            }

            Assert.That(anyOffset, Is.True, "geomorph offsets must be populated");
        }

        static void AssertSurfaceContractHolds(
            CelestialSurfacePatchSystem patchSystem,
            MeshRenderer globalRenderer,
            MeshCollider globalCollider)
        {
            bool patchesOwnCollision = patchSystem.CollisionAuthority ==
                CelestialSurfaceCollisionAuthority.LocalAuthoritative;

            Assert.That(
                globalCollider.enabled,
                Is.EqualTo(!patchesOwnCollision),
                "primary terrain collision must be active exactly when patches do not own it");
            CelestialScaledSpaceVisual scaledSpace =
                patchSystem.GetComponent<CelestialScaledSpaceVisual>();
            if (scaledSpace == null || !scaledSpace.IsUsingScaledSpace)
            {
                Assert.That(
                    globalRenderer.enabled,
                    Is.EqualTo(!patchSystem.SurfaceRenderActive),
                    "primary terrain must render exactly when patches do not");
            }
            Assert.That(
                patchesOwnCollision || globalCollider.enabled,
                Is.True,
                "a collidable surface must exist at every moment");

            if (patchesOwnCollision)
            {
                Assert.That(patchSystem.ActiveColliderCount, Is.GreaterThan(0));
            }
        }

        static void DisableCameraDrivers(Camera camera)
        {
            camera.enabled = false;
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
