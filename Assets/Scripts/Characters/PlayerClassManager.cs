using UnityEngine;
using Mirror;

namespace MmoPoC.Characters
{
    public enum CharacterClass
    {
        Warrior = 0,
        Archer = 1,
        Mage = 2
    }

    public class PlayerClassManager : NetworkBehaviour
    {
        [SyncVar(hook = nameof(OnClassChanged))]
        private CharacterClass currentClass;

        [Header("Class Visuals (Optional 3D Models / Prefab Children)")]
        [SerializeField] private GameObject warriorModel;
        [SerializeField] private GameObject archerModel;
        [SerializeField] private GameObject mageModel;

        public CharacterClass CurrentClass => currentClass;
        public event System.Action<CharacterClass> OnClassChangedEvent;

        private CharacterClass lastAppliedClass = (CharacterClass)(-1);
        private float clientApplyGuard = 0f;

        public override void OnStartServer()
        {
            base.OnStartServer();
            AssignRandomClass();
        }

        /// <summary>
        /// Server-authoritative: assigns a new random class. Used on spawn and on respawn.
        /// The SyncVar hook propagates the change (visuals + skills) to all clients.
        /// </summary>
        [Server]
        public void AssignRandomClass()
        {
            int count = System.Enum.GetValues(typeof(CharacterClass)).Length;
            CharacterClass newClass = (CharacterClass)Random.Range(0, count);

            // Force the hook to fire even if the same value is rolled again by
            // temporarily setting a different value first.
            if (newClass == currentClass && count > 1)
            {
                currentClass = (CharacterClass)(((int)newClass + 1) % count);
            }

            currentClass = newClass;
            ApplyClassVisuals(currentClass);
            Debug.Log($"[PlayerClassManager] SERVER: Spieler {netId} bekam Klasse '{currentClass}'.");
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            // Apply immediately, then keep re-checking briefly in case the SyncVar
            // value arrives a frame or two after OnStartClient (Mirror spawn timing).
            ApplyClassVisuals(currentClass);
            clientApplyGuard = 1.5f;
            Debug.Log($"[PlayerClassManager] CLIENT: Spieler {netId} startet mit Klasse '{currentClass}'.");
        }

        private void Update()
        {
            // Client-side safety: if the synced class differs from what we last applied
            // (because the SyncVar arrived after OnStartClient), re-apply the visuals.
            if (clientApplyGuard > 0f)
            {
                clientApplyGuard -= Time.deltaTime;
                if (currentClass != lastAppliedClass)
                {
                    ApplyClassVisuals(currentClass);
                    // Notify dependents (e.g. PlayerSkills, HUD) so they rebuild for the correct class.
                    OnClassChangedEvent?.Invoke(currentClass);
                    Debug.Log($"[PlayerClassManager] CLIENT: Klasse spät synchronisiert -> '{currentClass}'.");
                }
            }
        }

        private void OnClassChanged(CharacterClass oldClass, CharacterClass newClass)
        {
            ApplyClassVisuals(newClass);
            OnClassChangedEvent?.Invoke(newClass);
        }

        private void ApplyClassVisuals(CharacterClass characterClass)
        {
            lastAppliedClass = characterClass;
            UpdateClassVisuals(characterClass);
        }

        private void UpdateClassVisuals(CharacterClass characterClass)
        {
            if (warriorModel != null) warriorModel.SetActive(characterClass == CharacterClass.Warrior);
            if (archerModel != null) archerModel.SetActive(characterClass == CharacterClass.Archer);
            if (mageModel != null) mageModel.SetActive(characterClass == CharacterClass.Mage);
        }
    }
}

