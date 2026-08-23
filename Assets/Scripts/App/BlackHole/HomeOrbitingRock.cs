using UnityEngine;
using UnityEngine.UI;

namespace GravityHalfDead
{
    /// <summary>
    /// One code-generated asteroid orbiting the home black hole. Rocks spin, drift inward,
    /// shrink as they cross the event horizon, then respawn outside the screen edge.
    /// </summary>
    public sealed class HomeOrbitingRock : MonoBehaviour
    {
        private RectTransform rect;
        private Image image;
        private CanvasGroup group;
        private float radius;
        private float ellipse;
        private float angleDegrees;
        private float orbitSpeed;
        private float spinSpeed;
        private float inwardSpeed;
        private float baseScale;
        private float startRadius;
        private float captureRadius;
        private int direction;
        private int variant;
        private Color baseColor;

        public static HomeOrbitingRock Create(Transform parent, int index, float initialRadius,
            float ellipseRatio, float startingAngle, float orbitalSpeed, float rotationSpeed,
            float driftSpeed, float scale, float horizonRadius)
        {
            var go = new GameObject("Home orbiting rock " + (index + 1));
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(92f, 82f);

            var image = go.AddComponent<Image>();
            image.sprite = HomeBlackHoleVisualFactory.Rock(index);
            image.preserveAspect = true;
            image.raycastTarget = false;

            var group = go.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            var rock = go.AddComponent<HomeOrbitingRock>();
            rock.rect = rect;
            rock.image = image;
            rock.group = group;
            rock.variant = index;
            rock.Configure(initialRadius, ellipseRatio, startingAngle, orbitalSpeed,
                rotationSpeed, driftSpeed, scale, horizonRadius);
            return rock;
        }

        private void Configure(float initialRadius, float ellipseRatio, float startingAngle,
            float orbitalSpeed, float rotationSpeed, float driftSpeed, float scale, float horizonRadius)
        {
            startRadius = initialRadius;
            radius = initialRadius;
            ellipse = ellipseRatio;
            angleDegrees = startingAngle;
            orbitSpeed = Mathf.Abs(orbitalSpeed);
            spinSpeed = rotationSpeed;
            inwardSpeed = Mathf.Max(2f, driftSpeed);
            baseScale = scale;
            captureRadius = horizonRadius;
            direction = variant % 4 == 0 ? -1 : 1;

            switch (variant % 5)
            {
                case 0: baseColor = new Color(0.44f, 0.35f, 0.62f, 1f); break;
                case 1: baseColor = new Color(0.31f, 0.25f, 0.47f, 1f); break;
                case 2: baseColor = new Color(0.50f, 0.30f, 0.58f, 1f); break;
                case 3: baseColor = new Color(0.26f, 0.31f, 0.48f, 1f); break;
                default: baseColor = new Color(0.38f, 0.28f, 0.50f, 1f); break;
            }
            image.color = baseColor;
            ApplyPose();
        }

        private void Update()
        {
            var dt = Time.unscaledDeltaTime;
            if (dt <= 0f)
                return;

            angleDegrees += direction * orbitSpeed * dt;

            // Far rocks drift slowly. Once captured they accelerate sharply toward the core.
            var captureFactor = Mathf.InverseLerp(captureRadius * 2.25f, captureRadius * 0.55f, radius);
            radius -= inwardSpeed * Mathf.Lerp(1f, 5.8f, captureFactor) * dt;
            rect.localEulerAngles = new Vector3(0f, 0f, rect.localEulerAngles.z + spinSpeed * dt);

            if (radius <= captureRadius * 0.22f)
                Respawn();
            else
                ApplyPose();
        }

        private void ApplyPose()
        {
            var radians = angleDegrees * Mathf.Deg2Rad;
            var x = Mathf.Cos(radians) * radius;
            var y = Mathf.Sin(radians) * radius * ellipse;
            rect.anchoredPosition = new Vector2(x, y);

            // Bottom-half rocks read as closer to camera; top-half rocks read as farther away.
            var depth = Mathf.InverseLerp(650f, -650f, y);
            var perspective = Mathf.Lerp(0.68f, 1.38f, depth);

            var capture = Mathf.InverseLerp(captureRadius * 1.65f, captureRadius * 0.35f, radius);
            var shrink = Mathf.Lerp(1f, 0.06f, capture * capture);
            var finalScale = baseScale * perspective * shrink;
            rect.localScale = Vector3.one * finalScale;

            var fadeIn = Mathf.Clamp01((startRadius - radius + 55f) / 85f);
            var fadeOut = 1f - Mathf.Clamp01(Mathf.InverseLerp(captureRadius * 0.82f, captureRadius * 0.30f, radius));
            group.alpha = Mathf.Clamp01(fadeIn * fadeOut);

            var rimLight = Mathf.Clamp01((Mathf.Sin(radians - 0.7f) + 1f) * 0.5f);
            image.color = Color.Lerp(baseColor, new Color(0.66f, 0.42f, 0.80f, 1f), rimLight * 0.24f);
        }

        private void Respawn()
        {
            radius = Random.Range(690f, 940f);
            startRadius = radius;
            angleDegrees = Random.Range(0f, 360f);
            ellipse = Random.Range(0.62f, 1.10f);
            orbitSpeed = Random.Range(5f, 16f);
            spinSpeed = Random.Range(-48f, 48f);
            inwardSpeed = Random.Range(4.5f, 12f);
            baseScale = Random.Range(0.56f, 1.28f);
            direction = Random.value > 0.18f ? 1 : -1;
            group.alpha = 0f;
            ApplyPose();
        }
    }
}
