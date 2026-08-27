using Farion.Gameplay.Character;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Farion.Gameplay.Session
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-500)]
    public sealed class AnimationArenaBootstrap : MonoBehaviour
    {
        [SerializeField] FirstPersonMotor player;
        [Tooltip("Targets for the digit keys: pressing 1, 2 or 3 teleports the explorer to the matching entry, R returns to spawn.")]
        [SerializeField] Transform[] teleportPoints;
        [Tooltip("Falling below this world height teleports the explorer back to its spawn point.")]
        [SerializeField] float killHeight = -20f;

        Vector3 spawnPosition;
        Quaternion spawnRotation;

        void Awake()
        {
            Physics.simulationMode = SimulationMode.FixedUpdate;
            if (player == null)
            {
                player = FindAnyObjectByType<FirstPersonMotor>();
            }

            if (player != null)
            {
                spawnPosition = player.transform.position;
                spawnRotation = player.transform.rotation;
            }

            LockCursor(true);
        }

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                LockCursor(false);
            }

            Mouse mouse = Mouse.current;
            if (mouse != null &&
                mouse.leftButton.wasPressedThisFrame &&
                Cursor.lockState != CursorLockMode.Locked)
            {
                LockCursor(true);
            }

            if (player == null)
            {
                return;
            }

            if (keyboard.rKey.wasPressedThisFrame)
            {
                Teleport(spawnPosition, spawnRotation);
            }

            if (keyboard.digit1Key.wasPressedThisFrame)
            {
                TeleportToPoint(0);
            }

            if (keyboard.digit2Key.wasPressedThisFrame)
            {
                TeleportToPoint(1);
            }

            if (keyboard.digit3Key.wasPressedThisFrame)
            {
                TeleportToPoint(2);
            }
        }

        void FixedUpdate()
        {
            if (player != null && player.transform.position.y < killHeight)
            {
                Teleport(spawnPosition, spawnRotation);
            }
        }

        static void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        void TeleportToPoint(int index)
        {
            if (teleportPoints == null ||
                index >= teleportPoints.Length ||
                teleportPoints[index] == null)
            {
                return;
            }

            Teleport(teleportPoints[index].position, teleportPoints[index].rotation);
        }

        void Teleport(Vector3 position, Quaternion rotation)
        {
            Rigidbody body = player.Rigidbody;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.position = position;
            body.rotation = rotation;
            player.transform.SetPositionAndRotation(position, rotation);
            player.ResetMotorState();
        }
    }
}
