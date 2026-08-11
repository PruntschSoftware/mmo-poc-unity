using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MmoPoC.Combat;
using MmoPoC.Characters;

namespace MmoPoC.UI
{
    public class PlayerHUD : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Slider healthSlider;
        [SerializeField] private Image fillImage;
        [SerializeField] private Image damageGhostFillImage; // Secondary delayed damage bar
        [SerializeField] private TextMeshProUGUI hpText;
        [SerializeField] private TextMeshProUGUI hpPercentText;
        [SerializeField] private TextMeshProUGUI classText;
        [SerializeField] private Image classBadgeImage;
        [SerializeField] private Image mainFrameBorder;

        [Header("Colors")]
        [SerializeField] private Color fullHealthColor = new Color(0.2f, 0.9f, 0.4f);
        [SerializeField] private Color midHealthColor = new Color(0.95f, 0.75f, 0.2f);
        [SerializeField] private Color lowHealthColor = new Color(0.9f, 0.2f, 0.25f);
        [SerializeField] private Color ghostBarColor = new Color(1f, 0.4f, 0.1f, 0.8f);

        [Header("Class Theme Colors")]
        [SerializeField] private Color warriorColor = new Color(0.85f, 0.25f, 0.2f); // Red
        [SerializeField] private Color archerColor = new Color(0.25f, 0.8f, 0.35f);  // Green
        [SerializeField] private Color mageColor = new Color(0.35f, 0.5f, 0.95f);    // Blue/Purple

        private PlayerHealth localPlayerHealth;
        private PlayerClassManager localClassManager;

        private float targetHpRatio = 1f;
        private float currentDisplayHpRatio = 1f;
        private float ghostHpRatio = 1f;
        private float ghostDelayTimer = 0f;

        public static PlayerHUD Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void BindLocalPlayer(PlayerHealth health, PlayerClassManager classManager)
        {
            if (localPlayerHealth != null)
            {
                localPlayerHealth.OnHealthUpdated -= OnHealthUpdated;
            }

            if (localClassManager != null)
            {
                localClassManager.OnClassChangedEvent -= OnClassChanged;
            }

            localPlayerHealth = health;
            localClassManager = classManager;

            if (localPlayerHealth != null)
            {
                localPlayerHealth.OnHealthUpdated += OnHealthUpdated;
                OnHealthUpdated(localPlayerHealth.CurrentHealth, localPlayerHealth.MaxHealth);
            }

            if (localClassManager != null)
            {
                localClassManager.OnClassChangedEvent += OnClassChanged;
                UpdateClassTheme(localClassManager.CurrentClass);
            }
        }

        private void OnDestroy()
        {
            if (localPlayerHealth != null)
            {
                localPlayerHealth.OnHealthUpdated -= OnHealthUpdated;
            }

            if (localClassManager != null)
            {
                localClassManager.OnClassChangedEvent -= OnClassChanged;
            }
        }

        private void Update()
        {
            // Smoothly animate main health bar fill
            if (Mathf.Abs(currentDisplayHpRatio - targetHpRatio) > 0.001f)
            {
                currentDisplayHpRatio = Mathf.Lerp(currentDisplayHpRatio, targetHpRatio, Time.deltaTime * 10f);
                if (healthSlider != null)
                {
                    healthSlider.value = currentDisplayHpRatio;
                }

                // Dynamic fill color transition
                if (fillImage != null)
                {
                    if (currentDisplayHpRatio > 0.5f)
                    {
                        float t = (currentDisplayHpRatio - 0.5f) * 2f;
                        fillImage.color = Color.Lerp(midHealthColor, fullHealthColor, t);
                    }
                    else
                    {
                        float t = currentDisplayHpRatio * 2f;
                        fillImage.color = Color.Lerp(lowHealthColor, midHealthColor, t);
                    }
                }
            }

            // Smoothly catch up damage ghost bar after delay
            if (ghostDelayTimer > 0f)
            {
                ghostDelayTimer -= Time.deltaTime;
            }
            else if (ghostHpRatio > targetHpRatio)
            {
                ghostHpRatio = Mathf.Lerp(ghostHpRatio, targetHpRatio, Time.deltaTime * 5f);
                if (damageGhostFillImage != null)
                {
                    damageGhostFillImage.fillAmount = ghostHpRatio;
                }
            }
            else
            {
                ghostHpRatio = targetHpRatio;
                if (damageGhostFillImage != null)
                {
                    damageGhostFillImage.fillAmount = ghostHpRatio;
                }
            }
        }

