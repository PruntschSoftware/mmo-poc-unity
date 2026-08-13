using UnityEngine;
using Mirror;

namespace MmoPoC.Combat
{
    public enum ProjectileVisual
    {
        Orb = 0,
        Fireball = 1,
        Sword = 2,
        Arrow = 3
    }

    public class RangedProjectile : MonoBehaviour
    {
        private float speed = 22f;
        private int damage = 35;
        private float radius = 0.8f;
        private float lifetime = 3.5f;
        private uint ownerNetId;
        private Color elementColor = Color.yellow;
        private bool isExplosive = false;
        private float explosionRadius = 2.5f;

        private Vector3 direction;
        private float spawnTime;
        private bool hasHit = false;

        // Visual behavior
        public ProjectileVisual Visual = ProjectileVisual.Orb;
        private Vector3 spinAxisLocal = Vector3.forward;
        private float spinSpeed = 0f;
        private Transform visualRoot;

        public void Initialize(uint ownerId, Vector3 dir, float speed, int damage, Color color, bool explosive = false, float expRadius = 2.5f)
        {
            this.ownerNetId = ownerId;
            this.direction = dir.normalized;
            this.speed = speed;
            this.damage = damage;
            this.elementColor = color;
            this.isExplosive = explosive;
            this.explosionRadius = expRadius;
            this.spawnTime = Time.time;

            // Orient the projectile so it flies naturally along its travel direction
            transform.rotation = Quaternion.LookRotation(this.direction, Vector3.up);

            // Configure per-visual spin/tumble
            switch (Visual)
            {
                case ProjectileVisual.Sword:
                    // tumble end-over-end around local right axis
                    spinAxisLocal = Vector3.right;
                    spinSpeed = 720f;
                    break;
                case ProjectileVisual.Arrow:
                    // subtle roll around travel axis
                    spinAxisLocal = Vector3.forward;
                    spinSpeed = 160f;
                    break;
                case ProjectileVisual.Fireball:
                    spinAxisLocal = new Vector3(0.3f, 1f, 0.2f).normalized;
                    spinSpeed = 220f;
                    break;
                default:
                    spinAxisLocal = Vector3.forward;
                    spinSpeed = 90f;
                    break;
            }
        }

        private void Update()
        {
            if (hasHit) return;

            // Spin / tumble the visual for flair
            if (spinSpeed != 0f)
            {
                transform.Rotate(spinAxisLocal, spinSpeed * Time.deltaTime, Space.Self);
            }

            // Move projectile
            Vector3 moveDelta = direction * speed * Time.deltaTime;
            Vector3 nextPos = transform.position + moveDelta;

            // Check hit along path
            if (NetworkServer.active)
            {
                Collider[] hits = Physics.OverlapSphere(nextPos, radius);
                foreach (var hit in hits)
                {
                    // Don't hit self/owner or trigger colliders
                    if (hit.isTrigger) continue;

                    PlayerHealth targetHealth = hit.GetComponentInParent<PlayerHealth>();
                    if (targetHealth != null)
                    {
                        if (targetHealth.netId == ownerNetId) continue; // Skip owner

                        // Hit valid enemy!
                        ApplyHit(targetHealth);
                        return;
                    }
                }
            }

            transform.position = nextPos;

            // Destroy after lifetime
            if (Time.time - spawnTime > lifetime)
            {
                Destroy(gameObject);
            }
        }

        private void ApplyHit(PlayerHealth primaryTarget)
        {
            hasHit = true;

            if (NetworkServer.active)
            {
                if (isExplosive)
                {
                    // AoE Explosion
                    Collider[] areaHits = Physics.OverlapSphere(transform.position, explosionRadius);
                    System.Collections.Generic.HashSet<PlayerHealth> hitTargets = new System.Collections.Generic.HashSet<PlayerHealth>();

                    foreach (var col in areaHits)
                    {
                        PlayerHealth target = col.GetComponentInParent<PlayerHealth>();
                        if (target != null && target.netId != ownerNetId && !target.IsDead && !hitTargets.Contains(target))
                        {
                            hitTargets.Add(target);
                            target.TakeDamage(damage);
                        }
                    }
                }
                else
                {
                    // Single / Piercing hit
                    if (primaryTarget != null && !primaryTarget.IsDead)
                    {
                        primaryTarget.TakeDamage(damage);
                    }
                }
            }

            // Spawn Visual Impact Burst
            SpawnImpactVFX(transform.position, elementColor, isExplosive ? explosionRadius : 1.2f);
            Destroy(gameObject);
        }

