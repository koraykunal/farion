using Farion.Gameplay.Interaction;
using UnityEngine;

namespace Farion.Gameplay.ResourceNodes
{
    static class ResourceNodeFactory
    {
        const float MaximumTiltDegrees = 3f;

        public static bool TryCreate(
            ResourceDepositData deposit,
            Vector3 position,
            Quaternion rotation,
            Transform parent,
            ResourceDepositDeltaStore depositDeltaStore,
            out GameObject node)
        {
            node = null;
            if (deposit.Resource == null || !deposit.Resource.HasVisualPrefab)
            {
                return false;
            }

            node = Object.Instantiate(deposit.Resource.VisualPrefab, position, rotation, parent);
            return TryConfigure(node, deposit, position, rotation, parent, depositDeltaStore);
        }

        public static bool TryConfigure(
            GameObject node,
            ResourceDepositData deposit,
            Vector3 position,
            Quaternion rotation,
            Transform parent,
            ResourceDepositDeltaStore depositDeltaStore)
        {
            if (node == null || deposit.Resource == null)
            {
                return false;
            }

            node.transform.SetParent(parent, worldPositionStays: false);
            node.transform.SetPositionAndRotation(position, rotation);
            ApplyDepositVariation(node.transform, deposit.GenerationSeed);
            node.name = $"Resource Node - {deposit.Resource.DisplayName} ({deposit.DepositId})";
            if (!Application.isPlaying)
            {
                node.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            }

            ResourceNodeInteractable interactable = node.GetComponent<ResourceNodeInteractable>();
            if (interactable == null)
            {
                interactable = node.AddComponent<ResourceNodeInteractable>();
            }

            interactable.Configure(deposit.Resource, deposit.InitialReserve, deposit.GenerationSeed, deposit.DepositId, depositDeltaStore);
            EnsureInteractionCollider(node);
            ApplyResourceColor(node, deposit);
            node.SetActive(true);
            return true;
        }

        static void ApplyDepositVariation(Transform node, int generationSeed)
        {
            uint hash = Scramble(unchecked((uint)generationSeed));
            float yaw = (hash & 0xFFFFu) * (360f / 65536f);
            float pitch = (((hash >> 16) & 0xFFu) - 127.5f) * (MaximumTiltDegrees / 127.5f);
            float roll = (((hash >> 24) & 0xFFu) - 127.5f) * (MaximumTiltDegrees / 127.5f);
            node.Rotate(pitch, yaw, roll, Space.Self);
        }

        static uint Scramble(uint value)
        {
            unchecked
            {
                value ^= value >> 16;
                value *= 2246822519u;
                value ^= value >> 13;
                value *= 3266489917u;
                value ^= value >> 16;
                return value;
            }
        }

        static void EnsureInteractionCollider(GameObject node)
        {
            Collider[] colliders = node.GetComponentsInChildren<Collider>();
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null && colliders[i].isTrigger)
                {
                    return;
                }
            }

            node.AddComponent<SphereCollider>().isTrigger = true;
        }

        static void ApplyResourceColor(GameObject node, ResourceDepositData deposit)
        {
            if (!deposit.Resource.TintByBiome)
            {
                return;
            }

            Color color = deposit.Biome != null ? deposit.Biome.PreviewColor : Color.yellow;
            if (deposit.TerrainFeature != null)
            {
                color = Color.Lerp(color, deposit.TerrainFeature.PreviewColor, 0.35f);
            }

            color = Color.Lerp(color, Color.white, 0.25f);
            Renderer[] renderers = node.GetComponentsInChildren<Renderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                MaterialPropertyBlock block = new();
                renderer.GetPropertyBlock(block);
                block.SetColor("_BaseColor", color);
                block.SetColor("_Color", color);
                renderer.SetPropertyBlock(block);
            }
        }
    }
}
