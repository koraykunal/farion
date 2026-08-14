using Farion.Core.Identity;
using System.Collections.Generic;
using Farion.Gameplay.Commands;
using Farion.Gameplay.Definitions;
using Farion.Gameplay.Interaction;
using Farion.Gameplay.Inventory;
using Farion.Gameplay.Resources;
using Farion.Gameplay.Session;
using Farion.Multiplayer.Player;
using Farion.Multiplayer.Spawning;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Farion.Multiplayer.Session
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkSessionPlayer))]
    public sealed class NetworkGameplayCommands :
        NetworkBehaviour,
        IGameplayCommandGateway
    {
        [SerializeField, Min(0.1f)] float maximumHarvestDistance = 6f;

        NetworkSessionPlayer sessionPlayer;
        InventoryContainerComponent ownerInventory;
        GameplayDefinitionRegistry ownerDefinitions;

        void Awake()
        {
            sessionPlayer = GetComponent<NetworkSessionPlayer>();
        }

        public void BindOwnerExplorer(
            NetworkExplorerController player,
            GameplayRuntimeBindings bindings)
        {
            if (!IsOwner || player == null)
            {
                return;
            }

            ownerInventory =
                player.GetComponentInChildren<InventoryContainerComponent>(true);
            ownerDefinitions = bindings?.Definitions;
            PlayerInteractionRaycaster raycaster =
                player.GetComponentInChildren<PlayerInteractionRaycaster>(true);
            raycaster?.SetCommandGateway(this);
            RequestResourceSnapshotServerRpc();
        }

        public ResourceHarvestResult CanHarvest(ResourceHarvestRequest request)
        {
            if (!request.DepositId.IsValid)
            {
                return ResourceHarvestResult.MissingSource;
            }

            if (ownerInventory == null || !request.DestinationContainerId.IsValid)
            {
                return ResourceHarvestResult.MissingDestination;
            }

            if (ownerInventory.ContainerId != request.DestinationContainerId)
            {
                return ResourceHarvestResult.UnauthorizedDestination;
            }

            if (ownerInventory.Revision != request.ExpectedDestinationRevision)
            {
                return ResourceHarvestResult.StaleDestination;
            }

            return TryFindLoadedNode(request.DepositId, out ResourceNodeInteractable node)
                ? ResourceHarvestTransaction.CanExecute(node, ownerInventory)
                : ResourceHarvestResult.MissingSource;
        }

        public ResourceHarvestResult TryHarvest(ResourceHarvestRequest request)
        {
            if (!IsOwner)
            {
                return ResourceHarvestResult.MissingDestination;
            }

            ResourceHarvestResult validation = CanHarvest(request);
            if (validation != ResourceHarvestResult.Succeeded)
            {
                return validation;
            }

            HarvestServerRpc(request.DepositId.Value);
            return ResourceHarvestResult.Pending;
        }

        // ponytail: cargo and processing go networked in Faz 1 once the claimed
        // NetworkStarterShip exposes a per-player ShuttleRuntimeBinding.
        public CargoTransferResult CanLoadAssignedShuttleCargo(
            CargoTransferRequest request) => CargoTransferResult.MissingSource;

        public CargoTransferResult TryLoadAssignedShuttleCargo(
            CargoTransferRequest request) => CargoTransferResult.MissingSource;

        public CargoTransferResult CanUnloadAssignedShuttleCargo(
            CargoTransferRequest request) => CargoTransferResult.MissingSource;

        public CargoTransferResult TryUnloadAssignedShuttleCargo(
            CargoTransferRequest request) => CargoTransferResult.MissingSource;

        public FleetProcessingResult CanProcessFleetRecipe(
            FleetProcessingRequest request) => FleetProcessingResult.MissingStorage;

        public FleetProcessingResult TryProcessFleetRecipe(
            FleetProcessingRequest request) => FleetProcessingResult.MissingStorage;

        [ServerRpc]
        void HarvestServerRpc(
            ulong depositId,
            NetworkConnection sender = null)
        {
            if (sender == null ||
                !sender.IsActive ||
                sender.ClientId != OwnerId ||
                depositId == 0UL ||
                sessionPlayer.PossessionMode != PlayerPossessionMode.OnFoot ||
                !TryResolveServerContext(
                    out GameplayRuntimeBindings bindings,
                    out NetworkExplorerController explorer,
                    out InventoryContainerComponent destinationInventory))
            {
                return;
            }

            GeneratedEntityId resolvedDepositId = new(depositId);
            if (!TryResolveHarvestNode(
                    bindings,
                    explorer.transform.position,
                    resolvedDepositId,
                    maximumHarvestDistance,
                    out ResourceNodeInteractable node))
            {
                HarvestResultTargetRpc(
                    sender,
                    (byte)ResourceHarvestResult.MissingSource,
                    string.Empty);
                return;
            }

            // The client can't track the server's revision (snapshots carry no
            // revision), so the server always addresses its own inventory at
            // its own current revision instead of trusting anything from the
            // wire.
            ResourceHarvestResult result =
                ResourceHarvestTransaction.TryExecute(node, destinationInventory);
            if (result != ResourceHarvestResult.Succeeded)
            {
                Debug.LogWarning(
                    $"NetworkGameplayCommands: harvest failed for deposit {depositId} with result {result}.");
            }

            string snapshotJson = result == ResourceHarvestResult.Succeeded
                ? JsonUtility.ToJson(destinationInventory.CaptureContainerSnapshot())
                : string.Empty;
            HarvestResultTargetRpc(sender, (byte)result, snapshotJson);
            if (result == ResourceHarvestResult.Succeeded)
            {
                ResourceDeltaObserversRpc(
                    depositId,
                    node.InitialQuantity - node.RemainingQuantity);
            }
        }

        [ObserversRpc]
        void ResourceDeltaObserversRpc(ulong depositId, int extractedAmount)
        {
            ApplyResourceDeltaToLoadedScenes(
                new GeneratedEntityId(depositId),
                extractedAmount);
        }

        [ServerRpc]
        void RequestResourceSnapshotServerRpc(NetworkConnection sender = null)
        {
            NetworkPlayerSpawner spawner = sessionPlayer.PlayerSpawner;
            if (sender == null ||
                !sender.IsActive ||
                sender.ClientId != OwnerId ||
                spawner == null ||
                !spawner.TryGetRuntimeBindings(
                    out GameplayRuntimeBindings bindings))
            {
                return;
            }

            List<ResourceDepositDeltaSnapshot> snapshots = new();
            foreach (ResourceDepositRuntimeSpawner streamer in
                     bindings.ResourceStreamers)
            {
                if (streamer != null)
                {
                    streamer.CaptureDeltaSnapshot(snapshots);
                }
            }

            foreach (ResourceDepositDeltaSnapshot snapshot in snapshots)
            {
                ResourceDeltaTargetRpc(
                    sender,
                    snapshot.DepositId.Value,
                    snapshot.ExtractedAmount);
            }
        }

        [TargetRpc]
        void ResourceDeltaTargetRpc(
            NetworkConnection connection,
            ulong depositId,
            int extractedAmount)
        {
            ApplyResourceDeltaToLoadedScenes(
                new GeneratedEntityId(depositId),
                extractedAmount);
        }

        [TargetRpc]
        void HarvestResultTargetRpc(
            NetworkConnection connection,
            byte result,
            string snapshotJson)
        {
            if (IsServerStarted)
            {
                return;
            }

            if (result != (byte)ResourceHarvestResult.Succeeded)
            {
                Debug.LogWarning(
                    $"NetworkGameplayCommands: harvest failed with result {(ResourceHarvestResult)result}.");
                return;
            }

            if (string.IsNullOrEmpty(snapshotJson) ||
                ownerInventory == null ||
                ownerDefinitions == null)
            {
                return;
            }

            InventoryContainerSnapshot snapshot =
                JsonUtility.FromJson<InventoryContainerSnapshot>(snapshotJson);
            if (!ownerInventory.ApplyContainerSnapshot(snapshot, ownerDefinitions))
            {
                Debug.LogWarning(
                    $"NetworkGameplayCommands: failed to apply inventory snapshot for container '{snapshot?.ContainerId}'.");
            }
        }

        bool TryResolveServerContext(
            out GameplayRuntimeBindings bindings,
            out NetworkExplorerController explorer,
            out InventoryContainerComponent inventory)
        {
            bindings = null;
            explorer = null;
            inventory = null;
            NetworkPlayerSpawner spawner = sessionPlayer.PlayerSpawner;
            if (spawner == null ||
                !spawner.TryGetRuntimeBindings(
                    out bindings) ||
                !spawner.TryGetSpawnedExplorer(
                    sessionPlayer,
                    out explorer))
            {
                return false;
            }

            inventory =
                explorer.GetComponentInChildren<InventoryContainerComponent>(true);
            return inventory != null;
        }

        internal static bool IsWithinHarvestDistance(
            Vector3 explorerPosition,
            Vector3 depositPosition,
            float maximumDistance)
        {
            float limit = Mathf.Max(0f, maximumDistance);
            return (depositPosition - explorerPosition).sqrMagnitude <= limit * limit;
        }

        static bool TryResolveHarvestNode(
            GameplayRuntimeBindings bindings,
            Vector3 explorerPosition,
            GeneratedEntityId depositId,
            float maximumDistance,
            out ResourceNodeInteractable node)
        {
            node = null;
            if (bindings == null)
            {
                return false;
            }

            foreach (ResourceDepositRuntimeSpawner streamer in
                     bindings.ResourceStreamers)
            {
                if (streamer != null &&
                    streamer.TryGetDepositPosition(
                        depositId,
                        out Vector3 depositPosition) &&
                    IsWithinHarvestDistance(
                        explorerPosition,
                        depositPosition,
                        maximumDistance) &&
                    streamer.TryGetOrCreateSpawnedNode(depositId, out node))
                {
                    return true;
                }
            }

            return false;
        }

        static void ApplyResourceDeltaToLoadedScenes(
            GeneratedEntityId depositId,
            int extractedAmount)
        {
            for (int sceneIndex = 0;
                 sceneIndex < UnityEngine.SceneManagement.SceneManager.sceneCount;
                 sceneIndex++)
            {
                Scene scene =
                    UnityEngine.SceneManagement.SceneManager.GetSceneAt(sceneIndex);
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    ResourceDepositRuntimeSpawner[] streamers =
                        root.GetComponentsInChildren<ResourceDepositRuntimeSpawner>(true);
                    foreach (ResourceDepositRuntimeSpawner streamer in streamers)
                    {
                        if (streamer.ApplyAuthoritativeDelta(
                            depositId,
                            extractedAmount))
                        {
                            return;
                        }
                    }
                }
            }
        }

        static bool TryFindLoadedNode(
            GeneratedEntityId depositId,
            out ResourceNodeInteractable node)
        {
            node = null;
            for (int sceneIndex = 0;
                 sceneIndex < UnityEngine.SceneManagement.SceneManager.sceneCount;
                 sceneIndex++)
            {
                Scene scene =
                    UnityEngine.SceneManagement.SceneManager.GetSceneAt(sceneIndex);
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    ResourceDepositRuntimeSpawner[] streamers =
                        root.GetComponentsInChildren<ResourceDepositRuntimeSpawner>(true);
                    foreach (ResourceDepositRuntimeSpawner streamer in streamers)
                    {
                        if (streamer.TryGetSpawnedNode(depositId, out node))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }
}
