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
        [SerializeField] private float gravity = 15f;
        [SerializeField] private float jumpHeight = 2.5f;

        private CharacterController characterController;
        private Vector3 velocity;
        private Animator cachedAnimator;

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

        private Animator GetActiveAnimator()
        {
            if (cachedAnimator != null && cachedAnimator.gameObject.activeInHierarchy)
            {
                return cachedAnimator;
            }

            Animator[] animators = GetComponentsInChildren<Animator>(false);
            foreach (var a in animators)
            {
                if (a.gameObject.activeInHierarchy)
                {
                    cachedAnimator = a;
                    return cachedAnimator;
                }
            }

            return null;
        }

        private void Update()
        {
            Animator animator = GetActiveAnimator();

            // Only the local player handles input & movement
            if (!isLocalPlayer) return;

            // Read input using New Input System
            Vector2 input = Vector2.zero;
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;

            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed) input.y += 1f;
                if (keyboard.sKey.isPressed) input.y -= 1f;
                if (keyboard.aKey.isPressed) input.x -= 1f;
                if (keyboard.dKey.isPressed) input.x += 1f;
            }

            // Normalize horizontal input
            Vector3 moveDirection = new Vector3(input.x, 0f, input.y).normalized;
            bool isMoving = moveDirection.magnitude > 0.01f;

            // Apply movement
            if (isMoving)
            {
                characterController.Move(moveDirection * moveSpeed * Time.deltaTime);

                // Rotate towards movement direction
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }

            // Jump handling
            bool isGrounded = characterController.isGrounded;
            bool jumpPressed = keyboard != null && keyboard.spaceKey.wasPressedThisFrame;

            if (isGrounded)
            {
                if (velocity.y < 0)
                {
                    velocity.y = -2f; // Small constant downward force when grounded
                }

                if (jumpPressed)
                {
                    velocity.y = Mathf.Sqrt(jumpHeight * 2f * gravity);
                    CmdPerformJump();
                }
            }

            // Apply gravity
            velocity.y -= gravity * Time.deltaTime;
            characterController.Move(velocity * Time.deltaTime);

            // Sync Animator parameters locally and across clients
            if (animator != null)
            {
                animator.SetBool("Moving", isMoving);
                animator.SetFloat("Velocity", isMoving ? moveSpeed : 0f);
                animator.SetFloat("Animation Speed", 1f);
                animator.SetInteger("Jumping", isGrounded ? 0 : 1);
            }

            // Attack handling (Left Mouse Click or F key)
            bool attackPressed = (mouse != null && mouse.leftButton.wasPressedThisFrame) ||
                                 (keyboard != null && keyboard.fKey.wasPressedThisFrame);

            if (attackPressed)
            {
                CmdPerformAttack();
            }
        }

        [Command]
        private void CmdPerformJump()
        {
            RpcPerformJump();
        }

        [ClientRpc]
        private void RpcPerformJump()
        {
            Animator animator = GetActiveAnimator();
            if (animator != null)
            {
                animator.SetInteger("Trigger Number", 1); // JumpTrigger = 1
                animator.SetTrigger("Trigger");
            }
        }

        [Command]
        private void CmdPerformAttack()
        {
            RpcPerformAttack();
        }

        [ClientRpc]
        private void RpcPerformAttack()
        {
            Animator animator = GetActiveAnimator();
            if (animator != null)
            {
                animator.SetInteger("Trigger Number", 2); // AttackTrigger = 2
                animator.SetTrigger("Trigger");
            }
        }
    }
}


