using System;
using System.Collections.Generic;
using System.Text;

namespace Network_Game.Diagnostics
{
    public static class DiagnosticPromptComposer
    {
        public static string Compose(DiagnosticBrainPacket packet)
        {
            var builder = new StringBuilder(1024);
            builder.AppendLine($"Objective: {Coalesce(packet.Objective, "Restore stable multiplayer gameplay and dialogue flow.")}");
            builder.AppendLine($"Scene: {Coalesce(packet.SceneName, "unknown")}");
            builder.AppendLine($"Phase: {Coalesce(packet.CurrentPhase, "unknown")}");
            builder.AppendLine();

            builder.AppendLine("Top priorities:");
            AppendVariables(builder, packet.TopPriorities, "- none");
            builder.AppendLine();

            builder.AppendLine("Authority:");
            builder.AppendLine($"- {Coalesce(packet.Authority.Summary, "no authority snapshot")}");
            builder.AppendLine();

            builder.AppendLine("Scene snapshot:");
            string sceneSummary = Coalesce(packet.SceneSnapshot.SemanticSummary, "no scene snapshot");
            int objectCount = packet.SceneSnapshot.Objects != null ? packet.SceneSnapshot.Objects.Length : 0;
            builder.AppendLine($"- objects={objectCount} summary={sceneSummary}");
            builder.AppendLine();

            builder.AppendLine("Latest inference envelope:");
            if (string.IsNullOrWhiteSpace(packet.LatestEnvelope.EnvelopeId))
            {
                builder.AppendLine("- none");
            }
            else
            {
                builder.AppendLine(
                    $"- request={packet.LatestEnvelope.RequestId} model={Coalesce(packet.LatestEnvelope.ModelName, "unknown")} backend={Coalesce(packet.LatestEnvelope.BackendName, "unknown")} maxTokens={packet.LatestEnvelope.MaxTokens} estTokens={packet.LatestEnvelope.PromptTokenEstimate}"
                );
                if (!string.IsNullOrWhiteSpace(packet.LatestEnvelope.PromptPreview))
                {
                    string preview = packet.LatestEnvelope.PromptPreview;
                    if (preview.Length > 200) preview = preview[..200] + "\u2026";
                    builder.AppendLine($"- prompt_preview: {preview}");
                }
            }

            builder.AppendLine();
            builder.AppendLine("Latest execution trace:");
            if (string.IsNullOrWhiteSpace(packet.LatestExecutionTrace.TraceId))
            {
                builder.AppendLine("- none");
            }
            else
            {
                builder.AppendLine($"- {Coalesce(packet.LatestExecutionTrace.Summary, "execution trace available")}");
                if (!string.IsNullOrWhiteSpace(packet.LatestExecutionTrace.ResponsePreview))
                {
                    string preview = packet.LatestExecutionTrace.ResponsePreview;
                    if (preview.Length > 200) preview = preview[..200] + "\u2026";
                    builder.AppendLine($"- response_preview: {preview}");
                }
            }

            builder.AppendLine();
            builder.AppendLine("Latest action validation:");
            if (string.IsNullOrWhiteSpace(packet.LatestActionValidation.ResultId))
            {
                builder.AppendLine("- none");
            }
            else
            {
                builder.AppendLine($"- {Coalesce(packet.LatestActionValidation.Summary, "action validation available")}");
            }

            builder.AppendLine();
            builder.AppendLine("Latest replication trace:");
            if (string.IsNullOrWhiteSpace(packet.LatestReplicationTrace.TraceId))
            {
                builder.AppendLine("- none");
            }
            else
            {
                builder.AppendLine($"- {Coalesce(packet.LatestReplicationTrace.Summary, "replication trace available")}");
            }

            builder.AppendLine();
            builder.AppendLine("Conversation context:");
            string conversationKey = Coalesce(
                packet.LatestEnvelope.ConversationKey,
                Coalesce(packet.LatestExecutionTrace.ConversationKey, string.Empty)
            );
            if (string.IsNullOrWhiteSpace(conversationKey))
            {
                builder.AppendLine("- none");
            }
            else
            {
                builder.AppendLine(
                    $"- key={conversationKey} speaker={packet.LatestExecutionTrace.SpeakerNetworkId} listener={packet.LatestExecutionTrace.ListenerNetworkId}"
                );
            }

            builder.AppendLine();
            builder.AppendLine("Effect outcomes:");
            AppendEffectStats(builder, packet.RecentExecutionTraces);

            builder.AppendLine();
            builder.AppendLine("Recent action chains:");
            AppendActionChains(builder, packet.RecentActionChains, "- none");

            builder.AppendLine();
            builder.AppendLine("Recommended next checks:");
            AppendRecommendations(builder, packet.RecommendedActionChecks, "- none");

            if (packet.ActiveSuppressions != null && packet.ActiveSuppressions.Length > 0)
            {
                builder.AppendLine();
                builder.AppendLine("Suppressions:");
                AppendVariables(builder, packet.ActiveSuppressions, "- none");
            }

            return builder.ToString().TrimEnd();
        }

