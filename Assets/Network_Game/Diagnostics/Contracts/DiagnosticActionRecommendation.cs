using System;
using System.Collections.Generic;

namespace Network_Game.Diagnostics
{
    [Serializable]
    public struct DiagnosticActionRecommendation
    {
        public string ActionId;
        public string Stage;
        public string Priority;
        public string Summary;
        public string RecommendedBreakpointAnchorId;
        public string RecommendedBreakpointLocation;
        public string RecommendedMcpQuery;
    }

    public static class DiagnosticActionRecommendationEngine
    {
        public static DiagnosticActionRecommendation[] BuildRecommendations(
            DiagnosticActionChainSummary[] summaries,
            int limit
        )
        {
            if (summaries == null || summaries.Length == 0)
            {
                return Array.Empty<DiagnosticActionRecommendation>();
            }

            int clampedLimit = Math.Max(1, Math.Min(limit, 16));
            var results = new List<DiagnosticActionRecommendation>(clampedLimit);
            for (int i = 0; i < summaries.Length && results.Count < clampedLimit; i++)
            {
                if (TryBuildRecommendation(summaries[i], out DiagnosticActionRecommendation recommendation))
                {
                    results.Add(recommendation);
                }
            }

            return results.ToArray();
        }

        private static bool TryBuildRecommendation(
            DiagnosticActionChainSummary summary,
            out DiagnosticActionRecommendation recommendation
        )
        {
            recommendation = default;
            if (string.IsNullOrWhiteSpace(summary.ActionId))
            {
                return false;
            }

            string actionId = summary.ActionId;
            string chainQuery = $"GetDiagnosticActionChain(\"{actionId}\")";

            if (string.Equals(summary.LatestValidationDecision, "rejected", StringComparison.OrdinalIgnoreCase))
            {
                recommendation = new DiagnosticActionRecommendation
                {
                    ActionId = actionId,
                    Stage = "validation",
                    Priority = "P1",
                    Summary = "The action was rejected during server-side validation. Inspect target resolution, placement hints, and spatial validation first.",
                    RecommendedBreakpointAnchorId = DiagnosticBreakpointAnchors.DialogueValidationEffectParser,
                    RecommendedBreakpointLocation = "NetworkDialogueService.ApplyEffectParserIntents / ApplyPlayerSpecialEffects",
                    RecommendedMcpQuery = chainQuery,
                };
                return true;
            }

            if (
                string.Equals(summary.LatestReplicationStage, "rpc_sent", StringComparison.OrdinalIgnoreCase)
                && !summary.HasClientVisible
            )
            {
                recommendation = new DiagnosticActionRecommendation
                {
                    ActionId = actionId,
                    Stage = "rpc_sent",
                    Priority = "P1",
                    Summary = "The action was dispatched on the server but has not been observed at rpc_received or client_visible. Inspect NGO delivery and RPC handler entry first.",
                    RecommendedBreakpointAnchorId = DiagnosticBreakpointAnchors.DialogueRpcReceiveEffect,
                    RecommendedBreakpointLocation = "NetworkDialogueService.Apply*ClientRpc handlers",
                    RecommendedMcpQuery = chainQuery,
                };
                return true;
            }

            if (
                string.Equals(summary.LatestReplicationStage, "rpc_received", StringComparison.OrdinalIgnoreCase)
                && !summary.HasClientVisible
            )
            {
                recommendation = new DiagnosticActionRecommendation
                {
                    ActionId = actionId,
                    Stage = "rpc_received",
                    Priority = "P1",
                    Summary = "The client received the RPC but the effect did not become client-visible. Inspect effect controller application and deferred feedback blocking next.",
                    RecommendedBreakpointAnchorId = DiagnosticBreakpointAnchors.DialogueSceneEffectApply,
                    RecommendedBreakpointLocation = "DialogueSceneEffectsController.EmitEffectApplied / Apply*Effect methods",
                    RecommendedMcpQuery = chainQuery,
                };
                return true;
            }

            if (
                !summary.HasClientVisible
                && string.Equals(summary.LatestValidationDecision, "validated", StringComparison.OrdinalIgnoreCase)
            )
            {
                recommendation = new DiagnosticActionRecommendation
                {
                    ActionId = actionId,
                    Stage = "validated",
                    Priority = "P2",
                    Summary = "The action validated successfully but no visible outcome has been recorded. Inspect dispatch and follow the action chain forward from validation.",
                    RecommendedBreakpointAnchorId = DiagnosticBreakpointAnchors.DialogueDispatchEffectRpc,
                    RecommendedBreakpointLocation = "NetworkDialogueService.RecordExecutionTrace around effect dispatch",
                    RecommendedMcpQuery = chainQuery,
                };
                return true;
            }

            if (summary.HasFailure)
            {
                recommendation = new DiagnosticActionRecommendation
                {
                    ActionId = actionId,
                    Stage = string.IsNullOrWhiteSpace(summary.LatestStage) ? "failure" : summary.LatestStage,
                    Priority = "P2",
                    Summary = "The action chain contains a failure. Inspect the latest failing stage summary and trace backward to the previous successful stage.",
                    RecommendedBreakpointAnchorId = DiagnosticBreakpointAnchors.DialogueFailureGeneric,
                    RecommendedBreakpointLocation = "NetworkDialogueService / DialogueSceneEffectsController failing stage",
                    RecommendedMcpQuery = chainQuery,
                };
                return true;
            }

            return false;
        }
    }
}
