using UnityEngine;

namespace Farion.Rendering.Celestial
{
    public sealed class PlanetSurfaceMapSet : ScriptableObject
    {
        [SerializeField] Cubemap weightsA;
        [SerializeField] Cubemap weightsB;
        [SerializeField] Cubemap surfaceState;
        [SerializeField] Cubemap surfaceNormal;

        public Cubemap WeightsA => weightsA;
        public Cubemap WeightsB => weightsB;
        public Cubemap SurfaceState => surfaceState;
        public Cubemap SurfaceNormal => surfaceNormal;
        public bool IsComplete => weightsA != null
            && weightsB != null
            && surfaceState != null
            && surfaceNormal != null;

#if UNITY_EDITOR
        public void SetMaps(Cubemap mapA, Cubemap mapB, Cubemap stateMap, Cubemap normalMap)
        {
            weightsA = mapA;
            weightsB = mapB;
            surfaceState = stateMap;
            surfaceNormal = normalMap;
        }
#endif
    }
}
