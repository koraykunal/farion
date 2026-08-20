using Farion.Gameplay.Input;
using Farion.Gameplay.Inventory;
using Farion.Gameplay.Persistence;
using Farion.UI.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace Farion.Tests.EditMode
{
    public sealed class PlayabilityRegressionTests
    {
        static InventoryContainerSnapshot CreateSnapshot()
        {
            return new InventoryContainerSnapshot(
                "container.test",
                4,
                System.Array.Empty<InventoryStackSnapshot>());
        }

        [Test]
        public void ClimbingAwayFromTheSurfaceStaysWithinLandingLimits()
        {
            Assert.That(
                SpacecraftFlightHudGraphics.IsWithinLandingLimits(2.6f, 0.4f),
                Is.True);
        }

        [Test]
        public void DescendingFasterThanTheLimitLeavesLandingLimits()
        {
            Assert.That(
                SpacecraftFlightHudGraphics.IsWithinLandingLimits(-2.6f, 0.4f),
                Is.False);
        }

        [Test]
        public void ExcessiveLateralDriftLeavesLandingLimits()
        {
            Assert.That(
                SpacecraftFlightHudGraphics.IsWithinLandingLimits(-0.2f, 1.8f),
                Is.False);
        }

        [Test]
        public void InvertedLookFlipsOnlyTheVerticalAxis()
        {
            bool previous = FarionInputActions.InvertLookY;
            try
            {
                FarionInputActions.InvertLookY = false;
                Vector2 upright = FarionInputActions.ApplyLookInversion(
                    new Vector2(3f, 5f));
                Assert.That(upright.y, Is.EqualTo(5f).Within(0.0001f));

                FarionInputActions.InvertLookY = true;
                Vector2 inverted = FarionInputActions.ApplyLookInversion(
                    new Vector2(3f, 5f));
                Assert.That(inverted.x, Is.EqualTo(3f).Within(0.0001f));
                Assert.That(inverted.y, Is.EqualTo(-5f).Within(0.0001f));
            }
            finally
            {
                FarionInputActions.InvertLookY = previous;
            }
        }

        [Test]
        public void MouseSensitivityIsClampedToTheSupportedRange()
        {
            float previous = FarionInputActions.MouseSensitivityScale;
            try
            {
                FarionInputActions.MouseSensitivityScale = 99f;
                Assert.That(
                    FarionInputActions.MouseSensitivityScale,
                    Is.EqualTo(FarionInputActions.MaximumMouseSensitivity)
                        .Within(0.0001f));

                FarionInputActions.MouseSensitivityScale = -5f;
                Assert.That(
                    FarionInputActions.MouseSensitivityScale,
                    Is.EqualTo(FarionInputActions.MinimumMouseSensitivity)
                        .Within(0.0001f));
            }
            finally
            {
                FarionInputActions.MouseSensitivityScale = previous;
            }
        }

        [Test]
        public void FieldOfViewPreferenceIsClampedAndOffsetsAuthoredValue()
        {
            float previous = FarionViewPreferences.FieldOfView;
            try
            {
                FarionViewPreferences.FieldOfView = 500f;
                Assert.That(
                    FarionViewPreferences.FieldOfView,
                    Is.EqualTo(FarionViewPreferences.MaximumFieldOfView)
                        .Within(0.0001f));

                FarionViewPreferences.FieldOfView =
                    FarionViewPreferences.ReferenceFieldOfView + 10f;
                Assert.That(
                    FarionViewPreferences.ResolveFieldOfView(70f),
                    Is.EqualTo(80f).Within(0.0001f));
            }
            finally
            {
                FarionViewPreferences.FieldOfView = previous;
            }
        }

        [Test]
        public void ShipCargoEntryKeepsItsOwnerAcrossFormationSlots()
        {
            MultiplayerShipCargoSaveEntry owned = new(
                "player-a",
                2,
                CreateSnapshot());

            Assert.That(owned.HasOwner, Is.True);
            Assert.That(owned.PersistentPlayerId, Is.EqualTo("player-a"));
            Assert.That(owned.IsValid, Is.True);
        }

        [Test]
        public void LegacyShipCargoEntryWithoutOwnerStaysValidThroughItsSlot()
        {
            MultiplayerShipCargoSaveEntry legacy = new(
                string.Empty,
                1,
                CreateSnapshot());

            Assert.That(legacy.HasOwner, Is.False);
            Assert.That(legacy.IsValid, Is.True);
        }

        [Test]
        public void ShipCargoEntryWithoutOwnerOrSlotIsRejected()
        {
            MultiplayerShipCargoSaveEntry orphan = new(
                string.Empty,
                -1,
                CreateSnapshot());

            Assert.That(orphan.IsValid, Is.False);
        }
    }
}