        public static void SpawnImpactVFX(Vector3 position, Color color, float scale = 1.2f)
        {
            GameObject impact = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            impact.name = "ImpactVFX";
            impact.transform.position = position;
            impact.transform.localScale = Vector3.one * scale;

            Collider col = impact.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);

            Renderer r = impact.GetComponent<Renderer>();
            if (r != null)
            {
                Shader s = Shader.Find("HDRP/Unlit");
                if (s == null) s = Shader.Find("Unlit/Color");
                Material mat = new Material(s);
                if (mat.HasProperty("_UnlitColor")) mat.SetColor("_UnlitColor", color * 2f);
                else if (mat.HasProperty("_Color")) mat.color = color;
                r.sharedMaterial = mat;
            }

            // Fade and destroy
            Object.Destroy(impact, 0.25f);
        }

        public static GameObject CreateProjectilePrefab(string name, Color color, float visualRadius, ProjectileVisual visual)
        {
            GameObject root;
            switch (visual)
            {
                case ProjectileVisual.Fireball: root = BuildFireball(color, visualRadius); break;
                case ProjectileVisual.Sword:    root = BuildSword(color, visualRadius); break;
                case ProjectileVisual.Arrow:    root = BuildArrow(color, visualRadius); break;
                default:                        root = BuildOrb(color, visualRadius); break;
            }

            root.name = name;
            RangedProjectile rp = root.AddComponent<RangedProjectile>();
            rp.Visual = visual;
            return root;
        }

        // ---------- Material helpers ----------

        private static Material MakeUnlit(Color color, float brightness)
        {
            Shader s = Shader.Find("HDRP/Unlit");
            if (s == null) s = Shader.Find("Unlit/Color");
            Material mat = new Material(s);
            Color c = color * brightness;
            c.a = 1f;
            if (mat.HasProperty("_UnlitColor")) mat.SetColor("_UnlitColor", c);
            if (mat.HasProperty("_Color")) mat.color = c;
            return mat;
        }

