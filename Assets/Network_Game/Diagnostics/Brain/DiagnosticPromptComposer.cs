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
            }

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

        private static string Coalesce(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }
    }
}
