using UnityEngine;
using Mirror;

namespace MmoPoC.Characters
{
    public class CameraFollow : MonoBehaviour
    {
        [Header("Target Settings")]
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 6f, -10f);
        [SerializeField] private bool autoFindPlayer = true;

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
        }

        private void LateUpdate()
        {
            if (target == null && autoFindPlayer)
            {
                FindPlayer();
            }

            if (target != null)
            {
                transform.position = target.position + offset;
                transform.LookAt(target.position + Vector3.up * 1f); // Look slightly above the pivot (e.g. waist/chest level)
            }
        }

        private void FindPlayer()
        {
            if (NetworkClient.localPlayer != null)
            {
                target = NetworkClient.localPlayer.transform;
            }
            else
            {
                // Fallback to finding by tag, prioritizing the locally owned one if multiple exist
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
        }
    }
}