        private static Material MakeMetal(Color baseCol, float metallic, float smoothness)
        {
            Shader s = Shader.Find("HDRP/Lit");
            if (s == null) s = Shader.Find("Standard");
            Material mat = new Material(s);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseCol);
            if (mat.HasProperty("_Color")) mat.color = baseCol;
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            return mat;
        }

        private static GameObject MakePart(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 localScale, Quaternion localRot, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            Collider c = go.GetComponent<Collider>();
            if (c != null) Object.Destroy(c);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.transform.localScale = localScale;
            Renderer r = go.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = mat;
            return go;
        }

        private static void AddPointLight(Transform parent, Color color, float intensity, float range)
        {
            GameObject lightGo = new GameObject("Light");
            lightGo.transform.SetParent(parent, false);
            Light l = lightGo.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
        }

        private static void AddTrail(GameObject root, Color color, float startWidth, float time)
        {
            TrailRenderer tr = root.AddComponent<TrailRenderer>();
            tr.time = time;
            tr.startWidth = startWidth;
            tr.endWidth = 0f;
            tr.minVertexDistance = 0.05f;
            tr.material = MakeUnlit(color, 2.5f);
            tr.numCapVertices = 4;

            Gradient g = new Gradient();
            g.SetKeys(
                new GradientColorKey[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) }
            );
            tr.colorGradient = g;
        }

        // ---------- Visual builders ----------

        private static GameObject BuildOrb(Color color, float size)
        {
            GameObject root = new GameObject("Orb");
            Material core = MakeUnlit(color, 3f);
            MakePart(PrimitiveType.Sphere, root.transform, Vector3.zero, Vector3.one * size, Quaternion.identity, core);
            AddPointLight(root.transform, color, 160f, 6f);
            AddTrail(root, color, size * 0.9f, 0.22f);
            return root;
        }

        private static GameObject BuildFireball(Color color, float size)
        {
            GameObject root = new GameObject("Fireball");

            // Bright molten core
            Material coreMat = MakeUnlit(color, 4f);
            MakePart(PrimitiveType.Sphere, root.transform, Vector3.zero, Vector3.one * size, Quaternion.identity, coreMat);

            // Lumpy outer flames (slightly transparent-looking bright blobs)
            Color flame = Color.Lerp(color, new Color(1f, 0.85f, 0.3f), 0.5f);
            Material flameMat = MakeUnlit(flame, 3f);
            MakePart(PrimitiveType.Sphere, root.transform, new Vector3(size * 0.35f, size * 0.15f, -size * 0.2f), Vector3.one * size * 0.7f, Quaternion.identity, flameMat);
            MakePart(PrimitiveType.Sphere, root.transform, new Vector3(-size * 0.3f, -size * 0.2f, -size * 0.15f), Vector3.one * size * 0.6f, Quaternion.identity, flameMat);
            MakePart(PrimitiveType.Sphere, root.transform, new Vector3(0f, size * 0.3f, size * 0.25f), Vector3.one * size * 0.55f, Quaternion.identity, flameMat);

            AddPointLight(root.transform, color, 320f, 9f);
            AddTrail(root, flame, size * 1.3f, 0.3f);
            return root;
        }

        private static GameObject BuildSword(Color color, float size)
        {
            GameObject root = new GameObject("Sword");

            Material steel = MakeMetal(new Color(0.75f, 0.78f, 0.82f), 1f, 0.9f);
            Material dark = MakeMetal(new Color(0.15f, 0.12f, 0.1f), 0.2f, 0.4f);
            Material accent = MakeUnlit(color, 2.5f);

            float scale = size * 1.4f;

            // Blade - long thin box pointing forward (+Z)
            MakePart(PrimitiveType.Cube, root.transform,
                new Vector3(0f, 0f, scale * 0.9f),
                new Vector3(scale * 0.12f, scale * 0.03f, scale * 1.7f),
                Quaternion.identity, steel);

            // Blade tip (rotated cube = diamond point)
            MakePart(PrimitiveType.Cube, root.transform,
                new Vector3(0f, 0f, scale * 1.8f),
                new Vector3(scale * 0.12f, scale * 0.03f, scale * 0.12f),
                Quaternion.Euler(0f, 45f, 0f), steel);

            // Crossguard
            MakePart(PrimitiveType.Cube, root.transform,
                new Vector3(0f, 0f, 0f),
                new Vector3(scale * 0.55f, scale * 0.08f, scale * 0.1f),
                Quaternion.identity, accent);

            // Handle
            MakePart(PrimitiveType.Cylinder, root.transform,
                new Vector3(0f, 0f, -scale * 0.28f),
                new Vector3(scale * 0.06f, scale * 0.22f, scale * 0.06f),
                Quaternion.Euler(90f, 0f, 0f), dark);

            // Pommel
            MakePart(PrimitiveType.Sphere, root.transform,
                new Vector3(0f, 0f, -scale * 0.52f),
                Vector3.one * scale * 0.14f,
                Quaternion.identity, accent);

            AddPointLight(root.transform, color, 120f, 5f);
            AddTrail(root, color, size * 0.7f, 0.28f);
            return root;
        }

        private static GameObject BuildArrow(Color color, float size)
        {
            GameObject root = new GameObject("Arrow");

            Material wood = MakeMetal(new Color(0.45f, 0.32f, 0.18f), 0.1f, 0.3f);
            Material head = MakeUnlit(color, 3f);
            Material fletch = MakeUnlit(color, 2f);

            float scale = size * 2.2f;

            // Shaft - long thin cylinder along +Z
            MakePart(PrimitiveType.Cylinder, root.transform,
                new Vector3(0f, 0f, 0f),
                new Vector3(scale * 0.05f, scale * 0.9f, scale * 0.05f),
                Quaternion.Euler(90f, 0f, 0f), wood);

            // Arrowhead - diamond tip at the front
            MakePart(PrimitiveType.Cube, root.transform,
                new Vector3(0f, 0f, scale * 1.0f),
                new Vector3(scale * 0.16f, scale * 0.16f, scale * 0.28f),
                Quaternion.Euler(0f, 45f, 0f), head);

            // Fletching - three angled fins at the back
            for (int i = 0; i < 3; i++)
            {
                float ang = i * 120f;
                GameObject fin = MakePart(PrimitiveType.Cube, root.transform,
                    Vector3.zero,
                    new Vector3(scale * 0.02f, scale * 0.22f, scale * 0.28f),
                    Quaternion.Euler(0f, 0f, ang), fletch);
                // push out along the fin's local up after rotation, and back toward tail
                fin.transform.localPosition = Quaternion.Euler(0f, 0f, ang) * new Vector3(0f, scale * 0.12f, 0f) + new Vector3(0f, 0f, -scale * 0.78f);
            }

            AddPointLight(root.transform, color, 90f, 4f);
            AddTrail(root, color, size * 0.5f, 0.2f);
            return root;
        }
    }
}
