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
        [SerializeField] private TMP_InputField portInputField;
        [SerializeField] private TMP_Dropdown serverDropdown;
        [SerializeField] private Button connectButton;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private GameObject uiCanvas;

        [Header("Default Connection Settings")]
        [SerializeField] private string defaultAddress = "127.0.0.1";
        [SerializeField] private ushort defaultPort = 7777;

        private struct ServerPreset
        {
            public string name;
            public string address;
            public ushort port;
        }

        private readonly ServerPreset[] presets = new ServerPreset[]
        {
            new ServerPreset { name = "Railway Demo", address = "sakura.proxy.rlwy.net", port = 38260 },
            new ServerPreset { name = "Local Host", address = "127.0.0.1", port = 7777 },
            new ServerPreset { name = "Custom Server", address = "", port = 0 }
        };

        /// <summary>
        /// Public setter method to configure UI references from scene setup scripts
        /// </summary>
        public void ConfigureUI(TMP_InputField addressInput, TMP_InputField portInput, TMP_Dropdown dropdown, Button button, TextMeshProUGUI status, GameObject canvas)
        {
            addressInputField = addressInput;
            portInputField = portInput;
            serverDropdown = dropdown;
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
            // Register Callbacks once
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
                NetworkManager.Singleton.OnTransportFailure += OnTransportFailure;
                Debug.Log("[NetworkLauncher] Registered NetworkManager callbacks (OnClientConnectedCallback, OnClientDisconnectCallback, OnTransportFailure) once.");
            }

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

            if (portInputField != null)
            {
                portInputField.text = defaultPort.ToString();
            }

            if (statusText != null)
            {
                statusText.text = "Status: Idle";
            }

            // 2. Setup Dropdown
            if (serverDropdown != null)
            {
                serverDropdown.ClearOptions();
                var options = new System.Collections.Generic.List<string>();
                foreach (var preset in presets)
                {
                    options.Add(preset.name);
                }
                serverDropdown.AddOptions(options);
                serverDropdown.onValueChanged.AddListener(OnDropdownValueChanged);

                // Select default preset (Railway Demo)
                serverDropdown.value = 0;
                OnDropdownValueChanged(0);
            }

            // 3. Setup button listener
            if (connectButton != null)
            {
                connectButton.onClick.AddListener(OnConnectButtonClicked);
            }

            Debug.Log("[NetworkLauncher] Running in Client Mode.");
        }

        private void OnDropdownValueChanged(int index)
        {
            if (index < 0 || index >= presets.Length) return;

            var preset = presets[index];
            if (preset.name != "Custom Server")
            {
                if (addressInputField != null)
                {
                    addressInputField.text = preset.address;
                }
                if (portInputField != null)
                {
                    portInputField.text = preset.port.ToString();
                }
            }
            else
            {
                // Custom Server: Clear address and port to allow clean manual input, or keep previous values
                if (addressInputField != null)
                {
                    addressInputField.text = "";
                }
                if (portInputField != null)
                {
                    portInputField.text = "";
                }
            }
        }

        private void OnDestroy()
        {
            if (connectButton != null)
            {
                connectButton.onClick.RemoveListener(OnConnectButtonClicked);
            }

            if (serverDropdown != null)
            {
                serverDropdown.onValueChanged.RemoveListener(OnDropdownValueChanged);
            }

            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
                NetworkManager.Singleton.OnTransportFailure -= OnTransportFailure;
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
                    // 1. Explicitly enable WebSockets
                    transport.UseWebSockets = true;

                    // 2. Explicitly set Connection Data for Dedicated Server (0.0.0.0, 7777, 0.0.0.0)
                    transport.SetConnectionData("0.0.0.0", 7777, "0.0.0.0");

                    // 3. Log diagnostics before StartServer()
#if UNITY_SERVER
                    bool isUnityServerDefined = true;
#else
                    bool isUnityServerDefined = false;
#endif
                    Debug.Log($"[NetworkLauncher] Pre-StartServer Diagnostics:\n" +
                              $"- Application.platform: {Application.platform}\n" +
                              $"- UNITY_SERVER active: {isUnityServerDefined}\n" +
                              $"- Protocol: {transport.Protocol}\n" +
                              $"- UseWebSockets: {transport.UseWebSockets}\n" +
                              $"- ConnectionData.Address: {transport.ConnectionData.Address}\n" +
                              $"- ConnectionData.ServerListenAddress: {transport.ConnectionData.ServerListenAddress}\n" +
                              $"- ConnectionData.Port: {transport.ConnectionData.Port}");
                }

                bool success = NetworkManager.Singleton.StartServer();
                Debug.Log($"[NetworkLauncher] Dedicated Server StartServer() success: {success}");

                if (success)
                {
                    Debug.Log($"[NetworkLauncher] Dedicated Server successfully started on port {transport?.ConnectionData.Port ?? 7777}!");
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

            ushort targetPort = defaultPort;
            if (portInputField != null && ushort.TryParse(portInputField.text, out ushort parsedPort))
            {
                targetPort = parsedPort;
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
                    
                    // Call SetConnectionData before StartClient()
                    transport.SetConnectionData(targetAddress, targetPort);
                }

                // Log the required pre-connection details
                string enteredHost = targetAddress;
                ushort enteredPort = targetPort;
                string transportAddr = transport != null ? transport.ConnectionData.Address : "N/A";
                ushort transportPort = transport != null ? transport.ConnectionData.Port : (ushort)0;
                bool webSocketsEnabled = transport != null ? transport.UseWebSockets : false;

                Debug.Log($"[NetworkLauncher] About to call StartClient(). Configuration details:\n" +
                          $"- eingegebener Hostname: {enteredHost}\n" +
                          $"- eingegebener Port: {enteredPort}\n" +
                          $"- UnityTransport.ConnectionData.Address: {transportAddr}\n" +
                          $"- UnityTransport.ConnectionData.Port: {transportPort}\n" +
                          $"- WebSocket aktiviert: {webSocketsEnabled}");

                bool success = NetworkManager.Singleton.StartClient();
                Debug.Log($"[NetworkLauncher] StartClient() result: {success}");

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
                Debug.Log($"[NetworkLauncher] Local client connected successfully! ClientId: {clientId}");
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
            else
            {
                Debug.Log($"[NetworkLauncher] Client connected. ClientId: {clientId}");
            }
        }

        private void OnClientDisconnected(ulong clientId)
        {
            string reason = NetworkManager.Singleton != null ? NetworkManager.Singleton.DisconnectReason : "N/A";
            bool isClient = NetworkManager.Singleton != null ? NetworkManager.Singleton.IsClient : false;
            bool isConnectedClient = NetworkManager.Singleton != null ? NetworkManager.Singleton.IsConnectedClient : false;

            Debug.Log($"[NetworkLauncher] Client Disconnected. Details:\n" +
                      $"- ClientId: {clientId}\n" +
                      $"- NetworkManager.DisconnectReason: {reason}\n" +
                      $"- NetworkManager.Singleton.IsClient: {isClient}\n" +
                      $"- NetworkManager.Singleton.IsConnectedClient: {isConnectedClient}");

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
            }
        }

        private void OnTransportFailure()
        {
            Debug.LogError("[NetworkLauncher] OnTransportFailure Callback triggered! Transport failure occurred.");
            if (statusText != null)
            {
                statusText.text = "Status: Transport Failure";
            }
            if (connectButton != null)
            {
                connectButton.interactable = true;
            }
        }
    }
}
