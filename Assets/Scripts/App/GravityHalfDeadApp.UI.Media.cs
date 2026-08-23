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
        private RawImage MakeTextureImage(string name, Transform parent, Texture texture, Vector2 position, Vector2 size)
        {
            var imageObject = new GameObject(name);
            imageObject.transform.SetParent(parent, false);
            var rect = imageObject.AddComponent<RectTransform>();
            SetRect(rect, position, size);
            var image = imageObject.AddComponent<RawImage>();
            image.texture = texture;
            image.color = Color.white;
            image.raycastTarget = false;
            if (texture != null)
            {
                var aspect = imageObject.AddComponent<AspectRatioFitter>();
                aspect.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                aspect.aspectRatio = (float)texture.width / texture.height;
            }
            return image;
        }

        private RawImage CreateHeroFrame(string name, Transform parent, Texture2D texture)
        {
            var frameObject = new GameObject(name);
            frameObject.transform.SetParent(parent, false);
            var frameRect = frameObject.AddComponent<RectTransform>();
            Stretch(frameRect);
            var frame = frameObject.AddComponent<RawImage>();
            frame.texture = texture;
            frame.color = Color.white;
            frame.raycastTarget = false;
            var aspect = frameObject.AddComponent<AspectRatioFitter>();
            aspect.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            aspect.aspectRatio = texture != null ? (float)texture.width / texture.height : 2f / 3f;
            return frame;
        }


        private void AddCharacter(Transform parent, string resourcePath, Vector2 position, Vector2 size, float angle, float phase)
        {
            var texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null)
            {
                Debug.LogWarning("Character art missing at Resources/" + resourcePath);
                return;
            }

            var objectName = resourcePath[(resourcePath.LastIndexOf('/') + 1)..];
            var characterObject = new GameObject(objectName);
            characterObject.transform.SetParent(parent, false);
            var rect = characterObject.AddComponent<RectTransform>();
            SetRect(rect, position, size);
            rect.localEulerAngles = new Vector3(0, 0, angle);
            var rawImage = characterObject.AddComponent<RawImage>();
            rawImage.texture = texture;
            rawImage.color = Color.white;
            rawImage.raycastTarget = false;
            rawImage.uvRect = new Rect(0, 0, 1, 1);
            var floater = characterObject.AddComponent<FloatingElement>();
            floater.phase = phase;
            floater.amplitude = 10f;
        }

        private void BuildStarfield(Transform parent)
        {
            var random = new System.Random(1708);
            for (var i = 0; i < 30; i++)
            {
                var x = random.Next(-520, 521);
                var y = random.Next(-940, 941);
                var size = random.Next(3, 11);
                var star = CreateImage("Star", parent, new Color(1, 1, 1, (float)random.NextDouble() * 0.35f + 0.08f),
                    Centered(new Vector2(x, y), new Vector2(size, size)), RoundedSprite(size));
                var drift = star.gameObject.AddComponent<FloatingElement>();
                drift.amplitude = random.Next(3, 14);
                drift.speed = (float)random.NextDouble() * 0.45f + 0.18f;
                drift.phase = (float)random.NextDouble() * 6f;
            }
        }

        private void CreateOrb(Transform parent, Vector2 position, float size, Color color, float alpha, float speed)
        {
            color.a = alpha;
            var orb = CreateImage("Atmosphere", parent, color, Centered(position, new Vector2(size, size)), RoundedSprite(64));
            var floater = orb.gameObject.AddComponent<FloatingElement>();
            floater.amplitude = 34f;
            floater.speed = Mathf.Abs(speed);
            floater.phase = position.x * 0.01f;
        }
    }
}
