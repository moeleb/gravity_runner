using UnityEngine;
using UnityEngine.UI;

namespace GravityHalfDead
{
    internal static class BlackHoleRockVisual
    {
        private static Sprite rockSprite;

        public static BlackHoleRock Create(RectTransform parent, int index)
        {
            if (rockSprite == null)
                rockSprite = BuildRockSprite();

            var root = new GameObject("Pooled gravity rock " + (index + 1));
            root.transform.SetParent(parent, false);
            var rect = root.AddComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(Random.Range(34f, 78f), Random.Range(30f, 72f));

            var image = root.AddComponent<Image>();
            image.sprite = rockSprite;
            image.preserveAspect = true;
            image.color = RandomRockColor();
            image.raycastTarget = false;

            var shadowObject = new GameObject("Dark rock face");
            shadowObject.transform.SetParent(root.transform, false);
            var shadowRect = shadowObject.AddComponent<RectTransform>();
            shadowRect.anchorMin = shadowRect.anchorMax = shadowRect.pivot = new Vector2(0.5f, 0.5f);
            shadowRect.anchoredPosition = new Vector2(10f, -9f);
            shadowRect.sizeDelta = new Vector2(32f, 25f);
            var shadow = shadowObject.AddComponent<Image>();
            shadow.sprite = rockSprite;
            shadow.color = new Color(0.035f, 0.01f, 0.08f, 0.72f);
            shadow.raycastTarget = false;

            var facetObject = new GameObject("Neon rock facet");
            facetObject.transform.SetParent(root.transform, false);
            var facetRect = facetObject.AddComponent<RectTransform>();
            facetRect.anchorMin = facetRect.anchorMax = facetRect.pivot = new Vector2(0.5f, 0.5f);
            facetRect.anchoredPosition = new Vector2(-10f, 10f);
            facetRect.sizeDelta = new Vector2(25f, 10f);
            var facet = facetObject.AddComponent<Image>();
            facet.sprite = rockSprite;
            facet.color = Color.Lerp(image.color, Color.white, 0.58f);
            facet.raycastTarget = false;

            root.AddComponent<CanvasGroup>();
            return root.AddComponent<BlackHoleRock>();
        }

        private static Color RandomRockColor()
        {
            var pick = Random.Range(0, 4);
            return pick switch
            {
                0 => new Color(0.70f, 0.20f, 0.94f, 1f),
                1 => new Color(0.10f, 0.78f, 0.92f, 1f),
                2 => new Color(0.92f, 0.20f, 0.70f, 1f),
                _ => new Color(0.38f, 0.36f, 0.70f, 1f)
            };
        }

        private static Sprite BuildRockSprite()
        {
            const int size = 96;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Runtime Gravity Rock",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color32[size * size];
            var center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var delta = new Vector2(x, y) - center;
                    var angle = Mathf.Atan2(delta.y, delta.x);
                    var wobble = 0.88f + Mathf.Sin(angle * 5f + 0.7f) * 0.07f + Mathf.Sin(angle * 9f) * 0.035f;
                    var radius = size * 0.43f * wobble;
                    var distance = delta.magnitude;
                    var alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(radius + 1.4f - distance) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