        private void OnHealthUpdated(int currentHp, int maxHp)
        {
            if (maxHp <= 0) return;

            float newRatio = Mathf.Clamp01((float)currentHp / maxHp);

            if (newRatio < targetHpRatio)
            {
                // Health dropped - trigger ghost delay
                ghostDelayTimer = 0.35f;
            }
            else
            {
                // Healed or reset
                ghostHpRatio = newRatio;
                if (damageGhostFillImage != null) damageGhostFillImage.fillAmount = newRatio;
            }

            targetHpRatio = newRatio;

            if (hpText != null)
            {
                hpText.text = $"{currentHp} / {maxHp} HP";
            }

            if (hpPercentText != null)
            {
                hpPercentText.text = $"{Mathf.RoundToInt(newRatio * 100f)}%";
            }
        }

        private void OnClassChanged(CharacterClass newClass)
        {
            UpdateClassTheme(newClass);
        }

        private void UpdateClassTheme(CharacterClass characterClass)
        {
            Color themeColor = warriorColor;
            string classTitle = "WARRIOR";

            switch (characterClass)
            {
                case CharacterClass.Archer:
                    themeColor = archerColor;
                    classTitle = "ARCHER";
                    break;
                case CharacterClass.Mage:
                    themeColor = mageColor;
                    classTitle = "MAGE";
                    break;
                case CharacterClass.Warrior:
                default:
                    themeColor = warriorColor;
                    classTitle = "WARRIOR";
                    break;
            }

            if (classText != null)
            {
                classText.text = classTitle;
                classText.color = themeColor;
            }

            if (classBadgeImage != null)
            {
                classBadgeImage.color = themeColor;
            }

            if (mainFrameBorder != null)
            {
                mainFrameBorder.color = new Color(themeColor.r, themeColor.g, themeColor.b, 0.8f);
            }
        }

        public static PlayerHUD EnsureHUDExists()
        {
            if (Instance != null) return Instance;

            // Search specifically for a Screen Space Overlay canvas (never attach HUD to World Space OverheadCanvas)
            Canvas canvas = null;
            Canvas[] allCanvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude);
            foreach (var c in allCanvases)
            {
                if (c.renderMode == RenderMode.ScreenSpaceOverlay && c.gameObject.name != "OverheadCanvas")
                {
                    canvas = c;
                    break;
                }
            }

            if (canvas == null)
            {
                GameObject canvasGo = new GameObject("HUDCanvas");
                canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 50;
                CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                canvasGo.AddComponent<GraphicRaycaster>();
            }

            // Create HUD container in top-left
            GameObject hudGo = new GameObject("PlayerHUD", typeof(RectTransform));
            hudGo.transform.SetParent(canvas.transform, false);

            RectTransform hudRect = hudGo.GetComponent<RectTransform>();
            hudRect.anchorMin = new Vector2(0f, 1f);
            hudRect.anchorMax = new Vector2(0f, 1f);
            hudRect.pivot = new Vector2(0f, 1f);
            hudRect.anchoredPosition = new Vector2(25f, -25f);
            hudRect.sizeDelta = new Vector2(340f, 95f);

            // Outer Frame Border Accent
            Image outerBorder = hudGo.AddComponent<Image>();
            outerBorder.color = new Color(0.85f, 0.25f, 0.2f, 0.8f); // Default Warrior border

            // Inner Dark Panel (Glassmorphism look)
            GameObject panelGo = new GameObject("InnerPanel", typeof(RectTransform));
            panelGo.transform.SetParent(hudGo.transform, false);
            RectTransform panelRect = panelGo.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = new Vector2(2f, 2f);
            panelRect.offsetMax = new Vector2(-2f, -2f);