        private static void AppendVariables(
            StringBuilder builder,
            DiagnosticBrainVariable[] variables,
            string emptyLine
        )
        {
            if (variables == null || variables.Length == 0)
            {
                builder.AppendLine(emptyLine);
                return;
            }

            for (int i = 0; i < variables.Length; i++)
            {
                DiagnosticBrainVariable variable = variables[i];
                builder.Append("- ")
                    .Append(variable.Severity)
                    .Append(' ')
                    .Append(Coalesce(variable.Key, "unnamed"))
                    .Append(": ")
                    .AppendLine(Coalesce(variable.Value, string.Empty));
            }
        }

        private static void AppendActionChains(
            StringBuilder builder,
            DiagnosticActionChainSummary[] summaries,
            string emptyLine
        )
        {
            if (summaries == null || summaries.Length == 0)
            {
                builder.AppendLine(emptyLine);
                return;
            }

            for (int i = 0; i < summaries.Length; i++)
            {
                DiagnosticActionChainSummary summary = summaries[i];
                builder.Append("- ")
                    .Append(Coalesce(summary.ActionId, "action"))
                    .Append(": stage=")
                    .Append(Coalesce(summary.LatestStage, "none"))
                    .Append(" visible=")
                    .Append(summary.HasClientVisible ? "yes" : "no")
                    .Append(" failure=")
                    .Append(summary.HasFailure ? "yes" : "no")
                    .Append(" validation=")
                    .Append(summary.ValidationCount)
                    .Append(" execution=")
                    .Append(summary.ExecutionCount)
                    .Append(" replication=")
                    .Append(summary.ReplicationCount);

                string latestSummary = !string.IsNullOrWhiteSpace(summary.LatestReplicationSummary)
                    ? summary.LatestReplicationSummary
                    : !string.IsNullOrWhiteSpace(summary.LatestExecutionSummary)
                        ? summary.LatestExecutionSummary
                        : summary.LatestValidationSummary;
                if (!string.IsNullOrWhiteSpace(latestSummary))
                {
                    builder.Append(" summary=").Append(Coalesce(latestSummary, string.Empty));
                }

                builder.AppendLine();
            }
        }

        private static void AppendRecommendations(
            StringBuilder builder,
            DiagnosticActionRecommendation[] recommendations,
            string emptyLine
        )
        {
            if (recommendations == null || recommendations.Length == 0)
            {
                builder.AppendLine(emptyLine);
                return;
            }

            for (int i = 0; i < recommendations.Length; i++)
            {
                DiagnosticActionRecommendation recommendation = recommendations[i];
                builder.Append("- ")
                    .Append(Coalesce(recommendation.Priority, "P2"))
                    .Append(' ')
                    .Append(Coalesce(recommendation.ActionId, "action"))
                    .Append(": ")
                    .Append(Coalesce(recommendation.Summary, string.Empty));

                if (!string.IsNullOrWhiteSpace(recommendation.RecommendedBreakpointAnchorId))
                {
                    builder.Append(" anchor=").Append(recommendation.RecommendedBreakpointAnchorId.Trim());
                }

                if (!string.IsNullOrWhiteSpace(recommendation.RecommendedBreakpointLocation))
                {
                    builder.Append(" location=").Append(recommendation.RecommendedBreakpointLocation.Trim());
                }

                if (!string.IsNullOrWhiteSpace(recommendation.RecommendedMcpQuery))
                {
                    builder.Append(" mcp=").Append(recommendation.RecommendedMcpQuery.Trim());
                }

                builder.AppendLine();
            }
        }

        private static void AppendEffectStats(StringBuilder builder, DialogueExecutionTrace[] traces)
        {
            if (traces == null || traces.Length == 0)
            {
                builder.AppendLine("- none");
                return;
            }

            var totals = new Dictionary<string, int>(StringComparer.Ordinal);
            var successes = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < traces.Length; i++)
            {
                DialogueExecutionTrace trace = traces[i];
                string key = !string.IsNullOrWhiteSpace(trace.EffectName)
                    ? trace.EffectName
                    : !string.IsNullOrWhiteSpace(trace.EffectType)
                        ? trace.EffectType
                        : null;
                if (key == null) continue;

                if (!totals.ContainsKey(key))
                {
                    totals[key] = 0;
                    successes[key] = 0;
                }
                totals[key]++;
                if (trace.Success) successes[key]++;
            }

            if (totals.Count == 0)
            {
                builder.AppendLine("- none");
                return;
            }

            foreach (KeyValuePair<string, int> pair in totals)
            {
                builder.Append("- ").Append(pair.Key).Append(": ")
                    .Append(successes[pair.Key]).Append('/').Append(pair.Value).AppendLine(" ok");
            }
        }

        private static string Coalesce(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }
    }
}
