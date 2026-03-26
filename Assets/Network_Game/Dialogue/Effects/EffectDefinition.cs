using System;
using UnityEngine;

namespace Network_Game.Dialogue.Effects
{
    public enum EffectPlacementMode
    {
        Auto = 0,
        AttachMesh = 1,
        GroundAoe = 2,
        SkyVolume = 3,
        Projectile = 4,
    }

    public enum EffectTargetType
    {
        Auto = 0,
        Player = 1,
        Floor = 2,
        Npc = 3,
        WorldPoint = 4,
    }

    /// <summary>
    /// A named variant of an EffectDefinition that swaps the prefab and/or adjusts
    /// parameters so the LLM can request contextual variations
    /// (e.g. "sky", "ground", "intense", "subtle").
    /// </summary>
    [Serializable]
    public struct EffectVariant
    {
        [Tooltip("Short key the LLM uses in the variant= field (e.g. 'sky', 'ground', 'intense').")]
        public string variantKey;

        [Tooltip("Replacement prefab for this variant. Leave null to reuse the base prefab.")]
        public GameObject overridePrefab;

        [Tooltip("Scale multiplier applied on top of the intent scale.")]
        [Range(0.1f, 5f)]
        public float scaleMultiplier;

        [Tooltip("Y-axis height offset at spawn time (e.g. 15 for a sky variant).")]
        public float heightOffset;

        [Tooltip("Particle emission rate multiplier (1 = no change).")]
        [Range(0.1f, 5f)]
        public float emissionMultiplier;

        [Tooltip("Brief description shown in the LLM capabilities guide.")]
        public string description;
    }

    /// <summary>
    /// Semantic meaning for parameter values to guide LLM decision-making.
    /// </summary>
    [Serializable]
    public struct ParameterSemantic
    {
        [Tooltip("Subtle/minor effect value (whisper, hint, background)")]
        public float subtle;

        [Tooltip("Standard/normal effect value (regular combat)")]
        public float normal;

        [Tooltip("Dramatic effect value (important moment, climax building)")]
        public float dramatic;

        [Tooltip("Epic/maximum effect value (finale, ultimate ability, story moment)")]
        public float epic;

        [Tooltip("Description of when to use each level")]
        [TextArea(2, 3)]
        public string contextDescription;
    }

    /// <summary>
    /// Situational triggers to guide LLM effect selection based on context.
    /// </summary>
    [Serializable]
    public struct SituationalTrigger
    {
        [Tooltip("Prefer this effect when target health is below 25%")]
        public bool useWhenTargetLowHealth;

        [Tooltip("Prefer this effect when target health is above 75%")]
        public bool useWhenTargetFullHealth;

        [Tooltip("Prefer this effect for single targets")]
        public bool preferSingleTarget;

        [Tooltip("Prefer this effect for multiple/AOE targets")]
        public bool preferMultipleTargets;

        [Tooltip("Minimum optimal range in meters")]
        public float optimalRangeMin;

        [Tooltip("Maximum optimal range in meters")]
        public float optimalRangeMax;

        [Tooltip("Requires line of sight to target")]
        public bool requiresLineOfSight;

        [Tooltip("Requires outdoor space (sky access)")]
        public bool requiresOutdoor;

        [Tooltip("Effect categories this counters/is strong against")]
        public string[] strongAgainstCategories;

        [Tooltip("Effect categories this is weak against")]
        public string[] weakAgainstCategories;

        [Tooltip("Scene/story beats where this effect is appropriate")]
        public string[] appropriateStoryBeats;
    }

    /// <summary>
    /// Data-driven effect definition that the LLM can reference.
    /// Replaces ad-hoc keyword definitions in NpcDialogueProfile.PrefabPowerEntry.
    /// </summary>
    [CreateAssetMenu(fileName = "EffectDefinition", menuName = "Dialogue/Effect Definition")]
    public class EffectDefinition : ScriptableObject
    {
        [Header("Tag that the LLM must emit")]
        [Tooltip("Exact name used in [EFFECT: TagName]")]
        public string effectTag;

        [Header("Human-readable description for the LLM prompt")]
        [TextArea(3, 6)]
        public string description;

        [Header("Prefab that will be instantiated")]
        public GameObject effectPrefab;

        [Header("Default Parameters")]
        [Tooltip("Default scale multiplier when not specified in the effect tag")]
        public float defaultScale = 1f;

        [Tooltip("Default duration in seconds when not specified")]
        public float defaultDuration = 4f;

        [Tooltip("Default color when not specified")]
        public Color defaultColor = Color.white;

