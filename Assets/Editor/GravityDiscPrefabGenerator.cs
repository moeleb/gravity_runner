#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GravityHalfDead.Editor
{
    public static class GravityDiscPrefabGenerator
    {
        private const string PrefabFolder = "Assets/Resources/Discs3D";
        private const string MaterialFolder = "Assets/Generated/Discs/Materials";
        private static readonly string[] Ids =
        {
            "core_runner", "pulse_ring", "neon_orbit", "ion_skimmer", "void_circuit", "comet_drive",
            "prism_halo", "rift_bloom", "quantum_crown", "solar_forge", "abyss_engine", "aurora_sovereign"
        };
        private static readonly string[] Colors =
        {
            "18D9FF", "37E8FF", "8470FF", "23D4FF", "B24CFF", "FF9048",
            "56F7D4", "FF4EC8", "E9C35A", "FFB72E", "9C48FF", "42E8FF"
        };

        [MenuItem("Gravity Half Dead/Generate Rideable Discs")]
        public static void Generate()
        {
            EnsureFolder(PrefabFolder);
            EnsureFolder(MaterialFolder);
            for (var i = 0; i < Ids.Length; i++)
                GenerateDisc(i);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Generated 12 rider-ready gravity disc prefabs in " + PrefabFolder);
        }

        private static void GenerateDisc(int index)
        {
            var id = Ids[index];
            var accent = Html(Colors[index]);
            var dark = Color.Lerp(accent, new Color(0.01f, 0.025f, 0.07f), 0.78f);
            var deckMaterial = GetOrCreateMaterial(id + "_deck", dark, accent * 0.30f);
            var glowMaterial = GetOrCreateMaterial(id + "_glow", Color.Lerp(accent, Color.white, 0.08f), accent * 3.1f);

            var root = new GameObject(id);
            root.transform.position = Vector3.zero;
            var rideable = root.AddComponent<GravityDiscRideable>();
            var collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, -0.12f, 0f);
            collider.size = new Vector3(1.58f, 0.20f, 1.58f);

            var deck = Primitive(root.transform, "Rider deck", PrimitiveType.Cylinder,
                new Vector3(0f, -0.12f, 0f), new Vector3(0.90f, 0.09f, 0.90f), deckMaterial);
            deck.transform.localRotation = Quaternion.identity;
            Primitive(root.transform, "Lower drive", PrimitiveType.Cylinder,
                new Vector3(0f, -0.24f, 0f), new Vector3(0.70f, 0.08f, 0.70f), deckMaterial);
            Primitive(root.transform, "Core lens", PrimitiveType.Sphere,
                new Vector3(0f, -0.03f, 0f), new Vector3(0.28f, 0.055f, 0.28f), glowMaterial);

            var outerRing = CreateTorus("Outer anti-gravity ring", 0.76f, 0.055f, 48, 10);
            outerRing.transform.SetParent(root.transform, false);
            outerRing.transform.localPosition = new Vector3(0f, -0.08f, 0f);
            outerRing.GetComponent<MeshRenderer>().sharedMaterial = glowMaterial;
            var innerRing = CreateTorus("Inner energy ring", 0.48f, 0.025f, 48, 8);
            innerRing.transform.SetParent(root.transform, false);
            innerRing.transform.localPosition = new Vector3(0f, -0.005f, 0f);
            innerRing.GetComponent<MeshRenderer>().sharedMaterial = glowMaterial;

            AddVariantGeometry(root.transform, index, deckMaterial, glowMaterial);

            var riderMount = Anchor(root.transform, "RiderMount", new Vector3(0f, 0.055f, 0f));
            var leftTrail = Anchor(root.transform, "LeftTrailAnchor", new Vector3(-0.46f, -0.17f, -0.64f));
            var rightTrail = Anchor(root.transform, "RightTrailAnchor", new Vector3(0.46f, -0.17f, -0.64f));
            rideable.Configure(id, riderMount, leftTrail, rightTrail);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabFolder + "/" + id + ".prefab");
            Object.DestroyImmediate(root);
        }

        private static void AddVariantGeometry(Transform root, int index, Material deck, Material glow)
        {
            var spokeCount = 3 + index % 4;
            for (var i = 0; i < spokeCount; i++)
            {
                var angle = i * 360f / spokeCount + index * 7f;
                var spoke = Primitive(root, "Energy spoke " + i, PrimitiveType.Cube,
                    Quaternion.Euler(0f, angle, 0f) * new Vector3(0f, -0.025f, 0.40f),
                    new Vector3(0.055f, 0.025f, 0.48f), glow);
                spoke.transform.localRotation = Quaternion.Euler(0f, angle, 0f);
            }
            if (index >= 5)
            {
                for (var i = 0; i < 4; i++)
                {
                    var angle = i * 90f + 45f;
                    var fin = Primitive(root, "Stability fin " + i, PrimitiveType.Cube,
                        Quaternion.Euler(0f, angle, 0f) * new Vector3(0f, -0.16f, 0.83f),
                        new Vector3(0.18f, 0.06f, 0.30f), index >= 9 ? glow : deck);
                    fin.transform.localRotation = Quaternion.Euler(0f, angle, 0f);
                }
            }
            if (index >= 9)
            {
                var crown = CreateTorus("Premium crown ring", 0.62f, 0.035f, 48, 8);
                crown.transform.SetParent(root, false);
                crown.transform.localPosition = new Vector3(0f, 0.025f, 0f);
                crown.transform.localRotation = Quaternion.Euler(5f + (index - 9) * 5f, 0f, 0f);
                crown.GetComponent<MeshRenderer>().sharedMaterial = glow;
            }
        }

        private static GameObject Primitive(Transform parent, string name, PrimitiveType type,
            Vector3 localPosition, Vector3 localScale, Material material)
        {
            var result = GameObject.CreatePrimitive(type);
            result.name = name;
            result.transform.SetParent(parent, false);
            result.transform.localPosition = localPosition;
            result.transform.localScale = localScale;
            var primitiveCollider = result.GetComponent<Collider>();
            if (primitiveCollider != null)
                Object.DestroyImmediate(primitiveCollider);
            result.GetComponent<Renderer>().sharedMaterial = material;
            return result;
        }

        private static Transform Anchor(Transform parent, string name, Vector3 position)
        {
            var anchor = new GameObject(name).transform;
            anchor.SetParent(parent, false);
            anchor.localPosition = position;
            return anchor;
        }

        private static GameObject CreateTorus(string name, float radius, float tube, int segments, int sides)
        {
            var vertices = new Vector3[segments * sides];
            var normals = new Vector3[vertices.Length];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[segments * sides * 6];
            for (var segment = 0; segment < segments; segment++)
            {
                var u = segment / (float)segments * Mathf.PI * 2f;
                for (var side = 0; side < sides; side++)
                {
                    var v = side / (float)sides * Mathf.PI * 2f;
                    var index = segment * sides + side;
                    var radial = new Vector3(Mathf.Cos(u), 0f, Mathf.Sin(u));
                    vertices[index] = radial * (radius + tube * Mathf.Cos(v)) + Vector3.up * tube * Mathf.Sin(v);
                    normals[index] = (radial * Mathf.Cos(v) + Vector3.up * Mathf.Sin(v)).normalized;
                    uv[index] = new Vector2(segment / (float)segments, side / (float)sides);
                    var nextSegment = (segment + 1) % segments;
                    var nextSide = (side + 1) % sides;
                    var t = index * 6;
                    triangles[t] = index;
                    triangles[t + 1] = nextSegment * sides + side;
                    triangles[t + 2] = nextSegment * sides + nextSide;
                    triangles[t + 3] = index;
                    triangles[t + 4] = nextSegment * sides + nextSide;
                    triangles[t + 5] = segment * sides + nextSide;
                }
            }
            var mesh = new Mesh { name = name + " Mesh", vertices = vertices, normals = normals, uv = uv, triangles = triangles };
            mesh.RecalculateBounds();
            var result = new GameObject(name);
            result.AddComponent<MeshFilter>().sharedMesh = mesh;
            result.AddComponent<MeshRenderer>();
            return result;
        }

        private static Material GetOrCreateMaterial(string name, Color baseColor, Color emission)
        {
            var path = MaterialFolder + "/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                return existing;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { name = name, color = baseColor };
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", emission);
            material.SetFloat("_Smoothness", 0.78f);
            material.SetFloat("_Metallic", 0.72f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Color Html(string value)
        {
            ColorUtility.TryParseHtmlString("#" + value, out var color);
            return color;
        }

        private static void EnsureFolder(string path)
        {
            var current = "Assets";
            foreach (var part in path.Substring("Assets/".Length).Split('/'))
            {
                var next = current + "/" + part;
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, part);
                current = next;
            }
        }
    }
}
#endif
