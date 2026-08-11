using UnityEngine;
using TMPro;

namespace MmoPoC.Combat
{
    public class FloatingDamageText : MonoBehaviour
    {
        [SerializeField] private TextMeshPro textMesh;
        [SerializeField] private float floatSpeed = 1.5f;
        [SerializeField] private float fadeDuration = 0.8f;

        private Color textColor;
        private float timer;

        private void Awake()
        {
            if (textMesh == null)
            {
                textMesh = GetComponent<TextMeshPro>();
            }
        }

        public void Setup(int damageAmount, Color color)
        {
            if (textMesh == null)
            {
                textMesh = GetComponent<TextMeshPro>();
            }

            if (textMesh != null)
            {
                textMesh.text = $"-{damageAmount}";
                textMesh.color = color;
                textColor = color;
                textMesh.fontSize = 5f;
                textMesh.alignment = TextAlignmentOptions.Center;
            }

            timer = 0f;
        }

        private void Update()
        {
            // Move upwards
            transform.position += Vector3.up * (floatSpeed * Time.deltaTime);

            // Face main camera
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                transform.rotation = Quaternion.LookRotation(transform.position - mainCam.transform.position);
            }

            // Fade out
            timer += Time.deltaTime;
            if (fadeDuration > 0f)
            {
                float alpha = Mathf.Clamp01(1f - (timer / fadeDuration));
                if (textMesh != null)
                {
                    textMesh.color = new Color(textColor.r, textColor.g, textColor.b, alpha);
                }
            }

            if (timer >= fadeDuration)
            {
                Destroy(gameObject);
            }
        }

        public static void Spawn(Vector3 position, int amount)
        {
            GameObject go = new GameObject("FloatingDamageText");
            go.transform.position = position + Vector3.up * 2.2f + Random.insideUnitSphere * 0.2f;

            TextMeshPro tmp = go.AddComponent<TextMeshPro>();
            tmp.sortingOrder = 100;

            FloatingDamageText damageText = go.AddComponent<FloatingDamageText>();
            damageText.Setup(amount, new Color(1f, 0.25f, 0.25f, 1f));
        }
    }
}
