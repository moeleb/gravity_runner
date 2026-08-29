using UnityEngine;

namespace GravityHalfDead
{
    public static class GravityDiscLoadout
    {
        private const string SelectedDiscKey = "ghd.selected_disc";
        private static string selectedDiscId;

        public static string SelectedDiscId
        {
            get => string.IsNullOrWhiteSpace(selectedDiscId)
                ? PlayerPrefs.GetString(SelectedDiscKey, "core_runner") : selectedDiscId;
            set
            {
                selectedDiscId = string.IsNullOrWhiteSpace(value) ? "core_runner" : value;
                PlayerPrefs.SetString(SelectedDiscKey, selectedDiscId);
            }
        }

        public static GravityDiscRideable SpawnSelected(Transform parent)
        {
            var id = SelectedDiscId;
            var prefab = Resources.Load<GameObject>("Discs3D/" + id)
                         ?? Resources.Load<GameObject>("Discs3D/core_runner");
            if (prefab == null)
            {
                Debug.LogError("Rideable disc prefabs are missing. Run Gravity Half Dead/Generate Rideable Discs.");
                return null;
            }
            var instance = Object.Instantiate(prefab, parent, false);
            instance.name = "Equipped Disc · " + id;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            return instance.GetComponent<GravityDiscRideable>();
        }
    }

    /// <summary>
    /// Add to the runner root. Keep RiderVisualRoot as the animated character model; the running
    /// animator remains active while this component places the character's soles on the disc deck.
    /// </summary>
    public sealed class GravityDiscRider : MonoBehaviour
    {
        [SerializeField] private Transform riderVisualRoot;
        [SerializeField] private Transform discSocket;
        [Tooltip("Distance from the character visual root to the bottom of its shoes in metres.")]
        [SerializeField] private float soleToVisualRootHeight = 0.92f;
        [SerializeField] private bool spawnOnStart = true;
        private GravityDiscRideable equippedDisc;

        private void Start()
        {
            if (spawnOnStart)
                EquipSelectedDisc();
        }

        public void EquipSelectedDisc()
        {
            if (equippedDisc != null)
                Destroy(equippedDisc.gameObject);
            equippedDisc = GravityDiscLoadout.SpawnSelected(discSocket != null ? discSocket : transform);
            if (equippedDisc != null)
                equippedDisc.PositionRiderVisual(riderVisualRoot, soleToVisualRootHeight);
        }
    }
}
