using UnityEngine;
using UnityEngine.UI;

namespace GravityHalfDead
{
    public sealed class Spinner : MonoBehaviour
    {
        public float degreesPerSecond = 18f;
        private void Update() => transform.Rotate(0, 0, degreesPerSecond * Time.unscaledDeltaTime);
    }
}
