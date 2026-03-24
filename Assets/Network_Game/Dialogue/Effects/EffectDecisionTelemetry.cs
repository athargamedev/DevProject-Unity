using System;
using System.Collections.Generic;
using System.Text;
using Network_Game.Diagnostics;
using UnityEngine;

namespace Network_Game.Dialogue.Effects
{
    /// <summary>
    /// Telemetry for tracking effect decision quality and providing insights.
    /// Logs decision context, outcomes, and provides recommendations.
    /// </summary>
    public static class EffectDecisionTelemetry
    {
        /// <summary>
        /// Record of an effect decision and its outcome.
        /// </summary>
        [Serializable]
        public class EffectDecisionRecord
        {
            public string Timestamp;
            public string NpcName;
            public string EffectTag;
            public string[] Categories;
            public float RequestedScale;
            public float FinalScale;
            public float RequestedDuration;
            public float FinalDuration;
            public string TargetName;
            public float TargetHealthPercent;
            public float DistanceToTarget;
            public string StoryBeat;
            public float TensionLevel;
            public int ExchangeNumber;
            public bool Succeeded;
            public string FailureReason;
            public string AppropriatenessScore;
            public string[] RecentEffects;
            public string Suggestion;
        }

        // In-memory buffer for recent decisions (for runtime analysis)
        private static readonly Queue<EffectDecisionRecord> s_RecentDecisions = new Queue<EffectDecisionRecord>(50);
        private const int MaxBufferSize = 100;

        /// <summary>
        /// Log an effect decision with full context for quality analysis.
        /// </summary>
        public static void LogEffectDecision(
            string npcName,
            EffectDefinition definition,
            EffectIntent intent,
            DialogueTargetContext targetContext,
            EffectDecisionContext decisionContext,
            bool succeeded,
            string failureReason = null)
        {
            if (definition == null)
                return;

            var record = new EffectDecisionRecord
            {
                Timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                NpcName = npcName,
                EffectTag = definition.effectTag,
                Categories = definition.categories ?? new string[] { "attack" },
                RequestedScale = intent?.scale ?? definition.defaultScale,
                FinalScale = intent?.GetEffectiveScale() ?? definition.defaultScale,
                RequestedDuration = intent?.duration ?? definition.defaultDuration,
                FinalDuration = intent?.GetEffectiveDuration() ?? definition.defaultDuration,
                TargetName = targetContext?.displayName ?? "unknown",
                TargetHealthPercent = targetContext?.healthPercent ?? 1f,
                DistanceToTarget = targetContext?.distanceToSpeaker ?? 0f,
                StoryBeat = decisionContext?.CurrentStoryBeat ?? "unknown",
                TensionLevel = decisionContext?.TensionLevel ?? 0.5f,
                ExchangeNumber = decisionContext?.ExchangeCount ?? 0,
                Succeeded = succeeded,
                FailureReason = failureReason,
                AppropriatenessScore = CalculateAppropriatenessScore(definition, intent, targetContext, decisionContext),
                RecentEffects = decisionContext != null ? GetRecentEffects(decisionContext) : new string[0],
                Suggestion = GenerateSuggestion(definition, intent, targetContext, decisionContext, succeeded, failureReason)
            };

            // Add to buffer
            s_RecentDecisions.Enqueue(record);
            while (s_RecentDecisions.Count > MaxBufferSize)
                s_RecentDecisions.Dequeue();

            // Log structured telemetry
            NGLog.Info("DialogueFX.Telemetry", FormatTelemetryLog(record));

            // Log warnings for questionable decisions
            if (!succeeded && !string.IsNullOrWhiteSpace(failureReason))
            {
                NGLog.Warn("DialogueFX.Telemetry", 
                    $"Effect '{record.EffectTag}' failed: {failureReason}. Suggestion: {record.Suggestion}");
            }
            else if (record.AppropriatenessScore == "low")
            {
                NGLog.Warn("DialogueFX.Telemetry", 
                    $"Effect '{record.EffectTag}' may be inappropriate. Suggestion: {record.Suggestion}");
            }
        }

        /// <summary>
        /// Log a quick effect usage without full context.
        /// </summary>
        public static void LogQuickEffect(
            string npcName,
            string effectTag,
            float scale,
            bool succeeded,
            string failureReason = null)
        {
            var status = succeeded ? "SUCCESS" : "FAILED";
            var reason = !string.IsNullOrWhiteSpace(failureReason) ? $" ({failureReason})" : "";
            
            NGLog.Info("DialogueFX", 
                $"[{status}] {npcName} used {effectTag} at scale {scale:F2}{reason}");
        }

