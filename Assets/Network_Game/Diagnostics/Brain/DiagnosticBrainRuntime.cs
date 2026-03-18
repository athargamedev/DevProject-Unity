using System;
using UnityEngine;

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
                        ExpiresAt = now + Mathf.Max(1f, m_SnapshotIntervalSeconds * 4f),
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
                        ExpiresAt = now + Mathf.Max(1f, m_SnapshotIntervalSeconds * 4f),
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
                        ExpiresAt = now + Mathf.Max(1f, m_SnapshotIntervalSeconds * 4f),
                    }
                );
            }

            string blocker = authority.ResolvePrimaryAuthorityProblem();
            if (string.IsNullOrWhiteSpace(blocker))
            {
                return;
            }

            session.UpsertVariable(
                new DiagnosticBrainVariable
                {
                    Key = $"focus.{blocker}",
                    Kind = DiagnosticBrainVariableKind.Focus,
                    Severity = ResolveSeverity(blocker),
                    Phase = authority.CurrentPhase ?? string.Empty,
                    Value = BuildAuthorityFocusValue(blocker, authority),
                    Confidence = 0.95f,
                    Source = nameof(DiagnosticBrainRuntime),
                    CreatedAt = now,
                    ExpiresAt = now + Mathf.Max(1f, m_SnapshotIntervalSeconds * 4f),
                }
            );

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
                        ExpiresAt = now + Mathf.Max(1f, m_SnapshotIntervalSeconds * 4f),
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
                        ExpiresAt = now + Mathf.Max(1f, m_SnapshotIntervalSeconds * 4f),
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
        }

        private static void ClearUiFocuses(DiagnosticBrainSession session)
        {
            session.RemoveVariable("focus.ui_input_capture_before_ready");
            session.RemoveVariable("focus.ui_render_slow");
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
    }
}
