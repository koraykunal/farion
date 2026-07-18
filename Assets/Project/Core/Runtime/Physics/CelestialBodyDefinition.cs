using UnityEngine;

namespace Farion.Core.Physics
{
    [CreateAssetMenu(menuName = "Farion/Physics/Celestial Body Definition", fileName = "SO_CelestialBodyDefinition")]
    public sealed class CelestialBodyDefinition : ScriptableObject
    {
        [SerializeField] string bodyName = "Unnamed Body";
        [SerializeField] CelestialBodyType bodyType = CelestialBodyType.Planet;
        [Min(0.01f)]
        [SerializeField] float radius = 50f;
        [Min(0f)]
        [SerializeField] float surfaceGravity = 9.81f;
        [SerializeField] Vector3 initialVelocity;
        [SerializeField] bool deriveMassFromSurfaceGravity = true;
        [Min(0f)]
        [SerializeField] float explicitMass = 1000000000f;
        [SerializeField] bool participatesInNBody = true;
        [SerializeField] bool lockPosition;

        public string BodyName => bodyName;
        public CelestialBodyType BodyType => bodyType;
        public float Radius => radius;
        public float SurfaceGravity => surfaceGravity;
        public Vector3 InitialVelocity => initialVelocity;
        public bool DeriveMassFromSurfaceGravity => deriveMassFromSurfaceGravity;
        public float ExplicitMass => explicitMass;
        public bool ParticipatesInNBody => participatesInNBody;
        public bool LockPosition => lockPosition;
    }
}
