using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Network_Game.Diagnostics
{
    [DisallowMultipleComponent]
    public sealed class DiagnosticBrainSession : MonoBehaviour
    {
        private static DiagnosticBrainSession s_Instance;
        private readonly Dictionary<string, DiagnosticBrainVariable> m_Variables =
            new Dictionary<string, DiagnosticBrainVariable>(StringComparer.Ordinal);

        [SerializeField]
        private string m_RunId = string.Empty;

        [SerializeField]
        private string m_Objective = "Restore stable multiplayer gameplay and dialogue flow.";

        public static DiagnosticBrainSession Instance => s_Instance;
        public string RunId => m_RunId;

        public string Objective
        {
            get => string.IsNullOrWhiteSpace(m_Objective)
                ? "Restore stable multiplayer gameplay and dialogue flow."
                : m_Objective;
            set => m_Objective = string.IsNullOrWhiteSpace(value)
                ? "Restore stable multiplayer gameplay and dialogue flow."
                : value.Trim();
        }

        private void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                Destroy(this);
                return;
            }

            s_Instance = this;
            if (string.IsNullOrWhiteSpace(m_RunId))
            {
                m_RunId = Guid.NewGuid().ToString("N");
            }
        }

        private void OnDestroy()
        {
            if (s_Instance == this)
            {
                s_Instance = null;
            }
        }

        public void UpsertVariable(DiagnosticBrainVariable variable)
        {
            if (string.IsNullOrWhiteSpace(variable.Key))
            {
                return;
            }

            float now = Time.realtimeSinceStartup;
            if (variable.CreatedAt <= 0f)
            {
                variable.CreatedAt = now;
            }

            m_Variables[variable.Key] = variable;
        }

        public void RemoveVariable(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            m_Variables.Remove(key);
        }

        public bool TryGetVariable(string key, out DiagnosticBrainVariable variable)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                variable = default;
                return false;
            }

            return m_Variables.TryGetValue(key, out variable);
        }

        public DiagnosticBrainVariable[] GetActiveVariables()
        {
            RemoveExpiredVariables();
            return m_Variables.Values.ToArray();
        }

        public DiagnosticBrainPacket BuildPacket()
        {
            DiagnosticBrainVariable[] allVariables = GetActiveVariables();
            DiagnosticBrainVariable[] facts = allVariables
                .Where(variable => variable.Kind == DiagnosticBrainVariableKind.Fact)
                .OrderBy(variable => variable.Key, StringComparer.Ordinal)
                .ToArray();
            DiagnosticBrainVariable[] suppressions = allVariables
                .Where(variable => variable.Kind == DiagnosticBrainVariableKind.Suppression)
                .OrderBy(variable => variable.Key, StringComparer.Ordinal)
                .ToArray();
            DiagnosticBrainVariable[] priorities = DiagnosticPriorityEngine.GetTopPriorities(allVariables);

            AuthoritySnapshot authoritySnapshot = DiagnosticBrainRuntime.TryGetLatestAuthoritySnapshot(out AuthoritySnapshot authority)
                ? authority
                : default;
            AuthoritativeSceneSnapshot sceneSnapshot = DiagnosticBrainRuntime.TryGetLatestSceneSnapshot(out AuthoritativeSceneSnapshot snapshot)
                ? snapshot
                : default;
            DialogueInferenceEnvelope latestEnvelope =
                DialogueInferenceEnvelopeStore.Instance != null
                && DialogueInferenceEnvelopeStore.Instance.TryGetLatest(out DialogueInferenceEnvelope envelope)
                    ? envelope
                    : default;
            DialogueExecutionTrace latestExecutionTrace =
                DialogueExecutionTraceStore.Instance != null
                && DialogueExecutionTraceStore.Instance.TryGetLatest(out DialogueExecutionTrace executionTrace)
                    ? executionTrace
                    : default;
            DialogueActionValidationResult latestActionValidation =
                DialogueActionValidationStore.Instance != null
                && DialogueActionValidationStore.Instance.TryGetLatest(out DialogueActionValidationResult actionValidation)
                    ? actionValidation
                    : default;
            DialogueReplicationTrace latestReplicationTrace =
                DialogueReplicationTraceStore.Instance != null
                && DialogueReplicationTraceStore.Instance.TryGetLatest(out DialogueReplicationTrace replicationTrace)
                    ? replicationTrace
                    : default;
            DiagnosticActionChainSummary[] recentActionChains =
                DiagnosticActionChainSummarizer.BuildRecentSummaries(
                    DialogueActionValidationStore.Instance != null
                        ? DialogueActionValidationStore.Instance.GetRecent()
                        : Array.Empty<DialogueActionValidationResult>(),
                    DialogueExecutionTraceStore.Instance != null
                        ? DialogueExecutionTraceStore.Instance.GetRecent()
                        : Array.Empty<DialogueExecutionTrace>(),
                    DialogueReplicationTraceStore.Instance != null
                        ? DialogueReplicationTraceStore.Instance.GetRecent()
                        : Array.Empty<DialogueReplicationTrace>(),
                    3
                );
            DiagnosticActionRecommendation[] recommendedActionChecks =
                DiagnosticActionRecommendationEngine.BuildRecommendations(recentActionChains, 3);
            DialogueExecutionTrace[] recentExecutionTraces =
                DialogueExecutionTraceStore.Instance != null
                    ? DialogueExecutionTraceStore.Instance.GetRecent()
                    : Array.Empty<DialogueExecutionTrace>();

            var packet = new DiagnosticBrainPacket
            {
                RunId = m_RunId,
                BootId = SceneWorkflowDiagnostics.ActiveBootId,
                Objective = Objective,
                SceneName = string.IsNullOrWhiteSpace(sceneSnapshot.SceneName)
                    ? authoritySnapshot.SceneName
                    : sceneSnapshot.SceneName,
                CurrentPhase = string.IsNullOrWhiteSpace(authoritySnapshot.CurrentPhase)
                    ? "unknown"
                    : authoritySnapshot.CurrentPhase,
                Authority = authoritySnapshot,
                SceneSnapshot = sceneSnapshot,
                LatestEnvelope = latestEnvelope,
                LatestExecutionTrace = latestExecutionTrace,
                LatestActionValidation = latestActionValidation,
                LatestReplicationTrace = latestReplicationTrace,
                RecentActionChains = recentActionChains,
                RecommendedActionChecks = recommendedActionChecks,
                RecentExecutionTraces = recentExecutionTraces,
                TopPriorities = priorities,
                ActiveFacts = facts,
                ActiveSuppressions = suppressions,
                Summary = priorities.Length > 0
                    ? priorities[0].Value
                    : "No active blockers.",
            };

            return packet;
        }

        private void RemoveExpiredVariables()
        {
            if (m_Variables.Count == 0)
            {
                return;
            }

            float now = Time.realtimeSinceStartup;
            List<string> expiredKeys = null;
            foreach (KeyValuePair<string, DiagnosticBrainVariable> pair in m_Variables)
            {
                if (!pair.Value.IsExpired(now))
                {
                    continue;
                }

                if (expiredKeys == null)
                {
                    expiredKeys = new List<string>();
                }

                expiredKeys.Add(pair.Key);
            }

            if (expiredKeys == null)
            {
                return;
            }

            for (int i = 0; i < expiredKeys.Count; i++)
            {
                m_Variables.Remove(expiredKeys[i]);
            }
        }
    }
}
