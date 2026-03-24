using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Network_Game.Diagnostics;
using UnityEngine;

namespace Network_Game.Dialogue.Effects
{
    /// <summary>
    /// Comprehensive VFX debugging and parameter learning tool.
    /// Maps all manipulatable particle parameters for LLM guidance.
    /// </summary>
    public static class EffectVfxDebugger
    {
        /// <summary>
        /// Complete map of all particle system parameters that can be manipulated.
        /// </summary>
        [Serializable]
        public class ParticleParameterMap
        {
            // Core Emission
            public float emissionRate = 10f;
            public float emissionBurst = 0f;
            
            // Lifetime
            public float startLifetime = 5f;
            public float duration = 5f;
            public bool looping = true;
            
            // Size
            public float startSize = 1f;
            public float startSize3DX = 1f;
            public float startSize3DY = 1f;
            public float startSize3DZ = 1f;
            
            // Speed
            public float startSpeed = 5f;
            public float startSpeed3DX = 0f;
            public float startSpeed3DY = 5f;
            public float startSpeed3DZ = 0f;
            
            // Color (RGBA 0-1)
            public float startColorR = 1f;
            public float startColorG = 1f;
            public float startColorB = 1f;
            public float startColorA = 1f;
            
            // Rotation
            public float startRotation = 0f;
            public float startRotation3DX = 0f;
            public float startRotation3DY = 0f;
            public float startRotation3DZ = 0f;
            
            // Shape
            public float shapeRadius = 1f;
            public float shapeAngle = 25f;
            
            // Physics
            public float gravityModifier = 0f;
            
            // Limits
            public int maxParticles = 1000;
            
            // Rate over distance
            public float rateOverDistance = 0f;
            
            // Simulation
            public float simulationSpeed = 1f;
        }

        /// <summary>
        /// Extract all parameters from a ParticleSystem for LLM learning.
        /// </summary>
        public static ParticleParameterMap ExtractParameters(ParticleSystem ps)
        {
            if (ps == null) return null;

            var map = new ParticleParameterMap();
            var main = ps.main;
            var emission = ps.emission;
            var shape = ps.shape;

            // Core - handle MinMaxCurve by using constant or constantMax
            map.duration = main.duration;
            map.looping = main.loop;
            
            // Helper to get float from MinMaxCurve
            float GetCurveValue(ParticleSystem.MinMaxCurve curve)
            {
                if (curve.mode == ParticleSystemCurveMode.Constant) return curve.constant;
                if (curve.mode == ParticleSystemCurveMode.TwoConstants) return curve.constantMax;
                return curve.Evaluate(0f);
            }
            
            map.startLifetime = GetCurveValue(main.startLifetime);
            map.startSize = GetCurveValue(main.startSize);
            map.startSpeed = GetCurveValue(main.startSpeed);
            map.startRotation = GetCurveValue(main.startRotation);
            map.gravityModifier = GetCurveValue(main.gravityModifier);
            map.maxParticles = main.maxParticles;
            map.simulationSpeed = main.simulationSpeed;

            // Color - handle MinMaxGradient
            Color GetGradientColor(ParticleSystem.MinMaxGradient gradient)
            {
                if (gradient.mode == ParticleSystemGradientMode.Color) return gradient.color;
                if (gradient.mode == ParticleSystemGradientMode.TwoColors || gradient.mode == ParticleSystemGradientMode.TwoGradients) return gradient.colorMax;
                return Color.white;
            }
            
            Color startColor = GetGradientColor(main.startColor);
            map.startColorR = startColor.r;
            map.startColorG = startColor.g;
            map.startColorB = startColor.b;
            map.startColorA = startColor.a;

            // Emission
            map.emissionRate = GetCurveValue(emission.rateOverTime);
            map.rateOverDistance = GetCurveValue(emission.rateOverDistance);

            // Shape
            if (shape.enabled)
            {
                map.shapeRadius = shape.radius;
                map.shapeAngle = shape.angle;
            }

            return map;
        }

        /// <summary>
        /// Apply parameters to a ParticleSystem.
        /// </summary>
        public static void ApplyParameters(ParticleSystem ps, ParticleParameterMap map)
        {
            if (ps == null || map == null) return;

            var main = ps.main;
            var emission = ps.emission;

            main.duration = map.duration;
            main.loop = map.looping;
            main.startLifetime = map.startLifetime;
            main.startSize = map.startSize;
            main.startSpeed = map.startSpeed;
            main.startRotation = map.startRotation;
            main.gravityModifier = map.gravityModifier;
            main.maxParticles = map.maxParticles;
            main.simulationSpeed = map.simulationSpeed;
            main.startColor = new Color(map.startColorR, map.startColorG, map.startColorB, map.startColorA);

            emission.rateOverTime = map.emissionRate;
            emission.rateOverDistance = map.rateOverDistance;
        }

