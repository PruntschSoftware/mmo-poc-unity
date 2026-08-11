using UnityEngine;
using Mirror;
using Mirror.SimpleWeb;
using kcp2k;
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
            new ServerPreset { name = "Local Host", address = "127.0.0.1", port = 7777 },
            new ServerPreset { name = "vServer", address = "185.164.6.110", port = 7777 },
            new ServerPreset { name = "Railway Demo", address = "sakura.proxy.rlwy.net", port = 38260 },
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

                GameObject launcherCanvas = GameObject.Find("LauncherCanvas");
                if (launcherCanvas != null)
                {
                    launcherCanvas.SetActive(false);
                    Debug.Log("[NetworkLauncher] Disabled LauncherCanvas in Awake() for Headless Server.");
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

        private bool isConnecting = false;

        private void Start()
        {
            RegisterNetworkEvents();

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

                // Select default preset (Local Host)
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

        private void RegisterNetworkEvents()
        {
            NetworkClient.OnConnectedEvent -= OnClientConnected;
            NetworkClient.OnConnectedEvent += OnClientConnected;

            NetworkClient.OnDisconnectedEvent -= OnClientDisconnected;
            NetworkClient.OnDisconnectedEvent += OnClientDisconnected;

            NetworkClient.OnErrorEvent -= OnClientError;
            NetworkClient.OnErrorEvent += OnClientError;
        }

        private void UnregisterNetworkEvents()
        {
            NetworkClient.OnConnectedEvent -= OnClientConnected;
            NetworkClient.OnDisconnectedEvent -= OnClientDisconnected;
            NetworkClient.OnErrorEvent -= OnClientError;
        }

        private void Update()
        {
            if (isConnecting)
            {
                if (NetworkClient.isConnected)
                {
                    isConnecting = false;
                    OnClientConnected();
                }
                else if (!NetworkClient.active)
                {
                    isConnecting = false;
                    OnClientDisconnected();
                }
            }
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
                // Custom Server: Clear address and port to allow clean manual input
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

            UnregisterNetworkEvents();
        }

        private void StartDedicatedServer()
        {
            Debug.Log("[NetworkLauncher] Starting as Dedicated Server (Mirror)...");

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

            if (NetworkManager.singleton != null)
            {
                var portTransport = NetworkManager.singleton.GetComponent<PortTransport>();
                ushort serverPort = defaultPort;

                // 1. Check PORT environment variable (Railway / Docker standard)
                string envPort = System.Environment.GetEnvironmentVariable("PORT");
                if (!string.IsNullOrEmpty(envPort) && ushort.TryParse(envPort, out ushort parsedEnvPort))
                {
                    serverPort = parsedEnvPort;
                    Debug.Log($"[NetworkLauncher] Using PORT from Environment: {serverPort}");
                }

                // 2. Check -port command line argument
                string[] args = System.Environment.GetCommandLineArgs();
                for (int i = 0; i < args.Length - 1; i++)
                {
                    if (args[i].Equals("-port", System.StringComparison.OrdinalIgnoreCase) && ushort.TryParse(args[i + 1], out ushort parsedArgPort))
                    {
                        serverPort = parsedArgPort;
                        Debug.Log($"[NetworkLauncher] Using PORT from Command Line: {serverPort}");
                    }
                }

                if (portTransport != null)
                {
                    portTransport.Port = serverPort;
                }

#if UNITY_SERVER
                bool isUnityServerDefined = true;
#else
                bool isUnityServerDefined = false;
#endif
                var activeTransport = NetworkManager.singleton.transport;
                string transportName = activeTransport != null ? activeTransport.GetType().Name : "None";

                Debug.Log($"[NetworkLauncher] Pre-StartServer Diagnostics:\n" +
                          $"- Application.platform: {Application.platform}\n" +
                          $"- UNITY_SERVER active: {isUnityServerDefined}\n" +
                          $"- Transport: {transportName}\n" +
                          $"- Port: {(portTransport != null ? portTransport.Port : defaultPort)}");

                try
                {
                    NetworkManager.singleton.StartServer();
                    bool active = NetworkServer.active;
                    Debug.Log($"[NetworkLauncher] Dedicated Server StartServer() active: {active}");

                    if (active)
                    {
                        Debug.Log($"[NetworkLauncher] Dedicated Server successfully started on port {(portTransport != null ? portTransport.Port : defaultPort)}!");
                    }
                    else
                    {
                        Debug.LogError("[NetworkLauncher] Failed to start Dedicated Server!");
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[NetworkLauncher] Exception while starting Dedicated Server: {ex}");
                }
            }
            else
            {
                Debug.LogError("[NetworkLauncher] NetworkManager.singleton is null!");
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

            isConnecting = true;

            if (NetworkManager.singleton != null)
            {
                NetworkManager.singleton.networkAddress = targetAddress;

                var portTransport = NetworkManager.singleton.GetComponent<PortTransport>();
                if (portTransport != null)
                {
                    portTransport.Port = targetPort;
                }

                string enteredHost = targetAddress;
                ushort enteredPort = targetPort;

                Debug.Log($"[NetworkLauncher] About to call StartClient(). Host: {enteredHost}, Port: {enteredPort}");

                // StartClient initializes NetworkClient and resets delegates inside RegisterClientMessages()
                NetworkManager.singleton.StartClient();

                // Re-register network callbacks AFTER StartClient, as RegisterClientMessages overwrites static delegates
                RegisterNetworkEvents();

                Debug.Log($"[NetworkLauncher] StartClient() called and events re-subscribed. NetworkClient.active: {NetworkClient.active}");
            }
            else
            {
                isConnecting = false;
                Debug.LogError("[NetworkLauncher] NetworkManager.singleton is null!");
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

        private void OnClientConnected()
        {
            Debug.Log("[NetworkLauncher] Local client connected successfully to Mirror Server!");
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

        private void OnClientDisconnected()
        {
            Debug.LogWarning("[NetworkLauncher] Client disconnected from Mirror Server.");
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

        private void OnClientError(TransportError error, string message)
        {
            Debug.LogError($"[NetworkLauncher] Mirror Transport Error: {error} - {message}");
            if (statusText != null)
            {
                statusText.text = $"Status: Transport Error ({error})";
            }
            if (connectButton != null)
            {
                connectButton.interactable = true;
            }
        }
    }
}
