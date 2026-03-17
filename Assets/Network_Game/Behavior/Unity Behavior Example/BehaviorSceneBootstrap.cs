using System.Collections;
using Network_Game.Diagnostics;
using Network_Game.Behavior;
using UnityEngine;

namespace Network_Game.Behavior
{
    /// <summary>
    /// Scene bootstrap coordinator - ensures all bootstrap components are attached and wired.
    /// Delegates to specialized components: NetworkBootstrap, PlayerBootstrap, AuthBootstrap, RuntimeBinder.
    ///
    /// Execution order:
    /// 1. NetworkBootstrap (-100) - handles networking
    /// 2. AuthBootstrap (-220) - handles auth gate
    /// 3. PlayerBootstrap (-50) - handles player spawn
    /// 4. RuntimeBinder (100) - handles post-spawn wiring
    /// 5. BehaviorSceneBootstrap (default 0) - coordinator
    /// </summary>
    [DefaultExecutionOrder(0)]
    public class BehaviorSceneBootstrap : MonoBehaviour
    {
        private const string Category = "BehaviorSceneBootstrap";

        [Header("Component Configuration")]
        [SerializeField] private bool m_UseNewBootstrap = true;

        [Header("Legacy Settings (used if UseNewBootstrap=false)")]
        [SerializeField] private GameObject m_PrimaryNpc;
        [SerializeField] private Transform m_PlayerSpawnPoint;
        [SerializeField] private string m_PlayerTag = "Player";
        [SerializeField] private bool m_AlignLocalPlayerToSpawnPoint = true;

        [Header("Auth")]
        [SerializeField][Min(0.5f)] private float m_AuthGateTimeoutSeconds = 15f;
        [SerializeField] private bool m_BlockNetworkStartUntilAuthenticated = true;
        [SerializeField] private bool m_RequireExplicitLoginEachSession = true;

        [Header("Client Mode")]
        [SerializeField] private bool m_ForceClientMode;
        [SerializeField] private string m_ClientModeTag = "Client";
        [SerializeField] private bool m_AvoidHostStartWhenPortIsInUse = true;
        [SerializeField] private bool m_TryHostPortFallbackOnStartFailure = true;
        [SerializeField][Min(1024)] private int m_HostFallbackPortStart = 7778;
        [SerializeField][Range(1, 32)] private int m_HostFallbackPortAttempts = 8;

        [Header("Debug")]
        [SerializeField] private bool m_AutoCreateLlmDebugAssistant;
        [SerializeField] public bool m_EnableDebugAssistantOnClients;

        private void Awake()
        {
            NGLog.Lifecycle(
                Category,
                "awake",
                CreateTraceContext("scene_compose"),
                this,
                data: new[] { ("useNewBootstrap", (object)m_UseNewBootstrap) }
            );

            if (!m_UseNewBootstrap)
            {
                NGLog.Ready(
                    Category,
                    "legacy_bootstrap",
                    true,
                    CreateTraceContext("scene_compose"),
                    this
                );
                return;
            }

            EnsureComponents();
            SceneWorkflowDiagnostics.ReportMilestone(
                "scene_bootstrap_ready",
                this,
                ("useNewBootstrap", (object)m_UseNewBootstrap)
            );
            NGLog.Ready(
                Category,
                "modular_bootstrap_initialized",
                true,
                CreateTraceContext("scene_compose"),
                this
            );
        }

