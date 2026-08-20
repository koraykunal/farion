using Farion.Gameplay.Interaction;
using UnityEngine;

namespace Farion.Gameplay.ResourceNodes
{
    static class ResourceNodeFactory
    {
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

        static void EnsureInteractionCollider(GameObject node)
        {
            Collider collider = node.GetComponentInChildren<Collider>();
            if (collider == null)
            {
                collider = node.AddComponent<SphereCollider>();
            }

            collider.isTrigger = true;
        }

        static void ApplyResourceColor(GameObject node, ResourceDepositData deposit)
        {
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
