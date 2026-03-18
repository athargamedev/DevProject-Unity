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

            // Single reverse-order pass per input array to collect unique IDs in recency order.
            // Replications first (most authoritative for stage), then executions, then validations.
            var seen = new HashSet<string>(clampedLimit * 2, StringComparer.Ordinal);
            var orderedActionIds = new List<string>(clampedLimit);

            if (replications != null)
            {
                for (int i = replications.Length - 1; i >= 0 && orderedActionIds.Count < clampedLimit; i--)
                {
                    string id = replications[i].ActionId;
                    if (!string.IsNullOrWhiteSpace(id) && seen.Add(id)) orderedActionIds.Add(id);
                }
            }
            if (executions != null)
            {
                for (int i = executions.Length - 1; i >= 0 && orderedActionIds.Count < clampedLimit; i--)
                {
                    string id = executions[i].ActionId;
                    if (!string.IsNullOrWhiteSpace(id) && seen.Add(id)) orderedActionIds.Add(id);
                }
            }
            if (validations != null)
            {
                for (int i = validations.Length - 1; i >= 0 && orderedActionIds.Count < clampedLimit; i--)
                {
                    string id = validations[i].ActionId;
                    if (!string.IsNullOrWhiteSpace(id) && seen.Add(id)) orderedActionIds.Add(id);
                }
            }

            if (orderedActionIds.Count == 0)
            {
                return Array.Empty<DiagnosticActionChainSummary>();
            }

            var summaries = new DiagnosticActionChainSummary[orderedActionIds.Count];
            for (int i = 0; i < orderedActionIds.Count; i++)
            {
                string actionId = orderedActionIds[i];

                var valList = new List<DialogueActionValidationResult>();
                if (validations != null)
                {
                    for (int j = 0; j < validations.Length; j++)
                    {
                        if (string.Equals(validations[j].ActionId, actionId, StringComparison.Ordinal))
                            valList.Add(validations[j]);
                    }
                    valList.Sort((a, b) =>
                    {
                        int c = a.RealtimeSinceStartup.CompareTo(b.RealtimeSinceStartup);
                        return c != 0 ? c : a.Frame.CompareTo(b.Frame);
                    });
                }

                var execList = new List<DialogueExecutionTrace>();
                if (executions != null)
                {
                    for (int j = 0; j < executions.Length; j++)
                    {
                        if (string.Equals(executions[j].ActionId, actionId, StringComparison.Ordinal))
                            execList.Add(executions[j]);
                    }
                    execList.Sort((a, b) =>
                    {
                        int c = a.RealtimeSinceStartup.CompareTo(b.RealtimeSinceStartup);
                        return c != 0 ? c : a.Frame.CompareTo(b.Frame);
                    });
                }

                var replList = new List<DialogueReplicationTrace>();
                if (replications != null)
                {
                    for (int j = 0; j < replications.Length; j++)
                    {
                        if (string.Equals(replications[j].ActionId, actionId, StringComparison.Ordinal))
                            replList.Add(replications[j]);
                    }
                    replList.Sort((a, b) =>
                    {
                        int c = a.RealtimeSinceStartup.CompareTo(b.RealtimeSinceStartup);
                        return c != 0 ? c : a.Frame.CompareTo(b.Frame);
                    });
                }

                summaries[i] = BuildSummary(actionId, valList.ToArray(), execList.ToArray(), replList.ToArray());
            }

            return summaries;
        }
    }
}
