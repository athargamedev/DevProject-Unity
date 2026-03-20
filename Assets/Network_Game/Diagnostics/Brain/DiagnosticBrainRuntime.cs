using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Network_Game.Dialogue;

namespace Network_Game.Diagnostics
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-440)]
    public sealed class DiagnosticBrainRuntime : MonoBehaviour, IDiagnosticsRuntimeBridge
    {
        private static DiagnosticBrainRuntime s_Instance;
        private static AuthoritySnapshot s_LatestAuthoritySnapshot;
        private static AuthoritativeSceneSnapshot s_LatestSceneSnapshot;
        private static bool s_HasAuthoritySnapshot;
        private static bool s_HasSceneSnapshot;

        private static readonly List<TransportWarningEntry> s_RecentTransportWarnings =
            new List<TransportWarningEntry>(8);
        private const float k_TransportWarningWindowSeconds = 30f;
        private const float k_DefaultExpirationMultiplier = 4f;

        [SerializeField]
        [Min(0.1f)]
        private float m_SnapshotIntervalSeconds = 0.25f;

        [SerializeField]
        [Min(1)]
        private int m_MaxSceneObjects = 12;

        [SerializeField]
        [Min(5f)]
        private float m_MaxSceneDistance = 120f;

        private float m_NextSnapshotAt;

        public static DiagnosticBrainRuntime Instance => s_Instance;

        public static DiagnosticBrainRuntime EnsureAvailable()
        {
            if (s_Instance != null)
            {
                return s_Instance;
            }

            GameObject host = new GameObject(nameof(DiagnosticBrainRuntime));
            return host.AddComponent<DiagnosticBrainRuntime>();
        }

        public static bool TryGetLatestAuthoritySnapshot(out AuthoritySnapshot snapshot)
        {
            snapshot = s_LatestAuthoritySnapshot;
            return s_HasAuthoritySnapshot;
        }

        public static bool TryGetLatestSceneSnapshot(out AuthoritativeSceneSnapshot snapshot)
        {
            snapshot = s_LatestSceneSnapshot;
            return s_HasSceneSnapshot;
        }

        private void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                Destroy(this);
                return;
            }

            s_Instance = this;
            DiagnosticsRuntimeBridgeRegistry.Register(this);
            EnsureSupportComponents();
        }

        private void OnEnable()
        {
            SampleNow(force: true);
        }

        private void Update()
        {
            if (Time.realtimeSinceStartup < m_NextSnapshotAt)
            {
                return;
            }

            SampleNow(force: false);
        }

        private void OnDestroy()
        {
            if (s_Instance == this)
            {
                DiagnosticsRuntimeBridgeRegistry.Unregister(this);
                s_Instance = null;
                s_HasAuthoritySnapshot = false;
                s_HasSceneSnapshot = false;
                s_LatestAuthoritySnapshot = default;
                s_LatestSceneSnapshot = default;
            }
        }

        public void SampleNow(bool force)
        {
            if (!force && Time.realtimeSinceStartup < m_NextSnapshotAt)
            {
                return;
            }

            EnsureSupportComponents();

            string runId = DiagnosticBrainSession.Instance != null
                ? DiagnosticBrainSession.Instance.RunId
                : string.Empty;
            string bootId = SceneWorkflowDiagnostics.ActiveBootId;

            s_LatestAuthoritySnapshot = AuthoritySnapshotBuilder.Build(runId, bootId);
            s_HasAuthoritySnapshot = true;

            s_LatestSceneSnapshot = SceneProjectionBuilder.Build(m_MaxSceneObjects, m_MaxSceneDistance);
            s_HasSceneSnapshot = true;

            PublishVariables(s_LatestAuthoritySnapshot, s_LatestSceneSnapshot);
            m_NextSnapshotAt = Time.realtimeSinceStartup + Mathf.Max(0.1f, m_SnapshotIntervalSeconds);
        }

        public bool TryGetAuthoritySnapshot(out AuthoritySnapshot snapshot)
        {
            return TryGetLatestAuthoritySnapshot(out snapshot);
        }

        public bool TryGetSceneSnapshot(out AuthoritativeSceneSnapshot snapshot)
        {
            return TryGetLatestSceneSnapshot(out snapshot);
        }

        public AuthoritativeSceneSnapshot BuildSceneSnapshot(int maxObjects, float maxDistance)
        {
            return SceneProjectionBuilder.Build(maxObjects, maxDistance);
        }

        public bool TryGetLatestInferenceEnvelope(out DialogueInferenceEnvelope envelope)
        {
            DialogueInferenceEnvelopeStore store = DialogueInferenceEnvelopeStore.Instance;
            if (store == null)
            {
                envelope = default;
                return false;
            }

            return store.TryGetLatest(out envelope);
        }

        public void RecordInferenceEnvelope(DialogueInferenceEnvelope envelope)
        {
            EnsureSupportComponents();
            DialogueInferenceEnvelopeStore store = DialogueInferenceEnvelopeStore.Instance;
            if (store == null)
            {
                return;
            }

            store.Record(envelope);
        }

        public bool TryGetLatestDialogueExecutionTrace(out DialogueExecutionTrace trace)
        {
            DialogueExecutionTraceStore store = DialogueExecutionTraceStore.Instance;
            if (store == null)
            {
                trace = default;
                return false;
            }

            return store.TryGetLatest(out trace);
        }

        public void RecordDialogueExecutionTrace(DialogueExecutionTrace trace)
        {
            EnsureSupportComponents();
            DialogueExecutionTraceStore store = DialogueExecutionTraceStore.Instance;
            if (store == null)
            {
                return;
            }

            store.Record(trace);
        }

        public DialogueExecutionTrace[] GetRecentDialogueExecutionTraces()
        {
            DialogueExecutionTraceStore store = DialogueExecutionTraceStore.Instance;
            return store == null ? Array.Empty<DialogueExecutionTrace>() : store.GetRecent();
        }

        public bool TryGetLatestDialogueActionValidationResult(out DialogueActionValidationResult result)
        {
            DialogueActionValidationStore store = DialogueActionValidationStore.Instance;
            if (store == null)
            {
                result = default;
                return false;
            }

            return store.TryGetLatest(out result);
        }

        public void RecordDialogueActionValidationResult(DialogueActionValidationResult result)
        {
            EnsureSupportComponents();
            DialogueActionValidationStore store = DialogueActionValidationStore.Instance;
            if (store == null)
            {
                return;
            }

            store.Record(result);
        }

        public DialogueActionValidationResult[] GetRecentDialogueActionValidationResults()
        {
            DialogueActionValidationStore store = DialogueActionValidationStore.Instance;
            return store == null
                ? Array.Empty<DialogueActionValidationResult>()
                : store.GetRecent();
        }

        public bool TryGetLatestDialogueReplicationTrace(out DialogueReplicationTrace trace)
        {
            DialogueReplicationTraceStore store = DialogueReplicationTraceStore.Instance;
            if (store == null)
            {
                trace = default;
                return false;
            }

            return store.TryGetLatest(out trace);
        }

        public void RecordDialogueReplicationTrace(DialogueReplicationTrace trace)
        {
            EnsureSupportComponents();
            DialogueReplicationTraceStore store = DialogueReplicationTraceStore.Instance;
            if (store == null)
            {
                return;
            }

            store.Record(trace);
        }

        public DialogueReplicationTrace[] GetRecentDialogueReplicationTraces()
        {
            DialogueReplicationTraceStore store = DialogueReplicationTraceStore.Instance;
            return store == null ? Array.Empty<DialogueReplicationTrace>() : store.GetRecent();
        }

        public bool TryGetLatestUiBehaviorSnapshot(string uiId, out UIBehaviorSnapshot snapshot)
        {
            UiDiagnosticsStore store = UiDiagnosticsStore.Instance;
            if (store == null)
            {
                snapshot = default;
                return false;
            }

            return store.TryGetLatestBehaviorSnapshot(uiId, out snapshot);
        }

        public void RecordUiBehaviorSnapshot(UIBehaviorSnapshot snapshot)
        {
            EnsureSupportComponents();
            UiDiagnosticsStore store = UiDiagnosticsStore.Instance;
            if (store == null)
            {
                return;
            }

            store.RecordBehaviorSnapshot(snapshot);
        }

        public bool TryGetLatestUiPerformanceSample(string uiId, out UIPerformanceSample sample)
        {
            UiDiagnosticsStore store = UiDiagnosticsStore.Instance;
            if (store == null)
            {
                sample = default;
                return false;
            }

            return store.TryGetLatestPerformanceSample(uiId, out sample);
        }

        public void RecordUiPerformanceSample(UIPerformanceSample sample)
        {
            EnsureSupportComponents();
            UiDiagnosticsStore store = UiDiagnosticsStore.Instance;
            if (store == null)
            {
                return;
            }

            store.RecordPerformanceSample(sample);
        }

        public bool TryGetDiagnosticBrainPacket(out DiagnosticBrainPacket packet)
        {
            DiagnosticBrainSession session = DiagnosticBrainSession.Instance;
            if (session == null)
            {
                packet = default;
                return false;
            }

            packet = session.BuildPacket();
            return true;
        }

        public string BuildDiagnosticBrainPrompt()
        {
            DiagnosticBrainSession session = DiagnosticBrainSession.Instance;
            return session == null ? string.Empty : DiagnosticPromptComposer.Compose(session.BuildPacket());
        }

        public void UpsertBrainVariable(DiagnosticBrainVariable variable)
        {
            DiagnosticBrainSession.Instance?.UpsertVariable(variable);
        }

        private void EnsureSupportComponents()
        {
            if (DiagnosticBrainSession.Instance == null)
            {
                GetOrAddComponent<DiagnosticBrainSession>(gameObject);
            }

            if (DialogueInferenceEnvelopeStore.Instance == null)
            {
                GetOrAddComponent<DialogueInferenceEnvelopeStore>(gameObject);
            }

            if (DialogueExecutionTraceStore.Instance == null)
            {
                GetOrAddComponent<DialogueExecutionTraceStore>(gameObject);
            }

            if (DialogueActionValidationStore.Instance == null)
            {
                GetOrAddComponent<DialogueActionValidationStore>(gameObject);
            }

            if (DialogueReplicationTraceStore.Instance == null)
            {
                GetOrAddComponent<DialogueReplicationTraceStore>(gameObject);
            }

            if (UiDiagnosticsStore.Instance == null)
            {
                GetOrAddComponent<UiDiagnosticsStore>(gameObject);
            }
        }

        private void PublishVariables(
            AuthoritySnapshot authority,
            AuthoritativeSceneSnapshot sceneSnapshot
        )
        {
            DiagnosticBrainSession session = DiagnosticBrainSession.Instance;
            if (session == null)
            {
                return;
            }

            float now = Time.realtimeSinceStartup;
            session.UpsertVariable(
                new DiagnosticBrainVariable
                {
                    Key = "fact.authority.summary",
                    Kind = DiagnosticBrainVariableKind.Fact,
                    Severity = DiagnosticBrainSeverity.P2,
                    Phase = authority.CurrentPhase ?? string.Empty,
                    Value = authority.Summary ?? string.Empty,
                    Confidence = 1f,
                    Source = nameof(DiagnosticBrainRuntime),
                    CreatedAt = now,
                }
            );

            PublishWorkflowMilestoneFacts(session, authority, now);

            session.UpsertVariable(
                new DiagnosticBrainVariable
                {
                    Key = "fact.scene.snapshot",
                    Kind = DiagnosticBrainVariableKind.Fact,
                    Severity = DiagnosticBrainSeverity.P2,
                    Phase = authority.CurrentPhase ?? string.Empty,
                    Value = $"snapshot={sceneSnapshot.SnapshotId} objects={(sceneSnapshot.Objects != null ? sceneSnapshot.Objects.Length : 0)}",
                    Confidence = 1f,
                    Source = nameof(DiagnosticBrainRuntime),
                    CreatedAt = now,
                }
            );

            if (TryGetLatestUiBehaviorSnapshot(string.Empty, out UIBehaviorSnapshot uiSnapshot))
            {
                session.UpsertVariable(
                    new DiagnosticBrainVariable
                    {
                        Key = "fact.ui.summary",
                        Kind = DiagnosticBrainVariableKind.Fact,
                        Severity = DiagnosticBrainSeverity.P2,
                        Phase = authority.CurrentPhase ?? string.Empty,
                        Value = uiSnapshot.Summary ?? string.Empty,
                        Confidence = 0.95f,
                        Source = nameof(DiagnosticBrainRuntime),
                        CreatedAt = now,
                    }
                );
            }

            if (TryGetLatestDialogueExecutionTrace(out DialogueExecutionTrace executionTrace))
            {
                session.UpsertVariable(
                    new DiagnosticBrainVariable
                    {
                        Key = "fact.dialogue.execution",
                        Kind = DiagnosticBrainVariableKind.Fact,
                        Severity = executionTrace.Success
                            ? DiagnosticBrainSeverity.P2
                            : DiagnosticBrainSeverity.P1,
                        Phase = authority.CurrentPhase ?? string.Empty,
                        Value = executionTrace.Summary ?? string.Empty,
                        Confidence = 0.9f,
                        Source = nameof(DiagnosticBrainRuntime),
                        CreatedAt = now,
                    }
                );
            }

            if (TryGetLatestDialogueActionValidationResult(out DialogueActionValidationResult actionValidation))
            {
                session.UpsertVariable(
                    new DiagnosticBrainVariable
                    {
                        Key = "fact.dialogue.action_validation",
                        Kind = DiagnosticBrainVariableKind.Fact,
                        Severity = actionValidation.Success
                            ? DiagnosticBrainSeverity.P2
                            : DiagnosticBrainSeverity.P1,
                        Phase = authority.CurrentPhase ?? string.Empty,
                        Value = actionValidation.Summary ?? string.Empty,
                        Confidence = 0.9f,
                        Source = nameof(DiagnosticBrainRuntime),
                        CreatedAt = now,
                    }
                );
            }

            if (TryGetLatestDialogueReplicationTrace(out DialogueReplicationTrace replicationTrace))
            {
                session.UpsertVariable(
                    new DiagnosticBrainVariable
                    {
                        Key = "fact.dialogue.replication",
                        Kind = DiagnosticBrainVariableKind.Fact,
                        Severity = replicationTrace.Success
                            ? DiagnosticBrainSeverity.P2
                            : DiagnosticBrainSeverity.P1,
                        Phase = authority.CurrentPhase ?? string.Empty,
                        Value = replicationTrace.Summary ?? string.Empty,
                        Confidence = 0.9f,
                        Source = nameof(DiagnosticBrainRuntime),
                        CreatedAt = now,
                    }
                );
            }

            DiagnosticActionChainSummary[] recentActionChains = DiagnosticActionChainSummarizer.BuildRecentSummaries(
                GetRecentDialogueActionValidationResults(),
                GetRecentDialogueExecutionTraces(),
                GetRecentDialogueReplicationTraces(),
                5
            );
            if (recentActionChains.Length > 0)
            {
                session.UpsertVariable(
                    new DiagnosticBrainVariable
                    {
                        Key = "fact.dialogue.action_chains",
                        Kind = DiagnosticBrainVariableKind.Fact,
                        Severity = recentActionChains.Any(summary => summary.HasFailure)
                            ? DiagnosticBrainSeverity.P1
                            : DiagnosticBrainSeverity.P2,
                        Phase = authority.CurrentPhase ?? string.Empty,
                        Value = BuildRecentActionChainsValue(recentActionChains),
                        Confidence = 0.9f,
                        Source = nameof(DiagnosticBrainRuntime),
                        CreatedAt = now,
                    }
                );
            }

            if (TryGetLatestUiPerformanceSample(string.Empty, out UIPerformanceSample uiPerformance))
            {
                session.UpsertVariable(
                    new DiagnosticBrainVariable
                    {
                        Key = "fact.ui.performance",
                        Kind = DiagnosticBrainVariableKind.Fact,
                        Severity = uiPerformance.DurationMs >= 8f
                            ? DiagnosticBrainSeverity.P1
                            : DiagnosticBrainSeverity.P2,
                        Phase = authority.CurrentPhase ?? string.Empty,
                        Value = string.Format(
                            "{0} sample={1} durationMs={2:0.00} elements={3} text={4} transcriptChars={5}",
                            string.IsNullOrWhiteSpace(uiPerformance.UiKind)
                                ? "ui"
                                : uiPerformance.UiKind,
                            string.IsNullOrWhiteSpace(uiPerformance.SampleName)
                                ? "sample"
                                : uiPerformance.SampleName,
                            uiPerformance.DurationMs,
                            uiPerformance.VisibleElementCount,
                            uiPerformance.TextElementCount,
                            uiPerformance.TranscriptCharacterCount
                        ),
                        Confidence = 0.9f,
                        Source = nameof(DiagnosticBrainRuntime),
                        CreatedAt = now,
                    }
                );
            }

            ClearAuthorityFocuses(session);
            ClearDialogueFocuses(session);
            ClearUiFocuses(session);
            ClearSuppressions(session);

            if (
                TryGetLatestDialogueExecutionTrace(out DialogueExecutionTrace latestExecutionTrace)
                && (!latestExecutionTrace.Success || !string.IsNullOrWhiteSpace(latestExecutionTrace.Error))
            )
            {
                session.UpsertVariable(
                    new DiagnosticBrainVariable
                    {
                        Key = "focus.dialogue_execution_failure",
                        Kind = DiagnosticBrainVariableKind.Focus,
                        Severity = DiagnosticBrainSeverity.P1,
                        Phase = authority.CurrentPhase ?? string.Empty,
                        Value = string.IsNullOrWhiteSpace(latestExecutionTrace.Summary)
                            ? "Dialogue execution trace reported a failure."
                            : latestExecutionTrace.Summary,
                        Confidence = 0.8f,
                        Source = nameof(DiagnosticBrainRuntime),
                        CreatedAt = now,
                        ExpiresAt = now + Mathf.Max(1f, k_DefaultExpirationMultiplier),
                    }
                );
            }

            if (
                TryGetLatestDialogueActionValidationResult(out DialogueActionValidationResult latestActionValidation)
                && (!latestActionValidation.Success || string.Equals(latestActionValidation.Decision, "rejected", StringComparison.OrdinalIgnoreCase))
            )
            {
                session.UpsertVariable(
                    new DiagnosticBrainVariable
                    {
                        Key = "focus.dialogue_action_rejected",
                        Kind = DiagnosticBrainVariableKind.Focus,
                        Severity = DiagnosticBrainSeverity.P1,
                        Phase = authority.CurrentPhase ?? string.Empty,
                        Value = string.IsNullOrWhiteSpace(latestActionValidation.Summary)
                            ? "Dialogue action validation rejected the latest action."
                            : latestActionValidation.Summary,
                        Confidence = 0.85f,
                        Source = nameof(DiagnosticBrainRuntime),
                        CreatedAt = now,
                        ExpiresAt = now + Mathf.Max(1f, k_DefaultExpirationMultiplier),
                    }
                );
            }

            if (
                TryGetLatestDialogueReplicationTrace(out DialogueReplicationTrace latestReplicationTrace)
                && (!latestReplicationTrace.Success || string.Equals(latestReplicationTrace.Stage, "rpc_failed", StringComparison.OrdinalIgnoreCase))
            )
            {
                session.UpsertVariable(
                    new DiagnosticBrainVariable
                    {
                        Key = "focus.dialogue_replication_failure",
                        Kind = DiagnosticBrainVariableKind.Focus,
                        Severity = DiagnosticBrainSeverity.P1,
                        Phase = authority.CurrentPhase ?? string.Empty,
                        Value = string.IsNullOrWhiteSpace(latestReplicationTrace.Summary)
                            ? "Dialogue replication trace reported a failure."
                            : latestReplicationTrace.Summary,
                        Confidence = 0.85f,
                        Source = nameof(DiagnosticBrainRuntime),
                        CreatedAt = now,
                        ExpiresAt = now + Mathf.Max(1f, k_DefaultExpirationMultiplier),
                    }
                );
            }

            ApplyActionChainHeuristics(session, authority.CurrentPhase ?? string.Empty, recentActionChains, now);

            PublishDialogueBackendUnready(session, authority, now);

            if (
                TryGetLatestUiBehaviorSnapshot(string.Empty, out UIBehaviorSnapshot latestUiSnapshot)
                && latestUiSnapshot.IsVisible
                && latestUiSnapshot.GameplayInputSuppressed
                && !latestUiSnapshot.ConversationReady
            )
            {
                session.UpsertVariable(
                    new DiagnosticBrainVariable
                    {
                        Key = "focus.ui_input_capture_before_ready",
                        Kind = DiagnosticBrainVariableKind.Focus,
                        Severity = DiagnosticBrainSeverity.P1,
                        Phase = authority.CurrentPhase ?? string.Empty,
                        Value = string.Format(
                            "{0} is visible and suppressing gameplay input before conversation readiness.",
                            string.IsNullOrWhiteSpace(latestUiSnapshot.UiKind)
                                ? "UI"
                                : latestUiSnapshot.UiKind
                        ),
                        Confidence = 0.85f,
                        Source = nameof(DiagnosticBrainRuntime),
                        CreatedAt = now,
                        ExpiresAt = now + Mathf.Max(1f, k_DefaultExpirationMultiplier),
                    }
                );
            }

            if (
                TryGetLatestUiPerformanceSample(string.Empty, out UIPerformanceSample latestUiPerf)
                && latestUiPerf.DurationMs >= 8f
            )
            {
                session.UpsertVariable(
                    new DiagnosticBrainVariable
                    {
                        Key = "focus.ui_render_slow",
                        Kind = DiagnosticBrainVariableKind.Focus,
                        Severity = DiagnosticBrainSeverity.P2,
                        Phase = authority.CurrentPhase ?? string.Empty,
                        Value = string.Format(
                            "{0} {1} took {2:0.00} ms with {3} visible elements and {4} transcript characters.",
                            string.IsNullOrWhiteSpace(latestUiPerf.UiKind)
                                ? "UI"
                                : latestUiPerf.UiKind,
                            string.IsNullOrWhiteSpace(latestUiPerf.SampleName)
                                ? "sample"
                                : latestUiPerf.SampleName,
                            latestUiPerf.DurationMs,
                            latestUiPerf.VisibleElementCount,
                            latestUiPerf.TranscriptCharacterCount
                        ),
                        Confidence = 0.8f,
                        Source = nameof(DiagnosticBrainRuntime),
                        CreatedAt = now,
                        ExpiresAt = now + Mathf.Max(1f, k_DefaultExpirationMultiplier),
                    }
                );
            }

            PublishTransportNoiseSuppression(session, authority, now);
        }

        private static void PublishTransportNoiseSuppression(
            DiagnosticBrainSession session,
            AuthoritySnapshot authority,
            float now
        )
        {
            if (!ShouldSuppressTransportNoise(authority))
            {
                session.RemoveVariable("suppress.transport_bind_noise");
                return;
            }

            int warningCount = s_RecentTransportWarnings.Count;
            session.UpsertVariable(
                new DiagnosticBrainVariable
                {
                    Key = "suppress.transport_bind_noise",
                    Kind = DiagnosticBrainVariableKind.Suppression,
                    Severity = DiagnosticBrainSeverity.P3,
                    Phase = authority.CurrentPhase ?? string.Empty,
                    Value = $"Transport warnings detected ({warningCount} in last {k_TransportWarningWindowSeconds:F0}s). In host mode, bind/connection warnings are often noise and can be safely suppressed.",
                    Confidence = 0.7f,
                    Source = nameof(DiagnosticBrainRuntime),
                    CreatedAt = now,
                    ExpiresAt = now + k_TransportWarningWindowSeconds,
                }
            );
        }

        private static void PublishDialogueBackendUnready(
            DiagnosticBrainSession session,
            AuthoritySnapshot authority,
            float now
        )
        {
            NetworkDialogueService dialogueService = NetworkDialogueService.Instance;
            if (dialogueService == null)
            {
                return;
            }

            bool isWarmupDegraded = dialogueService.IsWarmupDegraded;
            int warmupFailureCount = dialogueService.WarmupFailureCount;
            bool isLLMReady = dialogueService.IsLLMReady;
            bool hasDialogueBackendConfig = dialogueService.HasDialogueBackendConfig;

            bool isBackendUnready = false;
            string reason = string.Empty;

            if (isWarmupDegraded && warmupFailureCount > 2)
            {
                isBackendUnready = true;
                reason = $"Warmup degraded with {warmupFailureCount} failures.";
            }
            else if (!isLLMReady && hasDialogueBackendConfig)
            {
                isBackendUnready = true;
                reason = "LLM not ready but backend config exists.";
            }
            else if (!hasDialogueBackendConfig && isLLMReady)
            {
                isBackendUnready = true;
                reason = "Backend config missing.";
            }
            else if (dialogueService.IsWarmupDegraded && !isLLMReady)
            {
                isBackendUnready = true;
                reason = $"Warmup degraded, LLM not ready ({warmupFailureCount} failures).";
            }

            if (!isBackendUnready)
            {
                return;
            }

            session.UpsertVariable(
                new DiagnosticBrainVariable
                {
                    Key = "focus.dialogue_backend_unready",
                    Kind = DiagnosticBrainVariableKind.Focus,
                    Severity = DiagnosticBrainSeverity.P1,
                    Phase = authority.CurrentPhase ?? string.Empty,
                    Value = $"Dialogue backend unready. {reason} isLLMReady={isLLMReady} hasBackendConfig={hasDialogueBackendConfig} warmupDegraded={isWarmupDegraded} warmupFailures={warmupFailureCount}",
                    Confidence = 0.85f,
                    Source = nameof(DiagnosticBrainRuntime),
                    CreatedAt = now,
                    ExpiresAt = now + Mathf.Max(1f, k_DefaultExpirationMultiplier),
                }
            );
        }

        private static void PublishWorkflowMilestoneFacts(
            DiagnosticBrainSession session,
            AuthoritySnapshot authority,
            float now
        )
        {
            IReadOnlyDictionary<string, float> completedMilestones = SceneWorkflowDiagnostics.GetCompletedMilestones();
            int totalMilestones = SceneWorkflowDiagnostics.CompletedMilestoneCount;
            string firstMissing = SceneWorkflowDiagnostics.GetFirstUncompletedMilestone();
            bool startupComplete = SceneWorkflowDiagnostics.StartupCompleted;

            session.UpsertVariable(
                new DiagnosticBrainVariable
                {
                    Key = "fact.workflow.progress",
                    Kind = DiagnosticBrainVariableKind.Fact,
                    Severity = startupComplete ? DiagnosticBrainSeverity.P3 : DiagnosticBrainSeverity.P2,
                    Phase = authority.CurrentPhase ?? string.Empty,
                    Value = $"completed={totalMilestones} startupComplete={startupComplete} firstMissing={firstMissing ?? "none"}",
                    Confidence = 1f,
                    Source = nameof(DiagnosticBrainRuntime),
                    CreatedAt = now,
                }
            );

            if (firstMissing != null)
            {
                session.UpsertVariable(
                    new DiagnosticBrainVariable
                    {
                        Key = "focus.workflow.milestone_blocked",
                        Kind = DiagnosticBrainVariableKind.Focus,
                        Severity = DiagnosticBrainSeverity.P1,
                        Phase = authority.CurrentPhase ?? string.Empty,
                        Value = $"Workflow blocked at milestone '{firstMissing}'. {totalMilestones} of {SceneWorkflowDiagnostics.TotalMilestoneCount} milestones completed.",
                        Confidence = 0.9f,
                        Source = nameof(DiagnosticBrainRuntime),
                        CreatedAt = now,
                        ExpiresAt = now + Mathf.Max(1f, k_DefaultExpirationMultiplier),
                    }
                );
            }
        }

        private static void ClearAuthorityFocuses(DiagnosticBrainSession session)
        {
            session.RemoveVariable("focus.network_manager_missing");
            session.RemoveVariable("focus.network_not_listening");
            session.RemoveVariable("focus.auth_identity_missing");
            session.RemoveVariable("focus.local_player_missing");
            session.RemoveVariable("focus.local_player_not_owner");
            session.RemoveVariable("focus.local_input_disabled");
            session.RemoveVariable("focus.prompt_context_not_applied");
        }

        private static void ClearDialogueFocuses(DiagnosticBrainSession session)
        {
            session.RemoveVariable("focus.dialogue_execution_failure");
            session.RemoveVariable("focus.dialogue_action_rejected");
            session.RemoveVariable("focus.dialogue_replication_failure");
            session.RemoveVariable("focus.dialogue_action_not_visible");
            session.RemoveVariable("focus.dialogue_action_stuck_rpc_sent");
            session.RemoveVariable("focus.dialogue_action_repeated_stage_failure");
            session.RemoveVariable("focus.dialogue_backend_unready");
        }

        private static void ClearUiFocuses(DiagnosticBrainSession session)
        {
            session.RemoveVariable("focus.ui_input_capture_before_ready");
            session.RemoveVariable("focus.ui_render_slow");
        }

        private static void ClearSuppressions(DiagnosticBrainSession session)
        {
            session.RemoveVariable("suppress.transport_bind_noise");
        }

        private static DiagnosticBrainSeverity ResolveSeverity(string blocker)
        {
            switch (blocker)
            {
                case "network_manager_missing":
                case "network_not_listening":
                case "auth_identity_missing":
                case "local_player_missing":
                case "local_player_not_owner":
                    return DiagnosticBrainSeverity.P0;
                case "local_input_disabled":
                case "prompt_context_not_applied":
                    return DiagnosticBrainSeverity.P1;
                default:
                    return DiagnosticBrainSeverity.P2;
            }
        }

        private static string BuildAuthorityFocusValue(string blocker, AuthoritySnapshot authority)
        {
            switch (blocker)
            {
                case "network_manager_missing":
                    return "NetworkManager is missing, so NGO state and player authority cannot be established.";
                case "network_not_listening":
                    return "NetworkManager exists but is not listening, so spawn and ownership state are not valid yet.";
                case "auth_identity_missing":
                    return "No authenticated local player is available. Fix auth before chasing dialogue or UI errors.";
                case "local_player_missing":
                    return "The local player object is not resolved. Check spawn, ownership, and player bootstrap first.";
                case "local_player_not_owner":
                    return $"Local player {authority.LocalPlayerObjectName} is spawned but owned by client {authority.LocalPlayerOwnerClientId}.";
                case "local_input_disabled":
                    return $"Local player {authority.LocalPlayerObjectName} is owned but input is disabled on action map {authority.LocalActionMap}.";
                case "prompt_context_not_applied":
                    return $"Auth prompt context for {authority.AuthNameId} is initialized but not bound to dialogue for player {authority.LocalPlayerNetworkObjectId}.";
                default:
                    return authority.Summary ?? blocker;
            }
        }

        private void ApplyActionChainHeuristics(
            DiagnosticBrainSession session,
            string phase,
            DiagnosticActionChainSummary[] recentActionChains,
            float now
        )
        {
            if (recentActionChains == null || recentActionChains.Length == 0)
            {
                return;
            }

            DiagnosticActionChainSummary latest = recentActionChains[0];
            if (
                latest.ValidationCount > 0
                && !latest.HasClientVisible
                && !latest.HasFailure
                && string.Equals(latest.LatestValidationDecision, "validated", StringComparison.OrdinalIgnoreCase)
            )
            {
                session.UpsertVariable(
                    new DiagnosticBrainVariable
                    {
                        Key = "focus.dialogue_action_not_visible",
                        Kind = DiagnosticBrainVariableKind.Focus,
                        Severity = DiagnosticBrainSeverity.P1,
                        Phase = phase,
                        Value = string.Format(
                            "Action {0} validated but has not reached a client-visible result yet.",
                            string.IsNullOrWhiteSpace(latest.ActionId) ? "<unknown>" : latest.ActionId
                        ),
                        Confidence = 0.82f,
                        Source = nameof(DiagnosticBrainRuntime),
                        CreatedAt = now,
                        ExpiresAt = now + Mathf.Max(1f, k_DefaultExpirationMultiplier),
                    }
                );
            }

            if (
                string.Equals(latest.LatestReplicationStage, "rpc_sent", StringComparison.OrdinalIgnoreCase)
                && !latest.HasClientVisible
            )
            {
                session.UpsertVariable(
                    new DiagnosticBrainVariable
                    {
                        Key = "focus.dialogue_action_stuck_rpc_sent",
                        Kind = DiagnosticBrainVariableKind.Focus,
                        Severity = DiagnosticBrainSeverity.P1,
                        Phase = phase,
                        Value = string.Format(
                            "Action {0} reached rpc_sent but has not been observed at rpc_received or client_visible.",
                            string.IsNullOrWhiteSpace(latest.ActionId) ? "<unknown>" : latest.ActionId
                        ),
                        Confidence = 0.88f,
                        Source = nameof(DiagnosticBrainRuntime),
                        CreatedAt = now,
                        ExpiresAt = now + Mathf.Max(1f, k_DefaultExpirationMultiplier),
                    }
                );
            }

            string repeatedFailureStage = recentActionChains
                .Where(summary => summary.HasFailure)
                .GroupBy(summary => ResolveActionChainFailureStage(summary), StringComparer.OrdinalIgnoreCase)
                .Where(group => !string.IsNullOrWhiteSpace(group.Key) && group.Count() >= 2)
                .OrderByDescending(group => group.Count())
                .Select(group => group.Key)
                .FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(repeatedFailureStage))
            {
                int repeatedCount = recentActionChains.Count(summary =>
                    summary.HasFailure
                    && string.Equals(
                        ResolveActionChainFailureStage(summary),
                        repeatedFailureStage,
                        StringComparison.OrdinalIgnoreCase
                    )
                );
                session.UpsertVariable(
                    new DiagnosticBrainVariable
                    {
                        Key = "focus.dialogue_action_repeated_stage_failure",
                        Kind = DiagnosticBrainVariableKind.Focus,
                        Severity = DiagnosticBrainSeverity.P1,
                        Phase = phase,
                        Value = string.Format(
                            "{0} recent dialogue actions failed repeatedly at stage {1}.",
                            repeatedCount,
                            repeatedFailureStage
                        ),
                        Confidence = 0.86f,
                        Source = nameof(DiagnosticBrainRuntime),
                        CreatedAt = now,
                        ExpiresAt = now + Mathf.Max(1f, k_DefaultExpirationMultiplier),
                    }
                );
            }
        }

        private static string BuildRecentActionChainsValue(DiagnosticActionChainSummary[] summaries)
        {
            if (summaries == null || summaries.Length == 0)
            {
                return string.Empty;
            }

            int count = Mathf.Min(3, summaries.Length);
            string[] parts = new string[count];
            for (int i = 0; i < count; i++)
            {
                DiagnosticActionChainSummary summary = summaries[i];
                parts[i] = string.Format(
                    "{0}:{1}:visible={2}:failure={3}",
                    string.IsNullOrWhiteSpace(summary.ActionId) ? "action" : summary.ActionId,
                    string.IsNullOrWhiteSpace(summary.LatestStage) ? "none" : summary.LatestStage,
                    summary.HasClientVisible ? "yes" : "no",
                    summary.HasFailure ? "yes" : "no"
                );
            }

            return string.Join(" | ", parts);
        }

        private static string ResolveActionChainFailureStage(DiagnosticActionChainSummary summary)
        {
            if (!string.IsNullOrWhiteSpace(summary.LatestReplicationStage) && summary.LatestReplicationStage != "none")
            {
                return summary.LatestReplicationStage;
            }

            if (!string.IsNullOrWhiteSpace(summary.LatestExecutionStage) && summary.LatestExecutionStage != "none")
            {
                return summary.LatestExecutionStage;
            }

            if (!string.IsNullOrWhiteSpace(summary.LatestValidationDecision) && summary.LatestValidationDecision != "none")
            {
                return summary.LatestValidationDecision;
            }

            return summary.LatestStage;
        }

        private static T GetOrAddComponent<T>(GameObject host)
            where T : Component
        {
            T component = host.GetComponent<T>();
            if (component == null)
            {
                component = host.AddComponent<T>();
            }

            return component;
        }

        private struct TransportWarningEntry
        {
            public float Timestamp;
            public string Message;
        }

        public static void RecordTransportWarning(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            s_RecentTransportWarnings.Add(new TransportWarningEntry
            {
                Timestamp = Time.realtimeSinceStartup,
                Message = message,
            });

            PruneTransportWarnings();
        }

        private static void PruneTransportWarnings()
        {
            float cutoff = Time.realtimeSinceStartup - k_TransportWarningWindowSeconds;
            s_RecentTransportWarnings.RemoveAll(entry => entry.Timestamp < cutoff);
        }

        private static bool DetectTransportBindNoise()
        {
            PruneTransportWarnings();
            if (s_RecentTransportWarnings.Count == 0)
            {
                return false;
            }

            foreach (TransportWarningEntry entry in s_RecentTransportWarnings)
            {
                string msg = entry.Message ?? string.Empty;
                if (msg.Contains("bind", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("address", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("port", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("connection", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ShouldSuppressTransportNoise(AuthoritySnapshot authority)
        {
            return authority.IsHost && DetectTransportBindNoise();
        }
    }
}
