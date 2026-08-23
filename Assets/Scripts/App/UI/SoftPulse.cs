using UnityEngine;
using UnityEngine.UI;

namespace GravityHalfDead
{
    public sealed class SoftPulse : MonoBehaviour
    {
        private void Update()
        {
            var scale = 1f + Mathf.Sin(Time.unscaledTime * 2.2f) * 0.025f;
            transform.localScale = Vector3.one * scale;
        }
    }
}
