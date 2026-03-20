using System;
using System.Collections;
using System.Collections.Generic;
using Network_Game.Auth;
using Unity.Netcode;
using UnityEngine;
using NGLogLevel = Network_Game.Diagnostics.LogLevel;

namespace Network_Game.Diagnostics
{
    /// <summary>
    /// Tracks ordered startup milestones for the active scene and emits a concise summary.
    /// </summary>
    [DefaultExecutionOrder(120)]
    public class SceneWorkflowDiagnostics : MonoBehaviour, ISceneWorkflowStateBridge
    {
        private const string Category = "SceneWorkflow";

        private static readonly string[] s_OrderedMilestones =
        {
            "scene_bootstrap_ready",
            "network_mode_known",
            "network_ready",
            "auth_identity_ready",
            "auth_gate_passed",
            "local_player_spawned",
            "local_player_ready",
            "runtime_bind_core_complete",
            "runtime_bind_auth_complete",
        };

        private static SceneWorkflowDiagnostics s_Instance;

        [Header("Startup Diagnostics")]
        [SerializeField]
        [Min(5f)]
        private float m_StartupTimeoutSeconds = 30f;

        [SerializeField]
        [Min(0.25f)]
        private float m_PollIntervalSeconds = 0.5f;

        private readonly Dictionary<string, float> m_CompletedMilestones =
            new Dictionary<string, float>(StringComparer.Ordinal);

        private string m_BootId;
        private float m_BootStartTime;
        private Coroutine m_TimeoutRoutine;
        private bool m_SummaryLogged;
        private bool m_HasPendingAuthIdentity;
        private string m_PendingAuthIdentityNameId = string.Empty;
        private GameObject m_PendingLocalPlayerSpawned;
        private GameObject m_PendingLocalPlayerReady;
        private INetworkBootstrapEventsBridge m_EventsBridge;

        public static string ActiveBootId => s_Instance != null ? s_Instance.m_BootId : string.Empty;
        public static bool StartupCompleted => s_Instance != null && s_Instance.HasCompletedAllMilestones();

        public static bool IsMilestoneComplete(string milestone)
        {
            return s_Instance != null && s_Instance.m_CompletedMilestones.ContainsKey(milestone);
        }

        public static IReadOnlyDictionary<string, float> GetCompletedMilestones()
        {
            return s_Instance != null
                ? s_Instance.m_CompletedMilestones
                : new Dictionary<string, float>(StringComparer.Ordinal);
        }

        public static int CompletedMilestoneCount =>
            s_Instance != null ? s_Instance.m_CompletedMilestones.Count : 0;

        public static int TotalMilestoneCount => s_OrderedMilestones.Length;

        public static string GetFirstUncompletedMilestone()
        {
            return s_Instance != null ? s_Instance.GetFirstMissingMilestone() : s_OrderedMilestones[0];
        }

        public static void ReportMilestone(
            string milestone,
            UnityEngine.Object context = null,
            params (string key, object value)[] data
        )
        {
            s_Instance?.RecordMilestone(milestone, context, data);
        }

        private void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                NGLog.Warn(
                    Category,
                    NGLog.Format(
                        "Duplicate scene workflow diagnostics destroyed",
                        ("object", gameObject.name)
                    ),
                    this
                );
                Destroy(this);
                return;
            }

            s_Instance = this;
            SceneWorkflowStateBridgeRegistry.Register(this);
            m_BootStartTime = Time.realtimeSinceStartup;
            m_BootId = Guid.NewGuid().ToString("N");

