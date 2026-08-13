using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Mirror;
using MmoPoC.Characters;

namespace MmoPoC.Combat
{
    [System.Serializable]
    public class SkillData
    {
        public string id;
        public string name;
        public string iconSymbol;
        public int manaCost;
        public float cooldown;
        public int damage;
        public Color themeColor;
        public bool isExplosive;
        public float explosionRadius;
        public float projectileSpeed;
        public float projectileSize;
        public ProjectileVisual visual;
    }

    public class PlayerSkills : NetworkBehaviour
    {
        private PlayerMana playerMana;
        private PlayerHealth playerHealth;
        private PlayerClassManager classManager;
        private Animator cachedAnimator;

        private List<SkillData> activeSkills = new List<SkillData>();
        private float[] cooldownTimers = new float[9];

        public event Action<List<SkillData>> OnSkillsUpdated;
        public event Action<int, float, float> OnCooldownStarted; // slotIndex, currentCooldown, maxCooldown

        public List<SkillData> ActiveSkills => activeSkills;

        private void Awake()
        {
            playerMana = GetComponent<PlayerMana>();
            playerHealth = GetComponent<PlayerHealth>();
            classManager = GetComponent<PlayerClassManager>();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();

            // CRITICAL: the server must also build the skill list, otherwise
            // CmdCastSkill validation fails on a dedicated (headless) server
            // where OnStartClient never runs.
            if (classManager != null)
            {
                SetupSkillsForClass(classManager.CurrentClass);
            }
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            if (classManager != null)
            {
                classManager.OnClassChangedEvent += OnClassChanged;
                SetupSkillsForClass(classManager.CurrentClass);
            }
        }

        private void OnDestroy()
        {
            if (classManager != null)
            {
                classManager.OnClassChangedEvent -= OnClassChanged;
            }
        }

        private void OnClassChanged(CharacterClass newClass)
        {
            SetupSkillsForClass(newClass);
        }

        public void SetupSkillsForClass(CharacterClass characterClass)
        {
            activeSkills.Clear();

            switch (characterClass)
            {
                case CharacterClass.Warrior:
                    activeSkills.Add(new SkillData
                    {
                        id = "warrior_shockwave",
                        name = "Schockwelle",
                        iconSymbol = "SW",
                        manaCost = 25,
                        cooldown = 1.8f,
                        damage = 35,
                        themeColor = new Color(1.0f, 0.6f, 0.1f),
                        isExplosive = false,
                        projectileSpeed = 24f,
                        projectileSize = 0.8f
                    });
                    activeSkills.Add(new SkillData
                    {
                        id = "warrior_shield",
                        name = "Schildwurf",
                        iconSymbol = "SH",
                        manaCost = 40,
                        cooldown = 4.5f,
                        damage = 55,
                        themeColor = new Color(0.9f, 0.3f, 0.15f),
                        isExplosive = true,
                        explosionRadius = 3.0f,
                        projectileSpeed = 18f,
                        projectileSize = 1.0f
                    });
                    break;

                case CharacterClass.Archer:
                    activeSkills.Add(new SkillData
                    {
                        id = "archer_powerpfeil",
                        name = "Machtpfeil",
                        iconSymbol = "AR",
                        manaCost = 20,
                        cooldown = 1.2f,
                        damage = 40,
                        themeColor = new Color(0.2f, 0.9f, 0.4f),
                        isExplosive = false,
                        projectileSpeed = 30f,
                        projectileSize = 0.5f
                    });
                    activeSkills.Add(new SkillData
                    {
                        id = "archer_volley",
                        name = "Dreifachschuss",
                        iconSymbol = "3X",
                        manaCost = 45,
                        cooldown = 5.0f,
                        damage = 30,
                        themeColor = new Color(0.4f, 1.0f, 0.6f),
                        isExplosive = false,
                        projectileSpeed = 26f,
                        projectileSize = 0.6f
                    });
                    break;

                case CharacterClass.Mage:
                    activeSkills.Add(new SkillData
                    {
                        id = "mage_feuerball",
                        name = "Feuerball",
                        iconSymbol = "FB",
                        manaCost = 30,
                        cooldown = 2.0f,
                        damage = 45,
                        themeColor = new Color(1.0f, 0.25f, 0.15f),
                        isExplosive = true,
                        explosionRadius = 3.5f,
                        projectileSpeed = 20f,
                        projectileSize = 0.9f
                    });
                    activeSkills.Add(new SkillData
                    {
                        id = "mage_froststrahl",
                        name = "Froststrahl",
                        iconSymbol = "FR",
                        manaCost = 50,
                        cooldown = 5.5f,
                        damage = 65,
                        themeColor = new Color(0.2f, 0.7f, 1.0f),
                        isExplosive = true,
                        explosionRadius = 2.5f,
                        projectileSpeed = 22f,
                        projectileSize = 0.8f
                    });
                    break;
            }

            // Assign class-specific projectile visuals
            foreach (var s in activeSkills)
            {
                if (s.id.StartsWith("mage")) s.visual = ProjectileVisual.Fireball;
                else if (s.id.StartsWith("warrior")) s.visual = ProjectileVisual.Sword;
                else if (s.id.StartsWith("archer")) s.visual = ProjectileVisual.Arrow;
                else s.visual = ProjectileVisual.Orb;
            }

            OnSkillsUpdated?.Invoke(activeSkills);
        }

