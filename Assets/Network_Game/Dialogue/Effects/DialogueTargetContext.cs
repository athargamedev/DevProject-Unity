using System;
using System.Collections.Generic;
using System.Linq;
using Network_Game.Combat;
using UnityEngine;

namespace Network_Game.Dialogue.Effects
{
    /// <summary>
    /// Tracks the state of a dialogue target (player or NPC) for LLM context.
    /// Updated each exchange to provide situational awareness.
    /// </summary>
    [Serializable]
    public class DialogueTargetContext
    {
        [Tooltip("Target's network object ID")]
        public ulong networkObjectId;

        [Tooltip("Target's display name")]
        public string displayName;

        [Tooltip("Current health percentage (0-1)")]
        public float healthPercent = 1f;

        [Tooltip("Maximum health value")]
        public float maxHealth = 100f;

        [Tooltip("Current health value")]
        public float currentHealth = 100f;

        [Tooltip("Active effect tags currently affecting this target")]
        public List<string> activeEffects = new List<string>();

        [Tooltip("Timestamp when each effect was applied")]
        public Dictionary<string, float> effectApplicationTimes = new Dictionary<string, float>();

        [Tooltip("The last action this target performed")]
        public string lastAction;

        [Tooltip("Timestamp of last action")]
        public float lastActionTime;

        [Tooltip("Distance to the NPC speaker in meters")]
        public float distanceToSpeaker;

        [Tooltip("Is the target currently moving")]
        public bool isMoving;

        [Tooltip("Target's velocity magnitude")]
        public float velocity;

        [Tooltip("Is the target behind cover/obstacle")]
        public bool isBehindCover;

        [Tooltip("Has line of sight from NPC to target")]
        public bool hasLineOfSight = true;

        [Tooltip("Elemental affinities (if known)")]
        public Dictionary<string, float> elementalAffinities = new Dictionary<string, float>();

        [Tooltip("Effect categories that have been used against this target recently")]
        public List<string> recentEffectCategories = new List<string>();

        [Tooltip("Target's current stance/state")]
        public TargetStance stance = TargetStance.Neutral;

        [Tooltip("Number of times this target has been hit by effects")]
        public int timesHit;

        [Tooltip("Time since last effect hit")]
        public float timeSinceLastHit = float.MaxValue;

        [Tooltip("Player customization data (if applicable)")]
        public string customizationJson;

        [Tooltip("Player archetype/class (if applicable)")]
        public string archetype;

        public enum TargetStance
        {
            Neutral,
            Aggressive,
            Defensive,
            Fleeing,
            Stunned,
            Buffed,
            Debuffed
        }

        /// <summary>
        /// Check if target is at low health (below 25%).
        /// </summary>
        public bool IsLowHealth => healthPercent < 0.25f;

        /// <summary>
        /// Check if target is at full/high health (above 75%).
        /// </summary>
        public bool IsHighHealth => healthPercent > 0.75f;

        /// <summary>
        /// Check if target is wounded but not critical (25-50%).
        /// </summary>
        public bool IsWounded => healthPercent >= 0.25f && healthPercent < 0.5f;

        /// <summary>
        /// Get a semantic description of health state.
        /// </summary>
        public string HealthDescription
        {
            get
            {
                if (healthPercent < 0.1f) return "critically wounded (near death)";
                if (healthPercent < 0.25f) return "seriously wounded";
                if (healthPercent < 0.5f) return "wounded";
                if (healthPercent < 0.75f) return "moderately healthy";
                if (healthPercent < 0.9f) return "healthy";
                return "full health";
            }
        }

        /// <summary>
        /// Get a semantic description of distance.
        /// </summary>
        public string DistanceDescription
        {
            get
            {
                if (distanceToSpeaker < 2f) return "melee range (very close)";
                if (distanceToSpeaker < 5f) return "close range";
                if (distanceToSpeaker < 10f) return "mid range";
                if (distanceToSpeaker < 20f) return "long range";
                return "very long range";
            }
        }

        /// <summary>
        /// Check if a specific effect category has been used recently.
        /// </summary>
        public bool HasRecentEffectCategory(string category, int maxRecentCount = 3)
        {
            if (string.IsNullOrWhiteSpace(category) || recentEffectCategories == null)
                return false;

            string target = category.ToLowerInvariant().Trim();
            int count = 0;
            
            // Check from most recent
            for (int i = recentEffectCategories.Count - 1; i >= 0 && count < maxRecentCount; i--)
            {
                if (recentEffectCategories[i]?.ToLowerInvariant().Trim() == target)
                    return true;
                count++;
            }
            return false;
        }

        /// <summary>
        /// Get the dominant element among active effects.
        /// </summary>
        public string GetDominantActiveElement()
        {
            if (activeEffects == null || activeEffects.Count == 0)
                return null;

            // Count elements from active effect tags
            var elementCounts = new Dictionary<string, int>();
            foreach (var effect in activeEffects)
            {
                // Try to extract element from effect name or lookup
                // This is a simplified version - in production, look up from EffectDefinition
                string element = InferElementFromEffectName(effect);
                if (!string.IsNullOrWhiteSpace(element))
                {
                    if (!elementCounts.ContainsKey(element))
                        elementCounts[element] = 0;
                    elementCounts[element]++;
                }
            }

            if (elementCounts.Count == 0)
                return null;

            return elementCounts.OrderByDescending(kvp => kvp.Value).First().Key;
        }

