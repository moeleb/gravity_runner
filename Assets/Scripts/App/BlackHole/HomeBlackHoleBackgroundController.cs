using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GravityHalfDead
{
    /// <summary>
    /// Full-screen, code-generated animated black-hole wallpaper used only by the home/game screen.
    /// It owns the star field, rotating vortex layers, event horizon and orbiting/spinning rocks.
    /// </summary>
    public sealed class HomeBlackHoleBackgroundController : MonoBehaviour
    {
        private readonly List<RectTransform> spinLayers = new List<RectTransform>();
        private readonly List<float> spinSpeeds = new List<float>();
        private RectTransform root;
        private RectTransform corePulse;
        private Image hotRing;
        private Image coolRing;
        private float pulsePhase;

        public static HomeBlackHoleBackgroundController Create(Transform parent)
        {
            var go = new GameObject("Animated full-screen black-hole wallpaper");
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var controller = go.AddComponent<HomeBlackHoleBackgroundController>();
            controller.root = rect;
            controller.Build();
            return controller;
        }

        private void Build()
        {
            HomeBlackHoleVisualFactory.CreateStretchImage("Black-hole deep space", root,
                new Color(0.004f, 0.006f, 0.028f, 1f));

            BuildNebulae();
            BuildStars();

            // Everything is centered slightly above screen center so it occupies the same visual
            // area as the approved reference while still filling the complete screen behind UI.
            var orbitRoot = new GameObject("Black-hole orbital center");
            orbitRoot.transform.SetParent(root, false);
            var orbitRect = orbitRoot.AddComponent<RectTransform>();
            orbitRect.anchorMin = orbitRect.anchorMax = new Vector2(0.5f, 0.575f);
            orbitRect.pivot = new Vector2(0.5f, 0.5f);
            orbitRect.anchoredPosition = Vector2.zero;
            orbitRect.sizeDelta = new Vector2(1080f, 1080f);

            BuildVortex(orbitRect);
            BuildOrbitingRocks(orbitRect);
            BuildCore(orbitRect);
        }

        private void BuildNebulae()
        {
            CreateAnchoredSoftGlow("Purple space cloud left", new Vector2(0.14f, 0.54f),
                new Vector2(920f, 760f), new Color(0.34f, 0.04f, 0.58f, 0.19f));
            CreateAnchoredSoftGlow("Blue space cloud right", new Vector2(0.84f, 0.48f),
                new Vector2(840f, 700f), new Color(0.03f, 0.24f, 0.70f, 0.16f));
            CreateAnchoredSoftGlow("Magenta space cloud center", new Vector2(0.52f, 0.60f),
                new Vector2(940f, 570f), new Color(0.72f, 0.08f, 0.52f, 0.12f));
            CreateAnchoredSoftGlow("Deep violet cloud bottom", new Vector2(0.50f, 0.18f),
                new Vector2(1180f, 720f), new Color(0.17f, 0.04f, 0.38f, 0.12f));
        }

        private void CreateAnchoredSoftGlow(string name, Vector2 anchor, Vector2 size, Color color)
        {
            var image = HomeBlackHoleVisualFactory.CreateCenteredImage(name, root, Vector2.zero, size,
                color, HomeBlackHoleVisualFactory.SoftCircle());
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = anchor;
            image.rectTransform.anchoredPosition = Vector2.zero;
        }

        private void BuildStars()
        {
            var random = new System.Random(8242026);
            for (var i = 0; i < 112; i++)
            {
                var star = HomeBlackHoleVisualFactory.CreateCenteredImage("Black-hole star " + i, root,
                    Vector2.zero, Vector2.one * random.Next(2, 8),
                    new Color(0.76f + (float)random.NextDouble() * 0.24f,
                              0.80f + (float)random.NextDouble() * 0.20f,
                              1f,
                              0.18f + (float)random.NextDouble() * 0.60f),
                    HomeBlackHoleVisualFactory.HardCircle());
                var rect = star.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(
                    0.015f + (float)random.NextDouble() * 0.97f,
                    0.02f + (float)random.NextDouble() * 0.96f);
                rect.anchoredPosition = Vector2.zero;
            }
        }

        private void BuildVortex(RectTransform orbitRoot)
        {
            AddVortexLayer(orbitRoot, "Outer violet gravity spiral", new Vector2(1120f, 760f),
                new Color(0.34f, 0.12f, 0.88f, 0.34f), 0, 2.6f, 8f);
            AddVortexLayer(orbitRoot, "Outer blue gravity spiral", new Vector2(1000f, 680f),
                new Color(0.08f, 0.43f, 1f, 0.34f), 1, -3.8f, -3f);
            AddVortexLayer(orbitRoot, "Magenta accretion spiral", new Vector2(900f, 590f),
                new Color(0.97f, 0.18f, 0.85f, 0.48f), 2, 5.4f, 5f);
            AddVortexLayer(orbitRoot, "White hot accretion spiral", new Vector2(760f, 500f),
                new Color(1f, 0.68f, 0.90f, 0.46f), 3, -7.1f, -8f);
            AddVortexLayer(orbitRoot, "Inner electric spiral", new Vector2(650f, 430f),
                new Color(0.48f, 0.47f, 1f, 0.46f), 4, 9.4f, 12f);

            // Long rotating plasma wisps give the vortex a more obvious orbital direction.
            for (var group = 0; group < 3; group++)
            {
                var spinner = HomeBlackHoleVisualFactory.CreateCenteredRoot(
                    "Plasma wisp orbit " + group, orbitRoot, Vector2.zero, new Vector2(1000f, 1000f));
                spinLayers.Add(spinner);
                spinSpeeds.Add(group == 0 ? 4.1f : group == 1 ? -6.2f : 8.3f);

                for (var i = 0; i < 8; i++)
                {
                    var angle = i * 45f + group * 17f;
                    var radius = 260f + group * 105f + (i % 3) * 34f;
                    var radians = angle * Mathf.Deg2Rad;
                    var position = new Vector2(Mathf.Cos(radians) * radius, Mathf.Sin(radians) * radius * 0.63f);
                    var wisp = HomeBlackHoleVisualFactory.CreateCenteredImage(
                        "Plasma streak " + group + "-" + i, spinner, position,
                        new Vector2(120f + group * 26f, 8f + group * 2f),
                        group == 1
                            ? new Color(0.98f, 0.28f, 0.86f, 0.32f)
                            : new Color(0.30f, 0.55f, 1f, 0.28f),
                        HomeBlackHoleVisualFactory.SoftCircle());
                    wisp.rectTransform.localEulerAngles = new Vector3(0f, 0f, angle + 90f);
                }
            }
        }

        private void AddVortexLayer(RectTransform parent, string name, Vector2 size, Color color,
            int spriteVariant, float speed, float startRotation)
        {
            var image = HomeBlackHoleVisualFactory.CreateCenteredImage(name, parent, Vector2.zero, size,
                color, HomeBlackHoleVisualFactory.Vortex(spriteVariant));
            image.preserveAspect = false;
            image.rectTransform.localEulerAngles = new Vector3(0f, 0f, startRotation);
            spinLayers.Add(image.rectTransform);
            spinSpeeds.Add(speed);
        }

        private void BuildOrbitingRocks(RectTransform orbitRoot)
        {
            var rockLayer = new GameObject("Full-screen orbiting rocks");
            rockLayer.transform.SetParent(orbitRoot, false);
            var layerRect = rockLayer.AddComponent<RectTransform>();
            layerRect.anchorMin = layerRect.anchorMax = layerRect.pivot = new Vector2(0.5f, 0.5f);
            layerRect.anchoredPosition = Vector2.zero;
            layerRect.sizeDelta = new Vector2(1080f, 1080f);

            const float horizon = 170f;
            for (var i = 0; i < 30; i++)
            {
                var radius = i < 8 ? Random.Range(300f, 500f) : Random.Range(500f, 940f);
                var ellipse = Random.Range(0.60f, 1.08f);
                var angle = Random.Range(0f, 360f);
                var speed = Random.Range(5f, 15f) * (i % 5 == 0 ? 1.30f : 1f);
                var spin = Random.Range(-55f, 55f);
                var drift = Random.Range(4.0f, 11.0f);
                var scale = i < 5 ? Random.Range(1.10f, 1.65f) : Random.Range(0.48f, 1.18f);
                HomeOrbitingRock.Create(layerRect, i, radius, ellipse, angle, speed, spin, drift, scale, horizon);
            }
        }

        private void BuildCore(RectTransform orbitRoot)
        {
            corePulse = HomeBlackHoleVisualFactory.CreateCenteredRoot(
                "Black-hole breathing core", orbitRoot, Vector2.zero, new Vector2(560f, 560f));

            HomeBlackHoleVisualFactory.CreateCenteredImage("Black-hole outer violet bloom", corePulse,
                Vector2.zero, new Vector2(540f, 540f), new Color(0.46f, 0.04f, 0.96f, 0.22f),
                HomeBlackHoleVisualFactory.SoftCircle());
            HomeBlackHoleVisualFactory.CreateCenteredImage("Black-hole magenta bloom", corePulse,
                Vector2.zero, new Vector2(470f, 470f), new Color(1f, 0.16f, 0.76f, 0.30f),
                HomeBlackHoleVisualFactory.SoftCircle());

            hotRing = HomeBlackHoleVisualFactory.CreateCenteredImage("Black-hole hot event horizon", corePulse,
                Vector2.zero, new Vector2(390f, 390f), new Color(1f, 0.62f, 0.88f, 0.95f),
                HomeBlackHoleVisualFactory.Vortex(3));
            coolRing = HomeBlackHoleVisualFactory.CreateCenteredImage("Black-hole blue lens ring", corePulse,
                Vector2.zero, new Vector2(356f, 356f), new Color(0.38f, 0.58f, 1f, 0.92f),
                HomeBlackHoleVisualFactory.Vortex(1));

            HomeBlackHoleVisualFactory.CreateCenteredImage("Black-hole event rim", corePulse,
                Vector2.zero, new Vector2(322f, 322f), new Color(0.68f, 0.23f, 0.94f, 0.78f),
                HomeBlackHoleVisualFactory.HardCircle());
            HomeBlackHoleVisualFactory.CreateCenteredImage("Black-hole singularity", corePulse,
                Vector2.zero, new Vector2(292f, 292f), Color.black,
                HomeBlackHoleVisualFactory.HardCircle());
            HomeBlackHoleVisualFactory.CreateCenteredImage("Black-hole inner darkness", corePulse,
                new Vector2(-4f, -3f), new Vector2(258f, 258f), new Color(0f, 0f, 0f, 1f),
                HomeBlackHoleVisualFactory.HardCircle());
        }

        private void Update()
        {
            var dt = Time.unscaledDeltaTime;
            if (dt <= 0f)
                return;

            for (var i = 0; i < spinLayers.Count; i++)
            {
                var layer = spinLayers[i];
                if (layer == null)
                    continue;
                var euler = layer.localEulerAngles;
                euler.z += spinSpeeds[i] * dt;
                layer.localEulerAngles = euler;
            }

            if (hotRing != null)
                hotRing.rectTransform.Rotate(0f, 0f, 11f * dt);
            if (coolRing != null)
                coolRing.rectTransform.Rotate(0f, 0f, -8f * dt);

            if (corePulse != null)
            {
                pulsePhase += dt;
                var pulse = 1f + Mathf.Sin(pulsePhase * 1.55f) * 0.012f;
                corePulse.localScale = Vector3.one * pulse;
            }
        }
    }
}
