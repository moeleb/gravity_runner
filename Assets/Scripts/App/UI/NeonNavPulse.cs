using UnityEngine;
using UnityEngine.UI;

namespace GravityHalfDead
{
    public sealed class NeonNavPulse : MonoBehaviour
    {
        public float phase;
        private Image image;
        private Color baseColor;

        private void Start()
        {
            image = GetComponent<Image>();
            baseColor = image.color;
        }

        private void Update()
        {
            var wave = (Mathf.Sin(Time.unscaledTime * 2.4f + phase) + 1f) * 0.5f;
            transform.localScale = Vector3.one * Mathf.Lerp(0.985f, 1.025f, wave);
            image.color = Color.Lerp(baseColor, new Color(
                Mathf.Min(1f, baseColor.r + 0.16f),
                Mathf.Min(1f, baseColor.g + 0.16f),
                Mathf.Min(1f, baseColor.b + 0.16f), baseColor.a), wave);
        }
    }
}
