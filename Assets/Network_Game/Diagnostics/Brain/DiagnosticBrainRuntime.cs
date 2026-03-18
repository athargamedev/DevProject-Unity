using System;
using UnityEngine;

namespace Network_Game.Diagnostics
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-440)]
    public sealed class DiagnosticBrainRuntime : MonoBehaviour
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

            ClearAuthorityFocuses(session);

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
