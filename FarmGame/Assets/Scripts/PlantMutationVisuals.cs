using System.Collections.Generic;
using FarmSimulator.Data;
using UnityEngine;

namespace FarmSimulator.Visual
{
    public class PlantMutationVisuals : MonoBehaviour
    {
        private readonly Dictionary<MutationData, ParticleSystem> _effects = new();
        private Transform _effectsRoot;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        public void Sync(IReadOnlyList<MutationData> mutations)
        {
            EnsureRoot();

            List<MutationData> toRemove = new();
            foreach (KeyValuePair<MutationData, ParticleSystem> pair in _effects)
            {
                bool stillPresent = false;
                if (mutations != null)
                {
                    for (int i = 0; i < mutations.Count; i++)
                    {
                        if (mutations[i] == pair.Key)
                        {
                            stillPresent = true;
                            break;
                        }
                    }
                }

                if (!stillPresent)
                {
                    toRemove.Add(pair.Key);
                }
            }

            for (int i = 0; i < toRemove.Count; i++)
            {
                RemoveEffect(toRemove[i]);
            }

            if (mutations == null)
            {
                return;
            }

            for (int i = 0; i < mutations.Count; i++)
            {
                MutationData mutation = mutations[i];
                if (mutation == null || _effects.ContainsKey(mutation))
                {
                    continue;
                }

                ParticleSystem effect = CreateEffect(mutation);
                _effects[mutation] = effect;
                EmitBurst(effect, mutation);
            }
        }

        private void EnsureRoot()
        {
            if (_effectsRoot != null)
            {
                return;
            }

            Transform existing = transform.Find("MutationEffects");
            if (existing != null)
            {
                _effectsRoot = existing;
                return;
            }

            GameObject root = new("MutationEffects");
            _effectsRoot = root.transform;
            _effectsRoot.SetParent(transform, false);
            _effectsRoot.localPosition = Vector3.up * 0.08f;
        }

        private ParticleSystem CreateEffect(MutationData mutation)
        {
            GameObject effectObject = new($"MutationFx_{SanitizeName(mutation.mutationName)}");
            effectObject.transform.SetParent(_effectsRoot, false);

            ParticleSystem system = effectObject.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            MutationVisualProfile profile = BuildProfile(mutation);
            ParticleSystemRenderer renderer = effectObject.GetComponent<ParticleSystemRenderer>();
            renderer.sortMode = ParticleSystemSortMode.Distance;
            renderer.alignment = ParticleSystemRenderSpace.Facing;
            renderer.sharedMaterial = CreateParticleMaterial(profile.startColor, profile.endColor);
            renderer.trailMaterial = renderer.sharedMaterial;

            ConfigureParticleSystem(system, mutation, profile);
            system.Play(true);
            return system;
        }

        private void ConfigureParticleSystem(ParticleSystem system, MutationData mutation, MutationVisualProfile profile)
        {
            var main = system.main;
            main.loop = true;
            main.playOnAwake = true;
            main.duration = 1.8f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = new ParticleSystem.MinMaxCurve(profile.lifetimeMin, profile.lifetimeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(profile.speedMin, profile.speedMax);
            main.startSize = new ParticleSystem.MinMaxCurve(profile.sizeMin, profile.sizeMax);
            main.startColor = profile.startColor;
            main.maxParticles = profile.maxParticles;
            main.gravityModifier = profile.gravity;

            var emission = system.emission;
            emission.enabled = true;
            emission.rateOverTime = profile.rateOverTime;

            var shape = system.shape;
            shape.enabled = true;
            shape.shapeType = profile.shapeType;
            shape.radius = profile.radius;
            shape.radiusThickness = profile.radiusThickness;
            shape.arc = profile.arc;

            var colorOverLifetime = system.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(profile.startColor, 0f),
                    new GradientColorKey(profile.endColor, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(profile.alphaStart, 0f),
                    new GradientAlphaKey(profile.alphaEnd, 1f)
                });
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

            var sizeOverLifetime = system.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve curve = new();
            curve.AddKey(0f, 0.35f);
            curve.AddKey(0.5f, 1f);
            curve.AddKey(1f, 0.1f);
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curve);

