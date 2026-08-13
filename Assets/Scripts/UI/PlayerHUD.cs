using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MmoPoC.Combat;
using MmoPoC.Characters;

namespace MmoPoC.UI
{
    public class PlayerHUD : MonoBehaviour
    {
        [Header("Health UI References")]
        [SerializeField] private Slider healthSlider;
        [SerializeField] private Image fillImage;
        [SerializeField] private Image damageGhostFillImage; // Secondary delayed damage bar
        [SerializeField] private TextMeshProUGUI hpText;
        [SerializeField] private TextMeshProUGUI hpPercentText;
        [SerializeField] private TextMeshProUGUI classText;
        [SerializeField] private Image classBadgeImage;
        [SerializeField] private Image mainFrameBorder;

        [Header("Mana UI References")]
        [SerializeField] private Slider manaSlider;
        [SerializeField] private Image manaFillImage;
        [SerializeField] private TextMeshProUGUI manaText;

        [Header("Skill Bar References")]
        [SerializeField] private RectTransform skillBarContainer;
        private List<SkillSlotUI> skillSlots = new List<SkillSlotUI>();

        [Header("Colors")]
        [SerializeField] private Color fullHealthColor = new Color(0.2f, 0.9f, 0.4f);
        [SerializeField] private Color midHealthColor = new Color(0.95f, 0.75f, 0.2f);
        [SerializeField] private Color lowHealthColor = new Color(0.9f, 0.2f, 0.25f);
        [SerializeField] private Color manaColor = new Color(0.2f, 0.65f, 1.0f);

        [Header("Class Theme Colors")]
        [SerializeField] private Color warriorColor = new Color(0.85f, 0.25f, 0.2f); // Red
        [SerializeField] private Color archerColor = new Color(0.25f, 0.8f, 0.35f);  // Green
        [SerializeField] private Color mageColor = new Color(0.35f, 0.5f, 0.95f);    // Blue/Purple

        private PlayerHealth localPlayerHealth;
        private PlayerMana localPlayerMana;
        private PlayerClassManager localClassManager;
        private PlayerSkills localPlayerSkills;

        private float targetHpRatio = 1f;
        private float currentDisplayHpRatio = 1f;
        private float ghostHpRatio = 1f;
        private float ghostDelayTimer = 0f;

        private float targetManaRatio = 1f;
        private float currentDisplayManaRatio = 1f;

        public static PlayerHUD Instance { get; private set; }

        private struct SkillSlotUI
        {
            public GameObject root;
            public Image bgImage;
            public TextMeshProUGUI iconText;
            public TextMeshProUGUI numberBadgeText;
            public TextMeshProUGUI nameCostText;
            public Image cooldownOverlay;
            public TextMeshProUGUI cooldownText;
            public Button button;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Ensure skill slots list is rebound on Awake at runtime
            RebindSkillSlotsFromScene();
        }

