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
        private CanvasGroup CreateScreen(string name)
        {
            var screenObject = new GameObject(name + " Screen");
            screenObject.transform.SetParent(safeRoot, false);
            var rect = screenObject.AddComponent<RectTransform>();
            Stretch(rect);
            var group = screenObject.AddComponent<CanvasGroup>();
            screens[name] = group;
            return group;
        }

        private async Task ShowScreenAsync(string nextName)
        {
            if (!screens.TryGetValue(nextName, out var next))
                return;

            isTransitioning = true;
            CanvasGroup current = null;
            foreach (var pair in screens)
            {
                if (pair.Value.gameObject.activeSelf && pair.Value.alpha > 0.01f)
                {
                    current = pair.Value;
                    break;
                }
            }

            next.gameObject.SetActive(true);
            next.blocksRaycasts = false;
            next.interactable = false;
            next.alpha = 0f;
            next.transform.localScale = Vector3.one * 0.975f;

            const float duration = 0.34f;
            for (var time = 0f; time < duration; time += Time.unscaledDeltaTime)
            {
                var t = Smooth(time / duration);
                next.alpha = t;
                next.transform.localScale = Vector3.one * Mathf.Lerp(0.975f, 1f, t);
                if (current != null)
                    current.alpha = 1f - t;
                await Task.Yield();
            }

            if (current != null && current != next)
            {
                current.alpha = 0f;
                current.blocksRaycasts = false;
                current.interactable = false;
                current.gameObject.SetActive(false);
            }

            next.alpha = 1f;
            next.blocksRaycasts = true;
            next.interactable = true;
            next.transform.localScale = Vector3.one;
            isTransitioning = false;
        }

        private void ShowImmediately(string name)
        {
            foreach (var pair in screens)
            {
                var visible = pair.Key == name;
                pair.Value.gameObject.SetActive(visible);
                pair.Value.alpha = visible ? 1f : 0f;
                pair.Value.blocksRaycasts = visible;
                pair.Value.interactable = visible;
            }
        }
    }
}
