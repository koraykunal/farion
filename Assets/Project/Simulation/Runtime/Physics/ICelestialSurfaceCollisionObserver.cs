using UnityEngine;

namespace Farion.Simulation.Physics
{
    public interface ICelestialSurfaceCollisionObserver
    {
        bool TryGetSurfaceCollisionObserver(
            out CelestialSurfaceCollisionObserverState observer);
    }

    public interface ICelestialSurfaceCollisionObserverGroup
    {
        void GetSurfaceCollisionObservers(
            System.Collections.Generic.List<CelestialSurfaceCollisionObserverState> results);
    }

    public readonly struct CelestialSurfaceCollisionObserverState
    {
        public CelestialSurfaceCollisionObserverState(Rigidbody rigidbody)
        {
            Rigidbody = rigidbody;
            Position = rigidbody != null ? rigidbody.position : Vector3.zero;
            Velocity = rigidbody != null ? rigidbody.linearVelocity : Vector3.zero;
        }

        public Rigidbody Rigidbody { get; }
        public Vector3 Position { get; }
        public Vector3 Velocity { get; }
        public bool IsValid => Rigidbody != null;
    }
}
