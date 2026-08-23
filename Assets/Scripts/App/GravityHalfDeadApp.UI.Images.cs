using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
#if UNITY_IOS
using System.Runtime.InteropServices;
#endif
using Firebase;
using Firebase.Auth;
using Firebase.Firestore;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace GravityHalfDead
{
    public sealed partial class GravityHalfDeadApp
    {
        private static Image CreateImage(string name, Transform parent, Color color, RectSpec spec, Sprite sprite = null)
        {
            var imageObject = new GameObject(name);
            imageObject.transform.SetParent(parent, false);
            var rect = imageObject.AddComponent<RectTransform>();
            rect.anchorMin = spec.AnchorMin;
            rect.anchorMax = spec.AnchorMax;
            rect.pivot = spec.Pivot;
            rect.anchoredPosition = spec.Position;
            rect.sizeDelta = spec.Size;
            rect.offsetMin = spec.OffsetMin;
            rect.offsetMax = spec.OffsetMax;
            var image = imageObject.AddComponent<Image>();
            image.color = color;
            image.sprite = sprite;
            if (sprite != null)
                image.type = Image.Type.Sliced;
            return image;
        }

        private static readonly Dictionary<int, Sprite> RoundedSprites = new();

        private static Sprite RoundedSprite(int radius)
        {
            radius = Mathf.Clamp(radius, 2, 64);
            if (RoundedSprites.TryGetValue(radius, out var cached))
                return cached;

            const int textureSize = 128;
            var texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false)
            {
                name = "Rounded Rectangle " + radius,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };

            var pixels = new Color32[textureSize * textureSize];
            var r = Mathf.Min(radius, textureSize / 2f);
            for (var y = 0; y < textureSize; y++)
            {
                for (var x = 0; x < textureSize; x++)
                {
                    var dx = Mathf.Max(r - x - 0.5f, 0f, x + 0.5f - (textureSize - r));
                    var dy = Mathf.Max(r - y - 0.5f, 0f, y + 0.5f - (textureSize - r));
                    var distance = Mathf.Sqrt(dx * dx + dy * dy);
                    var alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(r + 0.5f - distance) * 255f);
                    pixels[y * textureSize + x] = new Color32(255, 255, 255, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            var border = new Vector4(r, r, r, r);
            var sprite = Sprite.Create(texture, new Rect(0, 0, textureSize, textureSize), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect, border);
            sprite.name = "Rounded " + radius;
            RoundedSprites[radius] = sprite;
            return sprite;
        }

        private static Color Hex(string value)
        {
            ColorUtility.TryParseHtmlString("#" + value, out var color);
            return color;
        }


        private struct RectSpec
        {
            public Vector2 AnchorMin;
            public Vector2 AnchorMax;
            public Vector2 Pivot;
            public Vector2 Position;
            public Vector2 Size;
            public Vector2 OffsetMin;
            public Vector2 OffsetMax;
        }
    }
}
