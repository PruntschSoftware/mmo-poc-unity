using UnityEngine;
using UnityEngine.InputSystem;
using Mirror;

namespace MmoPoC.Characters
{
    public class CameraFollow : MonoBehaviour
    {
        [Header("Target Settings")]
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 6f, -10f);
        [SerializeField] private bool autoFindPlayer = true;

        [Header("Rotation Settings")]
        [SerializeField] private float rotationSensitivity = 0.2f;
        [SerializeField] private float returnSpeed = 3.0f;
        [SerializeField] private float minPitch = -20f;
        [SerializeField] private float maxPitch = 50f;
        [SerializeField] private float lookAtHeight = 1.0f;

        private float currentYaw = 0f;
        private float currentPitch = 0f;
        private Vector3 lastTargetPosition;

        public Transform Target
        {
            get => target;
            set => target = value;
        }

        public Vector3 Offset
        {
            get => offset;
            set => offset = value;
        }

        private void Start()
        {
            if (target == null && autoFindPlayer)
            {
                FindPlayer();
            }

            if (target != null)
            {
                lastTargetPosition = target.position;
            }
        }

        private void LateUpdate()
        {
            if (target == null && autoFindPlayer)
            {
                FindPlayer();
            }

            if (target == null) return;

            Mouse mouse = Mouse.current;
            bool isRmbHeld = mouse != null && mouse.rightButton.isPressed;

            // Handle manual orbit rotation with Right Mouse Button
            if (isRmbHeld)
            {
                Vector2 mouseDelta = mouse.delta.ReadValue();
                currentYaw += mouseDelta.x * rotationSensitivity;
                currentPitch = Mathf.Clamp(currentPitch - mouseDelta.y * rotationSensitivity, minPitch, maxPitch);
            }

            // Check if player is moving
            float targetSpeed = Time.deltaTime > 0f ? (target.position - lastTargetPosition).magnitude / Time.deltaTime : 0f;
            bool isMoving = targetSpeed > 0.2f;

            // If RMB is not held and player is moving, smoothly rotate camera back behind player
            if (!isRmbHeld && isMoving)
            {
                currentYaw = Mathf.Lerp(currentYaw, 0f, returnSpeed * Time.deltaTime);
                currentPitch = Mathf.Lerp(currentPitch, 0f, returnSpeed * Time.deltaTime);

                if (Mathf.Abs(currentYaw) < 0.05f) currentYaw = 0f;
                if (Mathf.Abs(currentPitch) < 0.05f) currentPitch = 0f;
            }

            // Calculate rotated position around target
            Quaternion rotation = Quaternion.Euler(currentPitch, currentYaw, 0f);
            Vector3 rotatedOffset = rotation * offset;

            transform.position = target.position + rotatedOffset;
            transform.LookAt(target.position + Vector3.up * lookAtHeight);

            lastTargetPosition = target.position;
        }

        private void FindPlayer()
        {
            if (NetworkClient.localPlayer != null)
            {
                target = NetworkClient.localPlayer.transform;
            }
            else
            {
                GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
                foreach (var p in players)
                {
                    NetworkIdentity netIdentity = p.GetComponent<NetworkIdentity>();
                    if (netIdentity == null || netIdentity.isLocalPlayer)
                    {
                        target = p.transform;
                        break;
                    }
                }
            }

            if (target != null)
            {
                lastTargetPosition = target.position;
            }
        }
    }
}