        /// <summary>
        /// Infer element from effect name (heuristic).
        /// </summary>
        private string InferElementFromEffectName(string effectName)
        {
            if (string.IsNullOrWhiteSpace(effectName))
                return null;

            string lower = effectName.ToLowerInvariant();
            
            if (lower.Contains("fire") || lower.Contains("flame") || lower.Contains("burn"))
                return "fire";
            if (lower.Contains("ice") || lower.Contains("frost") || lower.Contains("freeze"))
                return "ice";
            if (lower.Contains("lightning") || lower.Contains("storm") || lower.Contains("thunder"))
                return "storm";
            if (lower.Contains("water") || lower.Contains("rain") || lower.Contains("wave"))
                return "water";
            if (lower.Contains("earth") || lower.Contains("rock") || lower.Contains("stone"))
                return "earth";
            if (lower.Contains("nature") || lower.Contains("plant") || lower.Contains("vine"))
                return "nature";
            if (lower.Contains("void") || lower.Contains("shadow") || lower.Contains("dark"))
                return "void";
            if (lower.Contains("magic") || lower.Contains("arcane") || lower.Contains("mystic"))
                return "mystic";

            return null;
        }

        /// <summary>
        /// Update health from CombatHealthV2 component.
        /// </summary>
        public void UpdateHealth(CombatHealthV2 health)
        {
            if (health == null)
                return;

            currentHealth = health.CurrentHealth;
            maxHealth = health.MaxHealth;
            healthPercent = maxHealth > 0 ? currentHealth / maxHealth : 1f;
        }

        /// <summary>
        /// Record that an effect was applied to this target.
        /// </summary>
        public void RecordEffectApplied(string effectTag, string category)
        {
            if (string.IsNullOrWhiteSpace(effectTag))
                return;

            activeEffects.Add(effectTag);
            effectApplicationTimes[effectTag] = Time.time;
            
            if (!string.IsNullOrWhiteSpace(category))
                recentEffectCategories.Add(category);

            timesHit++;
            timeSinceLastHit = 0f;
        }

        /// <summary>
        /// Update timing-based fields (call each frame/tick).
        /// </summary>
        public void UpdateTiming()
        {
            timeSinceLastHit += Time.deltaTime;

            // Remove expired effects (default 10 second timeout for context purposes)
            var expired = new List<string>();
            foreach (var kvp in effectApplicationTimes)
            {
                if (Time.time - kvp.Value > 10f)
                    expired.Add(kvp.Key);
            }

            foreach (var effect in expired)
            {
                effectApplicationTimes.Remove(effect);
                activeEffects.Remove(effect);
            }
        }

        /// <summary>
        /// Get tactical suggestion based on current state.
        /// </summary>
        public string GetTacticalSuggestion()
        {
            var suggestions = new List<string>();

            if (IsLowHealth)
                suggestions.Add("Target is vulnerable - consider a finisher or show mercy");
            else if (IsHighHealth)
                suggestions.Add("Target is at full strength - open with your strongest abilities");

            if (isBehindCover)
                suggestions.Add("Target is behind cover - use AOE or repositioning effects");

            if (stance == TargetStance.Aggressive)
                suggestions.Add("Target is aggressive - consider defensive or counter effects");
            else if (stance == TargetStance.Fleeing)
                suggestions.Add("Target is fleeing - use ranged or movement-impeding effects");

            if (distanceToSpeaker < 2f)
                suggestions.Add("Close range - melee and attached effects are optimal");
            else if (distanceToSpeaker > 15f)
                suggestions.Add("Long range - projectiles and ranged effects recommended");

            var dominantElement = GetDominantActiveElement();
            if (!string.IsNullOrWhiteSpace(dominantElement))
                suggestions.Add($"Target is affected by {dominantElement} - consider elemental reactions");

            return suggestions.Count > 0 ? string.Join("; ", suggestions) : null;
        }

        /// <summary>
        /// Format as a concise string for LLM prompt.
        /// </summary>
        public string ToPromptString()
        {
            var parts = new List<string>
            {
                $"Health: {healthPercent * 100:F0}% ({HealthDescription})",
                $"Distance: {distanceToSpeaker:F1}m ({DistanceDescription})",
                $"Stance: {stance}"
            };

            if (activeEffects.Count > 0)
                parts.Add($"Active effects: {string.Join(", ", activeEffects.Take(3))}");

            if (!string.IsNullOrWhiteSpace(lastAction))
                parts.Add($"Last action: {lastAction}");

            var tactical = GetTacticalSuggestion();
            if (!string.IsNullOrWhiteSpace(tactical))
                parts.Add($"Tactical: {tactical}");

            return string.Join(" | ", parts);
        }
    }
}
