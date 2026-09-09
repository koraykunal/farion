using System.Collections.Generic;
using Farion.Core.Persistence;
using Farion.Gameplay.Definitions;
using Farion.Gameplay.Persistence;
using Farion.Gameplay.Session;
using Farion.Tests.Support;
using NUnit.Framework;
using UnityEngine;

namespace Farion.Tests.EditMode
{
    public sealed class GameplaySaveCoordinatorTests
    {
        const string Slot = "editmode-coordinator";

        sealed class FakeParticipant : IGameplaySaveParticipant
        {
            public bool CanCaptureResult = true;
            public bool CanApplyResult = true;
            public bool ApplyResult = true;
            public readonly List<string> Applied = new();

            public bool CanCapture(GameplaySaveContext context) => CanCaptureResult;

            public bool CanApply(GameplaySaveData saveData, GameplaySaveContext context) =>
                CanApplyResult;

            public void Capture(GameplaySaveCapture capture, GameplaySaveContext context)
            {
            }

            public bool Apply(GameplaySaveData saveData, GameplaySaveContext context)
            {
                Applied.Add(saveData.SavedAtUtc);
                return ApplyResult;
            }
        }

        sealed class FakeMultiplayerSource : IMultiplayerSaveSource
        {
            public string RejectedPlayerId = string.Empty;
            public int AppliedCount;

            public bool CanApply(
                IReadOnlyList<MultiplayerPlayerSaveEntry> players,
                IReadOnlyList<MultiplayerShipSaveEntry> shipCargo,
                GameplayDefinitionRegistry definitions)
            {
                for (int i = 0; players != null && i < players.Count; i++)
                {
                    if (players[i].PersistentPlayerId == RejectedPlayerId)
                    {
                        return false;
                    }
                }

                return true;
            }

            public void Capture(
                List<MultiplayerPlayerSaveEntry> players,
                List<MultiplayerShipSaveEntry> shipCargo)
            {
            }

            public void Apply(
                IReadOnlyList<MultiplayerPlayerSaveEntry> players,
                IReadOnlyList<MultiplayerShipSaveEntry> shipCargo,
                GameplayDefinitionRegistry definitions)
            {
                AppliedCount++;
            }
        }

        GameObject root;
        GameplaySaveCoordinator coordinator;
        FakeParticipant participant;
        FakeMultiplayerSource source;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("SaveCoordinatorTest");
            root.AddComponent<GameplayRuntimeRoot>();
            coordinator = root.AddComponent<GameplaySaveCoordinator>();
            participant = new FakeParticipant();
            source = new FakeMultiplayerSource();
            TestFieldAccess.SetField(
                coordinator,
                "saveParticipants",
                new IGameplaySaveParticipant[] { participant });
            coordinator.SetMultiplayerSource(source);
        }

        [TearDown]
        public void TearDown()
        {
            SaveGameFileService.DeleteSlot(Slot);
            Object.DestroyImmediate(root);
        }

        [Test]
        public void IncompatibleCurrentSaveFallsBackToTheBackup()
        {
            WriteSave("good", string.Empty);
            WriteSave("bad", "intruder");
            source.RejectedPlayerId = "intruder";

            SaveGameOperationResult result = coordinator.Load(Slot);

            Assert.That(result.Succeeded, Is.True, result.Status.ToString());
            Assert.That(result.Path, Does.EndWith(".bak"));
            Assert.That(participant.Applied, Is.EqualTo(new[] { "good" }));
            Assert.That(source.AppliedCount, Is.EqualTo(1));
        }

        [Test]
        public void IncompatibleCurrentAndBackupReportContentNotRuntime()
        {
            WriteSave("bad-1", "intruder");
            WriteSave("bad-2", "intruder");
            source.RejectedPlayerId = "intruder";

            SaveGameOperationResult result = coordinator.Load(Slot);

            Assert.That(result.Status, Is.EqualTo(SaveGameOperationStatus.IncompatibleContent));
            Assert.That(participant.Applied, Is.Empty);
        }

        [Test]
        public void FailedApplyRollsBackAndReportsApplyFailed()
        {
            WriteSave("target", string.Empty);
            participant.ApplyResult = false;

            SaveGameOperationResult result = coordinator.Load(Slot);

            Assert.That(result.Status, Is.EqualTo(SaveGameOperationStatus.ApplyFailed));
            Assert.That(participant.Applied.Count, Is.EqualTo(2));
            Assert.That(participant.Applied[0], Is.EqualTo("target"));
            Assert.That(participant.Applied[1], Is.Not.EqualTo("target"));
        }

        [Test]
        public void MissingRuntimeDoesNotReadOrApplyAnything()
        {
            WriteSave("target", string.Empty);
            participant.CanCaptureResult = false;

            SaveGameOperationResult result = coordinator.Load(Slot);

            Assert.That(result.Status, Is.EqualTo(SaveGameOperationStatus.MissingRuntimeReference));
            Assert.That(participant.Applied, Is.Empty);
        }

        [Test]
        public void TruncatedCurrentSaveFallsBackToTheBackup()
        {
            WriteSave("good", string.Empty);
            Assert.That(SaveGameFileService.WriteText(Slot, "{\"schemaVersion\": 9, ").Succeeded, Is.True);

            SaveGameOperationResult result = coordinator.Load(Slot);

            Assert.That(result.Succeeded, Is.True, result.Status.ToString());
            Assert.That(participant.Applied, Is.EqualTo(new[] { "good" }));
        }

        static void WriteSave(string savedAtUtc, string playerId)
        {
            GameplaySaveData data = new(savedAtUtc, 0d, null, default, null, null);
            List<MultiplayerPlayerSaveEntry> players = new();
            if (!string.IsNullOrEmpty(playerId))
            {
                players.Add(new MultiplayerPlayerSaveEntry(playerId, playerId, 0, null));
            }

            data.SetMultiplayerState(players, null);
            Assert.That(
                SaveGameFileService.WriteText(Slot, JsonUtility.ToJson(data)).Succeeded,
                Is.True);
        }
    }
}
