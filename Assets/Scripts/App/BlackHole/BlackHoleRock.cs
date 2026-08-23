using UnityEngine;

namespace GravityHalfDead
{
    /// <summary>A single pooled rock. It orbits, gets captured by the event horizon, then vanishes into the core.</summary>
    public sealed class BlackHoleRock : MonoBehaviour
    {
        private BlackHoleFieldController owner;
        private RectTransform rect;
        private CanvasGroup group;
        private Vector2 center;
        private float captureRadius;
        private float startRadius;
        private float startAngle;
        private float turns;
        private float duration;
        private float ellipse;
        private bool clockwise;
        private float startTime;
        private float baseScale;
        private float baseRotation;
        private bool running;

        private void Awake()
        {
            rect = (RectTransform)transform;
            group = GetComponent<CanvasGroup>();
            if (group == null)
                group = gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
        }

        public void Launch(BlackHoleFieldController controller, Vector2 holeCenter, float horizonRadius,
            float radius, float angle, float orbitTurns, float travelDuration, float ellipseRatio,
            bool isClockwise, float scale)
        {
            owner = controller;
            center = holeCenter;
            captureRadius = horizonRadius;
            startRadius = radius;
            startAngle = angle;
            turns = orbitTurns;
            duration = travelDuration;
            ellipse = ellipseRatio;
            clockwise = isClockwise;
            baseScale = scale;
            baseRotation = Random.Range(-35f, 35f);
            startTime = Time.unscaledTime;
            running = true;
            group.alpha = 0f;
            rect.localScale = Vector3.one * baseScale;
            rect.localEulerAngles = new Vector3(0f, 0f, baseRotation);
            gameObject.SetActive(true);
        }

        private void Update()
        {
            if (!running)
                return;

            var progress = Mathf.Clamp01((Time.unscaledTime - startTime) / Mathf.Max(0.1f, duration));
            var direction = clockwise ? -1f : 1f;

            // First 72%: visible orbital pull. Last 28%: trapped inside the event horizon and rapidly consumed.
            float radius;
            float scale;
            if (progress < 0.72f)
            {
                var orbital = Smooth(progress / 0.72f);
                radius = Mathf.Lerp(startRadius, captureRadius + 34f, orbital);
                scale = Mathf.Lerp(baseScale, baseScale * 0.72f, orbital);
            }
            else
            {
                var captured = Smooth((progress - 0.72f) / 0.28f);
                radius = Mathf.Lerp(captureRadius + 34f, 0f, captured);
                scale = Mathf.Lerp(baseScale * 0.72f, 0.015f, captured);
            }

            var spinBoost = progress < 0.72f ? progress : 0.72f + (progress - 0.72f) * 2.7f;
            var angle = (startAngle + direction * spinBoost * turns * 360f) * Mathf.Deg2Rad;
            rect.anchoredPosition = center + new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius * ellipse);
            rect.localScale = Vector3.one * scale;
            rect.localEulerAngles = new Vector3(0f, 0f, baseRotation + direction * spinBoost * 760f);

            var fadeIn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress * 10f));
            var fadeOut = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.87f, 1f, progress));
            group.alpha = fadeIn * fadeOut;

            if (progress >= 1f)
            {
                running = false;
                owner.RockWasSwallowed(this);
            }
        }

        private static float Smooth(float value) => value * value * (3f - 2f * value);
    }
}
