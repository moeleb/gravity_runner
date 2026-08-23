using UnityEngine;
using UnityEngine.UI;

namespace GravityHalfDead
{
    public sealed class HoverboardSpark : MonoBehaviour
    {
        public float phase;
        public float speed = 110f;
        private RectTransform rect;
        private Image image;
        private Vector2 origin;
        private Color baseColor;

        private void Start()
        {
            rect = (RectTransform)transform;
            image = GetComponent<Image>();
            origin = rect.anchoredPosition;
            baseColor = image.color;
        }

        private void Update()
        {
            var travel = Mathf.Repeat(Time.unscaledTime * speed + phase * 47f, 190f);
            rect.anchoredPosition = origin + new Vector2(-travel * 0.9f, -travel * 0.55f);
            rect.localScale = Vector3.one * Mathf.Lerp(1.4f, 0.25f, travel / 190f);
            var color = baseColor;
            color.a = Mathf.Sin(Mathf.Clamp01(travel / 190f) * Mathf.PI) * 0.95f;
            image.color = color;
        }
    }
}
