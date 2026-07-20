using UnityEngine;

namespace Farion.Rendering.Celestial
{
    internal static class CelestialMeshColliderBaker
    {
        public static void BakeImmediate(Mesh mesh)
        {
            if (mesh == null || mesh.vertexCount == 0)
            {
                return;
            }

            Physics.BakeMesh(mesh.GetEntityId(), false);
        }
    }
}
