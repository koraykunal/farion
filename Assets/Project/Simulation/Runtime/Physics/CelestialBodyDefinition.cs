using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace Farion.Simulation.Physics
{
    [MovedFrom(true, "Farion.Core.Physics", "Farion.Core.Runtime")]
    [CreateAssetMenu(menuName = "Farion/Simulation/Physics/Celestial Body Definition", fileName = "SO_CelestialBodyDefinition")]
    public sealed class CelestialBodyDefinition : ScriptableObject
    {
        [SerializeField] string bodyName = "Unnamed Body";
        [SerializeField] CelestialBodyType bodyType = CelestialBodyType.Planet;
        [Min(0.01f)]
        [SerializeField] float radius = 50f;
        [Min(0f)]
        [SerializeField] float surfaceGravity = 9.81f;
        [SerializeField] Vector3 initialVelocity;
        [SerializeField] Vector3 initialAngularVelocityDegreesPerSecond;
        [SerializeField] bool deriveMassFromSurfaceGravity = true;
        [Min(0f)]
        [SerializeField] float explicitMass = 1000000000f;
        [SerializeField] bool participatesInNBody = true;
        [SerializeField] CelestialBodyMotionMode motionMode = CelestialBodyMotionMode.KinematicOrbit;

        public string BodyName => bodyName;
        public CelestialBodyType BodyType => bodyType;
        public float Radius => radius;
        public float SurfaceGravity => surfaceGravity;
        public Vector3 InitialVelocity => initialVelocity;
        public Vector3 InitialAngularVelocityDegreesPerSecond => initialAngularVelocityDegreesPerSecond;
        public bool DeriveMassFromSurfaceGravity => deriveMassFromSurfaceGravity;
        public float ExplicitMass => explicitMass;
        public bool ParticipatesInNBody => participatesInNBody;
        public CelestialBodyMotionMode MotionMode => motionMode;
    }
}
