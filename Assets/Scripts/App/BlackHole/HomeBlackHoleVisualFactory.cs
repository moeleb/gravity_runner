using UnityEngine;
using UnityEngine.UI;

namespace GravityHalfDead
{
    /// <summary>
    /// Runtime-only visual factory for the animated home black-hole wallpaper.
    /// No imported wallpaper or rock art is required.
    /// </summary>
    internal static class HomeBlackHoleVisualFactory
    {
        private static Sprite softCircle;
        private static Sprite hardCircle;
        private static readonly Sprite[] vortexSprites = new Sprite[5];
        private static readonly Sprite[] rockSprites = new Sprite[8];

        public static Image CreateStretchImage(string name, Transform parent, Color color, Sprite sprite = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static Image CreateCenteredImage(string name, Transform parent, Vector2 position, Vector2 size,
            Color color, Sprite sprite)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static RectTransform CreateCenteredRoot(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        public static Sprite SoftCircle()
        {
            if (softCircle == null)
                softCircle = BuildCircleSprite(160, true);
            return softCircle;
        }

        public static Sprite HardCircle()
        {
            if (hardCircle == null)
                hardCircle = BuildCircleSprite(128, false);
            return hardCircle;
        }

        public static Sprite Vortex(int variant)
        {
            variant = Mathf.Abs(variant) % vortexSprites.Length;
            if (vortexSprites[variant] == null)
                vortexSprites[variant] = BuildVortexSprite(variant);
            return vortexSprites[variant];
        }

        public static Sprite Rock(int variant)
        {
            variant = Mathf.Abs(variant) % rockSprites.Length;
            if (rockSprites[variant] == null)
                rockSprites[variant] = BuildRockSprite(variant);
            return rockSprites[variant];
        }

        private static Sprite BuildCircleSprite(int size, bool soft)
        {
            var texture = NewTexture(size, size, soft ? "Home Soft Circle" : "Home Circle");
            var pixels = new Color32[size * size];
            var center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            var radius = size * 0.49f;

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var distance = Vector2.Distance(new Vector2(x, y), center) / radius;
                    float alpha;
                    if (soft)
                    {
                        alpha = Mathf.Clamp01(1f - distance);
                        alpha = alpha * alpha * (3f - 2f * alpha);
                    }
                    else
                    {
                        alpha = Mathf.Clamp01((1.01f - distance) * 18f);
                    }
                    pixels[y * size + x] = new Color32(255, 255, 255,
                        (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite BuildVortexSprite(int variant)
        {
            const int size = 256;
            var texture = NewTexture(size, size, "Home Vortex " + variant);
            var pixels = new Color32[size * size];
            var center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            var radius = size * 0.5f;
            var phase = variant * 1.31f;

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var delta = new Vector2(x, y) - center;
                    var r = delta.magnitude / radius;
                    if (r > 1f || r < 0.20f)
                    {
                        pixels[y * size + x] = new Color32(255, 255, 255, 0);
                        continue;
                    }

                    var angle = Mathf.Atan2(delta.y, delta.x);
                    var spiral = angle * (2.1f + variant * 0.08f) + r * (15f + variant * 1.7f) + phase;
                    var strands = Mathf.Abs(Mathf.Sin(spiral));
                    strands = Mathf.Pow(strands, 5.2f);

                    var ringA = Mathf.Exp(-Mathf.Pow((r - 0.50f) / 0.12f, 2f));
                    var ringB = Mathf.Exp(-Mathf.Pow((r - 0.72f) / 0.16f, 2f));
                    var ringC = Mathf.Exp(-Mathf.Pow((r - 0.88f) / 0.10f, 2f));
                    var radial = Mathf.Clamp01(ringA * 0.82f + ringB * 0.90f + ringC * 0.50f);
                    var noise = 0.72f + 0.28f * Mathf.PerlinNoise(x * 0.055f + variant * 2.7f, y * 0.055f + variant * 3.1f);
                    var edgeFade = Mathf.Clamp01((1f - r) * 7f) * Mathf.Clamp01((r - 0.18f) * 7f);
                    var alpha = Mathf.Clamp01(radial * (0.18f + strands * 0.94f) * noise * edgeFade);
                    pixels[y * size + x] = new Color32(255, 255, 255,
                        (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite BuildRockSprite(int variant)
        {
            const int size = 128;
            var texture = NewTexture(size, size, "Home Orbit Rock " + variant);
            var pixels = new Color32[size * size];
            var center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            var radius = size * 0.44f;
            var seedPhase = variant * 0.73f + 0.41f;

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var delta = new Vector2(x, y) - center;
                    var angle = Mathf.Atan2(delta.y, delta.x);
                    var jagged = 0.82f
                                 + Mathf.Sin(angle * (5f + variant % 3) + seedPhase) * 0.075f
                                 + Mathf.Sin(angle * (9f + variant % 4) - seedPhase * 1.8f) * 0.045f
                                 + Mathf.Sin(angle * 15f + variant) * 0.022f;
                    var localRadius = radius * jagged;
                    var distance = delta.magnitude;
                    var alpha = Mathf.Clamp01((localRadius + 2.2f - distance) * 0.72f);
                    if (alpha <= 0f)
                    {
                        pixels[y * size + x] = new Color32(0, 0, 0, 0);
                        continue;
                    }

                    var nx = delta.x / radius;
                    var ny = delta.y / radius;
                    var light = Mathf.Clamp01(0.48f - nx * 0.22f + ny * 0.30f);
                    var crater = Mathf.PerlinNoise(x * 0.085f + variant * 7.1f, y * 0.085f + variant * 2.3f);
                    var value = Mathf.Clamp01(0.22f + light * 0.55f + (crater - 0.5f) * 0.25f);
                    var c = (byte)Mathf.RoundToInt(value * 255f);
                    pixels[y * size + x] = new Color32(c, c, c,
                        (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Texture2D NewTexture(int width, int height, string name)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            return texture;
        }
    }
}