        /// <summary>
        /// Log complete parameter state for debugging.
        /// </summary>
        public static void LogParticleState(ParticleSystem ps, string context)
        {
            if (ps == null) return;

            var map = ExtractParameters(ps);
            var sb = new StringBuilder();
            sb.AppendLine($"=== ParticleSystem State: {context} ===");
            sb.AppendLine($"GameObject: {ps.gameObject.name}");
            sb.AppendLine($"Position: {ps.transform.position}");
            sb.AppendLine($"Local Scale: {ps.transform.localScale}");
            sb.AppendLine($"Is Active: {ps.gameObject.activeSelf}");
            sb.AppendLine($"Is Playing: {ps.isPlaying}");
            sb.AppendLine($"Particle Count: {ps.particleCount}");
            sb.AppendLine();
            sb.AppendLine("--- Parameters ---");
            sb.AppendLine($"Duration: {map.duration:F2}s");
            sb.AppendLine($"Looping: {map.looping}");
            sb.AppendLine($"Start Lifetime: {map.startLifetime:F2}s");
            sb.AppendLine($"Start Size: {map.startSize:F2}");
            sb.AppendLine($"Start Speed: {map.startSpeed:F2}");
            sb.AppendLine($"Start Color: ({map.startColorR:F2}, {map.startColorG:F2}, {map.startColorB:F2}, {map.startColorA:F2})");
            sb.AppendLine($"Emission Rate: {map.emissionRate:F2}/sec");
            sb.AppendLine($"Max Particles: {map.maxParticles}");
            sb.AppendLine($"Gravity Modifier: {map.gravityModifier:F2}");
            sb.AppendLine($"Simulation Speed: {map.simulationSpeed:F2}");

            NGLog.Info("VFX.Debug", sb.ToString());
        }

        /// <summary>
        /// Compare two particle states and log differences.
        /// </summary>
        public static void CompareAndLog(ParticleSystem ps1, ParticleSystem ps2, string label1, string label2)
        {
            var map1 = ExtractParameters(ps1);
            var map2 = ExtractParameters(ps2);

            var sb = new StringBuilder();
            sb.AppendLine($"=== VFX Comparison: {label1} vs {label2} ===");

            CompareField(sb, "Duration", map1.duration, map2.duration);
            CompareField(sb, "Looping", map1.looping, map2.looping);
            CompareField(sb, "Start Lifetime", map1.startLifetime, map2.startLifetime);
            CompareField(sb, "Start Size", map1.startSize, map2.startSize);
            CompareField(sb, "Start Speed", map1.startSpeed, map2.startSpeed);
            CompareField(sb, "Emission Rate", map1.emissionRate, map2.emissionRate);
            CompareField(sb, "Max Particles", map1.maxParticles, map2.maxParticles);
            CompareField(sb, "Gravity", map1.gravityModifier, map2.gravityModifier);

            NGLog.Info("VFX.Compare", sb.ToString());
        }

        private static void CompareField<T>(StringBuilder sb, string name, T val1, T val2)
        {
            if (!val1.Equals(val2))
            {
                sb.AppendLine($"  {name}: {val1} → {val2}");
            }
        }

        /// <summary>
        /// Generate LLM guidance text for VFX parameters.
        /// </summary>
        public static string GenerateLLMGuidance(ParticleSystem ps)
        {
            if (ps == null) return "No particle system available.";

            var map = ExtractParameters(ps);
            var sb = new StringBuilder();
            
            sb.AppendLine("[VFX Parameter Guide]");
            sb.AppendLine("Use these parameters when requesting effect modifications:");
            sb.AppendLine();
            sb.AppendLine("Emission Control:");
            sb.AppendLine($"  - emissionRate: {map.emissionRate:F1} (particles/sec, higher = denser)");
            sb.AppendLine($"  - maxParticles: {map.maxParticles} (cap on total particles)");
            sb.AppendLine();
            sb.AppendLine("Timing:");
            sb.AppendLine($"  - duration: {map.duration:F1}s (how long effect runs)");
            sb.AppendLine($"  - startLifetime: {map.startLifetime:F1}s (how long each particle lives)");
            sb.AppendLine($"  - looping: {map.looping} (true = repeats, false = one-shot)");
            sb.AppendLine();
            sb.AppendLine("Size & Speed:");
            sb.AppendLine($"  - startSize: {map.startSize:F1} (particle size, higher = bigger)");
            sb.AppendLine($"  - startSpeed: {map.startSpeed:F1} (initial velocity)");
            sb.AppendLine();
            sb.AppendLine("Color (RGBA 0-1):");
            sb.AppendLine($"  - Current: ({map.startColorR:F2}, {map.startColorG:F2}, {map.startColorB:F2}, {map.startColorA:F2})");
            sb.AppendLine("  - Red=1,0,0 | Green=0,1,0 | Blue=0,0,1 | White=1,1,1 | Yellow=1,1,0");
            sb.AppendLine();
            sb.AppendLine("Physics:");
            sb.AppendLine($"  - gravityModifier: {map.gravityModifier:F1} (0=weightless, 1=normal gravity)");
            sb.AppendLine($"  - simulationSpeed: {map.simulationSpeed:F1} (1=normal, 2=2x speed)");

            return sb.ToString();
        }

        /// <summary>
        /// Draw debug visualization for effect spawn.
        /// </summary>
        public static void VisualizeSpawn(Vector3 position, float scale, Color color, float duration = 5f)
        {
            Debug.DrawLine(position, position + Vector3.up * scale, color, duration);
            Debug.DrawLine(position, position + Vector3.right * scale * 0.5f, color, duration);
            Debug.DrawLine(position, position + Vector3.forward * scale * 0.5f, color, duration);
            Debug.DrawLine(position + Vector3.up * scale, position + Vector3.up * scale + Vector3.right * scale * 0.3f, color, duration);
            Debug.DrawLine(position + Vector3.up * scale, position + Vector3.up * scale - Vector3.right * scale * 0.3f, color, duration);
        }
    }
}
