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

        public override void OnStartServer()
        {
            base.OnStartServer();

            // Zufällige Klasse auf dem Server zuweisen (Server-authoritative)
            currentClass = (CharacterClass)Random.Range(0, System.Enum.GetValues(typeof(CharacterClass)).Length);
            UpdateClassVisuals(currentClass);
            Debug.Log($"[PlayerClassManager] Spieler {netId} hat zufällig die Klasse '{currentClass}' erhalten.");
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            UpdateClassVisuals(currentClass);
        }

        private void OnClassChanged(CharacterClass oldClass, CharacterClass newClass)
        {
            UpdateClassVisuals(newClass);
        }

        private void UpdateClassVisuals(CharacterClass characterClass)
        {
            if (warriorModel != null) warriorModel.SetActive(characterClass == CharacterClass.Warrior);
            if (archerModel != null) archerModel.SetActive(characterClass == CharacterClass.Archer);
            if (mageModel != null) mageModel.SetActive(characterClass == CharacterClass.Mage);
        }
    }
}
