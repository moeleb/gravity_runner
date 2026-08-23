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
        private void BuildSplashScreen()
        {
            var screen = CreateScreen("Splash");

            var tunnelTexture = Resources.Load<Texture2D>("Art/Splash/nova-neon-tunnel");
            var tunnelObject = new GameObject("Nova · Neon Tunnel Rush");
            tunnelObject.transform.SetParent(screen.transform, false);
            var tunnelRect = tunnelObject.AddComponent<RectTransform>();
            Stretch(tunnelRect);
            tunnelRect.offsetMin = new Vector2(-90, -90);
            tunnelRect.offsetMax = new Vector2(90, 90);
            var tunnelImage = tunnelObject.AddComponent<RawImage>();
            tunnelImage.texture = tunnelTexture;
            tunnelImage.color = Color.white;
            tunnelImage.raycastTarget = false;
            var tunnelAspect = tunnelObject.AddComponent<AspectRatioFitter>();
            tunnelAspect.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            tunnelAspect.aspectRatio = tunnelTexture != null ? (float)tunnelTexture.width / tunnelTexture.height : 2f / 3f;
            // Keep the splash artwork completely static. The previous TunnelRush
            // component scaled the full image in and out and felt like camera zoom.

            // Foreground light trails make Nova feel like she is skating toward the player.
            var random = new System.Random(4217);
            for (var i = 0; i < 16; i++)
            {
                var tint = i % 3 == 0 ? Coral : Cyan;
                tint.a = 0.18f + (float)random.NextDouble() * 0.34f;
                var x = random.Next(-500, 501);
                var y = random.Next(-920, 921);
                var streak = CreateImage("Neon speed trail", screen.transform, tint,
                    Centered(new Vector2(x, y), new Vector2(random.Next(5, 13), random.Next(70, 185))), RoundedSprite(8));
                streak.rectTransform.localEulerAngles = new Vector3(0, 0, x < 0 ? -18f : 18f);
            }

            var lowerShade = CreateCard("Loading HUD shade", screen.transform, new Vector2(0, -665),
                new Vector2(1080, 610), new Color(Ink.r, Ink.g, Ink.b, 0.88f), 0);

            MakeText(screen.transform, "NEON BREACH", 24, FontStyle.Bold, Cyan,
                new Vector2(0, 810), new Vector2(700, 45), TextAnchor.MiddleCenter, 8);
            MakeText(screen.transform, "GRAVITY", 72, FontStyle.Bold, Cream,
                new Vector2(0, 740), new Vector2(900, 95), TextAnchor.MiddleCenter, 8);
            MakeText(screen.transform, "HALF DEAD", 30, FontStyle.Bold, Coral,
                new Vector2(0, 670), new Vector2(700, 55), TextAnchor.MiddleCenter, 11);

            var liveBadge = CreateCard("Live badge", lowerShade.transform, new Vector2(0, 205),
                new Vector2(270, 54), new Color(Cyan.r, Cyan.g, Cyan.b, 0.13f), 27);
            MakeText(liveBadge.transform, "●  LIVE RUN", 21, FontStyle.Bold, Cyan,
                Vector2.zero, new Vector2(230, 40), TextAnchor.MiddleCenter, 3);

            splashStatusText = MakeText(lowerShade.transform, "Opening the neon tunnel", 28, FontStyle.Bold, Cream,
                new Vector2(0, 105), new Vector2(820, 55), TextAnchor.MiddleCenter);

            var track = CreateCard("Progress Track", lowerShade.transform, new Vector2(0, 20), new Vector2(760, 24), CardSoft, 12);
            var fillObject = CreateImage("Fill", track.transform, Cyan, FullStretch(), RoundedSprite(10));
            splashProgressFill = fillObject;
            splashProgressFill.type = Image.Type.Filled;
            splashProgressFill.fillMethod = Image.FillMethod.Horizontal;
            splashProgressFill.fillOrigin = 0;
            splashProgressFill.fillAmount = 0.04f;

            splashPercentText = MakeText(lowerShade.transform, "4%", 23, FontStyle.Bold, Cyan,
                new Vector2(0, -42), new Vector2(300, 40), TextAnchor.MiddleCenter);
            MakeText(lowerShade.transform, "Securely syncing pilot state · v" + Application.version, 20,
                FontStyle.Normal, Muted, new Vector2(0, -120), new Vector2(850, 45), TextAnchor.MiddleCenter);
            MakeText(lowerShade.transform, "FIREBASE  •  PLAYER  •  CONFIG  •  ENTITLEMENTS", 18,
                FontStyle.Bold, new Color(Muted.r, Muted.g, Muted.b, 0.72f),
                new Vector2(0, -185), new Vector2(850, 40), TextAnchor.MiddleCenter, 2);
        }


        private void SetSplashProgress(float value, string status)
        {
            if (splashProgressFill == null)
                return;

            StopCoroutine(nameof(AnimateProgress));
            StartCoroutine(AnimateProgress(value));
            splashStatusText.text = status;
            splashStatusText.gameObject.transform.localScale = Vector3.one * 0.96f;
        }

        private IEnumerator AnimateProgress(float target)
        {
            var start = splashProgressFill.fillAmount;
            var duration = 0.35f;
            for (var time = 0f; time < duration; time += Time.unscaledDeltaTime)
            {
                var t = Smooth(time / duration);
                splashProgressFill.fillAmount = Mathf.Lerp(start, target, t);
                splashPercentText.text = Mathf.RoundToInt(splashProgressFill.fillAmount * 100f) + "%";
                yield return null;
            }

            splashProgressFill.fillAmount = target;
            splashPercentText.text = Mathf.RoundToInt(target * 100f) + "%";
        }
    }
}
