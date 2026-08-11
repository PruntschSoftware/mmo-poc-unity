using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MmoPoC.Combat;

namespace MmoPoC.UI
{
    public class OverheadHealthBar : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Canvas worldCanvas;
        [SerializeField] private Slider healthSlider;
        [SerializeField] private Image fillImage;
        [SerializeField] private TextMeshProUGUI hpText;

        [Header("Colors")]
        [SerializeField] private Color fullHealthColor = new Color(0.2f, 0.9f, 0.4f);
        [SerializeField] private Color midHealthColor = new Color(0.95f, 0.75f, 0.2f);
        [SerializeField] private Color lowHealthColor = new Color(0.9f, 0.2f, 0.25f);

        private PlayerHealth targetHealth;
        private float targetHpRatio = 1f;
        private float currentHpRatio = 1f;

        public void Initialize(PlayerHealth health, bool isLocalPlayer)
        {
            targetHealth = health;

            if (targetHealth != null)
            {
                targetHealth.OnHealthUpdated += UpdateHealthBar;
                UpdateHealthBar(targetHealth.CurrentHealth, targetHealth.MaxHealth);
            }

            // Hide overhead health bar for local player (uses HUD instead)
            if (isLocalPlayer && worldCanvas != null)
            {
                worldCanvas.gameObject.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (targetHealth != null)
            {
                targetHealth.OnHealthUpdated -= UpdateHealthBar;
            }
        }

        private void Update()
        {
            if (Mathf.Abs(currentHpRatio - targetHpRatio) > 0.001f)
            {
                currentHpRatio = Mathf.Lerp(currentHpRatio, targetHpRatio, Time.deltaTime * 10f);
                if (healthSlider != null)
                {
                    healthSlider.value = currentHpRatio;
                }

                if (fillImage != null)
                {
                    if (currentHpRatio > 0.5f)
                    {
                        float t = (currentHpRatio - 0.5f) * 2f;
                        fillImage.color = Color.Lerp(midHealthColor, fullHealthColor, t);
                    }
                    else
                    {
                        float t = currentHpRatio * 2f;
                        fillImage.color = Color.Lerp(lowHealthColor, midHealthColor, t);
                    }
                }
            }
        }

        private void LateUpdate()
        {
            if (worldCanvas == null || !worldCanvas.gameObject.activeInHierarchy) return;

            // Billboard towards main camera
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                transform.rotation = Quaternion.LookRotation(transform.position - mainCam.transform.position);
            }
        }

        public void UpdateHealthBar(int currentHp, int maxHp)
        {
            if (maxHp <= 0) return;

            targetHpRatio = Mathf.Clamp01((float)currentHp / maxHp);

            if (hpText != null)
            {
                hpText.text = $"{currentHp} / {maxHp}";
            }

            // Hide overhead bar if dead or if local player
            if (worldCanvas != null && targetHealth != null)
            {
                bool shouldShow = !targetHealth.IsDead && !targetHealth.isLocalPlayer;
                worldCanvas.gameObject.SetActive(shouldShow);
            }
        }
    }
}