        private void Update()
        {
            // Cooldown timers update locally
            for (int i = 0; i < cooldownTimers.Length; i++)
            {
                if (cooldownTimers[i] > 0f)
                {
                    cooldownTimers[i] -= Time.deltaTime;
                    if (cooldownTimers[i] < 0f) cooldownTimers[i] = 0f;
                }
            }

            if (!isLocalPlayer) return;

            // Handle Keyboard Hotkeys 1 to 9
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            Key[] numKeys = new Key[]
            {
                Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5,
                Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9
            };

            for (int i = 0; i < numKeys.Length; i++)
            {
                if (keyboard[numKeys[i]].wasPressedThisFrame)
                {
                    TryCastSkill(i);
                }
            }
        }

        public void TryCastSkill(int skillIndex)
        {
            if (!isLocalPlayer) return;
            if (skillIndex < 0 || skillIndex >= activeSkills.Count) return;

            SkillData skill = activeSkills[skillIndex];

            if (playerHealth != null && playerHealth.IsDead) return;

            if (cooldownTimers[skillIndex] > 0f)
            {
                Debug.Log($"[PlayerSkills] Skill {skill.name} ist noch auf Abklingzeit! ({cooldownTimers[skillIndex]:F1}s)");
                return;
            }

            if (playerMana != null && playerMana.CurrentMana < skill.manaCost)
            {
                Debug.Log($"[PlayerSkills] Nicht genug Mana für {skill.name}! ({playerMana.CurrentMana}/{skill.manaCost})");
                return;
            }

            // Call Command on Server
            CmdCastSkill(skillIndex);
        }

