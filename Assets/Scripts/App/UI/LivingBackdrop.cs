using UnityEngine;
using UnityEngine.UI;

namespace GravityHalfDead
{
    public sealed class LivingBackdrop : MonoBehaviour
    {
        private Image image;
        private void Start() => image = GetComponent<Image>();

        private void Update()
        {
            var wave = (Mathf.Sin(Time.unscaledTime * 0.24f) + 1f) * 0.5f;
            image.color = Color.Lerp(new Color(0.025f, 0.06f, 0.11f), new Color(0.055f, 0.035f, 0.095f), wave);
        }
    }
}
