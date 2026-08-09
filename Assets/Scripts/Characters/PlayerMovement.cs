using UnityEngine;
using UnityEngine.InputSystem;
using Mirror;

namespace MmoPoC.Characters
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMovement : NetworkBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float rotationSpeed = 10f;
        [SerializeField] private float gravity = 9.81f;

        private CharacterController characterController;
        private Vector3 velocity;

        public float MoveSpeed
        {
            get => moveSpeed;
            set => moveSpeed = value;
        }

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            // Disable CharacterController on non-local players so NetworkTransform can sync position/rotation smoothly
            if (!isLocalPlayer && characterController != null)
            {
                characterController.enabled = false;
            }
        }

        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();

            // Assign ourselves as camera target immediately when local player spawns
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                CameraFollow follow = mainCam.GetComponent<CameraFollow>();
                if (follow != null)
                {
                    follow.Target = transform;
                }
            }
        }

        private void Update()
        {
            // Only the local player moves themselves
            if (!isLocalPlayer) return;

            // Read input using New Input System
            Vector2 input = Vector2.zero;
            Keyboard keyboard = Keyboard.current;

            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed) input.y += 1f;
                if (keyboard.sKey.isPressed) input.y -= 1f;
                if (keyboard.aKey.isPressed) input.x -= 1f;
                if (keyboard.dKey.isPressed) input.x += 1f;
            }

            // Normalise horizontal input to prevent faster diagonal movement
            Vector3 moveDirection = new Vector3(input.x, 0f, input.y).normalized;

            // Apply movement
            if (moveDirection.magnitude > 0.01f)
            {
                characterController.Move(moveDirection * moveSpeed * Time.deltaTime);

                // Rotate towards movement direction
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }

            // Apply gravity to keep grounded
            if (characterController.isGrounded)
            {
                velocity.y = -0.5f; // Small constant downward force to stay grounded
            }
            else
            {
                velocity.y -= gravity * Time.deltaTime;
            }

            characterController.Move(velocity * Time.deltaTime);
        }
    }
}
