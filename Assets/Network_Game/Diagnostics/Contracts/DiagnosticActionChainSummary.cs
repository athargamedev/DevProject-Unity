using System;
using System.Collections.Generic;
using System.Linq;

namespace Network_Game.Diagnostics
{
    [Serializable]
    public struct DiagnosticActionChainSummary
    {
        public string ActionId;
        public string LatestStage;
        public bool HasClientVisible;
        public bool HasFailure;
        public int RequestId;
        public int ClientRequestId;
        public int ValidationCount;
        public int ExecutionCount;
        public int ReplicationCount;
        public string LatestValidationDecision;
        public string LatestExecutionStage;
        public string LatestReplicationStage;
        public string LatestValidationSummary;
        public string LatestExecutionSummary;
        public string LatestReplicationSummary;
    }

    public static class DiagnosticActionChainSummarizer
    {
        public static DiagnosticActionChainSummary BuildSummary(
            string actionId,
            DialogueActionValidationResult[] validationMatches,
            DialogueExecutionTrace[] executionMatches,
            DialogueReplicationTrace[] replicationMatches
        )
        {
            DialogueActionValidationResult latestValidation =
                validationMatches != null && validationMatches.Length > 0
                    ? validationMatches[validationMatches.Length - 1]
                    : default;
            DialogueExecutionTrace latestExecution =
                executionMatches != null && executionMatches.Length > 0
                    ? executionMatches[executionMatches.Length - 1]
                    : default;
            DialogueReplicationTrace latestReplication =
                replicationMatches != null && replicationMatches.Length > 0
                    ? replicationMatches[replicationMatches.Length - 1]
                    : default;

            string latestStage = !string.IsNullOrWhiteSpace(latestReplication.Stage)
                ? latestReplication.Stage
                : !string.IsNullOrWhiteSpace(latestExecution.Stage)
                    ? latestExecution.Stage
                    : !string.IsNullOrWhiteSpace(latestValidation.Decision)
                        ? latestValidation.Decision
                        : "none";
            bool hasClientVisible = replicationMatches != null && replicationMatches.Any(trace =>
                string.Equals(trace.Stage, "client_visible", StringComparison.OrdinalIgnoreCase)
            );
            bool hasFailure =
                (validationMatches != null && validationMatches.Any(result => !result.Success))
                || (executionMatches != null && executionMatches.Any(trace => !trace.Success))
                || (replicationMatches != null && replicationMatches.Any(trace => !trace.Success));
            int requestId = latestExecution.RequestId > 0
                ? latestExecution.RequestId
                : latestValidation.RequestId > 0
                    ? latestValidation.RequestId
                    : latestReplication.RequestId;
            int clientRequestId = latestExecution.ClientRequestId > 0
                ? latestExecution.ClientRequestId
                : latestValidation.ClientRequestId > 0
                    ? latestValidation.ClientRequestId
                    : latestReplication.ClientRequestId;

            return new DiagnosticActionChainSummary
            {
                ActionId = actionId ?? string.Empty,
                LatestStage = latestStage,
                HasClientVisible = hasClientVisible,
                HasFailure = hasFailure,
                RequestId = requestId,
                ClientRequestId = clientRequestId,
                ValidationCount = validationMatches != null ? validationMatches.Length : 0,
                ExecutionCount = executionMatches != null ? executionMatches.Length : 0,
                ReplicationCount = replicationMatches != null ? replicationMatches.Length : 0,
                LatestValidationDecision = string.IsNullOrWhiteSpace(latestValidation.Decision)
                    ? "none"
                    : latestValidation.Decision,
                LatestExecutionStage = string.IsNullOrWhiteSpace(latestExecution.Stage)
                    ? "none"
                    : latestExecution.Stage,
                LatestReplicationStage = string.IsNullOrWhiteSpace(latestReplication.Stage)
                    ? "none"
                    : latestReplication.Stage,
                LatestValidationSummary = latestValidation.Summary ?? string.Empty,
                LatestExecutionSummary = latestExecution.Summary ?? string.Empty,
                LatestReplicationSummary = latestReplication.Summary ?? string.Empty,
            };
        }

        public static DiagnosticActionChainSummary[] BuildRecentSummaries(
            DialogueActionValidationResult[] validations,
            DialogueExecutionTrace[] executions,
            DialogueReplicationTrace[] replications,
            int limit
        )
        {
            int clampedLimit = Math.Max(1, Math.Min(limit, 64));
            List<string> orderedActionIds = (replications ?? Array.Empty<DialogueReplicationTrace>())
                .Select(trace => trace.ActionId)
                .Concat((executions ?? Array.Empty<DialogueExecutionTrace>()).Select(trace => trace.ActionId))
                .Concat((validations ?? Array.Empty<DialogueActionValidationResult>()).Select(result => result.ActionId))
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Reverse()
                .Distinct(StringComparer.Ordinal)
                .Take(clampedLimit)
                .ToList();

            var summaries = new List<DiagnosticActionChainSummary>(orderedActionIds.Count);
            for (int i = 0; i < orderedActionIds.Count; i++)
            {
                string actionId = orderedActionIds[i];
                DialogueActionValidationResult[] validationMatches = (validations ?? Array.Empty<DialogueActionValidationResult>())
                    .Where(result => string.Equals(result.ActionId, actionId, StringComparison.Ordinal))
                    .OrderBy(result => result.RealtimeSinceStartup)
                    .ThenBy(result => result.Frame)
                    .ToArray();
                DialogueExecutionTrace[] executionMatches = (executions ?? Array.Empty<DialogueExecutionTrace>())
                    .Where(trace => string.Equals(trace.ActionId, actionId, StringComparison.Ordinal))
                    .OrderBy(trace => trace.RealtimeSinceStartup)
                    .ThenBy(trace => trace.Frame)
                    .ToArray();
                DialogueReplicationTrace[] replicationMatches = (replications ?? Array.Empty<DialogueReplicationTrace>())
                    .Where(trace => string.Equals(trace.ActionId, actionId, StringComparison.Ordinal))
                    .OrderBy(trace => trace.RealtimeSinceStartup)
                    .ThenBy(trace => trace.Frame)
                    .ToArray();

                summaries.Add(BuildSummary(actionId, validationMatches, executionMatches, replicationMatches));
            }

            return summaries.ToArray();
        }
    }
}
