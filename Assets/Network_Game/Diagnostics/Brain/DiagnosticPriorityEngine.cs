using System;
using System.Collections.Generic;
using System.Linq;

namespace Network_Game.Diagnostics
{
    public static class DiagnosticPriorityEngine
    {
        public static DiagnosticBrainVariable[] GetTopPriorities(
            IReadOnlyList<DiagnosticBrainVariable> variables,
            int maxCount = 5
        )
        {
            if (variables == null || variables.Count == 0 || maxCount <= 0)
            {
                return Array.Empty<DiagnosticBrainVariable>();
            }

            return variables
                .Where(variable => variable.Kind != DiagnosticBrainVariableKind.Suppression)
                .Where(variable => !string.IsNullOrWhiteSpace(variable.Key))
                .OrderBy(variable => variable.Severity)
                .ThenByDescending(GetKindWeight)
                .ThenByDescending(variable => GetPinnedWeight(variable))
                .ThenByDescending(GetPhaseWeight)
                .ThenByDescending(variable => variable.Confidence)
                .ThenBy(variable => variable.Key, StringComparer.Ordinal)
                .Take(maxCount)
                .ToArray();
        }

        private static int GetKindWeight(DiagnosticBrainVariable variable)
        {
            switch (variable.Kind)
            {
                case DiagnosticBrainVariableKind.Focus:
                    return 4;
                case DiagnosticBrainVariableKind.Goal:
                    return 3;
                case DiagnosticBrainVariableKind.Hypothesis:
                    return 2;
                case DiagnosticBrainVariableKind.Fact:
                    return 1;
                default:
                    return 0;
            }
        }

        private static int GetPinnedWeight(DiagnosticBrainVariable variable)
        {
            return variable.Pinned ? 1 : 0;
        }

        private static int GetPhaseWeight(DiagnosticBrainVariable variable)
        {
            if (string.IsNullOrWhiteSpace(variable.Phase))
            {
                return 0;
            }

            switch (variable.Phase.Trim().ToLowerInvariant())
            {
                case "player_spawn":
                case "player_ready":
                case "authority":
                    return 4;
                case "auth_gate":
                    return 3;
                case "network_ready":
                case "network_mode":
                    return 2;
                case "dialogue":
                case "inference_started":
                    return 1;
                default:
                    return 0;
            }
        }
    }
}
