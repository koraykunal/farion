using Farion.Simulation.World;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Transporting;
using UnityEngine;

namespace Farion.Multiplayer.World
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkManager))]
    public sealed class MultiplayerWorldOriginAuthority : MonoBehaviour
    {
        [SerializeField] NetworkManager networkManager;

        readonly WorldOriginSequenceState state = new();
        WorldOriginRebaser rebaser;
        Transform serverTrackingTarget;

        public uint CurrentSequence => state.Sequence;

        void Awake()
        {
            networkManager ??= GetComponent<NetworkManager>();
            networkManager.ClientManager.RegisterBroadcast<WorldOriginBroadcast>(
                OnOriginBroadcast);
            networkManager.TimeManager.OnPostTick += OnPostTick;
        }

        void OnDestroy()
        {
            if (networkManager == null)
            {
                return;
            }

            networkManager.ClientManager.UnregisterBroadcast<WorldOriginBroadcast>(
                OnOriginBroadcast);
            networkManager.TimeManager.OnPostTick -= OnPostTick;
        }

        public void BindRebaser(WorldOriginRebaser nextRebaser)
        {
            rebaser = nextRebaser;
            if (rebaser == null)
            {
                return;
            }

            rebaser.SetAutomaticRebasing(false);
            Vector3 pendingDelta =
                state.AccumulatedOrigin - rebaser.AccumulatedOriginOffset;
            if (pendingDelta.sqrMagnitude > Mathf.Epsilon)
            {
                rebaser.Rebase(pendingDelta);
            }
        }

        public void SetServerTrackingTarget(Transform target)
        {
            serverTrackingTarget = target;
            if (networkManager.IsServerStarted && rebaser != null)
            {
                rebaser.SetTrackingTarget(target);
            }
        }

        public void ResetSession()
        {
            state.Reset();
            serverTrackingTarget = null;
            rebaser = null;
        }

        public void SendCurrentOrigin(NetworkConnection connection)
        {
            if (connection == null || !networkManager.IsServerStarted)
            {
                return;
            }

            networkManager.ServerManager.Broadcast(
                connection,
                CreateBroadcast(),
                requireAuthenticated: true,
                channel: Channel.Reliable);
        }

        void OnPostTick()
        {
            if (!networkManager.IsServerStarted ||
                rebaser == null ||
                serverTrackingTarget == null)
            {
                return;
            }

            rebaser.SetTrackingTarget(serverTrackingTarget);
            if (!rebaser.RebaseIfNeeded())
            {
                return;
            }

            state.RecordServerShift(rebaser.AccumulatedOriginOffset);
            networkManager.ServerManager.Broadcast(
                CreateBroadcast(),
                requireAuthenticated: true,
                channel: Channel.Reliable);
        }

        void OnOriginBroadcast(WorldOriginBroadcast message, Channel channel)
        {
            if (!state.TryAccept(
                    message.Sequence,
                    message.AccumulatedOrigin,
                    out Vector3 delta) ||
                rebaser == null ||
                delta.sqrMagnitude <= Mathf.Epsilon)
            {
                return;
            }

            rebaser.Rebase(delta);
        }

        WorldOriginBroadcast CreateBroadcast()
        {
            return new WorldOriginBroadcast(
                state.Sequence,
                rebaser != null ? rebaser.LastOriginOffset : Vector3.zero,
                state.AccumulatedOrigin);
        }
    }
}