        /// <summary>
        /// Get recent decisions for analysis.
        /// </summary>
        public static List<EffectDecisionRecord> GetRecentDecisions(int count = 10)
        {
            var result = new List<EffectDecisionRecord>();
            var array = s_RecentDecisions.ToArray();
            
            for (int i = array.Length - 1; i >= 0 && result.Count < count; i--)
            {
                result.Add(array[i]);
            }
            
            return result;
        }

        /// <summary>
        /// Get summary statistics for recent effect usage.
        /// </summary>
        public static EffectUsageStatistics GetStatistics()
        {
            var stats = new EffectUsageStatistics();
            var decisions = s_RecentDecisions.ToArray();

            if (decisions.Length == 0)
                return stats;

            int successCount = 0;
            var effectCounts = new Dictionary<string, int>();
            var categoryCounts = new Dictionary<string, int>();
            var npcCounts = new Dictionary<string, int>();

            foreach (var d in decisions)
            {
                if (d.Succeeded) successCount++;

                if (!effectCounts.ContainsKey(d.EffectTag))
                    effectCounts[d.EffectTag] = 0;
                effectCounts[d.EffectTag]++;

                if (d.Categories != null)
                {
                    foreach (var cat in d.Categories)
                    {
                        if (!categoryCounts.ContainsKey(cat))
                            categoryCounts[cat] = 0;
                        categoryCounts[cat]++;
                    }
                }

                if (!npcCounts.ContainsKey(d.NpcName))
                    npcCounts[d.NpcName] = 0;
                npcCounts[d.NpcName]++;
            }

            stats.TotalDecisions = decisions.Length;
            stats.SuccessRate = (float)successCount / decisions.Length;
            stats.MostUsedEffect = GetMaxKey(effectCounts);
            stats.MostUsedCategory = GetMaxKey(categoryCounts);
            stats.MostActiveNpc = GetMaxKey(npcCounts);
            stats.AverageScale = CalculateAverage(decisions, d => d.FinalScale);
            stats.AverageTension = CalculateAverage(decisions, d => d.TensionLevel);

            return stats;
        }

        /// <summary>
        /// Generate a report of recent effect decisions.
        /// </summary>
        public static string GenerateReport(int decisionCount = 20)
        {
            var decisions = GetRecentDecisions(decisionCount);
            var stats = GetStatistics();

            var sb = new StringBuilder();
            sb.AppendLine("=== Effect Decision Telemetry Report ===");
            sb.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            sb.AppendLine();
            
            sb.AppendLine("--- Statistics ---");
            sb.AppendLine($"Total Decisions Tracked: {stats.TotalDecisions}");
            sb.AppendLine($"Success Rate: {stats.SuccessRate:P1}");
            sb.AppendLine($"Most Used Effect: {stats.MostUsedEffect ?? "N/A"}");
            sb.AppendLine($"Most Used Category: {stats.MostUsedCategory ?? "N/A"}");
            sb.AppendLine($"Most Active NPC: {stats.MostActiveNpc ?? "N/A"}");
            sb.AppendLine($"Average Scale: {stats.AverageScale:F2}");
            sb.AppendLine($"Average Tension: {stats.AverageTension:F2}");
            sb.AppendLine();

            sb.AppendLine("--- Recent Decisions ---");
            foreach (var d in decisions)
            {
                sb.AppendLine($"[{d.Timestamp}] {d.NpcName} → {d.EffectTag} (scale {d.FinalScale:F2})");
                sb.AppendLine($"  Target: {d.TargetName} @ {d.DistanceToTarget:F1}m, Health: {d.TargetHealthPercent:P0}");
                sb.AppendLine($"  Beat: {d.StoryBeat}, Tension: {d.TensionLevel:F2}, Exchange: {d.ExchangeNumber}");
                sb.AppendLine($"  Result: {(d.Succeeded ? "SUCCESS" : "FAILED")} {d.FailureReason}");
                if (!string.IsNullOrWhiteSpace(d.Suggestion))
                    sb.AppendLine($"  Suggestion: {d.Suggestion}");
                sb.AppendLine();
            }

            return sb.ToString();
        }

        /// <summary>
        /// Clear all telemetry data.
        /// </summary>
        public static void Clear()
        {
            s_RecentDecisions.Clear();
        }

        #region Private Helpers

