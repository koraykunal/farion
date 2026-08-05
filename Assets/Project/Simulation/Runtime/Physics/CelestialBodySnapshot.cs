using System;
using Farion.Core.Persistence;
using UnityEngine;

namespace Farion.Simulation.Physics
{
    [Serializable]
    public struct CelestialBodySnapshot
    {
        [SerializeField] string persistentId;
        [SerializeField] string bodyName;
        [SerializeField] TransformPoseSnapshot pose;

        public CelestialBodySnapshot(string persistentId, string bodyName, TransformPoseSnapshot pose)
        {
            this.persistentId = PersistentObjectId.Normalize(persistentId);
            this.bodyName = string.IsNullOrWhiteSpace(bodyName) ? string.Empty : bodyName.Trim();
            this.pose = pose;
        }

        public string PersistentId => PersistentObjectId.Normalize(persistentId);
        public string BodyName => string.IsNullOrWhiteSpace(bodyName) ? string.Empty : bodyName.Trim();
        public TransformPoseSnapshot Pose => pose;
        public bool HasPersistentId => !string.IsNullOrEmpty(PersistentId);
        public bool IsValid => HasPersistentId || !string.IsNullOrEmpty(BodyName);
    }
}
