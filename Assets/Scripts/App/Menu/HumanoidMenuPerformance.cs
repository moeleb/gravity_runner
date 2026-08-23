using UnityEngine;

namespace GravityHalfDead
{
    public sealed class HumanoidMenuPerformance : MonoBehaviour
    {
        private Animator animator;
        private Transform chest;
        private Transform head;
        private Transform rightUpperArm;
        private Transform rightLowerArm;
        private Transform rightHand;
        private Quaternion chestBase;
        private Quaternion headBase;
        private Quaternion upperBase;
        private Quaternion lowerBase;
        private Quaternion handBase;
        private Vector3 rootBase;
        private Quaternion rootRotationBase;
        private ParticleSystem spray;

        public void Initialize(Animator target)
        {
            animator = target;
            animator.applyRootMotion = false;
            chest = animator.GetBoneTransform(HumanBodyBones.Chest);
            head = animator.GetBoneTransform(HumanBodyBones.Head);
            rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            rightLowerArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            rootBase = transform.localPosition;
            rootRotationBase = transform.localRotation;
            CaptureBasePose();
            spray = MenuSprayFx.Create(rightHand, gameObject.layer);
        }

        private void CaptureBasePose()
        {
            if (chest != null) chestBase = chest.localRotation;
            if (head != null) headBase = head.localRotation;
            if (rightUpperArm != null) upperBase = rightUpperArm.localRotation;
            if (rightLowerArm != null) lowerBase = rightLowerArm.localRotation;
            if (rightHand != null) handBase = rightHand.localRotation;
        }

        private void LateUpdate()
        {
            if (animator == null)
                return;

            var time = Mathf.Repeat(Time.unscaledTime, MenuCharacterPerformanceDirector.LoopDuration);
            var jump = MenuPerformanceMath.Window(time, 0.5f, 4f);
            var sprayAmount = MenuPerformanceMath.Window(time, 4f, 13f);
            var admire = MenuPerformanceMath.Window(time, 13f, 18f);
            var wave = MenuPerformanceMath.Window(time, 18f, 23f);
            var trick = MenuPerformanceMath.Window(time, 23f, 28f);

            transform.localPosition = rootBase + Vector3.up *
                (Mathf.Sin(jump * Mathf.PI) * 0.72f + Mathf.Sin(trick * Mathf.PI) * 0.48f);
            transform.localRotation = rootRotationBase * Quaternion.Euler(
                Mathf.Sin(jump * Mathf.PI) * -7f,
                trick > 0f ? MenuPerformanceMath.Smooth(trick) * 360f : 0f,
                Mathf.Sin(jump * Mathf.PI * 2f) * 5f);

            Apply(chest, chestBase, Quaternion.Euler(0f, admire * -12f, sprayAmount * -9f + wave * 8f));
            Apply(head, headBase, Quaternion.Euler(wave * -4f, admire * 18f, wave * -8f));
            Apply(rightUpperArm, upperBase, Quaternion.Euler(
                -62f * sprayAmount - 105f * wave, 12f * sprayAmount, -28f * sprayAmount - 18f * wave));
            Apply(rightLowerArm, lowerBase, Quaternion.Euler(
                -48f * sprayAmount, 0f, 68f * wave + Mathf.Sin(time * 8f) * 24f * wave));
            Apply(rightHand, handBase, Quaternion.Euler(
                Mathf.Sin(time * 17f) * 12f * sprayAmount, Mathf.Sin(time * 9f) * 8f * wave, 0f));

            if (spray != null)
            {
                var emission = spray.emission;
                emission.enabled = time >= 5f && time <= 12.4f;
            }
        }

        private static void Apply(Transform bone, Quaternion baseline, Quaternion offset)
        {
            if (bone != null)
                bone.localRotation = baseline * offset;
        }
    }
}
