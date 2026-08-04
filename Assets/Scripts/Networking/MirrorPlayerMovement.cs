using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MmoPoC.Networking
{
    /// <summary>
    /// Minimal Mirror based player movement for the TestWorldMirror scene.
    /// Runs in parallel to the existing NGO implementation and does not replace it.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class MirrorPlayerMovement : NetworkBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float rotationSpeed = 10f;
        [SerializeField] private float gravity = 9.81f;

        [Header("Camera Settings")]
        [SerializeField] private Vector3 cameraOffset = new Vector3(0f, 6f, -10f);

        private CharacterController characterController;
        private Camera localCamera;
        private Vector3 velocity;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
        }

        public override void OnStartClient()
        {
            // Remote players are driven by NetworkTransform, so the CharacterController
            // must not fight with the synced transform.
            if (!isOwned && characterController != null)
            {
                characterController.enabled = false;
            }
        }

        public override void OnStartLocalPlayer()
        {
            localCamera = Camera.main;
        }

        private void Update()
        {
            if (!isOwned) return;
            if (characterController == null || !characterController.enabled) return;

            Vector2 input = Vector2.zero;
            Keyboard keyboard = Keyboard.current;

            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed) input.y += 1f;
                if (keyboard.sKey.isPressed) input.y -= 1f;
                if (keyboard.aKey.isPressed) input.x -= 1f;
                if (keyboard.dKey.isPressed) input.x += 1f;
            }

            Vector3 moveDirection = new Vector3(input.x, 0f, input.y).normalized;

            if (moveDirection.sqrMagnitude > 0.0001f)
            {
                characterController.Move(moveDirection * moveSpeed * Time.deltaTime);

                Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }

            if (characterController.isGrounded)
            {
                velocity.y = -0.5f;
            }
            else
            {
                velocity.y -= gravity * Time.deltaTime;
            }

            characterController.Move(velocity * Time.deltaTime);
        }

        private void LateUpdate()
        {
            // Only the local player controls the camera.
            if (!isOwned) return;

            if (localCamera == null)
            {
                localCamera = Camera.main;
                if (localCamera == null) return;
            }

            localCamera.transform.position = transform.position + cameraOffset;
            localCamera.transform.LookAt(transform.position + Vector3.up);
        }
    }
}
