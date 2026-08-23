using UnityEngine;
using UnityEngine.UI;

namespace GravityHalfDead
{
    public sealed class NeonSpeedTrail : MonoBehaviour
    {
        public float speed = 360f;
        public float sideDrift = 1f;
        private RectTransform rect;
        private Image image;
        private float baseAlpha;

        private void Start()
        {
            rect = (RectTransform)transform;
            image = GetComponent<Image>();
            baseAlpha = image.color.a;
        }

        private void Update()
        {
            var delta = Time.unscaledDeltaTime;
            rect.anchoredPosition += new Vector2(sideDrift * speed * 0.2f, -speed) * delta;
            var tint = image.color;
            tint.a = baseAlpha * (0.62f + Mathf.Sin(Time.unscaledTime * 5f + rect.anchoredPosition.x) * 0.28f);
            image.color = tint;

            if (rect.anchoredPosition.y < -1030f)
                rect.anchoredPosition = new Vector2(-rect.anchoredPosition.x * 0.82f, 1030f);
        }
    }
}
