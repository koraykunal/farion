using System;
using Farion.Gameplay.Actors;
using UnityEngine;

namespace Farion.Gameplay.Flight
{
    [Obsolete("Use CelestialActorProbe for new actor systems. This component is kept only to preserve existing spacecraft scene references.")]
    [DisallowMultipleComponent]
    public sealed class SpacecraftCelestialProbe : CelestialActorProbe
    {
    }
}
