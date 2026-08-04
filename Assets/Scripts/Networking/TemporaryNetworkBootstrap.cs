using UnityEngine;
using Unity.Netcode;

namespace MmoPoC.Networking
{
    public class TemporaryNetworkBootstrap : MonoBehaviour
    {
        [SerializeField] private bool autoStartHost = false;

        private void Start()
        {
            if (!autoStartHost)
            {
                Debug.Log("[TemporaryNetworkBootstrap] Auto-start host is disabled.");
                return;
            }

            if (NetworkManager.Singleton != null)
            {
                if (!NetworkManager.Singleton.IsServer && !NetworkManager.Singleton.IsClient)
                {
                    Debug.Log("[TemporaryNetworkBootstrap] Starting Netcode Host automatically...");
                    bool success = NetworkManager.Singleton.StartHost();
                    if (success)
                    {
                        Debug.Log("[TemporaryNetworkBootstrap] Successfully started Host!");
                    }
                    else
                    {
                        Debug.LogError("[TemporaryNetworkBootstrap] Failed to start Host!");
                    }
                }
            }
            else
            {
                Debug.LogError("[TemporaryNetworkBootstrap] NetworkManager.Singleton is null! Ensure a NetworkManager component exists in the scene.");
            }
        }
    }
}
