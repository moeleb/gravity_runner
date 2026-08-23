using UnityEngine;
using UnityEngine.UI;

namespace GravityHalfDead
{
    public sealed class HeroActionPerformance : MonoBehaviour
    {
        public RawImage idleFrame;
        public CanvasGroup actionFrame;
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
            var cycle = Mathf.Repeat(time, MenuCharacterPerformanceDirector.LoopDuration);
            float action;
            if (cycle < 4f) action = 0f;
            else if (cycle < 5f) action = Mathf.SmoothStep(0f, 1f, cycle - 4f);
            else if (cycle < 12f) action = 1f;
            else if (cycle < 13f) action = Mathf.SmoothStep(1f, 0f, cycle - 12f);
            else action = 0f;

            if (actionFrame != null)
                actionFrame.alpha = action;
            if (idleFrame != null)
            {
                var tint = 0.93f + Mathf.Sin(time * 4.2f) * 0.035f;
                idleFrame.color = new Color(tint, tint, 1f, 1f);
            }

            var seamless = cycle / MenuCharacterPerformanceDirector.LoopDuration * Mathf.PI * 2f;
            var jump = cycle < 4f ? Mathf.Sin(cycle / 4f * Mathf.PI) : 0f;
            var trick = cycle >= 23f && cycle < 28f ? Mathf.Sin((cycle - 23f) / 5f * Mathf.PI) : 0f;
            rect.anchoredPosition = origin + new Vector2(Mathf.Sin(seamless) * 8f, Mathf.Sin(seamless * 2f) * 12f + jump * 26f + trick * 18f);
            rect.localEulerAngles = new Vector3(0f, 0f, Mathf.Sin(seamless) * 0.75f);
            rect.localScale = Vector3.one * (1.035f + Mathf.Sin(seamless * 2f) * 0.012f);
        }
    }
}
