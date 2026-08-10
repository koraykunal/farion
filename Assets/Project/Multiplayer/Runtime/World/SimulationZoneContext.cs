using Farion.Core.Identity;
using Farion.Simulation.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Farion.Multiplayer.World
{
    [DisallowMultipleComponent]
    public sealed class SimulationZoneContext : MonoBehaviour
    {
        [SerializeField] ulong zoneId;

        ZonePhysicsTickDriver physicsDriver;

        public GeneratedEntityId ZoneId => zoneId == 0UL
            ? GeneratedEntityId.None
            : new GeneratedEntityId(zoneId);
        public Scene Scene => gameObject.scene;
        public PhysicsScene PhysicsScene => Scene.GetPhysicsScene();
        public bool IsConfigured => zoneId != 0UL;

        internal bool IsDrivenBy(ZonePhysicsTickDriver driver) =>
            physicsDriver == driver;

        public void Configure(GeneratedEntityId value)
        {
            if (!value.IsValid)
            {
                throw new System.ArgumentOutOfRangeException(
                    nameof(value),
                    "Simulation zones require a persistent non-zero id.");
            }

            if (zoneId == value.Value)
            {
                return;
            }

            physicsDriver?.UnregisterZone(this);
            zoneId = value.Value;
        }

        internal void BindPhysicsDriver(ZonePhysicsTickDriver driver)
        {
            if (physicsDriver == driver)
            {
                return;
            }

            physicsDriver?.UnregisterZone(this);
            physicsDriver = driver;
        }

        internal void UnbindPhysicsDriver(ZonePhysicsTickDriver driver)
        {
            if (physicsDriver == driver)
            {
                physicsDriver = null;
            }
        }

        void OnDisable()
        {
            physicsDriver?.UnregisterZone(this);
        }
    }
}