        private static string FormatTelemetryLog(EffectDecisionRecord record)
        {
            return NGLog.Format(
                "EffectDecision",
                ("npc", record.NpcName),
                ("effect", record.EffectTag),
                ("scale", $"{record.FinalScale:F2}"),
                ("target", record.TargetName),
                ("distance", $"{record.DistanceToTarget:F1}m"),
                ("health", $"{record.TargetHealthPercent:P0}"),
                ("beat", record.StoryBeat),
                ("tension", $"{record.TensionLevel:F2}"),
                ("success", record.Succeeded),
                ("appropriateness", record.AppropriatenessScore)
            );
        }

        private static string CalculateAppropriatenessScore(
            EffectDefinition definition,
            EffectIntent intent,
            DialogueTargetContext targetContext,
            EffectDecisionContext decisionContext)
        {
            int score = 50; // Base score

            // Check situational appropriateness
            if (targetContext != null && definition != null)
            {
                if (definition.situationalTriggers.useWhenTargetLowHealth && targetContext.IsLowHealth)
                    score += 20;
                if (definition.situationalTriggers.useWhenTargetFullHealth && targetContext.IsHighHealth)
                    score += 20;
                if (definition.situationalTriggers.optimalRangeMax > 0 && 
                    targetContext.distanceToSpeaker <= definition.situationalTriggers.optimalRangeMax)
                    score += 15;
            }

            // Check variety
            if (decisionContext != null && !decisionContext.WasUsedRecently(definition?.effectTag))
                score += 15;

            // Check tension match
            if (decisionContext != null && definition != null)
            {
                float expectedScale = decisionContext.TensionLevel switch
                {
                    < 0.3f => definition.scaleSemantics.subtle,
                    < 0.6f => definition.scaleSemantics.normal,
                    < 0.8f => definition.scaleSemantics.dramatic,
                    _ => definition.scaleSemantics.epic
                };

                float actualScale = intent?.GetEffectiveScale() ?? definition.defaultScale;
                float scaleDiff = Mathf.Abs(actualScale - expectedScale) / expectedScale;
                
                if (scaleDiff < 0.3f) score += 20;
                else if (scaleDiff < 0.6f) score += 10;
                else score -= 10;
            }

            // Check story beat
            if (decisionContext != null && definition != null && 
                definition.IsAppropriateForStoryBeat(decisionContext.CurrentStoryBeat))
                score += 10;

            return score >= 80 ? "high" : score >= 60 ? "medium" : score >= 40 ? "low" : "poor";
        }

        private static string GenerateSuggestion(
            EffectDefinition definition,
            EffectIntent intent,
            DialogueTargetContext targetContext,
            EffectDecisionContext decisionContext,
            bool succeeded,
            string failureReason)
        {
            if (!succeeded && !string.IsNullOrWhiteSpace(failureReason))
                return $"Fix: {failureReason}";

            if (decisionContext?.WasUsedRecently(definition?.effectTag) ?? false)
                return "Consider using a different effect for variety";

            if (targetContext != null && definition != null)
            {
                if (definition.situationalTriggers.useWhenTargetLowHealth && !targetContext.IsLowHealth)
                    return "Target is healthy - this effect is designed for finishing wounded targets";

                if (definition.situationalTriggers.optimalRangeMax > 0 && 
                    targetContext.distanceToSpeaker > definition.situationalTriggers.optimalRangeMax)
                    return $"Target is far away ({targetContext.distanceToSpeaker:F1}m) - consider a ranged effect";
            }

            return null;
        }

        private static string[] GetRecentEffects(EffectDecisionContext context)
        {
            // This is a simplified version - in production, expose this from EffectDecisionContext
            return new string[0];
        }

        private static string GetMaxKey(Dictionary<string, int> dict)
        {
            if (dict.Count == 0) return null;
            
            string maxKey = null;
            int maxValue = 0;
            
            foreach (var kvp in dict)
            {
                if (kvp.Value > maxValue)
                {
                    maxValue = kvp.Value;
                    maxKey = kvp.Key;
                }
            }
            
            return maxKey;
        }

        private static float CalculateAverage(EffectDecisionRecord[] decisions, Func<EffectDecisionRecord, float> selector)
        {
            if (decisions.Length == 0) return 0f;
            
            float sum = 0f;
            foreach (var d in decisions)
                sum += selector(d);
            
            return sum / decisions.Length;
        }

        #endregion
    }

    /// <summary>
    /// Summary statistics for effect usage.
    /// </summary>
    public struct EffectUsageStatistics
    {
        public int TotalDecisions;
        public float SuccessRate;
        public string MostUsedEffect;
        public string MostUsedCategory;
        public string MostActiveNpc;
        public float AverageScale;
        public float AverageTension;
    }
}