        private void EnsureComponents()
        {
            int addedComponents = 0;

            // Ensure NetworkBootstrapEvents exists
            if (NetworkBootstrapEvents.Instance == null)
            {
                gameObject.AddComponent<NetworkBootstrapEvents>();
                addedComponents++;
            }

            // Ensure NetworkBootstrap
            if (GetComponent<NetworkBootstrap>() == null)
            {
                var network = gameObject.AddComponent<NetworkBootstrap>();
                ConfigureNetworkBootstrap(network);
                addedComponents++;
            }

            // Ensure AuthBootstrap
            if (GetComponent<AuthBootstrap>() == null)
            {
                var auth = gameObject.AddComponent<AuthBootstrap>();
                ConfigureAuthBootstrap(auth);
                addedComponents++;
            }

            // Ensure PlayerBootstrap
            if (GetComponent<PlayerBootstrap>() == null)
            {
                var player = gameObject.AddComponent<PlayerBootstrap>();
                ConfigurePlayerBootstrap(player);
                addedComponents++;
            }

            // Ensure RuntimeBinder
            if (GetComponent<RuntimeBinder>() == null)
            {
                var binder = gameObject.AddComponent<RuntimeBinder>();
                ConfigureRuntimeBinder(binder);
                addedComponents++;
            }

            if (GetComponent<SceneWorkflowDiagnostics>() == null)
            {
                gameObject.AddComponent<SceneWorkflowDiagnostics>();
                addedComponents++;
            }

            if (GetComponent<DialogueFlowDiagnostics>() == null)
            {
                gameObject.AddComponent<DialogueFlowDiagnostics>();
                addedComponents++;
            }

            NGLog.Trigger(
                Category,
                "ensure_components",
                CreateTraceContext("scene_compose"),
                this,
                data: new[] { ("addedComponents", (object)addedComponents) }
            );
        }

        private void ConfigureNetworkBootstrap(NetworkBootstrap network)
        {
            // Copy serialized fields
            network.m_ForceClientMode = m_ForceClientMode;
            network.m_ClientModeTag = m_ClientModeTag;
            network.m_AvoidHostStartWhenPortIsInUse = m_AvoidHostStartWhenPortIsInUse;
            network.m_TryHostPortFallbackOnStartFailure = m_TryHostPortFallbackOnStartFailure;
            network.m_HostFallbackPortStart = m_HostFallbackPortStart;
            network.m_HostFallbackPortAttempts = m_HostFallbackPortAttempts;
            network.m_PlayerSpawnPoint = m_PlayerSpawnPoint;
            network.m_PlayerTag = m_PlayerTag;
        }

        private void ConfigureAuthBootstrap(AuthBootstrap auth)
        {
            auth.m_TimeoutSeconds = m_AuthGateTimeoutSeconds;
            auth.m_BlockNetworkStartUntilAuthenticated = m_BlockNetworkStartUntilAuthenticated;
            auth.m_RequireExplicitLoginEachSession = m_RequireExplicitLoginEachSession;
        }

        private void ConfigurePlayerBootstrap(PlayerBootstrap player)
        {
            player.m_PlayerTag = m_PlayerTag;
            player.m_PlayerSpawnPoint = m_PlayerSpawnPoint;
            player.m_AlignLocalPlayerToSpawnPoint = m_AlignLocalPlayerToSpawnPoint;
        }

        private void ConfigureRuntimeBinder(RuntimeBinder binder)
        {
            binder.m_PrimaryNpc = m_PrimaryNpc;
            binder.m_AutoCreateLlmDebugAssistant = m_AutoCreateLlmDebugAssistant;
            binder.m_EnableDebugAssistantOnClients = m_EnableDebugAssistantOnClients;
        }

        private void OnDisable()
        {
            NGLog.Lifecycle(Category, "disable", CreateTraceContext("scene_compose"), this);

            // Cleanup editor endpoint files
#if UNITY_EDITOR
            if (!m_UseNewBootstrap)
            {
                var network = GetComponent<Unity.Netcode.NetworkManager>();
                if (network != null && !IsClientMode())
                {
                    ClearEditorHostEndpoint();
                }
            }
#endif
        }

        private static TraceContext CreateTraceContext(
            string phase,
            [System.Runtime.CompilerServices.CallerMemberName] string caller = null
        )
        {
            return new TraceContext(
                phase: phase,
                script: nameof(BehaviorSceneBootstrap),
                callback: caller
            );
        }

        private bool IsClientMode()
        {
            if (m_ForceClientMode) return true;

            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], "-client", System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

#if UNITY_EDITOR
        private static void ClearEditorHostEndpoint()
        {
            try
            {
                string folder = System.IO.Path.Combine(
                    System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
                    "NetworkGame"
                );
                string path = System.IO.Path.Combine(folder, "behavior-scene-host-endpoint.txt");
                if (System.IO.File.Exists(path))
                    System.IO.File.Delete(path);
            }
            catch {}
        }

#endif
    }
}