        [Header("Parameter Schema")]
        [Tooltip("Allow the LLM to override scale via 'Scale: X' parameter")]
        public bool allowCustomScale = true;

        [Tooltip("Allow the LLM to override duration via 'Duration: X' parameter")]
        public bool allowCustomDuration = true;

        [Tooltip("Allow the LLM to override color via 'Color: X' parameter")]
        public bool allowCustomColor = true;

        [Header("Execution Profile")]
        [Tooltip("Default placement behavior for this effect when parser hints are ambiguous.")]
        public EffectPlacementMode placementMode = EffectPlacementMode.Auto;

        [Tooltip("Expected semantic target for this effect.")]
        public EffectTargetType targetType = EffectTargetType.Auto;

        [Tooltip("When true and placement is mesh-attached, try to fit to target render bounds.")]
        public bool preferFitTargetMesh = true;

        [Tooltip("Optional bone/transform hint for future attachment logic (e.g. Head, Spine).")]
        public string attachBone = string.Empty;

        [Min(0.1f)]
        public float minScale = 0.25f;

        [Min(0.1f)]
        public float maxScale = 20f;

        [Min(0.05f)]
        public float minDuration = 0.2f;

        [Min(0.1f)]
        public float maxDuration = 20f;

        [Min(0.1f)]
        public float minRadius = 0.25f;

        [Min(0.1f)]
        public float maxRadius = 25f;

        [Header("Gameplay (Optional)")]
        [Tooltip("When enabled, the spawned effect can apply gameplay damage")]
        public bool enableGameplayDamage;

        [Tooltip("When enabled, projectile movement steers toward the selected target")]
        public bool enableHoming;

        [Tooltip("Projectile travel speed in meters per second")]
        [Min(0.1f)]
        public float projectileSpeed = 10f;

        [Tooltip("Maximum steering rotation speed (degrees/second) for homing projectiles")]
        [Min(0f)]
        public float homingTurnRateDegrees = 240f;

        [Tooltip("Damage applied on impact")]
        [Min(0f)]
        public float damageAmount = 10f;

        [Tooltip("Impact overlap radius used for damage application")]
        [Min(0.1f)]
        public float damageRadius = 1f;

        [Tooltip("If true, damage is applied only to objects recognized as player actors")]
        public bool affectPlayerOnly = true;

        [Tooltip("Damage type label used for logs/telemetry")]
        public string damageType = "effect";

        [Header("Surface Material FX (Optional)")]
        [Tooltip("When enabled, this effect can temporarily override a target surface material.")]
        public bool enableSurfaceMaterialOverride;

        [Tooltip("Material to apply when surface material override is enabled.")]
        public Material surfaceOverrideMaterial;

        [Tooltip("Default duration in seconds for the surface material override.")]
        [Min(0.25f)]
        public float surfaceOverrideDuration = 8f;

        [Header("Trigger Hints")]
        [Tooltip("Designer on/off toggle — disabled effects are ignored at runtime.")]
        public bool enabled = true;

        [Tooltip("Keywords that trigger this effect in the prompt-fallback path.")]
        public string[] keywords = new string[0];

        [Tooltip("Element type for contextual matching (fire, ice, storm, water, earth, nature, mystic, void).")]
        public string element = "";

        [Tooltip("Creative phrases beyond exact keyword matches.")]
        public string[] creativeTriggers = new string[0];

        [Header("Spawn Positioning")]
        [Tooltip("World-space offset applied at spawn relative to the target.")]
        public Vector3 spawnOffset = new Vector3(0f, 0.5f, 0f);

        [Tooltip("When true the effect spawns in front of the NPC rather than at its origin.")]
        public bool spawnInFrontOfNpc = true;

        [Min(0f)]
        [Tooltip("Forward distance in metres when spawnInFrontOfNpc is true.")]
        public float forwardDistance = 2f;

        [Header("Legacy Compatibility")]
        [Tooltip("Alternative tags that also trigger this effect (for backwards compatibility)")]
        public string[] alternativeTags = new string[0];

        [Header("LLM Reasoning Context")]
        [Tooltip("Categories for effect classification (attack, defense, heal, buff, debuff, ultimate, ambient, opener, finisher)")]
        public string[] categories = new string[] { "attack" };

        [Tooltip("Effects that combine well with this for combos")]
        public EffectDefinition[] synergisticEffects;

        [Tooltip("Alternative effects for similar situations")]
        public EffectDefinition[] alternativeEffects;

        [Tooltip("Effects that should NOT be used with this (redundant or conflicting)")]
        public EffectDefinition[] conflictingEffects;