            Image panelBg = panelGo.AddComponent<Image>();
            panelBg.color = new Color(0.08f, 0.09f, 0.12f, 0.88f);

            // Class Badge Icon (Left side)
            GameObject badgeGo = new GameObject("ClassBadge", typeof(RectTransform));
            badgeGo.transform.SetParent(panelGo.transform, false);
            RectTransform badgeRect = badgeGo.GetComponent<RectTransform>();
            badgeRect.anchorMin = new Vector2(0f, 0.5f);
            badgeRect.anchorMax = new Vector2(0f, 0.5f);
            badgeRect.pivot = new Vector2(0f, 0.5f);
            badgeRect.anchoredPosition = new Vector2(12f, 0f);
            badgeRect.sizeDelta = new Vector2(40f, 40f);

            Image badgeImg = badgeGo.AddComponent<Image>();
            badgeImg.color = new Color(0.85f, 0.25f, 0.2f, 1f);

            // Class Badge Label inside Icon
            GameObject badgeLabelGo = new GameObject("Label", typeof(RectTransform));
            badgeLabelGo.transform.SetParent(badgeGo.transform, false);
            RectTransform badgeLabelRect = badgeLabelGo.GetComponent<RectTransform>();
            badgeLabelRect.anchorMin = Vector2.zero;
            badgeLabelRect.anchorMax = Vector2.one;
            badgeLabelRect.sizeDelta = Vector2.zero;

            TextMeshProUGUI badgeTxt = badgeLabelGo.AddComponent<TextMeshProUGUI>();
            badgeTxt.fontSize = 20f;
            badgeTxt.fontStyle = FontStyles.Bold;
            badgeTxt.alignment = TextAlignmentOptions.Center;
            badgeTxt.color = Color.white;
            badgeTxt.text = "⚔";

            // Class Name Text Header
            GameObject classTextGo = new GameObject("ClassText", typeof(RectTransform));
            classTextGo.transform.SetParent(panelGo.transform, false);
            RectTransform classRect = classTextGo.GetComponent<RectTransform>();
            classRect.anchorMin = new Vector2(0f, 1f);
            classRect.anchorMax = new Vector2(1f, 1f);
            classRect.pivot = new Vector2(0f, 1f);
            classRect.anchoredPosition = new Vector2(62f, -10f);
            classRect.sizeDelta = new Vector2(-75f, 24f);

            TextMeshProUGUI classTmp = classTextGo.AddComponent<TextMeshProUGUI>();
            classTmp.fontSize = 17f;
            classTmp.fontStyle = FontStyles.Bold;
            classTmp.color = new Color(0.85f, 0.25f, 0.2f);
            classTmp.text = "WARRIOR";

            // HP Percent Text (Top Right)
            GameObject hpPercentGo = new GameObject("HPPercentText", typeof(RectTransform));
            hpPercentGo.transform.SetParent(panelGo.transform, false);
            RectTransform hpPercentRect = hpPercentGo.GetComponent<RectTransform>();
            hpPercentRect.anchorMin = new Vector2(1f, 1f);
            hpPercentRect.anchorMax = new Vector2(1f, 1f);
            hpPercentRect.pivot = new Vector2(1f, 1f);
            hpPercentRect.anchoredPosition = new Vector2(-15f, -10f);
            hpPercentRect.sizeDelta = new Vector2(80f, 24f);

            TextMeshProUGUI percentTmp = hpPercentGo.AddComponent<TextMeshProUGUI>();
            percentTmp.fontSize = 15f;
            percentTmp.fontStyle = FontStyles.Bold;
            percentTmp.alignment = TextAlignmentOptions.Right;
            percentTmp.color = new Color(0.9f, 0.9f, 0.95f);
            percentTmp.text = "100%";

            // Health Bar Container / Slider
            GameObject sliderGo = new GameObject("HealthSlider", typeof(RectTransform));
            sliderGo.transform.SetParent(panelGo.transform, false);
            RectTransform sliderRect = sliderGo.GetComponent<RectTransform>();
            sliderRect.anchorMin = new Vector2(0f, 0f);
            sliderRect.anchorMax = new Vector2(1f, 0f);
            sliderRect.pivot = new Vector2(0f, 0f);
            sliderRect.anchoredPosition = new Vector2(62f, 14f);
            sliderRect.sizeDelta = new Vector2(-75f, 28f);

