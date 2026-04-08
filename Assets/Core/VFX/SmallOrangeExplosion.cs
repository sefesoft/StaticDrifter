using UnityEngine;

namespace StaticDrift.VFX
{
    /// <summary>
    /// Lightweight orange burst (same look language as game-over / boss death, scaled down).
    /// </summary>
    public static class SmallOrangeExplosion
    {
        private const float DefaultLifetime = 0.55f;

        public static GameObject Spawn(Vector3 worldPosition, float intensity = 0.45f)
        {
            float i = Mathf.Clamp(intensity, 0.15f, 1.25f);
            GameObject root = new GameObject("SmallOrangeExplosion");
            root.transform.position = worldPosition;

            AddBurst(root, i);
            AddEmbers(root, i);

            Object.Destroy(root, DefaultLifetime + 0.15f);
            return root;
        }

        private static void AddBurst(GameObject root, float i)
        {
            GameObject go = new GameObject("Burst");
            go.transform.SetParent(root.transform, false);

            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.06f;
            main.startLifetime = Mathf.Clamp(0.18f + 0.22f * i, 0.15f, 0.55f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.4f * i, 3.8f * i);
            main.startSize = new ParticleSystem.MinMaxCurve(0.032f * i, 0.1f * i);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 2f * Mathf.PI);
            main.gravityModifier = 0.12f;
            main.maxParticles = Mathf.Min(2000, Mathf.RoundToInt(1200 * i));
            main.stopAction = ParticleSystemStopAction.Destroy;
            main.useUnscaledTime = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            ParticleSystem.EmissionModule emissionBurst = ps.emission;
            emissionBurst.rateOverTime = 0f;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = Mathf.Min(0.22f, 0.06f * i);

            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(new Gradient
            {
                colorKeys = new GradientColorKey[]
                {
                    new GradientColorKey(new Color(1f, 0.95f, 0.35f, 1f), 0f),
                    new GradientColorKey(new Color(1f, 0.55f, 0.08f, 1f), 0.25f),
                    new GradientColorKey(new Color(1f, 0.25f, 0.04f, 0.75f), 0.55f),
                    new GradientColorKey(new Color(0.2f, 0.04f, 0f, 0f), 1f),
                },
                alphaKeys = new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0.9f, 0f),
                    new GradientAlphaKey(0.68f, 0.3f),
                    new GradientAlphaKey(0f, 1f),
                }
            });

            ParticleSystem.SizeOverLifetimeModule sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, 0.06f);

            SharedVfxMaterials.ApplyUrpParticlesUnlit(ps.GetComponent<ParticleSystemRenderer>());
            ps.Play();
            ps.Emit(Mathf.Clamp(Mathf.RoundToInt(200f * i), 64, 680));
        }

        private static void AddEmbers(GameObject root, float i)
        {
            GameObject go = new GameObject("Embers");
            go.transform.SetParent(root.transform, false);

            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.05f;
            main.startLifetime = Mathf.Clamp(0.28f + 0.35f * i, 0.22f, 0.85f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f * i, 1.35f * i);
            main.startSize = new ParticleSystem.MinMaxCurve(0.026f * i, 0.08f * i);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 2f * Mathf.PI);
            main.gravityModifier = -0.04f;
            main.maxParticles = Mathf.Min(1200, Mathf.RoundToInt(560 * i));
            main.stopAction = ParticleSystemStopAction.Destroy;
            main.useUnscaledTime = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 0f;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = Mathf.Min(0.28f, 0.1f * i);

            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(new Gradient
            {
                colorKeys = new GradientColorKey[]
                {
                    new GradientColorKey(new Color(1f, 0.7f, 0.16f, 0.9f), 0f),
                    new GradientColorKey(new Color(1f, 0.35f, 0.06f, 0.55f), 0.5f),
                    new GradientColorKey(new Color(0.3f, 0.06f, 0.02f, 0f), 1f),
                },
                alphaKeys = new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0.78f, 0f),
                    new GradientAlphaKey(0f, 1f),
                }
            });

            ParticleSystem.SizeOverLifetimeModule sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, 1.32f);

            SharedVfxMaterials.ApplyUrpParticlesUnlit(ps.GetComponent<ParticleSystemRenderer>());
            ps.Play();
            ps.Emit(Mathf.Clamp(Mathf.RoundToInt(100f * i), 36, 360));
        }
    }
}
