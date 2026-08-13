using UnityEngine;
using UnityEngine.InputSystem;
using Mirror;
using MmoPoC.Combat;
using MmoPoC.UI;

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
        private Vector3 lastPosition;
        private PlayerHealth playerHealth;

        public float MoveSpeed
        {
            get => moveSpeed;
            set => moveSpeed = value;
        }

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            playerHealth = GetComponent<PlayerHealth>();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();

            // Ensure player spawns above ground level if spawning below terrain
            Terrain terrain = Terrain.activeTerrain;
            if (terrain != null)
            {
                float groundY = terrain.SampleHeight(transform.position) + terrain.transform.position.y;
                if (transform.position.y < groundY + 0.5f)
                {
                    Vector3 safePos = transform.position;
                    safePos.y = groundY + 1.0f;
                    transform.position = safePos;
                }
            }

            // Randomize spawn position slightly around initial spawn point
            Vector2 randomCircle = Random.insideUnitCircle * 3.5f;
            Vector3 randomOffset = new Vector3(randomCircle.x, 0f, randomCircle.y);

            if (characterController != null && characterController.enabled)
            {
                characterController.Move(randomOffset);
            }
            else
            {
                transform.position += randomOffset;
            }
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            lastPosition = transform.position;

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

            // Bind HUD for local player
            PlayerHealth health = GetComponent<PlayerHealth>();
            PlayerClassManager classMgr = GetComponent<PlayerClassManager>();
            PlayerHUD.EnsureHUDExists().BindLocalPlayer(health, classMgr);
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
            // Do nothing if dead
            if (playerHealth != null && playerHealth.IsDead) return;

            Animator animator = GetActiveAnimator();

            // Handle remote player animations based on NetworkTransform position changes
            if (!isLocalPlayer)
            {
                Vector3 positionDelta = transform.position - lastPosition;
                positionDelta.y = 0f;
                float remoteSpeed = Time.deltaTime > 0f ? positionDelta.magnitude / Time.deltaTime : 0f;
                bool isMovingRemote = remoteSpeed > 0.1f;

                if (animator != null)
                {
                    animator.SetBool("Moving", isMovingRemote);
                    animator.SetFloat("Velocity", isMovingRemote ? moveSpeed : 0f);
                    animator.SetFloat("Animation Speed", 1f);

                    bool isGroundedRemote = Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, 0.4f);
                    animator.SetInteger("Jumping", isGroundedRemote ? 0 : 1);
                }

                lastPosition = transform.position;
                return;
            }

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
            if (isMoving && characterController != null && characterController.enabled)
            {
                characterController.Move(moveDirection * moveSpeed * Time.deltaTime);

                // Rotate towards movement direction
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }

            // Jump handling
            bool isGrounded = characterController != null && characterController.isGrounded;
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
            if (characterController != null && characterController.enabled)
            {
                velocity.y -= gravity * Time.deltaTime;
                characterController.Move(velocity * Time.deltaTime);
            }

            // Sync Animator parameters locally
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

            lastPosition = transform.position;
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
            PlayerHealth selfHealth = GetComponent<PlayerHealth>();
            if (selfHealth != null && selfHealth.IsDead) return;

            // Trigger animation immediately on clients
            RpcPerformAttack();

            // Delay damage check to align with the end/impact of the attack animation swing
            StartCoroutine(ServerAttackHitRoutine(selfHealth, 0.4f));
        }

        private System.Collections.IEnumerator ServerAttackHitRoutine(PlayerHealth selfHealth, float delay)
        {
            yield return new WaitForSeconds(delay);

            if (selfHealth != null && selfHealth.IsDead) yield break;

            // Server-side hit detection in front of attacking player
            Vector3 attackOrigin = transform.position + Vector3.up * 1.0f + transform.forward * 1.2f;
            float attackRadius = 1.4f;
            Collider[] hitColliders = Physics.OverlapSphere(attackOrigin, attackRadius);

            // Use HashSet so each target PlayerHealth is only hit ONCE per attack sweep
            System.Collections.Generic.HashSet<PlayerHealth> hitTargets = new System.Collections.Generic.HashSet<PlayerHealth>();

            foreach (var hitCollider in hitColliders)
            {
                PlayerHealth targetHealth = hitCollider.GetComponentInParent<PlayerHealth>();
                if (targetHealth != null && targetHealth != selfHealth && !targetHealth.IsDead)
                {
                    if (!hitTargets.Contains(targetHealth))
                    {
                        hitTargets.Add(targetHealth);
                        targetHealth.TakeDamage(20);
                    }
                }
            }
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




