using UnityEngine;
using UnityEngine.UI;

namespace GravityHalfDead
{
    public sealed class FloatingElement : MonoBehaviour
    {
        public float amplitude = 8f;
        public float speed = 0.55f;
        public float phase;
        private Vector3 origin;

        private void Start() => origin = transform.localPosition;

        private void Update()
        {
            transform.localPosition = origin + Vector3.up * (Mathf.Sin(Time.unscaledTime * speed + phase) * amplitude);
        }
    }
}