        [Tooltip("Situational triggers for when to use this effect")]
        public SituationalTrigger situationalTriggers;

        [Tooltip("Semantic guidance for scale parameter interpretation")]
        public ParameterSemantic scaleSemantics = new ParameterSemantic
        {
            subtle = 0.5f,
            normal = 1.0f,
            dramatic = 2.0f,
            epic = 3.5f,
            contextDescription = "Subtle for ambience/hints, Normal for standard combat, Dramatic for important moments, Epic for climaxes"
        };

        [Tooltip("Semantic guidance for duration parameter interpretation")]
        public ParameterSemantic durationSemantics = new ParameterSemantic
        {
            subtle = 1.0f,
            normal = 4.0f,
            dramatic = 8.0f,
            epic = 15.0f,
            contextDescription = "Brief for interruptions, Normal for attacks, Long for zone control, Epic for environmental changes"
        };

        [Tooltip("Recommended cooldown before using this effect again (in exchanges)")]
        [Min(0)]
        public int recommendedCooldownExchanges = 0;

        [Tooltip("Maximum times this effect should be used in a single conversation")]
        [Min(0)]
        public int maxUsesPerConversation = 0; // 0 = unlimited

        [Tooltip("Whether this effect escalates well (true) or is a one-off surprise (false)")]
        public bool canEscalate = true;

        [Header("Variants")]
        [Tooltip("Optional prefab/parameter variants the LLM can request with variant=key in the EFFECT action.")]
        public EffectVariant[] variants = new EffectVariant[0];

        /// <summary>
        /// Try to resolve a named variant by key (case-insensitive).
        /// Returns true and sets <paramref name="variant"/> when found.
        /// </summary>
        public bool TryGetVariant(string key, out EffectVariant variant)
        {
            variant = default;
            if (variants == null || string.IsNullOrWhiteSpace(key))
                return false;
            string lower = key.ToLowerInvariant().Trim();
            foreach (var v in variants)
            {
                if (!string.IsNullOrWhiteSpace(v.variantKey)
                    && v.variantKey.ToLowerInvariant().Trim() == lower)
                {
                    variant = v;
                    return true;
                }
            }
            return false;
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(effectTag))
            {
                effectTag = name;
            }

            minScale = Mathf.Clamp(minScale, 0.1f, 100f);
            maxScale = Mathf.Clamp(maxScale, minScale, 200f);
            minDuration = Mathf.Clamp(minDuration, 0.05f, 120f);
            maxDuration = Mathf.Clamp(maxDuration, minDuration, 240f);
            minRadius = Mathf.Clamp(minRadius, 0.1f, 120f);
            maxRadius = Mathf.Clamp(maxRadius, minRadius, 240f);
            surfaceOverrideDuration = Mathf.Clamp(surfaceOverrideDuration, 0.25f, 120f);

            // Ensure categories have defaults
            if (categories == null || categories.Length == 0)
            {
                categories = new string[] { "attack" };
            }
        }

        /// <summary>
        /// Check if this effect has a specific category.
        /// </summary>
        public bool HasCategory(string category)
        {
            if (categories == null || string.IsNullOrWhiteSpace(category))
                return false;

            string target = category.ToLowerInvariant().Trim();
            foreach (var cat in categories)
            {
                if (cat != null && cat.ToLowerInvariant().Trim() == target)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Check if this effect is appropriate for the given story beat.
        /// </summary>
        public bool IsAppropriateForStoryBeat(string beat)
        {
            if (string.IsNullOrWhiteSpace(beat) || 
                situationalTriggers.appropriateStoryBeats == null ||
                situationalTriggers.appropriateStoryBeats.Length == 0)
                return true; // No restrictions = appropriate

            string target = beat.ToLowerInvariant().Trim();
            foreach (var appropriate in situationalTriggers.appropriateStoryBeats)
            {
                if (appropriate != null && appropriate.ToLowerInvariant().Trim() == target)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Get semantic description for a scale value.
        /// </summary>
        public string GetScaleDescription(float scale)
        {
            if (scale <= scaleSemantics.subtle * 1.2f)
                return $"subtle ({scale:F1}x) - whisper, hint, background ambience";
            if (scale <= scaleSemantics.normal * 1.2f)
                return $"normal ({scale:F1}x) - standard combat, regular ability";
            if (scale <= scaleSemantics.dramatic * 1.2f)
                return $"dramatic ({scale:F1}x) - important moment, building tension";
            return $"epic ({scale:F1}x) - climax, ultimate ability, story moment";
        }
    }
}