        public void BindLocalPlayer(PlayerHealth health, PlayerClassManager classManager)
        {
            if (localPlayerHealth != null)
            {
                localPlayerHealth.OnHealthUpdated -= OnHealthUpdated;
            }

            if (localPlayerMana != null)
            {
                localPlayerMana.OnManaUpdated -= OnManaUpdated;
            }

            if (localClassManager != null)
            {
                localClassManager.OnClassChangedEvent -= OnClassChanged;
            }

            if (localPlayerSkills != null)
            {
                localPlayerSkills.OnSkillsUpdated -= OnSkillsUpdated;
                localPlayerSkills.OnCooldownStarted -= OnCooldownStarted;
            }

            localPlayerHealth = health;
            localClassManager = classManager;

            if (localPlayerHealth != null)
            {
                localPlayerHealth.OnHealthUpdated += OnHealthUpdated;
                OnHealthUpdated(localPlayerHealth.CurrentHealth, localPlayerHealth.MaxHealth);

                localPlayerMana = localPlayerHealth.GetComponent<PlayerMana>();
                if (localPlayerMana != null)
                {
                    localPlayerMana.OnManaUpdated += OnManaUpdated;
                    OnManaUpdated(localPlayerMana.CurrentMana, localPlayerMana.MaxMana);
                }

                localPlayerSkills = localPlayerHealth.GetComponent<PlayerSkills>();
                if (localPlayerSkills != null)
                {
                    localPlayerSkills.OnSkillsUpdated += OnSkillsUpdated;
                    localPlayerSkills.OnCooldownStarted += OnCooldownStarted;
                    OnSkillsUpdated(localPlayerSkills.ActiveSkills);
                }
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

            if (localPlayerMana != null)
            {
                localPlayerMana.OnManaUpdated -= OnManaUpdated;
            }

            if (localClassManager != null)
            {
                localClassManager.OnClassChangedEvent -= OnClassChanged;
            }

            if (localPlayerSkills != null)
            {
                localPlayerSkills.OnSkillsUpdated -= OnSkillsUpdated;
                localPlayerSkills.OnCooldownStarted -= OnCooldownStarted;
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

            // Smoothly animate mana bar fill
            if (Mathf.Abs(currentDisplayManaRatio - targetManaRatio) > 0.001f)
            {
                currentDisplayManaRatio = Mathf.Lerp(currentDisplayManaRatio, targetManaRatio, Time.deltaTime * 10f);
                if (manaSlider != null)
                {
                    manaSlider.value = currentDisplayManaRatio;
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

            // Update skill slot cooldown visuals
            if (localPlayerSkills != null)
            {
                var skills = localPlayerSkills.ActiveSkills;
                for (int i = 0; i < skillSlots.Count; i++)
                {
                    var slot = skillSlots[i];
                    if (i < skills.Count)
                    {
                        float cd = localPlayerSkills.GetCooldownRemaining(i);
                        float maxCd = skills[i].cooldown;

                        if (cd > 0f && maxCd > 0f)
                        {
                            slot.cooldownOverlay.gameObject.SetActive(true);
                            slot.cooldownOverlay.fillAmount = cd / maxCd;
                            slot.cooldownText.gameObject.SetActive(true);
                            slot.cooldownText.text = $"{cd:F1}s";
                        }
                        else
                        {
                            slot.cooldownOverlay.gameObject.SetActive(false);
                            slot.cooldownText.gameObject.SetActive(false);
                        }
                    }
                }
            }
        }

        private void OnHealthUpdated(int currentHp, int maxHp)
        {
            if (maxHp <= 0) return;

            float newRatio = Mathf.Clamp01((float)currentHp / maxHp);

            if (newRatio < targetHpRatio)
            {
                ghostDelayTimer = 0.35f;
            }
            else
            {
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

        private void OnManaUpdated(int currentMana, int maxMana)
        {
            if (maxMana <= 0) return;

            targetManaRatio = Mathf.Clamp01((float)currentMana / maxMana);

            if (manaText != null)
            {
                manaText.text = $"{currentMana} / {maxMana} MP";
            }
        }

        private void OnSkillsUpdated(List<SkillData> skills)
        {
            if (skills == null) return;

            if (skillSlots.Count == 0)
            {
                RebindSkillSlotsFromScene();
            }

            for (int i = 0; i < skillSlots.Count; i++)
            {
                var slot = skillSlots[i];
                if (slot.iconText == null || slot.nameCostText == null) continue;

                if (i < skills.Count)
                {
                    var skill = skills[i];
                    slot.root.SetActive(true);
                    slot.iconText.text = skill.iconSymbol;
                    slot.iconText.color = skill.themeColor;
                    slot.nameCostText.text = $"{skill.manaCost} MP";
                    // Tint the slot background slightly toward the skill's theme color
                    Color bg = Color.Lerp(new Color(0.12f, 0.14f, 0.18f, 0.92f), skill.themeColor, 0.18f);
                    bg.a = 0.92f;
                    slot.bgImage.color = bg;
                }
                else
                {
                    // Empty slot
                    slot.root.SetActive(true);
                    slot.iconText.text = "";
                    slot.nameCostText.text = "";
                    slot.bgImage.color = new Color(0.08f, 0.09f, 0.12f, 0.5f);
                }
            }
        }

        private void RebindSkillSlotsFromScene()
        {
            if (skillBarContainer == null)
            {
                GameObject barGo = GameObject.Find("SkillBarPanel");
                if (barGo != null) skillBarContainer = barGo.GetComponent<RectTransform>();
            }

            if (skillBarContainer == null) return;

            skillSlots.Clear();

            int slotIdx = 0;
            foreach (Transform child in skillBarContainer)
            {
                if (child.name.StartsWith("SkillSlot_"))
                {
                    int index = slotIdx;

                    Image bg = child.GetComponent<Image>();
                    Button btn = child.GetComponent<Button>();
                    if (btn == null) btn = child.gameObject.AddComponent<Button>();

                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() =>
                    {
                        if (localPlayerSkills != null)
                        {
                            localPlayerSkills.TryCastSkill(index);
                        }
                    });

                    TextMeshProUGUI iconTxt = null;
                    Transform iconT = child.Find("Icon");
                    if (iconT != null) iconTxt = iconT.GetComponent<TextMeshProUGUI>();

                    TextMeshProUGUI numTxt = null;
                    Transform numT = child.Find("NumberBadge");
                    if (numT != null) numTxt = numT.GetComponent<TextMeshProUGUI>();

                    TextMeshProUGUI costTxt = null;
                    Transform costT = child.Find("CostText");
                    if (costT != null) costTxt = costT.GetComponent<TextMeshProUGUI>();

                    Image cdImg = null;
                    Transform cdT = child.Find("CooldownOverlay");
                    if (cdT != null) cdImg = cdT.GetComponent<Image>();

                    TextMeshProUGUI cdTxt = null;
                    Transform cdTextT = child.Find("CooldownText");
                    if (cdTextT != null) cdTxt = cdTextT.GetComponent<TextMeshProUGUI>();

                    skillSlots.Add(new SkillSlotUI
                    {
                        root = child.gameObject,
                        bgImage = bg,
                        iconText = iconTxt,
                        numberBadgeText = numTxt,
                        nameCostText = costTxt,
                        cooldownOverlay = cdImg,
                        cooldownText = cdTxt,
                        button = btn
                    });

                    slotIdx++;
                }
            }
        }

        private void OnCooldownStarted(int slotIndex, float currentCd, float maxCd)
        {
            if (slotIndex >= 0 && slotIndex < skillSlots.Count)
            {
                var slot = skillSlots[slotIndex];
                slot.cooldownOverlay.gameObject.SetActive(true);
                slot.cooldownOverlay.fillAmount = 1f;
                slot.cooldownText.gameObject.SetActive(true);
                slot.cooldownText.text = $"{currentCd:F1}s";
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

            // Create HUD container in top-left (Expanded height to 125px for Health + Mana)
            GameObject hudGo = new GameObject("PlayerHUD", typeof(RectTransform));
            hudGo.transform.SetParent(canvas.transform, false);

            RectTransform hudRect = hudGo.GetComponent<RectTransform>();
            hudRect.anchorMin = new Vector2(0f, 1f);
            hudRect.anchorMax = new Vector2(0f, 1f);
            hudRect.pivot = new Vector2(0f, 1f);
            hudRect.anchoredPosition = new Vector2(25f, -25f);
            hudRect.sizeDelta = new Vector2(360f, 125f);

            // Outer Frame Border Accent
            Image outerBorder = hudGo.AddComponent<Image>();
            outerBorder.color = new Color(0.85f, 0.25f, 0.2f, 0.8f);

            // Inner Dark Panel
            GameObject panelGo = new GameObject("InnerPanel", typeof(RectTransform));
            panelGo.transform.SetParent(hudGo.transform, false);
            RectTransform panelRect = panelGo.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = new Vector2(2f, 2f);
            panelRect.offsetMax = new Vector2(-2f, -2f);

            Image panelBg = panelGo.AddComponent<Image>();
            panelBg.color = new Color(0.08f, 0.09f, 0.12f, 0.88f);

            // Class Badge Icon
            GameObject badgeGo = new GameObject("ClassBadge", typeof(RectTransform));
            badgeGo.transform.SetParent(panelGo.transform, false);
            RectTransform badgeRect = badgeGo.GetComponent<RectTransform>();
            badgeRect.anchorMin = new Vector2(0f, 0.5f);
            badgeRect.anchorMax = new Vector2(0f, 0.5f);
            badgeRect.pivot = new Vector2(0f, 0.5f);
            badgeRect.anchoredPosition = new Vector2(12f, 0f);
            badgeRect.sizeDelta = new Vector2(44f, 44f);

            Image badgeImg = badgeGo.AddComponent<Image>();
            badgeImg.color = new Color(0.85f, 0.25f, 0.2f, 1f);

            // Class Badge Label
            GameObject badgeLabelGo = new GameObject("Label", typeof(RectTransform));
            badgeLabelGo.transform.SetParent(badgeGo.transform, false);
            RectTransform badgeLabelRect = badgeLabelGo.GetComponent<RectTransform>();
            badgeLabelRect.anchorMin = Vector2.zero;
            badgeLabelRect.anchorMax = Vector2.one;
            badgeLabelRect.sizeDelta = Vector2.zero;

            TextMeshProUGUI badgeTxt = badgeLabelGo.AddComponent<TextMeshProUGUI>();
            badgeTxt.fontSize = 22f;
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
            classRect.anchoredPosition = new Vector2(66f, -10f);
            classRect.sizeDelta = new Vector2(-80f, 24f);

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

            // Health Bar Slider
            GameObject sliderGo = new GameObject("HealthSlider", typeof(RectTransform));
            sliderGo.transform.SetParent(panelGo.transform, false);
            RectTransform sliderRect = sliderGo.GetComponent<RectTransform>();
            sliderRect.anchorMin = new Vector2(0f, 1f);
            sliderRect.anchorMax = new Vector2(1f, 1f);
            sliderRect.pivot = new Vector2(0f, 1f);
            sliderRect.anchoredPosition = new Vector2(66f, -38f);
            sliderRect.sizeDelta = new Vector2(-80f, 26f);

            Slider slider = sliderGo.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;

            GameObject sliderBgGo = new GameObject("Background", typeof(RectTransform));
            sliderBgGo.transform.SetParent(sliderGo.transform, false);
            RectTransform sliderBgRect = sliderBgGo.GetComponent<RectTransform>();
            sliderBgRect.anchorMin = Vector2.zero;
            sliderBgRect.anchorMax = Vector2.one;
            sliderBgRect.sizeDelta = Vector2.zero;
            Image sliderBg = sliderBgGo.AddComponent<Image>();
            sliderBg.color = new Color(0.15f, 0.16f, 0.2f, 0.95f);

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

            GameObject hpTextGo = new GameObject("HPText", typeof(RectTransform));
            hpTextGo.transform.SetParent(sliderGo.transform, false);
            RectTransform hpRect = hpTextGo.GetComponent<RectTransform>();
            hpRect.anchorMin = Vector2.zero;
            hpRect.anchorMax = Vector2.one;
            hpRect.sizeDelta = Vector2.zero;

            TextMeshProUGUI hpTmp = hpTextGo.AddComponent<TextMeshProUGUI>();
            hpTmp.fontSize = 13f;
            hpTmp.fontStyle = FontStyles.Bold;
            hpTmp.alignment = TextAlignmentOptions.Center;
            hpTmp.color = Color.white;
            hpTmp.outlineWidth = 0.2f;
            hpTmp.outlineColor = Color.black;
            hpTmp.text = "100 / 100 HP";

            // Mana Bar Slider (Below Health Bar)
            GameObject manaSliderGo = new GameObject("ManaSlider", typeof(RectTransform));
            manaSliderGo.transform.SetParent(panelGo.transform, false);
            RectTransform manaSliderRect = manaSliderGo.GetComponent<RectTransform>();
            manaSliderRect.anchorMin = new Vector2(0f, 1f);
            manaSliderRect.anchorMax = new Vector2(1f, 1f);
            manaSliderRect.pivot = new Vector2(0f, 1f);
            manaSliderRect.anchoredPosition = new Vector2(66f, -70f);
            manaSliderRect.sizeDelta = new Vector2(-80f, 22f);

            Slider mSlider = manaSliderGo.AddComponent<Slider>();
            mSlider.minValue = 0f;
            mSlider.maxValue = 1f;
            mSlider.value = 1f;

            GameObject mBgGo = new GameObject("Background", typeof(RectTransform));
            mBgGo.transform.SetParent(manaSliderGo.transform, false);
            RectTransform mBgRect = mBgGo.GetComponent<RectTransform>();
            mBgRect.anchorMin = Vector2.zero;
            mBgRect.anchorMax = Vector2.one;
            mBgRect.sizeDelta = Vector2.zero;
            Image mBg = mBgGo.AddComponent<Image>();
            mBg.color = new Color(0.12f, 0.15f, 0.25f, 0.95f);

            GameObject mFillArea = new GameObject("Fill Area", typeof(RectTransform));
            mFillArea.transform.SetParent(manaSliderGo.transform, false);
            RectTransform mFillAreaRect = mFillArea.GetComponent<RectTransform>();
            mFillAreaRect.anchorMin = Vector2.zero;
            mFillAreaRect.anchorMax = Vector2.one;
            mFillAreaRect.sizeDelta = Vector2.zero;

            GameObject mFillGo = new GameObject("Fill", typeof(RectTransform));
            mFillGo.transform.SetParent(mFillArea.transform, false);
            RectTransform mFillRect = mFillGo.GetComponent<RectTransform>();
            mFillRect.anchorMin = Vector2.zero;
            mFillRect.anchorMax = Vector2.one;
            mFillRect.sizeDelta = Vector2.zero;
            Image mFillImg = mFillGo.AddComponent<Image>();
            mFillImg.color = new Color(0.2f, 0.65f, 1.0f);

            mSlider.fillRect = mFillRect;
            mSlider.targetGraphic = mFillImg;

            GameObject manaTextGo = new GameObject("ManaText", typeof(RectTransform));
            manaTextGo.transform.SetParent(manaSliderGo.transform, false);
            RectTransform manaRect = manaTextGo.GetComponent<RectTransform>();
            manaRect.anchorMin = Vector2.zero;
            manaRect.anchorMax = Vector2.one;
            manaRect.sizeDelta = Vector2.zero;

            TextMeshProUGUI mTmp = manaTextGo.AddComponent<TextMeshProUGUI>();
            mTmp.fontSize = 12f;
            mTmp.fontStyle = FontStyles.Bold;
            mTmp.alignment = TextAlignmentOptions.Center;
            mTmp.color = Color.white;
            mTmp.outlineWidth = 0.2f;
            mTmp.outlineColor = Color.black;
            mTmp.text = "100 / 100 MP";

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
            hud.manaSlider = mSlider;
            hud.manaFillImage = mFillImg;
            hud.manaText = mTmp;

            // Build Action / Skill Bar at Bottom Center
            hud.BuildSkillBar(canvas.transform);

            return hud;
        }

        private void BuildSkillBar(Transform canvasTransform)
        {
            GameObject skillBarGo = new GameObject("SkillBarPanel", typeof(RectTransform));
            skillBarGo.transform.SetParent(canvasTransform, false);

            RectTransform barRect = skillBarGo.GetComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0.5f, 0f);
            barRect.anchorMax = new Vector2(0.5f, 0f);
            barRect.pivot = new Vector2(0.5f, 0f);
            barRect.anchoredPosition = new Vector2(0f, 25f);
            barRect.sizeDelta = new Vector2(670f, 80f);

            // Action Bar Background Panel
            Image barBg = skillBarGo.AddComponent<Image>();
            barBg.color = new Color(0.06f, 0.07f, 0.1f, 0.90f);

            // Outer Accent Border - must NOT be arranged by the layout group
            GameObject borderGo = new GameObject("Border", typeof(RectTransform));
            borderGo.transform.SetParent(skillBarGo.transform, false);
            RectTransform borderRect = borderGo.GetComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.sizeDelta = Vector2.zero;
            Image borderImg = borderGo.AddComponent<Image>();
            borderImg.color = new Color(0.2f, 0.65f, 1.0f, 0.5f);
            borderImg.raycastTarget = false;
            LayoutElement borderLe = borderGo.AddComponent<LayoutElement>();
            borderLe.ignoreLayout = true; // CRITICAL: prevent HorizontalLayoutGroup from treating Border as a slot

            // Cache the panel transform for runtime rebinding
            skillBarContainer = barRect;

            // Horizontal Layout for 9 Skill Slots
            HorizontalLayoutGroup hlg = skillBarGo.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(10, 10, 10, 10);
            hlg.spacing = 8f;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;

            skillSlots.Clear();

            for (int i = 0; i < 9; i++)
            {
                int slotIndex = i;

                GameObject slotGo = new GameObject($"SkillSlot_{i + 1}", typeof(RectTransform));
                slotGo.transform.SetParent(skillBarGo.transform, false);

                Image slotBg = slotGo.AddComponent<Image>();
                slotBg.color = new Color(0.12f, 0.14f, 0.18f, 0.92f);

                Button slotBtn = slotGo.AddComponent<Button>();
                slotBtn.onClick.AddListener(() =>
                {
                    if (localPlayerSkills != null)
                    {
                        localPlayerSkills.TryCastSkill(slotIndex);
                    }
                });

                // Hotkey Number Badge (Top Left)
                GameObject numBadgeGo = new GameObject("NumberBadge", typeof(RectTransform));
                numBadgeGo.transform.SetParent(slotGo.transform, false);
                RectTransform numRect = numBadgeGo.GetComponent<RectTransform>();
                numRect.anchorMin = new Vector2(0f, 1f);
                numRect.anchorMax = new Vector2(0f, 1f);
                numRect.pivot = new Vector2(0f, 1f);
                numRect.anchoredPosition = new Vector2(3f, -2f);
                numRect.sizeDelta = new Vector2(18f, 18f);

                TextMeshProUGUI numTmp = numBadgeGo.AddComponent<TextMeshProUGUI>();
                numTmp.fontSize = 12f;
                numTmp.fontStyle = FontStyles.Bold;
                numTmp.alignment = TextAlignmentOptions.TopLeft;
                numTmp.color = new Color(0.9f, 0.85f, 0.3f);
                numTmp.text = $"{i + 1}";

                // Icon Symbol Text (Center)
                GameObject iconGo = new GameObject("Icon", typeof(RectTransform));
                iconGo.transform.SetParent(slotGo.transform, false);
                RectTransform iconRect = iconGo.GetComponent<RectTransform>();
                iconRect.anchorMin = Vector2.zero;
                iconRect.anchorMax = Vector2.one;
                iconRect.offsetMin = new Vector2(2f, 14f);
                iconRect.offsetMax = new Vector2(-2f, -14f);

                TextMeshProUGUI iconTmp = iconGo.AddComponent<TextMeshProUGUI>();
                iconTmp.fontSize = 24f;
                iconTmp.alignment = TextAlignmentOptions.Center;
                iconTmp.color = Color.white;
                iconTmp.text = "";

                // Mana Cost Label (Bottom)
                GameObject nameCostGo = new GameObject("CostText", typeof(RectTransform));
                nameCostGo.transform.SetParent(slotGo.transform, false);
                RectTransform costRect = nameCostGo.GetComponent<RectTransform>();
                costRect.anchorMin = new Vector2(0f, 0f);
                costRect.anchorMax = new Vector2(1f, 0f);
                costRect.pivot = new Vector2(0.5f, 0f);
                costRect.anchoredPosition = new Vector2(0f, 2f);
                costRect.sizeDelta = new Vector2(0f, 14f);

                TextMeshProUGUI costTmp = nameCostGo.AddComponent<TextMeshProUGUI>();
                costTmp.fontSize = 10f;
                costTmp.alignment = TextAlignmentOptions.Center;
                costTmp.color = new Color(0.3f, 0.8f, 1.0f);
                costTmp.text = "";

                // Cooldown Fill Overlay
                GameObject cdOverlayGo = new GameObject("CooldownOverlay", typeof(RectTransform));
                cdOverlayGo.transform.SetParent(slotGo.transform, false);
                RectTransform cdRect = cdOverlayGo.GetComponent<RectTransform>();
                cdRect.anchorMin = Vector2.zero;
                cdRect.anchorMax = Vector2.one;
                cdRect.sizeDelta = Vector2.zero;

                Image cdImg = cdOverlayGo.AddComponent<Image>();
                cdImg.color = new Color(0.05f, 0.05f, 0.08f, 0.82f);
                cdImg.type = Image.Type.Filled;
                cdImg.fillMethod = Image.FillMethod.Vertical;
                cdImg.fillOrigin = (int)Image.OriginVertical.Top;
                cdImg.fillAmount = 0f;
                cdOverlayGo.SetActive(false);

                // Cooldown Countdown Text
                GameObject cdTextGo = new GameObject("CooldownText", typeof(RectTransform));
                cdTextGo.transform.SetParent(slotGo.transform, false);
                RectTransform cdTextRect = cdTextGo.GetComponent<RectTransform>();
                cdTextRect.anchorMin = Vector2.zero;
                cdTextRect.anchorMax = Vector2.one;
                cdTextRect.sizeDelta = Vector2.zero;

                TextMeshProUGUI cdTmp = cdTextGo.AddComponent<TextMeshProUGUI>();
                cdTmp.fontSize = 15f;
                cdTmp.fontStyle = FontStyles.Bold;
                cdTmp.alignment = TextAlignmentOptions.Center;
                cdTmp.color = new Color(1.0f, 0.9f, 0.3f);
                cdTextGo.SetActive(false);

                skillSlots.Add(new SkillSlotUI
                {
                    root = slotGo,
                    bgImage = slotBg,
                    iconText = iconTmp,
                    numberBadgeText = numTmp,
                    nameCostText = costTmp,
                    cooldownOverlay = cdImg,
                    cooldownText = cdTmp,
                    button = slotBtn
                });
            }
        }
    }
}


