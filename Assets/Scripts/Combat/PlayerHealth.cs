using System.Collections;
using UnityEngine;
using Mirror;
using MmoPoC.Characters;
using MmoPoC.UI;

namespace MmoPoC.Combat
{
    public class PlayerHealth : NetworkBehaviour
    {
        [Header("Health Settings")]
        [SerializeField] private int maxHealth = 100;

        [SyncVar(hook = nameof(OnHealthChanged))]
        private int currentHealth = 100;

        [SyncVar(hook = nameof(OnDeathStateChanged))]
        private bool isDead = false;

        [Header("Hit Feedback Settings")]
        [SerializeField] private float hitFlashDuration = 0.2f;
        [SerializeField] private Color hitFlashColor = new Color(1f, 0.2f, 0.2f, 1f);

        // Events
        public event System.Action<int, int> OnHealthUpdated;
        public event System.Action OnDeath;
        public event System.Action OnRespawn;

        public int CurrentHealth => currentHealth;
        public int MaxHealth => maxHealth;
        public bool IsDead => isDead;

        private PlayerMovement playerMovement;
        private CharacterController characterController;
        private Renderer[] cachedRenderers;
        private Color[] originalColors;
        private Vector3 initialScale = Vector3.one;

        private void Awake()
        {
            playerMovement = GetComponent<PlayerMovement>();
            characterController = GetComponent<CharacterController>();
            initialScale = transform.localScale;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            currentHealth = maxHealth;
            isDead = false;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            CacheRenderers();

            OverheadHealthBar overhead = GetComponentInChildren<OverheadHealthBar>(true);
            if (overhead != null)
            {
                overhead.Initialize(this, isLocalPlayer);
            }

            OnHealthUpdated?.Invoke(currentHealth, maxHealth);
            if (isDead)
            {
                ApplyDeathState(true);
            }
        }

        private void CacheRenderers()
        {
            cachedRenderers = GetComponentsInChildren<Renderer>(true);
            if (cachedRenderers != null && cachedRenderers.Length > 0)
            {
                originalColors = new Color[cachedRenderers.Length];
                for (int i = 0; i < cachedRenderers.Length; i++)
                {
                    if (cachedRenderers[i].material.HasProperty("_Color"))
                    {
                        originalColors[i] = cachedRenderers[i].material.color;
                    }
                    else if (cachedRenderers[i].material.HasProperty("_BaseColor"))
                    {
                        originalColors[i] = cachedRenderers[i].material.GetColor("_BaseColor");
                    }
                }
            }
        }

        [Server]
        public void TakeDamage(int damage)
        {
            if (isDead || damage <= 0) return;

            currentHealth = Mathf.Max(0, currentHealth - damage);
            Debug.Log($"[PlayerHealth] Player {netId} took {damage} damage! Remaining HP: {currentHealth}");

            RpcOnHit(damage);

            if (currentHealth <= 0)
            {
                Die();
            }
        }

        [Server]
        private void Die()
        {
            if (isDead) return;

            isDead = true;
            Debug.Log($"[PlayerHealth] Player {netId} died!");

            // Schedule respawn after 3 seconds
            StartCoroutine(ServerRespawnRoutine(3f));
        }

        [Server]
        private IEnumerator ServerRespawnRoutine(float delay)
        {
            yield return new WaitForSeconds(delay);

            currentHealth = maxHealth;
            isDead = false;

            // Assign a fresh random class on respawn (server-authoritative)
            PlayerClassManager classManager = GetComponent<PlayerClassManager>();
            if (classManager != null)
            {
                classManager.AssignRandomClass();
            }

            // Teleport back to spawn or start position with random offset
            Vector3 spawnPos = Vector3.zero;
            if (NetworkManager.singleton != null)
            {
                Transform startPos = NetworkManager.singleton.GetStartPosition();
                if (startPos != null)
                {
                    spawnPos = startPos.position;
                }
            }

            Vector2 randomOffset = Random.insideUnitCircle * 3.5f;
            spawnPos += new Vector3(randomOffset.x, 0f, randomOffset.y);

            RpcRespawn(spawnPos);
        }

        [ClientRpc]
        private void RpcOnHit(int damage)
        {
            // Spawn damage popup text
            FloatingDamageText.Spawn(transform.position, damage);

            // Hit feedback flash
            StopAllCoroutines();
            StartCoroutine(HitFeedbackRoutine());
        }

        private IEnumerator HitFeedbackRoutine()
        {
            if (cachedRenderers == null || cachedRenderers.Length == 0)
            {
                CacheRenderers();
            }

            // Red color flash on mesh
            if (cachedRenderers != null)
            {
                for (int i = 0; i < cachedRenderers.Length; i++)
                {
                    if (cachedRenderers[i] == null) continue;
                    if (cachedRenderers[i].material.HasProperty("_Color"))
                        cachedRenderers[i].material.color = hitFlashColor;
                    else if (cachedRenderers[i].material.HasProperty("_BaseColor"))
                        cachedRenderers[i].material.SetColor("_BaseColor", hitFlashColor);
                }
            }

            yield return new WaitForSeconds(hitFlashDuration);

            // Reset renderer colors
            if (cachedRenderers != null && originalColors != null)
            {
                for (int i = 0; i < cachedRenderers.Length; i++)
                {
                    if (cachedRenderers[i] == null) continue;
                    if (cachedRenderers[i].material.HasProperty("_Color"))
                        cachedRenderers[i].material.color = originalColors[i];
                    else if (cachedRenderers[i].material.HasProperty("_BaseColor"))
                        cachedRenderers[i].material.SetColor("_BaseColor", originalColors[i]);
                }
            }
        }

        [ClientRpc]
        private void RpcRespawn(Vector3 newPosition)
        {
            if (characterController != null)
            {
                characterController.enabled = false;
            }

            transform.position = newPosition;

            if (characterController != null && isLocalPlayer)
            {
                characterController.enabled = true;
            }

            ApplyDeathState(false);
            OnRespawn?.Invoke();
            OnHealthUpdated?.Invoke(currentHealth, maxHealth);
        }

        private void OnHealthChanged(int oldHp, int newHp)
        {
            OnHealthUpdated?.Invoke(newHp, maxHealth);
        }

        private void OnDeathStateChanged(bool oldDead, bool newDead)
        {
            ApplyDeathState(newDead);
        }

        private void ApplyDeathState(bool dead)
        {
            if (playerMovement != null)
            {
                playerMovement.enabled = !dead;
            }

            if (characterController != null && !isLocalPlayer)
            {
                characterController.enabled = false;
            }

            if (dead)
            {
                OnDeath?.Invoke();
            }

            // Hide/Show visuals on death
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                // Keep UI canvas visible if needed, hide character mesh
                if (r.GetComponentInParent<Canvas>() == null)
                {
                    r.enabled = !dead;
                }
            }
        }
    }
}