            var velocityOverLifetime = system.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            velocityOverLifetime.space = ParticleSystemSimulationSpace.Local;
            velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(profile.velocity.x);
            velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(profile.velocity.y);
            velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(profile.velocity.z);
            velocityOverLifetime.orbitalY = new ParticleSystem.MinMaxCurve(profile.orbitalY);
            velocityOverLifetime.radial = new ParticleSystem.MinMaxCurve(profile.radialVelocity);

            var noise = system.noise;
            noise.enabled = true;
            noise.strength = profile.noiseStrength;
            noise.frequency = profile.noiseFrequency;

            var rotationOverLifetime = system.rotationOverLifetime;
            rotationOverLifetime.enabled = true;
            rotationOverLifetime.z = new ParticleSystem.MinMaxCurve(profile.rotationSpeed);

            var trails = system.trails;
            trails.enabled = profile.useTrails;
            if (profile.useTrails)
            {
                trails.mode = ParticleSystemTrailMode.Ribbon;
                trails.ratio = 0.6f;
                trails.lifetime = profile.trailLifetime;
                trails.dieWithParticles = true;
                trails.sizeAffectsWidth = true;
                trails.widthOverTrail = 0.6f;
                trails.colorOverLifetime = new ParticleSystem.MinMaxGradient(profile.endColor);
            }

            system.transform.localPosition = profile.localOffset;
        }

