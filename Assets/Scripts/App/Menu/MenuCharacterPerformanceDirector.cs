using UnityEngine;
using UnityEngine.UI;

namespace GravityHalfDead
{
    public sealed class MenuCharacterPerformanceDirector : MonoBehaviour
    {
        public const float LoopDuration = 32f;

        private RawImage stageDisplay;
        private GameObject fallbackVisual;
        private MenuCharacterStage stage;
        private GameObject activeCharacter;

        public void Configure(RawImage display, GameObject fallback)
        {
            stageDisplay = display;
            fallbackVisual = fallback;
            if (stageDisplay != null)
                stageDisplay.gameObject.SetActive(false);
        }

        public bool SetCharacter(string characterId)
        {
            if (activeCharacter != null)
            {
                Destroy(activeCharacter);
                activeCharacter = null;
            }

            var prefab = Resources.Load<GameObject>("Characters3D/" + characterId.ToLowerInvariant());
            if (prefab == null)
            {
                SetFallback(true);
                Debug.Log("Menu character prefab not found yet: Resources/Characters3D/" + characterId);
                return false;
            }

            stage ??= new MenuCharacterStage(stageDisplay);
            stage.Ensure();
            activeCharacter = stage.Spawn(prefab, characterId);

            var animator = activeCharacter.GetComponentInChildren<Animator>();
            if (animator == null || !animator.isHuman)
            {
                Debug.LogError(characterId + " must use a Unity Humanoid Avatar to share menu animations.");
                Destroy(activeCharacter);
                activeCharacter = null;
                SetFallback(true);
                return false;
            }

            activeCharacter.AddComponent<HumanoidMenuPerformance>().Initialize(animator);
            SetFallback(false);
            return true;
        }

        private void SetFallback(bool enabled)
        {
            if (fallbackVisual != null)
                fallbackVisual.SetActive(enabled);
            if (stageDisplay != null)
                stageDisplay.gameObject.SetActive(!enabled);
        }

        private void OnDestroy()
        {
            stage?.Dispose();
            stage = null;
        }
    }
}
