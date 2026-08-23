using UnityEngine;

namespace GravityHalfDead
{
    internal static class MenuSprayFx
    {
        public static ParticleSystem Create(Transform rightHand, int layer)
        {
            if (rightHand == null)
                return null;

            var emitter = new GameObject("Neon Spray FX");
            emitter.transform.SetParent(rightHand, false);
            emitter.transform.localPosition = new Vector3(0.08f, 0f, 0.12f);
            emitter.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
            emitter.layer = layer;

            var spray = emitter.AddComponent<ParticleSystem>();
            var main = spray.main;
            main.startLifetime = 0.55f;
            main.startSpeed = 3.5f;
            main.startSize = 0.045f;
            main.maxParticles = 240;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = spray.emission;
            emission.rateOverTime = 90f;
            emission.enabled = false;
            var shape = spray.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 9f;

            var color = spray.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.cyan, 0f), new GradientColorKey(new Color(1f, 0.05f, 0.8f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;
            return spray;
        }
    }
}
