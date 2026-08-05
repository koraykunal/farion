using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace Farion.Simulation.Physics
{
    [MovedFrom(true, "Farion.Core.Physics", "Farion.Core.Runtime")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class GravityActor : MonoBehaviour
    {
        [SerializeField] GravitySimulation simulation;
        [SerializeField] bool alignUpAgainstGravity = true;
        [Min(0f)]
        [SerializeField] float alignmentSpeed = 8f;
        [Min(0f)]
        [SerializeField] float gravityMultiplier = 1f;

        Rigidbody cachedRigidbody;
        Vector3 lastGravityAcceleration;

        public Vector3 LastGravityAcceleration => lastGravityAcceleration;
        public GravitySimulation Simulation => simulation;
        public Vector3 GravityUp => lastGravityAcceleration.sqrMagnitude > 0.0001f ? -lastGravityAcceleration.normalized : transform.up;
        public Rigidbody Rigidbody => cachedRigidbody != null ? cachedRigidbody : cachedRigidbody = GetComponent<Rigidbody>();

        void Awake()
        {
            Rigidbody.useGravity = false;
            Rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        }

        public void ConfigureAlignment(bool shouldAlignUpAgainstGravity, float speed)
        {
            alignUpAgainstGravity = shouldAlignUpAgainstGravity;
            alignmentSpeed = Mathf.Max(0f, speed);
        }

        void FixedUpdate()
        {
            GravitySimulation source = simulation;
            if (source == null)
            {
                lastGravityAcceleration = Vector3.zero;
                return;
            }

            lastGravityAcceleration =
                source.CalculateReferenceFrameAcceleration(Rigidbody.position) *
                gravityMultiplier;
            Rigidbody.AddForce(lastGravityAcceleration, ForceMode.Acceleration);

            if (alignUpAgainstGravity && lastGravityAcceleration.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRotation = Quaternion.FromToRotation(transform.up, GravityUp) * Rigidbody.rotation;
                Rigidbody.MoveRotation(Quaternion.Slerp(Rigidbody.rotation, targetRotation, alignmentSpeed * UnityEngine.Time.fixedDeltaTime));
            }
        }
    }
}
