using UnityEngine;

namespace GravityHalfDead
{
    /// <summary>
    /// Shared physical contract for every rideable gravity disc. Geometry is generated below y=0;
    /// RiderMount is the common top-deck plane, so one character foot offset works for all discs.
    /// </summary>
    public sealed class GravityDiscRideable : MonoBehaviour
    {
        public const float StandardDeckDiameter = 1.8f;
        public const float StandardDeckHeight = 0.18f;

        [SerializeField] private string discId = "core_runner";
        [SerializeField] private Transform riderMount;
        [SerializeField] private Transform leftTrailAnchor;
        [SerializeField] private Transform rightTrailAnchor;

        public string DiscId => discId;
        public Transform RiderMount => riderMount != null ? riderMount : transform;
        public Transform LeftTrailAnchor => leftTrailAnchor;
        public Transform RightTrailAnchor => rightTrailAnchor;

        public void Configure(string id, Transform mount, Transform leftTrail, Transform rightTrail)
        {
            discId = string.IsNullOrWhiteSpace(id) ? "core_runner" : id;
            riderMount = mount;
            leftTrailAnchor = leftTrail;
            rightTrailAnchor = rightTrail;
        }

        public void PositionRiderVisual(Transform riderVisual, float soleToVisualRootHeight)
        {
            if (riderVisual == null)
                return;
            riderVisual.position = RiderMount.position + RiderMount.up * Mathf.Max(0f, soleToVisualRootHeight);
            riderVisual.rotation = RiderMount.rotation;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.1f, 0.9f, 1f, 0.9f);
            Gizmos.DrawWireSphere(RiderMount.position, 0.08f);
            Gizmos.DrawLine(RiderMount.position - transform.right * 0.38f,
                RiderMount.position + transform.right * 0.38f);
        }
#endif
    }
}