            NGLog.Lifecycle(Category, "awake", CreateTraceContext("scene_compose"), this);
        }

        private void OnEnable()
        {
            SubscribeEvents();
            CatchUpCurrentState();

            if (m_TimeoutRoutine != null)
            {
                StopCoroutine(m_TimeoutRoutine);
            }

            m_TimeoutRoutine = StartCoroutine(MonitorStartupTimeout());
        }

        private void OnDisable()
        {
            UnsubscribeEvents();

            if (m_TimeoutRoutine != null)
            {
                StopCoroutine(m_TimeoutRoutine);
                m_TimeoutRoutine = null;
            }
        }

        private void OnDestroy()
        {
            if (s_Instance == this)
            {
                s_Instance = null;
            }

            SceneWorkflowStateBridgeRegistry.Unregister(this);
        }

        private void SubscribeEvents()
        {
            INetworkBootstrapEventsBridge eventsBridge = NetworkBootstrapEventsBridgeRegistry.Current;
            if (eventsBridge != null && !ReferenceEquals(m_EventsBridge, eventsBridge))
            {
                UnsubscribeBootstrapEvents();
                m_EventsBridge = eventsBridge;
                m_EventsBridge.OnClientModeDetermined += HandleClientModeDetermined;
                m_EventsBridge.OnNetworkReady += HandleNetworkReady;
                m_EventsBridge.OnAuthGatePassed += HandleAuthGatePassed;
                m_EventsBridge.OnLocalPlayerSpawned += HandleLocalPlayerSpawned;
                m_EventsBridge.OnLocalPlayerReady += HandleLocalPlayerReady;

                NGLog.Subscribe(Category, "bootstrap_events", CreateTraceContext("scene_compose"), this);
            }

            LocalPlayerAuthService.OnPlayerLoggedIn += HandlePlayerLoggedIn;
        }

        private void UnsubscribeEvents()
        {
            UnsubscribeBootstrapEvents();

            LocalPlayerAuthService.OnPlayerLoggedIn -= HandlePlayerLoggedIn;
        }

        private void UnsubscribeBootstrapEvents()
        {
            if (m_EventsBridge == null)
            {
                return;
            }

            m_EventsBridge.OnClientModeDetermined -= HandleClientModeDetermined;
            m_EventsBridge.OnNetworkReady -= HandleNetworkReady;
            m_EventsBridge.OnAuthGatePassed -= HandleAuthGatePassed;
            m_EventsBridge.OnLocalPlayerSpawned -= HandleLocalPlayerSpawned;
            m_EventsBridge.OnLocalPlayerReady -= HandleLocalPlayerReady;
            m_EventsBridge = null;
        }

        private void CatchUpCurrentState()
        {
            LocalPlayerAuthService authService = LocalPlayerAuthService.Instance;
            if (authService != null && authService.HasCurrentPlayer)
            {
                QueueAuthIdentityMilestone(authService.CurrentPlayer.NameId);
            }

            NetworkManager manager = NetworkManager.Singleton;
            if (manager != null && manager.IsListening)
            {
                RecordMilestone("network_ready", manager, ("listening", true));
                FlushPendingAuthIdentity(authService);
            }
        }

        private IEnumerator MonitorStartupTimeout()
        {
            while (!m_SummaryLogged)
            {
                if (HasCompletedAllMilestones())
                {
                    EmitSuccessSummary();
                    yield break;
                }

                float elapsed = Time.realtimeSinceStartup - m_BootStartTime;
                if (elapsed >= m_StartupTimeoutSeconds)
                {
                    // One final catch-up before declaring failure — catches network_ready if the
                    // event fired before we subscribed (e.g. domain-reload race).
                    CatchUpCurrentState();
                    if (HasCompletedAllMilestones())
                    {
                        EmitSuccessSummary();
                        yield break;
                    }
                    EmitFailureSummary(elapsed);
                    yield break;
                }

                yield return new WaitForSecondsRealtime(m_PollIntervalSeconds);
            }
        }

        private void HandleClientModeDetermined(bool isClient)
        {
            RecordMilestone("network_mode_known", this, ("isClient", (object)isClient));
        }

        private void HandleNetworkReady(NetworkManager manager)
        {
            RecordMilestone(
                "network_ready",
                manager != null ? manager : this,
                ("manager", (object)(manager != null ? manager.name : "null"))
            );
            FlushPendingAuthIdentity(LocalPlayerAuthService.Instance);
        }

        private void HandlePlayerLoggedIn(LocalPlayerAuthService.LocalPlayerRecord player)
        {
            QueueAuthIdentityMilestone(player.NameId);
            FlushPendingAuthIdentity(this);
        }

        private void HandleAuthGatePassed()
        {
            RecordMilestone("auth_gate_passed", this);
            FlushPendingPlayerMilestones();
        }

        private void HandleLocalPlayerSpawned(GameObject player)
        {
            if (!m_CompletedMilestones.ContainsKey("auth_gate_passed"))
            {
                m_PendingLocalPlayerSpawned = player;
                return;
            }

            RecordLocalPlayerSpawned(player);
        }

        private void HandleLocalPlayerReady(GameObject player)
        {
            if (!m_CompletedMilestones.ContainsKey("auth_gate_passed"))
            {
                m_PendingLocalPlayerReady = player;
                return;
            }

            RecordLocalPlayerReady(player);
        }

        private void RecordMilestone(
            string milestone,
            UnityEngine.Object context,
            params (string key, object value)[] data
        )
        {
            if (string.IsNullOrWhiteSpace(milestone))
            {
                return;
            }

            if (m_CompletedMilestones.ContainsKey(milestone))
            {
                return;
            }

            int milestoneIndex = Array.IndexOf(s_OrderedMilestones, milestone);
            if (milestoneIndex < 0)
            {
                NGLog.Warn(
                    Category,
                    NGLog.Format("Unknown startup milestone", ("milestone", milestone)),
                    context != null ? context : this
                );
                return;
            }

            string expectedMilestone = GetFirstUncompletedMilestone();
            bool inOrder = string.Equals(expectedMilestone, milestone, StringComparison.Ordinal);
            float elapsedMs = (Time.realtimeSinceStartup - m_BootStartTime) * 1000f;
            m_CompletedMilestones[milestone] = elapsedMs;

            NGLog.Ready(
                Category,
                milestone,
                true,
                CreateTraceContext(ResolvePhaseForMilestone(milestone)),
                context != null ? context : this,
                data: AppendData(
                    data,
                    ("elapsedMs", (object)Mathf.RoundToInt(elapsedMs)),
                    ("inOrder", inOrder)
                )
            );

            if (!inOrder)
            {
                NGLog.Warn(
                    Category,
                    NGLog.Format(
                        "Startup milestone arrived out of order",
                        ("milestone", milestone),
                        ("expected", expectedMilestone ?? "none")
                    ),
                    context != null ? context : this
                );
            }

            if (HasCompletedAllMilestones())
            {
                EmitSuccessSummary();
            }
        }

        private void QueueAuthIdentityMilestone(string nameId)
        {
            if (m_CompletedMilestones.ContainsKey("auth_identity_ready"))
            {
                return;
            }

            m_HasPendingAuthIdentity = true;
            m_PendingAuthIdentityNameId = nameId ?? string.Empty;
        }

        private void FlushPendingAuthIdentity(UnityEngine.Object context)
        {
            if (!m_HasPendingAuthIdentity || !m_CompletedMilestones.ContainsKey("network_ready"))
            {
                return;
            }

            m_HasPendingAuthIdentity = false;
            RecordMilestone(
                "auth_identity_ready",
                context != null ? context : this,
                ("nameId", (object)m_PendingAuthIdentityNameId)
            );
            m_PendingAuthIdentityNameId = string.Empty;
        }

        private void FlushPendingPlayerMilestones()
        {
            if (!m_CompletedMilestones.ContainsKey("auth_gate_passed"))
            {
                return;
            }

            if (m_PendingLocalPlayerSpawned != null)
            {
                RecordLocalPlayerSpawned(m_PendingLocalPlayerSpawned);
                m_PendingLocalPlayerSpawned = null;
            }

            if (m_PendingLocalPlayerReady != null)
            {
                RecordLocalPlayerReady(m_PendingLocalPlayerReady);
                m_PendingLocalPlayerReady = null;
            }
        }

        private void RecordLocalPlayerSpawned(GameObject player)
        {
            RecordMilestone(
                "local_player_spawned",
                player != null ? player : this,
                ("player", (object)(player != null ? player.name : "null"))
            );
        }

        private void RecordLocalPlayerReady(GameObject player)
        {
            RecordMilestone(
                "local_player_ready",
                player != null ? player : this,
                ("player", (object)(player != null ? player.name : "null"))
            );
        }

        private void EmitSuccessSummary()
        {
            if (m_SummaryLogged)
            {
                return;
            }

            m_SummaryLogged = true;
            NGLog.Ready(
                Category,
                "startup_summary",
                true,
                CreateTraceContext("scene_compose"),
                this,
                data:
                new[]
                {
                    ("completed", (object)s_OrderedMilestones.Length),
                    ("elapsedMs", (object)Mathf.RoundToInt((Time.realtimeSinceStartup - m_BootStartTime) * 1000f)),
                    ("order", string.Join(" -> ", s_OrderedMilestones)),
                }
            );
        }

        private void EmitFailureSummary(float elapsedSeconds)
        {
            if (m_SummaryLogged)
            {
                return;
            }

            m_SummaryLogged = true;
            NGLog.Ready(
                Category,
                "startup_summary",
                false,
                CreateTraceContext("scene_compose"),
                this,
                NGLogLevel.Warning,
                data:
                new[]
                {
                    ("missing", (object)(GetFirstUncompletedMilestone() ?? "unknown")),
                    ("elapsedMs", (object)Mathf.RoundToInt(elapsedSeconds * 1000f)),
                    ("completed", (object)m_CompletedMilestones.Count),
                }
            );
        }

        private bool HasCompletedAllMilestones()
        {
            return m_CompletedMilestones.Count >= s_OrderedMilestones.Length;
        }

        private string GetFirstMissingMilestone()
        {
            for (int i = 0; i < s_OrderedMilestones.Length; i++)
            {
                string milestone = s_OrderedMilestones[i];
                if (!m_CompletedMilestones.ContainsKey(milestone))
                {
                    return milestone;
                }
            }

            return null;
        }

        private TraceContext CreateTraceContext(string phase)
        {
            return new TraceContext(
                bootId: m_BootId,
                phase: phase,
                script: nameof(SceneWorkflowDiagnostics)
            );
        }

        private static string ResolvePhaseForMilestone(string milestone)
        {
            switch (milestone)
            {
                case "scene_bootstrap_ready":
                    return "scene_compose";
                case "network_mode_known":
                    return "network_mode";
                case "network_ready":
                    return "network_ready";
                case "auth_identity_ready":
                case "auth_gate_passed":
                    return "auth_gate";
                case "local_player_spawned":
                    return "player_spawn";
                case "local_player_ready":
                    return "player_ready";
                case "runtime_bind_core_complete":
                case "runtime_bind_auth_complete":
                    return "runtime_bind";
                default:
                    return "scene_compose";
            }
        }

        private static (string key, object value)[] AppendData(
            (string key, object value)[] data,
            params (string key, object value)[] extra
        )
        {
            int dataLength = data != null ? data.Length : 0;
            int extraLength = extra != null ? extra.Length : 0;
            var merged = new (string key, object value)[dataLength + extraLength];

            if (dataLength > 0)
            {
                Array.Copy(data, 0, merged, 0, dataLength);
            }

            if (extraLength > 0)
            {
                Array.Copy(extra, 0, merged, dataLength, extraLength);
            }

            return merged;
        }

        string ISceneWorkflowStateBridge.ActiveBootId => m_BootId;

        bool ISceneWorkflowStateBridge.StartupCompleted => HasCompletedAllMilestones();

        bool ISceneWorkflowStateBridge.IsMilestoneComplete(string milestone)
        {
            return !string.IsNullOrWhiteSpace(milestone) && m_CompletedMilestones.ContainsKey(milestone);
        }
    }
}
