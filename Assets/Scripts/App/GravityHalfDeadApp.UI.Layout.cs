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
        private static RectSpec FullStretch() => new()
        {
            AnchorMin = Vector2.zero,
            AnchorMax = Vector2.one,
            Pivot = new Vector2(0.5f, 0.5f),
            Position = Vector2.zero,
            Size = Vector2.zero,
            OffsetMin = Vector2.zero,
            OffsetMax = Vector2.zero
        };

        private static RectSpec Centered(Vector2 position, Vector2 size) => new()
        {
            AnchorMin = new Vector2(0.5f, 0.5f),
            AnchorMax = new Vector2(0.5f, 0.5f),
            Pivot = new Vector2(0.5f, 0.5f),
            Position = position,
            Size = size,
            OffsetMin = Vector2.zero,
            OffsetMax = Vector2.zero
        };

        private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static float Smooth(float value) => value * value * (3f - 2f * value);

        private static Task Delay(int milliseconds) => Task.Delay(milliseconds);
    }
}