        [Command]
        private void CmdCastSkill(int skillIndex)
        {
            if (playerHealth != null && playerHealth.IsDead) return;

            // CRITICAL: the server rebuilds its skill list from the CURRENT class every cast.
            // On a dedicated server OnStartClient never runs and the class-change event is
            // never received, so without this the server keeps the skills of the first class
            // the player ever spawned as (icons update client-side, but casts stay stale).
            if (classManager != null)
            {
                SetupSkillsForClass(classManager.CurrentClass);
            }

            if (skillIndex < 0 || skillIndex >= activeSkills.Count) return;
            SkillData skill = activeSkills[skillIndex];

            // Check mana on server
            if (playerMana != null && !playerMana.ConsumeMana(skill.manaCost))
            {
                return; // Not enough mana on server
            }

            // Start cooldown on caller client
            TargetStartCooldown(connectionToClient, skillIndex, skill.cooldown);

            // Play animation across network
            RpcPlayCastAnimation();

            // Spawn Ranged Projectile forward
            Vector3 spawnPos = transform.position + Vector3.up * 1.2f + transform.forward * 0.8f;
            Vector3 shootDir = transform.forward;

            Vector3[] dirs;
            if (skill.id == "archer_volley")
            {
                // Fire 3 arrows in spread
                dirs = new Vector3[]
                {
                    Quaternion.Euler(0, -12f, 0) * shootDir,
                    shootDir,
                    Quaternion.Euler(0, 12f, 0) * shootDir
                };
            }
            else
            {
                dirs = new Vector3[] { shootDir };
            }

            foreach (var d in dirs)
            {
                // Server-authoritative logic projectile (does the actual damage)
                SpawnProjectileOnServer(skill, spawnPos, d);

                // Visual-only projectile on all remote clients (host is skipped inside the RPC)
                RpcSpawnVisualProjectile(spawnPos, d, skill.themeColor, skill.projectileSpeed, skill.projectileSize, (int)skill.visual);
            }
        }

        [Server]
        private void SpawnProjectileOnServer(SkillData skill, Vector3 position, Vector3 direction)
        {
            GameObject projGO = RangedProjectile.CreateProjectilePrefab("SkillProj_" + skill.name, skill.themeColor, skill.projectileSize, skill.visual);
            projGO.transform.position = position;

            RangedProjectile proj = projGO.GetComponent<RangedProjectile>();
            if (proj != null)
            {
                proj.Initialize(netId, direction, skill.projectileSpeed, skill.damage, skill.themeColor, skill.isExplosive, skill.explosionRadius);
            }
        }

        [ClientRpc]
        private void RpcSpawnVisualProjectile(Vector3 position, Vector3 direction, Color color, float speed, float size, int visual)
        {
            // The host already spawned a rendering server projectile - avoid a duplicate visual.
            if (NetworkServer.active) return;

            GameObject projGO = RangedProjectile.CreateProjectilePrefab("SkillProjVisual", color, size, (ProjectileVisual)visual);
            projGO.transform.position = position;

            RangedProjectile proj = projGO.GetComponent<RangedProjectile>();
            if (proj != null)
            {
                // damage 0 and non-explosive: on clients NetworkServer.active is false so no damage is applied anyway,
                // this projectile purely flies forward and shows its impact burst.
                proj.Initialize(netId, direction, speed, 0, color, false, 0f);
            }
        }

        [TargetRpc]
        private void TargetStartCooldown(NetworkConnection target, int slotIndex, float cdDuration)
        {
            if (slotIndex >= 0 && slotIndex < cooldownTimers.Length)
            {
                cooldownTimers[slotIndex] = cdDuration;
                OnCooldownStarted?.Invoke(slotIndex, cdDuration, cdDuration);
            }
        }

        [ClientRpc]
        private void RpcPlayCastAnimation()
        {
            Animator animator = GetActiveAnimator();
            if (animator != null)
            {
                animator.SetInteger("Trigger Number", 2); // Attack / Cast animation trigger
                animator.SetTrigger("Trigger");
            }
        }

        private Animator GetActiveAnimator()
        {
            if (cachedAnimator != null && cachedAnimator.gameObject.activeInHierarchy) return cachedAnimator;

            Animator[] anims = GetComponentsInChildren<Animator>(false);
            foreach (var a in anims)
            {
                if (a.gameObject.activeInHierarchy)
                {
                    cachedAnimator = a;
                    return cachedAnimator;
                }
            }
            return null;
        }

        public float GetCooldownRemaining(int slotIndex)
        {
            if (slotIndex >= 0 && slotIndex < cooldownTimers.Length)
                return cooldownTimers[slotIndex];
            return 0f;
        }
    }
}
