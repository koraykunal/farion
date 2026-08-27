using Farion.Core.Identity;
using System;
using Farion.Simulation.World;
using FishNet.Managing.Scened;
using UnityEngine.SceneManagement;

namespace Farion.Multiplayer.World
{
    public static class MultiplayerZoneSceneLoad
    {
        const int EncodedZoneIdSize = sizeof(ulong);

        public static SceneLoadData Create(
            string sceneName,
            GeneratedEntityId zoneId)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                throw new ArgumentException(
                    "A zone scene name is required.",
                    nameof(sceneName));
            }

            if (!zoneId.IsValid)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(zoneId),
                    "A persistent zone id is required.");
            }

            return Create(new SceneLookupData(sceneName), zoneId);
        }

        public static SceneLoadData Create(
            Scene scene,
            GeneratedEntityId zoneId)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                throw new ArgumentException(
                    "An existing loaded zone scene is required.",
                    nameof(scene));
            }

            if (!zoneId.IsValid)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(zoneId),
                    "A persistent zone id is required.");
            }

            return Create(new SceneLookupData(scene), zoneId);
        }

        static SceneLoadData Create(
            SceneLookupData lookup,
            GeneratedEntityId zoneId)
        {
            SceneLoadData data = new(lookup)
            {
                ReplaceScenes = ReplaceOption.None,
                PreferredActiveScene = new PreferredScene(lookup, null)
            };
            data.Options.AllowStacking = true;
            data.Options.AutomaticallyUnload = false;
            data.Options.LocalPhysics = LocalPhysicsMode.Physics3D;
            data.Params.ServerParams = new object[] { zoneId };
            data.Params.ClientParams = Encode(zoneId);
            return data;
        }

        public static bool TryReadZoneId(
            SceneLoadEndEventArgs args,
            out GeneratedEntityId zoneId)
        {
            return TryReadZoneId(args.QueueData?.SceneLoadData, out zoneId);
        }

        public static bool TryReadZoneId(
            SceneLoadData data,
            out GeneratedEntityId zoneId)
        {
            if (data == null)
            {
                zoneId = GeneratedEntityId.None;
                return false;
            }

            if (data.Params?.ServerParams != null &&
                data.Params.ServerParams.Length > 0 &&
                data.Params.ServerParams[0] is GeneratedEntityId serverZoneId &&
                serverZoneId.IsValid)
            {
                zoneId = serverZoneId;
                return true;
            }

            return TryDecode(data.Params?.ClientParams, out zoneId);
        }

        public static bool TryDecode(
            byte[] bytes,
            out GeneratedEntityId zoneId)
        {
            if (bytes == null || bytes.Length != EncodedZoneIdSize)
            {
                zoneId = GeneratedEntityId.None;
                return false;
            }

            ulong value = 0UL;
            for (int i = 0; i < EncodedZoneIdSize; i++)
            {
                value |= (ulong)bytes[i] << (i * 8);
            }

            if (value == 0UL)
            {
                zoneId = GeneratedEntityId.None;
                return false;
            }

            zoneId = new GeneratedEntityId(value);
            return true;
        }

        static byte[] Encode(GeneratedEntityId zoneId)
        {
            byte[] bytes = new byte[EncodedZoneIdSize];
            for (int i = 0; i < EncodedZoneIdSize; i++)
            {
                bytes[i] = (byte)(zoneId.Value >> (i * 8));
            }

            return bytes;
        }
    }
}
