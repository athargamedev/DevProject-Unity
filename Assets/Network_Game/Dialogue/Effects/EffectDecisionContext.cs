using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Network_Game.Dialogue.Effects
{
    /// <summary>
    /// Tracks effect usage and results throughout a conversation for LLM context.
    /// Provides memory of recent effects, escalation patterns, and feedback.
    /// </summary>
    public class EffectDecisionContext
    {
        /// <summary>
        /// Record of a single effect usage.
        /// </summary>
        [Serializable]
        public class EffectUsageRecord
        {
            public string effectTag;
            public string[] categories;
            public float scale;
            public float duration;
            public float timestamp;
            public int exchangeNumber;
            public string targetName;
            public bool succeeded;
            public string failureReason;
            public float situationMatchScore;
            public string emotionalTone;
        }

        /// <summary>
        /// Result of an effect application for feedback.
        /// </summary>
        [Serializable]
        public class EffectResult
        {
            public string effectTag;
            public bool succeeded;
            public string failureReason;
            public float finalScale;
            public float finalDuration;
            public float timestamp;
            public string suggestion;
        }

        // Conversation state
        private List<EffectUsageRecord> m_UsageHistory = new List<EffectUsageRecord>();
        private Dictionary<string, int> m_EffectUsageCounts = new Dictionary<string, int>();
        private Dictionary<string, int> m_CategoryUsageCounts = new Dictionary<string, int>();
        private Queue<string> m_RecentEffectTags = new Queue<string>(5);
        private List<EffectResult> m_RecentResults = new List<EffectResult>();
        
        private int m_ExchangeCount = 0;
        private float m_ConversationStartTime;
        private float m_TensionLevel = 0.5f;
        private string m_CurrentStoryBeat = "greeting";
        private float m_LastEffectTime;

        // Constants
        private const int MaxHistorySize = 20;
        private const int MaxRecentEffects = 5;
        private const int MaxResultsFeedback = 3;

        /// <summary>
        /// Current story beat in the narrative arc.
        /// </summary>
        public string CurrentStoryBeat => m_CurrentStoryBeat;

        /// <summary>
        /// Current tension level (0-1).
        /// </summary>
        public float TensionLevel => m_TensionLevel;

        /// <summary>
        /// Number of exchanges in this conversation.
        /// </summary>
        public int ExchangeCount => m_ExchangeCount;

        /// <summary>
        /// Initialize context for a new conversation.
        /// </summary>
        public void BeginConversation(string storyBeat = "greeting", float initialTension = 0.3f)
        {
            m_UsageHistory.Clear();
            m_EffectUsageCounts.Clear();
            m_CategoryUsageCounts.Clear();
            m_RecentEffectTags.Clear();
            m_RecentResults.Clear();
            m_ExchangeCount = 0;
            m_ConversationStartTime = Time.time;
            m_CurrentStoryBeat = storyBeat;
            m_TensionLevel = Mathf.Clamp01(initialTension);
            m_LastEffectTime = 0f;
        }

        /// <summary>
        /// Record an effect usage.
        /// </summary>
        public void RecordEffectUsage(EffectDefinition definition, float scale, float duration, 
            string targetName, string emotionalTone, int exchangeNumber)
        {
            if (definition == null)
                return;

            var record = new EffectUsageRecord
            {
                effectTag = definition.effectTag,
                categories = definition.categories?.ToArray() ?? new string[] { "attack" },
                scale = scale,
                duration = duration,
                timestamp = Time.time,
                exchangeNumber = exchangeNumber,
                targetName = targetName,
                emotionalTone = emotionalTone,
                situationMatchScore = CalculateSituationMatch(definition, scale)
            };

            m_UsageHistory.Add(record);
            m_LastEffectTime = Time.time;

            // Update counts
            if (!m_EffectUsageCounts.ContainsKey(definition.effectTag))
                m_EffectUsageCounts[definition.effectTag] = 0;
            m_EffectUsageCounts[definition.effectTag]++;

            // Update category counts
            if (definition.categories != null)
            {
                foreach (var category in definition.categories)
                {
                    if (!string.IsNullOrWhiteSpace(category))
                    {
                        if (!m_CategoryUsageCounts.ContainsKey(category))
                            m_CategoryUsageCounts[category] = 0;
                        m_CategoryUsageCounts[category]++;
                    }
                }
            }

            // Update recent effects queue
            m_RecentEffectTags.Enqueue(definition.effectTag);
            while (m_RecentEffectTags.Count > MaxRecentEffects)
                m_RecentEffectTags.Dequeue();

            // Trim history
            while (m_UsageHistory.Count > MaxHistorySize)
                m_UsageHistory.RemoveAt(0);

            // Escalate tension slightly with each effect
            m_TensionLevel = Mathf.Min(1f, m_TensionLevel + 0.05f);
        }

        /// <summary>
        /// Record the result of an effect application.
        /// </summary>
        public void RecordEffectResult(string effectTag, bool succeeded, string failureReason,
            float finalScale, float finalDuration)
        {
            var result = new EffectResult
            {
                effectTag = effectTag,
                succeeded = succeeded,
                failureReason = failureReason,
                finalScale = finalScale,
                finalDuration = finalDuration,
                timestamp = Time.time,
                suggestion = GenerateSuggestion(effectTag, succeeded, failureReason)
            };

            m_RecentResults.Add(result);
            while (m_RecentResults.Count > MaxResultsFeedback)
                m_RecentResults.RemoveAt(0);
        }

        /// <summary>
        /// Advance to next exchange.
        /// </summary>
        public void NextExchange()
        {
            m_ExchangeCount++;
        }

        /// <summary>
        /// Set the current story beat.
        /// </summary>
        public void SetStoryBeat(string beat)
        {
            m_CurrentStoryBeat = beat?.ToLowerInvariant() ?? "greeting";
            
            // Adjust tension based on beat
            m_TensionLevel = m_CurrentStoryBeat switch
            {
                "greeting" => 0.2f,
                "exposition" => 0.3f,
                "risingaction" => 0.5f,
                "climax" => 0.9f,
                "fallingaction" => 0.6f,
                "resolution" => 0.3f,
                _ => m_TensionLevel
            };
        }

        /// <summary>
        /// Check if an effect has been used recently.
        /// </summary>
        public bool WasUsedRecently(string effectTag, int maxExchanges = 3)
        {
            if (string.IsNullOrWhiteSpace(effectTag))
                return false;

            string target = effectTag.ToLowerInvariant().Trim();
            
            for (int i = m_UsageHistory.Count - 1; i >= 0 && i >= m_UsageHistory.Count - maxExchanges; i--)
            {
                if (m_UsageHistory[i].effectTag?.ToLowerInvariant().Trim() == target)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Check if an effect category has been used recently.
        /// </summary>
        public bool WasCategoryUsedRecently(string category, int maxExchanges = 3)
        {
            if (string.IsNullOrWhiteSpace(category))
                return false;

            string target = category.ToLowerInvariant().Trim();
            
            for (int i = m_UsageHistory.Count - 1; i >= 0 && i >= m_UsageHistory.Count - maxExchanges; i--)
            {
                var record = m_UsageHistory[i];
                if (record.categories != null)
                {
                    foreach (var cat in record.categories)
                    {
                        if (cat?.ToLowerInvariant().Trim() == target)
                            return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Get the count of how many times an effect has been used.
        /// </summary>
        public int GetUsageCount(string effectTag)
        {
            if (string.IsNullOrWhiteSpace(effectTag))
                return 0;

            string target = effectTag.ToLowerInvariant().Trim();
            return m_EffectUsageCounts.TryGetValue(target, out int count) ? count : 0;
        }

        /// <summary>
        /// Get the most frequently used categories.
        /// </summary>
        public List<string> GetMostUsedCategories(int count = 3)
        {
            return m_CategoryUsageCounts
                .OrderByDescending(kvp => kvp.Value)
                .Take(count)
                .Select(kvp => kvp.Key)
                .ToList();
        }

        /// <summary>
        /// Get the last effect used.
        /// </summary>
        public string GetLastEffectUsed()
        {
            if (m_UsageHistory.Count == 0)
                return null;
            return m_UsageHistory[m_UsageHistory.Count - 1].effectTag;
        }

        /// <summary>
        /// Check if an effect is at or exceeds its recommended cooldown.
        /// </summary>
        public bool IsEffectOffCooldown(EffectDefinition definition)
        {
            if (definition == null)
                return true;

            int usesSince = 0;
            int exchangesToCheck = definition.recommendedCooldownExchanges;
            
            for (int i = m_UsageHistory.Count - 1; i >= 0 && i >= m_UsageHistory.Count - exchangesToCheck; i--)
            {
                if (m_UsageHistory[i].effectTag == definition.effectTag)
                    usesSince++;
            }

            return usesSince == 0;
        }

        /// <summary>
        /// Check if an effect has reached its max uses per conversation.
        /// </summary>
        public bool IsEffectAvailable(EffectDefinition definition)
        {
            if (definition == null)
                return false;

            if (definition.maxUsesPerConversation <= 0)
                return true;

            int currentUses = GetUsageCount(definition.effectTag);
            return currentUses < definition.maxUsesPerConversation;
        }

        /// <summary>
        /// Calculate how well an effect choice matches the situation.
        /// </summary>
        private float CalculateSituationMatch(EffectDefinition definition, float scale)
        {
            float score = 0.5f;

            // Scale appropriateness for tension
            float expectedScale = m_TensionLevel switch
            {
                < 0.3f => definition.scaleSemantics.subtle,
                < 0.6f => definition.scaleSemantics.normal,
                < 0.8f => definition.scaleSemantics.dramatic,
                _ => definition.scaleSemantics.epic
            };

            float scaleDiff = Mathf.Abs(scale - expectedScale) / expectedScale;
            score += Mathf.Max(0, 0.3f - scaleDiff);

            // Story beat appropriateness
            if (definition.IsAppropriateForStoryBeat(m_CurrentStoryBeat))
                score += 0.2f;

            return Mathf.Clamp01(score);
        }

        /// <summary>
        /// Generate a suggestion based on effect result.
        /// </summary>
        private string GenerateSuggestion(string effectTag, bool succeeded, string failureReason)
        {
            if (succeeded)
                return null;

            if (string.IsNullOrWhiteSpace(failureReason))
                return $"{effectTag} failed - try a different approach";

            return failureReason.ToLowerInvariant() switch
            {
                var s when s.Contains("range") => $"{effectTag} failed: target out of range - try a longer-range effect or close the distance",
                var s when s.Contains("line_of_sight") || s.Contains("los") => $"{effectTag} failed: no clear line of sight - reposition or use an AOE effect",
                var s when s.Contains("outdoor") || s.Contains("sky") => $"{effectTag} requires outdoor space - use an indoor-capable effect instead",
                var s when s.Contains("cooldown") => $"{effectTag} is on cooldown - try an alternative effect",
                var s when s.Contains("whitelist") => $"{effectTag} is not available to this NPC",
                _ => $"{effectTag} failed ({failureReason}) - try a different effect"
            };
        }

        /// <summary>
        /// Get a suggestion for variety based on usage patterns.
        /// </summary>
        public string GetVarietySuggestion()
        {
            if (m_UsageHistory.Count < 2)
                return null;

            var last = m_UsageHistory[m_UsageHistory.Count - 1];
            var secondLast = m_UsageHistory[m_UsageHistory.Count - 2];

            // Check for repetition
            if (last.effectTag == secondLast.effectTag)
                return $"⚠️ You just used {last.effectTag} twice in a row. Try a different effect for variety.";

            // Check for category repetition
            if (last.categories != null && secondLast.categories != null)
            {
                var shared = last.categories.Intersect(secondLast.categories).ToList();
                if (shared.Count > 0)
                {
                    string category = shared[0];
                    if (category.ToLowerInvariant() == "attack")
                        return "Consider using a defensive effect or buff to vary your approach.";
                    return $"You've been focusing on {category} effects. Try a different category.";
                }
            }

            // Suggest escalation if tension is rising
            if (m_TensionLevel > 0.7f && last.scale < 2.0f)
                return "Tension is high - this is the time for dramatic, large-scale effects!";

            // Suggest de-escalation if overusing big effects
            if (m_TensionLevel < 0.4f && last.scale > 2.5f)
                return "Consider a more subtle approach to build tension gradually.";

            return null;
        }

        /// <summary>
        /// Build the full context string for LLM prompt.
        /// </summary>
        public string BuildContextPrompt()
        {
            var sb = new StringBuilder();

            // Story beat and tension
            sb.AppendLine($"[Narrative Context]");
            sb.AppendLine($"Story Beat: {m_CurrentStoryBeat} | Tension: {m_TensionLevel:F2}/1.0");
            
            string intensityGuidance = m_TensionLevel switch
            {
                < 0.3f => "Keep effects subtle (scale 0.5-1.0) - this is a calm moment",
                < 0.6f => "Use normal intensity (scale 1.0-1.5) - standard exchanges",
                < 0.8f => "Build drama (scale 1.5-2.5) - rising action",
                _ => "GO EPIC (scale 2.5+) - this is THE moment!"
            };
            sb.AppendLine($"Intensity Guidance: {intensityGuidance}");
            sb.AppendLine();

            // Effect history
            if (m_RecentEffectTags.Count > 0)
            {
                sb.AppendLine($"[Your Recent Effects] {string.Join(" → ", m_RecentEffectTags.ToArray())}");
                
                // Check for patterns
                var varietySuggestion = GetVarietySuggestion();
                if (!string.IsNullOrWhiteSpace(varietySuggestion))
                    sb.AppendLine($"Variety Note: {varietySuggestion}");
                
                sb.AppendLine();
            }

            // Category usage
            if (m_CategoryUsageCounts.Count > 0)
            {
                var topCategories = m_CategoryUsageCounts
                    .OrderByDescending(kvp => kvp.Value)
                    .Take(3)
                    .Select(kvp => $"{kvp.Key}({kvp.Value})")
                    .ToList();
                
                sb.AppendLine($"[Your Effect Style] Focusing on: {string.Join(", ", topCategories)}");
                sb.AppendLine();
            }

            // Recent effect results — failures and successes separated.
            var recentFailures = m_RecentResults.Where(r => !r.succeeded).Take(2).ToList();
            var recentSuccesses = m_RecentResults.Where(r => r.succeeded).Take(3).ToList();
            if (recentFailures.Count > 0 || recentSuccesses.Count > 0)
            {
                sb.AppendLine("[Recent Effect Results]");
                foreach (var s in recentSuccesses)
                    sb.AppendLine($"  {s.effectTag}: SPAWNED (scale={s.finalScale:F1}, dur={s.finalDuration:F1}s)");
                foreach (var f in recentFailures)
                    sb.AppendLine($"  {f.effectTag}: FAILED -- {f.suggestion}");
                sb.AppendLine();
            }

            // Usage counts for this conversation
            if (m_EffectUsageCounts.Count > 0)
            {
                sb.AppendLine($"[Usage This Conversation]");
                foreach (var kvp in m_EffectUsageCounts.OrderByDescending(kvp => kvp.Value).Take(5))
                {
                    sb.AppendLine($"  {kvp.Key}: {kvp.Value}x");
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        /// <summary>
        /// Get debug information.
        /// </summary>
        public string GetDebugInfo()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"EffectDecisionContext Debug:");
            sb.AppendLine($"  Story Beat: {m_CurrentStoryBeat}");
            sb.AppendLine($"  Tension: {m_TensionLevel:F2}");
            sb.AppendLine($"  Exchanges: {m_ExchangeCount}");
            sb.AppendLine($"  Total Effects Used: {m_UsageHistory.Count}");
            sb.AppendLine($"  Recent Effects: {string.Join(", ", m_RecentEffectTags.ToArray())}");
            return sb.ToString();
        }
    }
}
