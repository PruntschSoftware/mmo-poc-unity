using UnityEngine;
using Mirror;

namespace MmoPoC.Combat
{
    public class PlayerMana : NetworkBehaviour
    {
        [Header("Mana Settings")]
        [SerializeField] private int maxMana = 100;
        [SerializeField] private float manaRegenRate = 10f; // Mana regenerated per second

        [SyncVar(hook = nameof(OnManaChanged))]
        private int currentMana = 100;

        private float regenAccumulator = 0f;

        public event System.Action<int, int> OnManaUpdated;

        public int CurrentMana => currentMana;
        public int MaxMana => maxMana;

        public override void OnStartServer()
        {
            base.OnStartServer();
            currentMana = maxMana;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            OnManaUpdated?.Invoke(currentMana, maxMana);
        }

        private void Update()
        {
            if (!isServer) return;

            // Mana Regeneration on Server
            if (currentMana < maxMana)
            {
                regenAccumulator += manaRegenRate * Time.deltaTime;
                if (regenAccumulator >= 1f)
                {
                    int addAmount = Mathf.FloorToInt(regenAccumulator);
                    currentMana = Mathf.Min(maxMana, currentMana + addAmount);
                    regenAccumulator -= addAmount;
                }
            }
            else
            {
                regenAccumulator = 0f;
            }
        }

        [Server]
        public bool ConsumeMana(int amount)
        {
            if (amount <= 0) return true;
            if (currentMana < amount) return false;

            currentMana -= amount;
            return true;
        }

        [Server]
        public void RestoreMana(int amount)
        {
            if (amount <= 0) return;
            currentMana = Mathf.Min(maxMana, currentMana + amount);
        }

        private void OnManaChanged(int oldMana, int newMana)
        {
            OnManaUpdated?.Invoke(newMana, maxMana);
        }
    }
}
