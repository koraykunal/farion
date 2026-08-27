using Farion.Core.Identity;
using Farion.Multiplayer.World;
using FishNet.Managing.Scened;
using NUnit.Framework;
using UnityEngine.SceneManagement;

namespace Farion.Tests.EditMode
{
    public sealed class MultiplayerZoneSceneLoadTests
    {
        [Test]
        public void ZoneSceneLoadUsesConnectionStackingAndIsolatedPhysics()
        {
            GeneratedEntityId expected = new(0x1020304050607080UL);

            SceneLoadData data = MultiplayerZoneSceneLoad.Create(
                "SC_WorldZone",
                expected);

            Assert.That(data.Options.AllowStacking, Is.True);
            Assert.That(data.Options.AutomaticallyUnload, Is.False);
            Assert.That(data.Options.LocalPhysics, Is.EqualTo(LocalPhysicsMode.Physics3D));
            Assert.That(data.ReplaceScenes, Is.EqualTo(ReplaceOption.None));
            Assert.That(
                MultiplayerZoneSceneLoad.TryDecode(
                    data.Params.ClientParams,
                    out GeneratedEntityId decoded),
                Is.True);
            Assert.That(decoded, Is.EqualTo(expected));
        }

        [Test]
        public void ZoneSceneLoadReadsZoneIdWithoutServerParams()
        {
            GeneratedEntityId expected = new(0x1020304050607080UL);
            SceneLoadData data = MultiplayerZoneSceneLoad.Create(
                "SC_WorldZone",
                expected);
            data.Params.ServerParams = System.Array.Empty<object>();

            Assert.That(
                MultiplayerZoneSceneLoad.TryReadZoneId(
                    data,
                    out GeneratedEntityId decoded),
                Is.True);
            Assert.That(decoded, Is.EqualTo(expected));
        }

        [Test]
        public void ZoneSceneLoadRejectsMissingIdentity()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                MultiplayerZoneSceneLoad.Create(
                    "SC_WorldZone",
                    GeneratedEntityId.None));
            Assert.That(
                MultiplayerZoneSceneLoad.TryDecode(
                    new byte[sizeof(ulong)],
                    out GeneratedEntityId decoded),
                Is.False);
            Assert.That(decoded, Is.EqualTo(GeneratedEntityId.None));
        }
    }
}
