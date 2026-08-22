using System.Collections.Generic;
using Farion.Simulation.World;
using NUnit.Framework;
using UnityEngine;
using Farion.Tests.Support;

namespace Farion.Tests.EditMode
{
    public sealed class WorldOriginRebaserTests
    {
        GameObject owner;
        readonly List<GameObject> created = new();

        [SetUp]
        public void SetUp()
        {
            owner = new GameObject("World Origin Rebaser Test");
            created.Add(owner);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = created.Count - 1; i >= 0; i--)
            {
                if (created[i] != null)
                {
                    Object.DestroyImmediate(created[i]);
                }
            }

            created.Clear();
        }

        [Test]
        public void RebaseShiftsEveryRootAndAccumulatesTheOffset()
        {
            Transform bodies = CreateRoot("Bodies", new Vector3(100f, 0f, 0f));
            Transform actors = CreateRoot("Actors", new Vector3(0f, 50f, 0f));
            WorldOriginRebaser rebaser = CreateRebaser(bodies, actors);

            Vector3 offset = new(1000f, 0f, -2000f);
            rebaser.Rebase(offset);

            Assert.That(Vector3.Distance(bodies.position, new Vector3(100f, 0f, 0f) - offset), Is.LessThan(0.001f));
            Assert.That(Vector3.Distance(actors.position, new Vector3(0f, 50f, 0f) - offset), Is.LessThan(0.001f));
            Assert.That(Vector3.Distance(rebaser.AccumulatedOriginOffset, offset), Is.LessThan(0.001f));
            Assert.That(Vector3.Distance(rebaser.LastOriginOffset, offset), Is.LessThan(0.001f));
            Assert.That(rebaser.ShiftCount, Is.EqualTo(1));
        }

        [Test]
        public void RebasePreservesRelativePositionsBetweenRoots()
        {
            Transform bodies = CreateRoot("Bodies", new Vector3(100f, 0f, 0f));
            Transform actors = CreateRoot("Actors", new Vector3(160f, 0f, 0f));
            WorldOriginRebaser rebaser = CreateRebaser(bodies, actors);

            Vector3 before = actors.position - bodies.position;
            rebaser.Rebase(new Vector3(5000f, 5000f, 5000f));
            rebaser.Rebase(new Vector3(-1250f, 0f, 700f));

            Assert.That(Vector3.Distance(actors.position - bodies.position, before), Is.LessThan(0.001f));
            Assert.That(rebaser.ShiftCount, Is.EqualTo(2));
        }

        [Test]
        public void AccumulatedOffsetSumsEveryShift()
        {
            Transform bodies = CreateRoot("Bodies", Vector3.zero);
            WorldOriginRebaser rebaser = CreateRebaser(bodies);

            rebaser.Rebase(new Vector3(1000f, 0f, 0f));
            rebaser.Rebase(new Vector3(0f, 0f, 400f));

            Assert.That(
                Vector3.Distance(rebaser.AccumulatedOriginOffset, new Vector3(1000f, 0f, 400f)),
                Is.LessThan(0.001f));
        }

        [Test]
        public void NestedRootsAreShiftedOnlyOnce()
        {
            Transform parent = CreateRoot("Bodies", Vector3.zero);
            GameObject childObject = new("Nested");
            created.Add(childObject);
            childObject.transform.SetParent(parent);
            childObject.transform.position = new Vector3(10f, 0f, 0f);

            WorldOriginRebaser rebaser = CreateRebaser(parent, childObject.transform);
            rebaser.Rebase(new Vector3(100f, 0f, 0f));

            Assert.That(Vector3.Distance(childObject.transform.position, new Vector3(-90f, 0f, 0f)), Is.LessThan(0.001f));
        }

        [Test]
        public void ZeroOffsetIsIgnored()
        {
            Transform bodies = CreateRoot("Bodies", Vector3.zero);
            WorldOriginRebaser rebaser = CreateRebaser(bodies);

            rebaser.Rebase(Vector3.zero);

            Assert.That(rebaser.ShiftCount, Is.Zero);
            Assert.That(rebaser.AccumulatedOriginOffset, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void SnapshotRoundTripRestoresAccumulatedState()
        {
            Transform bodies = CreateRoot("Bodies", Vector3.zero);
            WorldOriginRebaser rebaser = CreateRebaser(bodies);
            rebaser.Rebase(new Vector3(2500f, 0f, 0f));

            WorldOriginSnapshot snapshot = rebaser.CaptureSnapshot();
            rebaser.ResetRuntimeState();
            Assert.That(rebaser.ShiftCount, Is.Zero);

            Assert.That(rebaser.RestoreSnapshot(snapshot), Is.True);
            Assert.That(rebaser.ShiftCount, Is.EqualTo(1));
            Assert.That(
                Vector3.Distance(rebaser.AccumulatedOriginOffset, new Vector3(2500f, 0f, 0f)),
                Is.LessThan(0.001f));
        }

        [Test]
        public void RebaseNotifiesSubscribersWithTheAppliedOffset()
        {
            Transform bodies = CreateRoot("Bodies", Vector3.zero);
            WorldOriginRebaser rebaser = CreateRebaser(bodies);

            Vector3 observed = Vector3.zero;
            int callCount = 0;
            rebaser.Rebased += value =>
            {
                observed = value;
                callCount++;
            };

            Vector3 offset = new(0f, 900f, 0f);
            rebaser.Rebase(offset);

            Assert.That(callCount, Is.EqualTo(1));
            Assert.That(Vector3.Distance(observed, offset), Is.LessThan(0.001f));
        }

        [Test]
        public void RuntimeShiftRootMovesWithAuthoredRoots()
        {
            Transform bodies = CreateRoot("Bodies", Vector3.zero);
            Transform cameraRig = CreateRoot("CameraRig", new Vector3(10f, 20f, 30f));
            WorldOriginRebaser rebaser = CreateRebaser(bodies);
            rebaser.RegisterShiftRoot(cameraRig);

            Vector3 offset = new(1000f, 2000f, -500f);
            rebaser.Rebase(offset);

            Assert.That(
                Vector3.Distance(cameraRig.position, new Vector3(10f, 20f, 30f) - offset),
                Is.LessThan(0.001f));
        }

        [Test]
        public void ExplicitTrackingPositionCanDriveCollectiveRebase()
        {
            Transform actors = CreateRoot("Actors", new Vector3(1500f, 0f, 0f));
            WorldOriginRebaser rebaser = CreateRebaser(actors);

            Assert.That(
                rebaser.RebaseIfNeeded(new Vector3(1500f, 0f, 0f)),
                Is.True);
            Assert.That(actors.position, Is.EqualTo(Vector3.zero));
        }

        Transform CreateRoot(string rootName, Vector3 position)
        {
            GameObject rootObject = new(rootName);
            created.Add(rootObject);
            rootObject.transform.position = position;
            return rootObject.transform;
        }

        WorldOriginRebaser CreateRebaser(params Transform[] roots)
        {
            WorldOriginRebaser rebaser = owner.AddComponent<WorldOriginRebaser>();
            TestFieldAccess.SetField(rebaser, "shiftedRoots", new List<Transform>(roots));
            rebaser.RefreshShiftRoots();
            return rebaser;
        }
    }
}
