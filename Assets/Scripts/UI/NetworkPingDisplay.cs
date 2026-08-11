using UnityEngine;
using Mirror;
using TMPro;

namespace MmoPoC.UI
{
    /// <summary>
    /// Displays live network ping / latency (RTT) in milliseconds using TextMeshProUGUI.
    /// </summary>
    public class NetworkPingDisplay : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI pingText;

        [Header("Settings")]
        [SerializeField] private float updateInterval = 0.5f;
        [SerializeField] private bool showColorCoding = true;

        [Header("Color Thresholds (ms)")]
        [SerializeField] private int goodPingThreshold = 80;
        [SerializeField] private int warningPingThreshold = 150;

        [SerializeField] private Color goodColor = new Color(0.3f, 0.9f, 0.3f);    // Green
        [SerializeField] private Color warningColor = new Color(0.95f, 0.8f, 0.2f); // Yellow
        [SerializeField] private Color badColor = new Color(0.95f, 0.3f, 0.3f);     // Red
        [SerializeField] private Color defaultColor = Color.white;

        private float timer;

        private void Awake()
        {
            if (pingText == null)
            {
                pingText = GetComponent<TextMeshProUGUI>();
            }
        }

        private void Update()
        {
            timer += Time.deltaTime;
            if (timer >= updateInterval)
            {
                timer = 0f;
                UpdatePingDisplay();
            }
        }

        private void UpdatePingDisplay()
        {
            if (pingText == null) return;

            if (NetworkClient.isConnected)
            {
                int pingMs = Mathf.RoundToInt((float)(NetworkTime.rtt * 1000.0));
                pingText.text = $"Ping: {pingMs} ms";

                if (showColorCoding)
                {
                    if (pingMs <= goodPingThreshold)
                        pingText.color = goodColor;
                    else if (pingMs <= warningPingThreshold)
                        pingText.color = warningColor;
                    else
                        pingText.color = badColor;
                }
            }
            else if (NetworkServer.active)
            {
                pingText.text = "Ping: 0 ms (Server)";
                pingText.color = defaultColor;
            }
            else
            {
                pingText.text = "Ping: -- ms";
                pingText.color = defaultColor;
            }
        }
    }
}
