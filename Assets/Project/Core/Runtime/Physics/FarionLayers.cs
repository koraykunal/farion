using UnityEngine;

namespace Farion.Core.Physics
{
    public static class FarionLayers
    {
        public const int CelestialSurface = 6;
        public const int SpacecraftExterior = 7;
        public const int SpacecraftInterior = 8;
        public const int Explorer = 9;
        public const int ExplorerInterior = 10;
        public const int Interactable = 11;

        public static readonly LayerMask CameraObstacleMask =
            (1 << CelestialSurface) | (1 << SpacecraftExterior) | (1 << SpacecraftInterior);

        public static readonly LayerMask GroundMask =
            UnityEngine.Physics.DefaultRaycastLayers &
            ~((1 << Explorer) | (1 << ExplorerInterior));

        public static readonly LayerMask InteractionMask =
            (1 << Interactable) | (1 << SpacecraftInterior) | (1 << SpacecraftExterior);
    }
}
