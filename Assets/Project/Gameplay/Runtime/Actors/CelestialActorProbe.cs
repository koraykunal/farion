using Farion.Simulation.Celestial;
using UnityEngine;

namespace Farion.Gameplay.Actors
{
    [DefaultExecutionOrder(40)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public class CelestialActorProbe : MonoBehaviour
    {
        [Header("Source")]
        [SerializeField] CelestialFrameProvider frameProvider;

        [Header("Runtime Sample")]
        [SerializeField] string dominantBodyName;
        [SerializeField] float surfaceAltitude;
        [SerializeField] float surfaceSlopeAngleDegrees;
        [SerializeField] float oceanAltitude;
        [SerializeField] float atmosphereAltitude;
        [SerializeField] bool insideAtmosphere;
        [SerializeField] bool belowOceanLevel;
        [SerializeField] float radialVelocity;
        [SerializeField] float tangentialSpeed;
        [SerializeField] float surfaceNormalVelocity;
        [SerializeField] float surfaceTangentialSpeed;
        [SerializeField] float speedRelativeToBody;
        [SerializeField] Vector3 localUp = Vector3.up;
        [SerializeField] Vector3 gravityAcceleration;

        Rigidbody cachedRigidbody;
        CelestialFrameSample currentSample;

        public CelestialFrameProvider FrameProvider => frameProvider;
        public CelestialFrameSample CurrentSample => currentSample;
        public bool HasSample => currentSample.HasBody;
        public Rigidbody Rigidbody => cachedRigidbody != null ? cachedRigidbody : cachedRigidbody = GetComponent<Rigidbody>();

        protected virtual void Awake()
        {
            cachedRigidbody = GetComponent<Rigidbody>();
        }

        protected virtual void FixedUpdate()
        {
            RefreshSample();
        }

        public void SetFrameProvider(CelestialFrameProvider provider)
        {
            frameProvider = provider;
        }

        [ContextMenu("Refresh Celestial Sample")]
        public void RefreshSample()
        {
            CelestialFrameProvider provider = FrameProvider;
            if (provider == null)
            {
                currentSample = CelestialFrameSample.Empty(Rigidbody.position, Rigidbody.linearVelocity);
                ApplyRuntimeState();
                return;
            }

            currentSample = provider.Sample(Rigidbody.position, Rigidbody.linearVelocity);
            ApplyRuntimeState();
        }

        void ApplyRuntimeState()
        {
            dominantBodyName = currentSample.HasBody ? currentSample.Body.BodyName : string.Empty;
            surfaceAltitude = currentSample.SurfaceAltitude;
            surfaceSlopeAngleDegrees = currentSample.SurfaceSlopeAngleDegrees;
            oceanAltitude = currentSample.HasOcean ? currentSample.OceanAltitude : 0f;
            atmosphereAltitude = currentSample.HasAtmosphere ? currentSample.AtmosphereAltitude : 0f;
            insideAtmosphere = currentSample.IsInsideAtmosphere;
            belowOceanLevel = currentSample.IsBelowOceanLevel;
            radialVelocity = currentSample.RadialVelocity;
            tangentialSpeed = currentSample.TangentialSpeed;
            surfaceNormalVelocity = currentSample.SurfaceNormalVelocity;
            surfaceTangentialSpeed = currentSample.SurfaceTangentialSpeed;
            speedRelativeToBody = currentSample.SpeedRelativeToBody;
            localUp = currentSample.LocalUp;
            gravityAcceleration = currentSample.GravityAcceleration;
        }
    }
}

