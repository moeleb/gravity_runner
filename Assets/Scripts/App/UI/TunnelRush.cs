using UnityEngine;
using UnityEngine.UI;

namespace GravityHalfDead
{
    public sealed class TunnelRush : MonoBehaviour
    {
        private RectTransform rect;
        private Vector2 origin;

        private void Start()
        {
            rect = (RectTransform)transform;
            origin = rect.anchoredPosition;
        }

        private void Update()
        {
            var time = Time.unscaledTime;
            var surge = (Mathf.Sin(time * 1.15f) + 1f) * 0.5f;
            rect.localScale = Vector3.one * Mathf.Lerp(1.025f, 1.075f, surge);
            rect.anchoredPosition = origin + new Vector2(Mathf.Sin(time * 0.72f) * 10f, Mathf.Sin(time * 1.15f) * 16f);
        }
    }
}