        private static Material CreateParticleMaterial(Color startColor, Color endColor)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Particles/Standard Unlit");
            }

            if (shader == null)
            {
                shader = Shader.Find("Legacy Shaders/Particles/Additive");
            }

            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            Material material = new(shader);
            material.name = "PlantMutationFxRuntime";

            Color tint = Color.Lerp(startColor, endColor, 0.35f);
            if (material.HasProperty(BaseColorId))
            {
                material.SetColor(BaseColorId, tint);
            }

            if (material.HasProperty(ColorId))
            {
                material.SetColor(ColorId, tint);
            }

            if (material.HasProperty(EmissionColorId))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor(EmissionColorId, tint * 1.5f);
            }

            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
            }

            if (material.HasProperty("_Blend"))
            {
                material.SetFloat("_Blend", 0f);
            }

            if (material.HasProperty("_SrcBlend"))
            {
                material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            }

            if (material.HasProperty("_DstBlend"))
            {
                material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            }

            if (material.HasProperty("_ZWrite"))
            {
                material.SetFloat("_ZWrite", 0f);
            }

            material.renderQueue = 3000;
            return material;
        }

        private void EmitBurst(ParticleSystem system, MutationData mutation)
        {
            if (system == null || mutation == null)
            {
                return;
            }

            int burstCount = mutation.rarity switch
            {
                MutationRarity.Common => 10,
                MutationRarity.Rare => 16,
                MutationRarity.Unique => 24,
                _ => 10
            };

            system.Emit(burstCount);
        }

        private void RemoveEffect(MutationData mutation)
        {
            if (!_effects.TryGetValue(mutation, out ParticleSystem effect))
            {
                return;
            }

            _effects.Remove(mutation);
            if (effect != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(effect.gameObject);
                }
                else
                {
                    DestroyImmediate(effect.gameObject);
                }
            }
        }

        private static string SanitizeName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "Mutation";
            }

            return value.Replace(" ", string.Empty);
        }

        private MutationVisualProfile BuildProfile(MutationData mutation)
        {
            MutationVisualProfile profile = MutationVisualProfile.Default();

            if (mutation == null)
            {
                return profile;
            }

            if (!string.IsNullOrWhiteSpace(mutation.sourceEventName))
            {
                switch (mutation.sourceEventName)
                {
                    case "Ливень":
                        return MutationVisualProfile.Rain();
                    case "Засуха":
                        return MutationVisualProfile.Drought();
                    case "Вьюга":
                    case "Мороз":
                        return MutationVisualProfile.Frost();
                    case "Гроза":
                        return MutationVisualProfile.Storm();
                    case "Полная луна":
                        return MutationVisualProfile.Moon();
                    case "Солнцестояние":
                        return MutationVisualProfile.Solstice();
                }
            }

            return mutation.type switch
            {
                MutationType.SpeedBoost => MutationVisualProfile.Speed(),
                MutationType.YieldBoost => MutationVisualProfile.Harvest(),
                MutationType.MutationCatalyst => MutationVisualProfile.Catalyst(),
                MutationType.GoldenHarvest => MutationVisualProfile.Solstice(),
                MutationType.NightBloom => MutationVisualProfile.Moon(),
                MutationType.SpeedPenalty => MutationVisualProfile.Decay(),
                MutationType.YieldPenalty => MutationVisualProfile.Decay(),
                MutationType.Unstable => MutationVisualProfile.Unstable(),
                _ => profile
            };
        }

        private readonly struct MutationVisualProfile
        {
            public readonly Color startColor;
            public readonly Color endColor;
            public readonly float alphaStart;
            public readonly float alphaEnd;
            public readonly float rateOverTime;
            public readonly float radius;
            public readonly float radiusThickness;
            public readonly float arc;
            public readonly float lifetimeMin;
            public readonly float lifetimeMax;
            public readonly float speedMin;
            public readonly float speedMax;
            public readonly float sizeMin;
            public readonly float sizeMax;
            public readonly int maxParticles;
            public readonly float gravity;
            public readonly Vector3 velocity;
            public readonly float orbitalY;
            public readonly float radialVelocity;
            public readonly float noiseStrength;
            public readonly float noiseFrequency;
            public readonly float rotationSpeed;
            public readonly bool useTrails;
            public readonly float trailLifetime;
            public readonly Vector3 localOffset;
            public readonly ParticleSystemShapeType shapeType;

            public MutationVisualProfile(
                Color startColor,
                Color endColor,
                float alphaStart,
                float alphaEnd,
                float rateOverTime,
                float radius,
                float radiusThickness,
                float arc,
                float lifetimeMin,
                float lifetimeMax,
                float speedMin,
                float speedMax,
                float sizeMin,
                float sizeMax,
                int maxParticles,
                float gravity,
                Vector3 velocity,
                float orbitalY,
                float radialVelocity,
                float noiseStrength,
                float noiseFrequency,
                float rotationSpeed,
                bool useTrails,
                float trailLifetime,
                Vector3 localOffset,
                ParticleSystemShapeType shapeType)
            {
                this.startColor = startColor;
                this.endColor = endColor;
                this.alphaStart = alphaStart;
                this.alphaEnd = alphaEnd;
                this.rateOverTime = rateOverTime;
                this.radius = radius;
                this.radiusThickness = radiusThickness;
                this.arc = arc;
                this.lifetimeMin = lifetimeMin;
                this.lifetimeMax = lifetimeMax;
                this.speedMin = speedMin;
                this.speedMax = speedMax;
                this.sizeMin = sizeMin;
                this.sizeMax = sizeMax;
                this.maxParticles = maxParticles;
                this.gravity = gravity;
                this.velocity = velocity;
                this.orbitalY = orbitalY;
                this.radialVelocity = radialVelocity;
                this.noiseStrength = noiseStrength;
                this.noiseFrequency = noiseFrequency;
                this.rotationSpeed = rotationSpeed;
                this.useTrails = useTrails;
                this.trailLifetime = trailLifetime;
                this.localOffset = localOffset;
                this.shapeType = shapeType;
            }

            public static MutationVisualProfile Default()
            {
                Color start = new(0.52f, 0.96f, 0.54f, 1f);
                return new MutationVisualProfile(
                    start,
                    new Color(0.24f, 0.76f, 0.32f, 1f),
                    0.85f,
                    0f,
                    7f,
                    0.18f,
                    1f,
                    360f,
                    0.45f,
                    0.75f,
                    0.04f,
                    0.1f,
                    0.03f,
                    0.065f,
                    28,
                    0f,
                    new Vector3(0f, 0.2f, 0f),
                    0f,
                    0f,
                    0.18f,
                    0.45f,
                    0.5f,
                    false,
                    0f,
                    new Vector3(0f, 0.04f, 0f),
                    ParticleSystemShapeType.Hemisphere);
            }

            public static MutationVisualProfile Rain()
            {
                Color start = new(0.34f, 0.72f, 1f, 1f);
                return new MutationVisualProfile(start, new Color(0.62f, 0.88f, 1f, 1f), 0.9f, 0f, 14f, 0.14f, 0.15f, 30f, 0.28f, 0.5f, 0.55f, 1.1f, 0.018f, 0.032f, 42, 0.75f, new Vector3(0f, -0.65f, 0f), 0f, 0f, 0.05f, 0.2f, 0.05f, true, 0.1f, new Vector3(0f, 0.22f, 0f), ParticleSystemShapeType.Cone);
            }

            public static MutationVisualProfile Drought()
            {
                Color start = new(1f, 0.56f, 0.14f, 1f);
                return new MutationVisualProfile(start, new Color(0.66f, 0.27f, 0.07f, 1f), 0.9f, 0f, 10f, 0.2f, 0.9f, 360f, 0.45f, 0.82f, 0.04f, 0.14f, 0.03f, 0.07f, 34, -0.05f, new Vector3(0f, 0.32f, 0f), 0.45f, 0.08f, 0.5f, 0.65f, 1f, false, 0f, new Vector3(0f, 0.03f, 0f), ParticleSystemShapeType.Hemisphere);
            }

            public static MutationVisualProfile Frost()
            {
                Color start = new(0.76f, 0.92f, 1f, 1f);
                return new MutationVisualProfile(start, new Color(1f, 1f, 1f, 1f), 0.95f, 0f, 11f, 0.18f, 1f, 360f, 0.65f, 1.15f, 0.01f, 0.09f, 0.03f, 0.055f, 38, -0.02f, new Vector3(0.02f, 0.08f, 0.02f), 0.6f, 0f, 0.15f, 0.35f, 0.2f, false, 0f, new Vector3(0f, 0.12f, 0f), ParticleSystemShapeType.Sphere);
            }

            public static MutationVisualProfile Storm()
            {
                Color start = new(0.36f, 0.54f, 1f, 1f);
                return new MutationVisualProfile(start, new Color(0.95f, 0.95f, 1f, 1f), 0.95f, 0f, 9f, 0.08f, 0.1f, 18f, 0.18f, 0.32f, 0.2f, 0.45f, 0.022f, 0.04f, 24, 0f, new Vector3(0f, 0.04f, 0f), 0f, 0f, 1.15f, 1.2f, 2.4f, true, 0.08f, new Vector3(0f, 0.18f, 0f), ParticleSystemShapeType.Cone);
            }

            public static MutationVisualProfile Moon()
            {
                Color start = new(0.7f, 0.76f, 1f, 1f);
                return new MutationVisualProfile(start, new Color(0.86f, 0.9f, 1f, 1f), 0.82f, 0f, 8f, 0.24f, 1f, 360f, 0.85f, 1.35f, 0.01f, 0.05f, 0.03f, 0.06f, 32, 0f, new Vector3(0f, 0.03f, 0f), 0.95f, 0f, 0.55f, 0.3f, 0.9f, false, 0f, new Vector3(0f, 0.16f, 0f), ParticleSystemShapeType.Circle);
            }

            public static MutationVisualProfile Solstice()
            {
                Color start = new(1f, 0.84f, 0.22f, 1f);
                return new MutationVisualProfile(start, new Color(1f, 0.96f, 0.72f, 1f), 0.95f, 0f, 10f, 0.16f, 0.4f, 360f, 0.55f, 0.95f, 0.03f, 0.11f, 0.035f, 0.07f, 36, -0.01f, new Vector3(0f, 0.28f, 0f), 0.2f, 0.12f, 0.12f, 0.4f, 0.45f, false, 0f, new Vector3(0f, 0.06f, 0f), ParticleSystemShapeType.Hemisphere);
            }

            public static MutationVisualProfile Speed()
            {
                Color start = new(0.24f, 0.94f, 0.34f, 1f);
                return new MutationVisualProfile(start, new Color(0.52f, 1f, 0.6f, 1f), 0.85f, 0f, 12f, 0.1f, 0.2f, 18f, 0.22f, 0.42f, 0.18f, 0.4f, 0.02f, 0.04f, 30, 0f, new Vector3(0f, 0.55f, 0f), 0f, 0f, 0.22f, 0.85f, 1.9f, true, 0.12f, new Vector3(0f, 0.08f, 0f), ParticleSystemShapeType.Cone);
            }

            public static MutationVisualProfile Harvest()
            {
                Color start = new(0.78f, 1f, 0.32f, 1f);
                return new MutationVisualProfile(start, new Color(1f, 0.88f, 0.36f, 1f), 0.9f, 0f, 9f, 0.18f, 0.6f, 360f, 0.6f, 1f, 0.02f, 0.1f, 0.03f, 0.06f, 34, 0f, new Vector3(0f, 0.18f, 0f), 0.3f, 0.1f, 0.14f, 0.38f, 0.6f, false, 0f, new Vector3(0f, 0.05f, 0f), ParticleSystemShapeType.Hemisphere);
            }

            public static MutationVisualProfile Catalyst()
            {
                Color start = new(0.18f, 0.98f, 0.9f, 1f);
                return new MutationVisualProfile(start, new Color(0.3f, 0.72f, 1f, 1f), 0.9f, 0f, 13f, 0.18f, 1f, 360f, 0.35f, 0.65f, 0.04f, 0.14f, 0.025f, 0.05f, 36, 0f, new Vector3(0f, 0.24f, 0f), 1.2f, 0.15f, 0.75f, 0.9f, 1.7f, false, 0f, new Vector3(0f, 0.08f, 0f), ParticleSystemShapeType.Circle);
            }

            public static MutationVisualProfile Decay()
            {
                Color start = new(0.56f, 0.18f, 0.18f, 1f);
                return new MutationVisualProfile(start, new Color(0.16f, 0.08f, 0.08f, 1f), 0.72f, 0f, 7f, 0.12f, 1f, 360f, 0.75f, 1.2f, 0.005f, 0.05f, 0.04f, 0.085f, 24, -0.06f, new Vector3(0f, 0.03f, 0f), -0.25f, -0.04f, 0.4f, 0.32f, 0.35f, false, 0f, new Vector3(0f, 0.1f, 0f), ParticleSystemShapeType.Hemisphere);
            }

            public static MutationVisualProfile Unstable()
            {
                Color start = new(1f, 0.2f, 0.2f, 1f);
                return new MutationVisualProfile(start, new Color(1f, 0.68f, 0.12f, 1f), 0.92f, 0f, 10f, 0.12f, 1f, 360f, 0.18f, 0.4f, 0.08f, 0.24f, 0.025f, 0.055f, 26, 0f, new Vector3(0f, 0.12f, 0f), 0.4f, 0.06f, 1.05f, 1.2f, 2.2f, true, 0.09f, new Vector3(0f, 0.12f, 0f), ParticleSystemShapeType.Circle);
            }
        }
    }
}
