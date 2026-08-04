using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using TMPro;
using UnityEngine.UI;

namespace MmoPoC.Networking
{
    public class NetworkLauncher : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TMP_InputField addressInputField;
        [SerializeField] private Button connectButton;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private GameObject uiCanvas;

        [Header("Default Connection Settings")]
        [SerializeField] private string defaultAddress = "127.0.0.1";
        [SerializeField] private ushort defaultPort = 7777;

        /// <summary>
        /// Public setter method to configure UI references from scene setup scripts
        /// </summary>
        public void ConfigureUI(TMP_InputField addressInput, Button button, TextMeshProUGUI status, GameObject canvas)
        {
            addressInputField = addressInput;
            connectButton = button;
            statusText = status;
            uiCanvas = canvas;
        }

        private bool CheckIsServer()
        {
#if UNITY_SERVER
            return true;
#else
            bool isHeadless = SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null;
            bool isServerArg = false;

            string[] args = System.Environment.GetCommandLineArgs();
            foreach (var arg in args)
            {
                if (arg == "-dedicated" || arg == "-server" || arg == "-headless")
                {
                    isServerArg = true;
                    break;
                }
            }

            return isHeadless || isServerArg;
#endif
        }

        private void Awake()
        {
            if (CheckIsServer())
            {
                // Disable UI canvas immediately in Awake to prevent TMPro culling/layout loops from running in headless mode
                if (uiCanvas != null)
                {
                    uiCanvas.SetActive(false);
                    Debug.Log("[NetworkLauncher] Disabled UI Canvas in Awake() to prevent headless TMPro culling errors.");
                }

                // Also find and disable EventSystem on the server
                GameObject eventSystem = GameObject.Find("EventSystem");
                if (eventSystem != null)
                {
                    eventSystem.SetActive(false);
                    Debug.Log("[NetworkLauncher] Disabled EventSystem on Server.");
                }
            }
        }

        private void Start()
        {
            if (CheckIsServer())
            {
                StartDedicatedServer();
                return; // Skip client-only setup
            }

            // 1. Populate default values
            if (addressInputField != null)
            {
                addressInputField.text = defaultAddress;
            }

            if (statusText != null)
            {
                statusText.text = "Status: Idle";
            }

            // 2. Setup button listener
            if (connectButton != null)
            {
                connectButton.onClick.AddListener(OnConnectButtonClicked);
            }

            Debug.Log("[NetworkLauncher] Running in Client Mode.");
        }

        private void OnDestroy()
        {
            if (connectButton != null)
            {
                connectButton.onClick.RemoveListener(OnConnectButtonClicked);
            }

            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            }
        }

        private void StartDedicatedServer()
        {
            Debug.Log("[NetworkLauncher] Starting as Dedicated Server...");

            // Disable main camera since a server does not render gameplay
            GameObject mainCam = GameObject.FindWithTag("MainCamera");
            if (mainCam != null)
            {
                mainCam.SetActive(false);
                Debug.Log("[NetworkLauncher] Disabled Main Camera for Dedicated Server.");
            }

            // Disable UI canvas since server needs no UI
            if (uiCanvas != null)
            {
                uiCanvas.SetActive(false);
            }

            if (NetworkManager.Singleton != null)
            {
                // Setup Transport
                var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
                if (transport != null)
                {
                    transport.UseWebSockets = true; // Ensure WebSocket mode is clearly enabled
                    transport.ConnectionData.Address = "0.0.0.0"; // Listen on all interfaces
                    transport.ConnectionData.Port = defaultPort;

                    // Log start information clearly
                    Debug.Log($"[NetworkLauncher] Modus: Server | Transport: WebSocket | Listenadresse: {transport.ConnectionData.Address} | Port: {transport.ConnectionData.Port}");
                }

                bool success = NetworkManager.Singleton.StartServer();
                if (success)
                {
                    Debug.Log($"[NetworkLauncher] Dedicated Server successfully started on port {defaultPort}!");
                }
                else
                {
                    Debug.LogError("[NetworkLauncher] Failed to start Dedicated Server!");
                }
            }
            else
            {
                Debug.LogError("[NetworkLauncher] NetworkManager.Singleton is null!");
            }
        }

        private void OnConnectButtonClicked()
        {
            string targetAddress = addressInputField != null ? addressInputField.text : defaultAddress;
            if (string.IsNullOrEmpty(targetAddress))
            {
                targetAddress = defaultAddress;
            }

            if (statusText != null)
            {
                statusText.text = "Status: Connecting...";
            }

            if (connectButton != null)
            {
                connectButton.interactable = false;
            }

            if (NetworkManager.Singleton != null)
            {
                // Configure transport
                var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
                if (transport != null)
                {
                    transport.UseWebSockets = true; // Ensure WebSocket mode is clearly enabled
                    transport.ConnectionData.Address = targetAddress;
                    transport.ConnectionData.Port = defaultPort;

                    // Log start information clearly
                    Debug.Log($"[NetworkLauncher] Modus: Client | Transport: WebSocket | Zieladresse: {transport.ConnectionData.Address} | Port: {transport.ConnectionData.Port}");
                }

                // Register callback listeners
                NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

                bool success = NetworkManager.Singleton.StartClient();
                if (!success)
                {
                    Debug.LogError("[NetworkLauncher] Failed to initiate StartClient!");
                    if (statusText != null)
                    {
                        statusText.text = "Status: Failed to Start Client";
                    }
                    if (connectButton != null)
                    {
                        connectButton.interactable = true;
                    }
                }
            }
            else
            {
                Debug.LogError("[NetworkLauncher] NetworkManager.Singleton is null!");
                if (statusText != null)
                {
                    statusText.text = "Status: Error (No NetworkManager)";
                }
                if (connectButton != null)
                {
                    connectButton.interactable = true;
                }
            }
        }

        private void OnClientConnected(ulong clientId)
        {
            // If we are the local client that connected
            if (NetworkManager.Singleton != null && clientId == NetworkManager.Singleton.LocalClientId)
            {
                Debug.Log("[NetworkLauncher] Local client connected successfully!");
                if (statusText != null)
                {
                    statusText.text = "Status: Connected";
                }

                // Disable UI once connected to see the game
                if (uiCanvas != null)
                {
                    uiCanvas.SetActive(false);
                }
            }
        }

        private void OnClientDisconnected(ulong clientId)
        {
            // If the disconnected client is us
            if (NetworkManager.Singleton != null && clientId == NetworkManager.Singleton.LocalClientId)
            {
                Debug.LogWarning("[NetworkLauncher] Local client disconnected.");
                if (statusText != null)
                {
                    statusText.text = "Status: Disconnected";
                }

                // Re-enable UI so client can try to reconnect
                if (uiCanvas != null)
                {
                    uiCanvas.SetActive(true);
                }

                if (connectButton != null)
                {
                    connectButton.interactable = true;
                }

                // Clean up listeners
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            }
        }
    }
}
