using System.Collections;
using Farion.Core.Persistence;
using Farion.Multiplayer.Session;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Farion.Tests.PlayMode
{
    public static class SoloSessionTestScope
    {
        const float ConnectTimeoutSeconds = 120f;
        const float StopTimeoutSeconds = 10f;

        public static IEnumerator Start(string slotName)
        {
            SaveGameFileService.DeleteSlot(slotName);
            SaveGameStartupRequest.RequestNewGame(slotName);
            yield return StartRequested();
        }

        public static IEnumerator StartFromSave(string slotName)
        {
            SaveGameStartupRequest.RequestLoad(slotName);
            yield return StartRequested();
        }

        static IEnumerator StartRequested()
        {
            Assert.That(
                MultiplayerSessionLauncher.TryCreateSession(
                    out MultiplayerSessionController session),
                Is.True);
            session.StartSolo();

            float deadline = Time.realtimeSinceStartup + ConnectTimeoutSeconds;
            while (session.State != MultiplayerSessionState.Connected &&
                   session.State != MultiplayerSessionState.Failed &&
                   Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(
                session.State,
                Is.EqualTo(MultiplayerSessionState.Connected),
                $"Solo session did not connect (failure: {session.FailureReason}).");
            Assert.That(SceneManager.GetSceneByName("SC_WorldZone").isLoaded, Is.True);
            yield return null;
        }

        public static IEnumerator Stop(string slotName, bool deleteSlot = true)
        {
            if (deleteSlot)
            {
                SaveGameFileService.DeleteSlot(slotName);
            }

            MultiplayerSessionController session = MultiplayerSessionController.Active;
            if (session == null)
            {
                yield break;
            }

            session.Stop();
            float deadline = Time.realtimeSinceStartup + StopTimeoutSeconds;
            while (MultiplayerSessionController.Active != null &&
                   Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
        }
    }
}
