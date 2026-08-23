using UnityEngine;
using UnityEngine.UI;

namespace GravityHalfDead
{
    public sealed class ButtonGlow : MonoBehaviour
    {
        private Image image;
        private Color baseColor;

        private void Start()
        {
            image = GetComponent<Image>();
            baseColor = image.color;
        }

        private void Update()
        {
            var lift = Mathf.Sin(Time.unscaledTime * 2.5f) * 0.035f;
            image.color = new Color(
                Mathf.Clamp01(baseColor.r + lift),
                Mathf.Clamp01(baseColor.g + lift),
                Mathf.Clamp01(baseColor.b + lift),
                baseColor.a);
        }
    }
}
