using System;
using System.Collections;
using System.Collections.Generic;
using Network_Game.Auth;
using Network_Game.Behavior;
using Unity.Netcode;
using UnityEngine;
using NGLogLevel = Network_Game.Diagnostics.LogLevel;

namespace Network_Game.Diagnostics
{
    /// <summary>
    /// Tracks ordered startup milestones for the active scene and emits a concise summary.
    /// </summary>
    [DefaultExecutionOrder(120)]
    public class SceneWorkflowDiagnostics : MonoBehaviour
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

        public static string ActiveBootId => s_Instance != null ? s_Instance.m_BootId : string.Empty;
        public static bool StartupCompleted => s_Instance != null && s_Instance.HasCompletedAllMilestones();

        public static bool IsMilestoneComplete(string milestone)
        {
            return s_Instance != null && s_Instance.m_CompletedMilestones.ContainsKey(milestone);
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
        }

        private void SubscribeEvents()
        {
            NetworkBootstrapEvents events = NetworkBootstrapEvents.Instance;
            if (events != null)
            {
                events.OnClientModeDetermined += HandleClientModeDetermined;
                events.OnNetworkReady += HandleNetworkReady;
                events.OnAuthGatePassed += HandleAuthGatePassed;
                events.OnLocalPlayerSpawned += HandleLocalPlayerSpawned;
                events.OnLocalPlayerReady += HandleLocalPlayerReady;

                NGLog.Subscribe(Category, "bootstrap_events", CreateTraceContext("scene_compose"), this);
            }

            LocalPlayerAuthService.OnPlayerLoggedIn += HandlePlayerLoggedIn;
        }

        private void UnsubscribeEvents()
        {
            NetworkBootstrapEvents events = NetworkBootstrapEvents.Instance;
            if (events != null)
            {
                events.OnClientModeDetermined -= HandleClientModeDetermined;
                events.OnNetworkReady -= HandleNetworkReady;
                events.OnAuthGatePassed -= HandleAuthGatePassed;
                events.OnLocalPlayerSpawned -= HandleLocalPlayerSpawned;
                events.OnLocalPlayerReady -= HandleLocalPlayerReady;
            }

            LocalPlayerAuthService.OnPlayerLoggedIn -= HandlePlayerLoggedIn;
        }

        private void CatchUpCurrentState()
        {
            LocalPlayerAuthService authService = LocalPlayerAuthService.Instance;
            if (authService != null && authService.HasCurrentPlayer)
            {
                RecordMilestone(
                    "auth_identity_ready",
                    authService,
                    ("nameId", (object)authService.CurrentPlayer.NameId)
                );
            }

            NetworkManager manager = NetworkManager.Singleton;
            if (manager != null && manager.IsListening)
            {
                RecordMilestone("network_ready", manager, ("listening", true));
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
        }

        private void HandlePlayerLoggedIn(LocalPlayerAuthService.LocalPlayerRecord player)
        {
            RecordMilestone("auth_identity_ready", this, ("nameId", (object)player.NameId));
        }

        private void HandleAuthGatePassed()
        {
            RecordMilestone("auth_gate_passed", this);
        }

        private void HandleLocalPlayerSpawned(GameObject player)
        {
            RecordMilestone(
                "local_player_spawned",
                player != null ? player : this,
                ("player", (object)(player != null ? player.name : "null"))
            );
        }

        private void HandleLocalPlayerReady(GameObject player)
        {
            RecordMilestone(
                "local_player_ready",
                player != null ? player : this,
                ("player", (object)(player != null ? player.name : "null"))
            );
        }

        private void RecordMilestone(
            string milestone,
            UnityEngine.Object context,
            params (string key, object value)[] data
        )
        {
            if (string.IsNullOrWhiteSpace(milestone) || m_SummaryLogged)
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

            string expectedMilestone = GetFirstMissingMilestone();
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
                    ("missing", (object)(GetFirstMissingMilestone() ?? "unknown")),
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
    }
}
