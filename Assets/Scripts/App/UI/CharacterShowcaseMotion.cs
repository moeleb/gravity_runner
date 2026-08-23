using UnityEngine;
using UnityEngine.UI;

namespace GravityHalfDead
{
    public sealed class CharacterShowcaseMotion : MonoBehaviour
    {
        private RectTransform rect;
        private RectTransform hoverboard;
        private RectTransform shadow;
        private Vector2 origin;

        public CharacterShowcaseMotion Configure(RectTransform board, RectTransform groundShadow)
        {
            hoverboard = board;
            shadow = groundShadow;
            return this;
        }

        private void Start()
        {
            rect = (RectTransform)transform;
            origin = rect.anchoredPosition;
        }

        private void Update()
        {
            const float performanceLength = 3.6f;
            var cycle = Mathf.Repeat(Time.unscaledTime, performanceLength * 5f);
            var performance = Mathf.FloorToInt(cycle / performanceLength);
            var local = Mathf.Repeat(cycle, performanceLength) / performanceLength;
            var envelope = Mathf.Sin(local * Mathf.PI);
            var beat = local * Mathf.PI * 2f;
            var x = 0f;
            var y = Mathf.Sin(Time.unscaledTime * 2.4f) * 3f;
            var rotation = 0f;
            var scaleX = 1f;
            var scaleY = 1f;

            // Five performances rotate continuously: hoverboard, wave, dance, aerial trick,
            // and victory pose. Every phase returns to the neutral pose at its boundaries.
            if (performance == 0)
            {
                x = Mathf.Sin(beat) * 20f * envelope;
                y += (34f + Mathf.Sin(beat * 2f) * 9f) * envelope;
                rotation = Mathf.Sin(beat) * 5f * envelope;
            }
            else if (performance == 1)
            {
                x = Mathf.Sin(beat) * 10f * envelope;
                y += Mathf.Abs(Mathf.Sin(beat * 2f)) * 12f * envelope;
                rotation = (-5f + Mathf.Sin(beat * 3f) * 3f) * envelope;
            }
            else if (performance == 2)
            {
                x = Mathf.Sin(beat * 2f) * 32f * envelope;
                y += Mathf.Abs(Mathf.Sin(beat * 3f)) * 18f * envelope;
                rotation = Mathf.Sin(beat * 2f) * 9f * envelope;
                scaleX = 1f + Mathf.Sin(beat * 4f) * 0.035f * envelope;
                scaleY = 1f - Mathf.Sin(beat * 4f) * 0.025f * envelope;
            }
            else if (performance == 3)
            {
                y += Mathf.Sin(local * Mathf.PI) * 108f;
                x = Mathf.Sin(beat) * 26f * envelope;
                rotation = Mathf.Sin(local * Mathf.PI) * 360f;
                scaleX = 1f - 0.08f * envelope;
                scaleY = 1f + 0.08f * envelope;
            }
            else
            {
                y += Mathf.Abs(Mathf.Sin(beat * 2f)) * 22f * envelope;
                x = 15f * envelope;
                rotation = -7f * envelope + Mathf.Sin(beat * 2f) * 2f * envelope;
                scaleX = 1f + 0.07f * envelope;
                scaleY = 1f + 0.07f * envelope;
            }

            rect.anchoredPosition = origin + new Vector2(x, y);
            rect.localEulerAngles = new Vector3(0f, 0f, rotation);
            rect.localScale = new Vector3(scaleX, scaleY, 1f);

            if (hoverboard != null)
            {
                hoverboard.gameObject.SetActive(performance == 0);
                hoverboard.anchoredPosition = new Vector2(x, 42f + y * 0.78f);
                hoverboard.localEulerAngles = new Vector3(0f, 0f, rotation * 0.75f);
            }
            if (shadow != null)
            {
                var heightScale = Mathf.Clamp(1f - Mathf.Max(0f, y) / 260f, 0.48f, 1f);
                shadow.localScale = new Vector3(heightScale, heightScale, 1f);
            }
        }
    }
}