            Slider slider = sliderGo.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;

            // Slider Dark Track Background
            GameObject sliderBgGo = new GameObject("Background", typeof(RectTransform));
            sliderBgGo.transform.SetParent(sliderGo.transform, false);
            RectTransform sliderBgRect = sliderBgGo.GetComponent<RectTransform>();
            sliderBgRect.anchorMin = Vector2.zero;
            sliderBgRect.anchorMax = Vector2.one;
            sliderBgRect.sizeDelta = Vector2.zero;
            Image sliderBg = sliderBgGo.AddComponent<Image>();
            sliderBg.color = new Color(0.15f, 0.16f, 0.2f, 0.95f);

            // Damage Ghost Bar (Behind Main Fill)
            GameObject ghostGo = new GameObject("GhostFill", typeof(RectTransform));
            ghostGo.transform.SetParent(sliderGo.transform, false);
            RectTransform ghostRect = ghostGo.GetComponent<RectTransform>();
            ghostRect.anchorMin = Vector2.zero;
            ghostRect.anchorMax = Vector2.one;
            ghostRect.sizeDelta = Vector2.zero;
            Image ghostImg = ghostGo.AddComponent<Image>();
            ghostImg.type = Image.Type.Filled;
            ghostImg.fillMethod = Image.FillMethod.Horizontal;
            ghostImg.fillOrigin = (int)Image.OriginHorizontal.Left;
            ghostImg.color = new Color(1f, 0.4f, 0.1f, 0.85f);
            ghostImg.fillAmount = 1f;

            // Main Health Bar Fill Area
            GameObject fillAreaGo = new GameObject("Fill Area", typeof(RectTransform));
            fillAreaGo.transform.SetParent(sliderGo.transform, false);
            RectTransform fillAreaRect = fillAreaGo.GetComponent<RectTransform>();
            fillAreaRect.anchorMin = Vector2.zero;
            fillAreaRect.anchorMax = Vector2.one;
            fillAreaRect.sizeDelta = Vector2.zero;

            GameObject fillGo = new GameObject("Fill", typeof(RectTransform));
            fillGo.transform.SetParent(fillAreaGo.transform, false);
            RectTransform fillRect = fillGo.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.sizeDelta = Vector2.zero;
            Image fillImg = fillGo.AddComponent<Image>();
            fillImg.color = new Color(0.2f, 0.9f, 0.4f);

            slider.fillRect = fillRect;
            slider.targetGraphic = fillImg;

            // Health Numbers Text on top of Bar
            GameObject hpTextGo = new GameObject("HPText", typeof(RectTransform));
            hpTextGo.transform.SetParent(sliderGo.transform, false);
            RectTransform hpRect = hpTextGo.GetComponent<RectTransform>();
            hpRect.anchorMin = Vector2.zero;
            hpRect.anchorMax = Vector2.one;
            hpRect.sizeDelta = Vector2.zero;

            TextMeshProUGUI hpTmp = hpTextGo.AddComponent<TextMeshProUGUI>();
            hpTmp.fontSize = 14f;
            hpTmp.fontStyle = FontStyles.Bold;
            hpTmp.alignment = TextAlignmentOptions.Center;
            hpTmp.color = Color.white;
            hpTmp.outlineWidth = 0.2f;
            hpTmp.outlineColor = Color.black;
            hpTmp.text = "100 / 100 HP";

            // Attach PlayerHUD component
            PlayerHUD hud = hudGo.AddComponent<PlayerHUD>();
            hud.healthSlider = slider;
            hud.fillImage = fillImg;
            hud.damageGhostFillImage = ghostImg;
            hud.hpText = hpTmp;
            hud.hpPercentText = percentTmp;
            hud.classText = classTmp;
            hud.classBadgeImage = badgeImg;
            hud.mainFrameBorder = outerBorder;

            return hud;
        }
    }
}

