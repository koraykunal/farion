using System;
using Farion.Core.Persistence;
using Farion.Gameplay.Flight;
using UnityEngine;

namespace Farion.Gameplay.Interaction
{
    [Serializable]
    public sealed class PlayerPossessionSnapshot
    {
        public const int CurrentVersion = 2;

        [SerializeField] int version;
        [SerializeField] PlayerPossessionMode mode;
        [SerializeField] string explorerId;
        [SerializeField] TransformPoseSnapshot explorerPose;
        [SerializeField] string spacecraftId;
        [SerializeField] TransformPoseSnapshot spacecraftPose;
        [SerializeField] SpacecraftPilotCameraView pilotCameraView;

        public PlayerPossessionSnapshot(
            PlayerPossessionMode mode,
            string explorerId,
            TransformPoseSnapshot explorerPose,
            string spacecraftId,
            TransformPoseSnapshot spacecraftPose,
            SpacecraftPilotCameraView pilotCameraView)
        {
            version = CurrentVersion;
            this.mode = mode;
            this.explorerId = PersistentObjectId.Normalize(explorerId);
            this.explorerPose = explorerPose;
            this.spacecraftId = PersistentObjectId.Normalize(spacecraftId);
            this.spacecraftPose = spacecraftPose;
            this.pilotCameraView = pilotCameraView;
        }

        public int Version => version;
        public PlayerPossessionMode Mode => mode;
        public string ExplorerId => PersistentObjectId.Normalize(explorerId);
        public TransformPoseSnapshot ExplorerPose => explorerPose;
        public string SpacecraftId => PersistentObjectId.Normalize(spacecraftId);
        public TransformPoseSnapshot SpacecraftPose => spacecraftPose;
        public SpacecraftPilotCameraView PilotCameraView => IsSupportedPilotCameraView(pilotCameraView)
            ? pilotCameraView
            : SpacecraftPilotCameraView.Exterior;
        public bool IsSupported => Version == CurrentVersion;

        static bool IsSupportedPilotCameraView(SpacecraftPilotCameraView view)
        {
            return view == SpacecraftPilotCameraView.Exterior ||
                   view == SpacecraftPilotCameraView.Cockpit;
        }
    }
}
