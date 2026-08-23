using UnityEngine;
using UnityEngine.UI;

namespace GravityHalfDead
{
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private Rect lastArea;
        private Vector2Int lastScreen;

        private void Update()
        {
            if (lastArea == Screen.safeArea && lastScreen.x == Screen.width && lastScreen.y == Screen.height)
                return;

            lastArea = Screen.safeArea;
            lastScreen = new Vector2Int(Screen.width, Screen.height);
            var rect = (RectTransform)transform;
            var min = Screen.safeArea.position;
            var max = Screen.safeArea.position + Screen.safeArea.size;
            min.x /= Screen.width;
            min.y /= Screen.height;
            max.x /= Screen.width;
            max.y /= Screen.height;
            rect.anchorMin = min;
            rect.anchorMax = max;
        }
    }
}
