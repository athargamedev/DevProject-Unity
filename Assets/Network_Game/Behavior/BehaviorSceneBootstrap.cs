using Network_Game.Diagnostics;
using Network_Game.Dialogue;
using Network_Game.Dialogue.Persistence;
using UnityEngine;

namespace Network_Game.Behavior
{
    /// <summary>
    /// Composition root for Behavior_Scene.
    /// Keeps the modular multiplayer runtime components present and synchronized
    /// with the scene-level contract, but does not own networking/auth/player logic itself.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(0)]
    public sealed class BehaviorSceneBootstrap : MonoBehaviour
    {
        private const string Category = "BehaviorSceneBootstrap";

        [Header("Scene References")]
        [SerializeField] private GameObject m_PrimaryNpc;
        [SerializeField] private bool m_AlignLocalPlayerToSpawnPoint = true;

        [Header("Authentication")]
        [SerializeField][Min(0.5f)] private float m_AuthGateTimeoutSeconds = 15f;
        [SerializeField] private bool m_BlockNetworkStartUntilAuthenticated = true;
        [SerializeField] private bool m_RequireExplicitLoginEachSession = true;

        [Header("Networking")]
        [SerializeField] private bool m_ForceClientMode;
        [SerializeField] private string m_ClientModeTag = "Client";
        [SerializeField] private bool m_AvoidHostStartWhenPortIsInUse = true;
        [SerializeField] private bool m_TryHostPortFallbackOnStartFailure = true;
        [SerializeField][Min(1024)] private int m_HostFallbackPortStart = 7778;
        [SerializeField][Range(1, 32)] private int m_HostFallbackPortAttempts = 8;
        [SerializeField][Min(0f)] private float m_PlayerSpawnSpacing = 3f;

        [Header("Diagnostics")]

        [SerializeField] private bool m_EnableDebugAssistantOnClients;

        private void Reset()
        {
            SyncExistingComponents();
        }

        private void OnValidate()
        {
            if (m_HostFallbackPortAttempts < 1)
            {
                m_HostFallbackPortAttempts = 1;
            }

            SyncExistingComponents();
        }

        private void Awake()
        {
            SceneSpawnManager spawnManager = GetComponent<SceneSpawnManager>();
            NGLog.Lifecycle(
                Category,
                "awake",
                CreateTraceContext("scene_compose"),
                this,
                data: new[]
                {
                    ("hasSpawnPoint", (object)(spawnManager != null && spawnManager.PlayerSpawnPoint != null)),
                    ("playerTag", (object)(spawnManager != null ? spawnManager.PlayerTag : string.Empty)),
                    ("forceClientMode", (object)m_ForceClientMode),
                }
            );

            CompositionResult composition = ComposeRuntime();
            SceneWorkflowDiagnostics.ReportMilestone(
                "scene_bootstrap_ready",
                this,
                ("componentsAdded", (object)composition.ComponentsAdded),
                ("componentsConfigured", (object)composition.ComponentsConfigured)
            );

            NGLog.Ready(
                Category,
                "scene_runtime_composed",
                true,
                CreateTraceContext("scene_compose"),
                this,
                data: new[]
                {
                    ("componentsAdded", (object)composition.ComponentsAdded),
                    ("componentsConfigured", (object)composition.ComponentsConfigured)
                }
            );
        }

        private CompositionResult ComposeRuntime()
        {
            int addedComponents = 0;
            int configuredComponents = 0;

            NetworkBootstrapEvents eventsComponent = GetOrAddComponent(ref addedComponents, out NetworkBootstrapEvents existingEvents);
            configuredComponents += CountConfigured(existingEvents ?? eventsComponent);

            NetworkBootstrap networkBootstrap = GetOrAddComponent(ref addedComponents, out NetworkBootstrap existingNetwork);
            ConfigureNetworkBootstrap(existingNetwork ?? networkBootstrap);
            configuredComponents++;

            AuthBootstrap authBootstrap = GetOrAddComponent(ref addedComponents, out AuthBootstrap existingAuth);
            ConfigureAuthBootstrap(existingAuth ?? authBootstrap);
            configuredComponents++;

            SceneSpawnManager sceneSpawnManager = GetOrAddComponent(ref addedComponents, out SceneSpawnManager existingSceneSpawnManager);
            ConfigureSceneSpawnManager(existingSceneSpawnManager ?? sceneSpawnManager);
            configuredComponents++;

            DialoguePersistenceGateway persistenceGateway = GetOrAddComponent(ref addedComponents, out DialoguePersistenceGateway existingPersistenceGateway);
            ConfigureDialoguePersistenceGateway(existingPersistenceGateway ?? persistenceGateway);
            configuredComponents++;

            DialogueMemoryWorker memoryWorker = GetOrAddComponent(ref addedComponents, out DialogueMemoryWorker existingMemoryWorker);
            ConfigureDialogueMemoryWorker(existingMemoryWorker ?? memoryWorker);
            configuredComponents++;

            PlayerBootstrap playerBootstrap = GetOrAddComponent(ref addedComponents, out PlayerBootstrap existingPlayer);
            ConfigurePlayerBootstrap(existingPlayer ?? playerBootstrap);
            configuredComponents++;

            RuntimeBinder runtimeBinder = GetOrAddComponent(ref addedComponents, out RuntimeBinder existingBinder);
            ConfigureRuntimeBinder(existingBinder ?? runtimeBinder);
            configuredComponents++;

            SceneWorkflowDiagnostics sceneWorkflow = GetOrAddComponent(ref addedComponents, out SceneWorkflowDiagnostics existingWorkflow);
            configuredComponents += CountConfigured(existingWorkflow ?? sceneWorkflow);

            DialogueSceneTargetRegistry sceneTargetRegistry = GetOrAddComponent(ref addedComponents, out DialogueSceneTargetRegistry existingTargetRegistry);
            configuredComponents += CountConfigured(existingTargetRegistry ?? sceneTargetRegistry);

            NGLog.Trigger(
                Category,
                "compose_runtime",
                CreateTraceContext("scene_compose"),
                this,
                data: new[]
                {
                    ("componentsAdded", (object)addedComponents),
                    ("componentsConfigured", (object)configuredComponents),
                }
            );

            return new CompositionResult(addedComponents, configuredComponents);
        }

        private void SyncExistingComponents()
        {
            ConfigureSceneSpawnManager(GetComponent<SceneSpawnManager>());
            ConfigureDialoguePersistenceGateway(GetComponent<DialoguePersistenceGateway>());
            ConfigureDialogueMemoryWorker(GetComponent<DialogueMemoryWorker>());
            ConfigureNetworkBootstrap(GetComponent<NetworkBootstrap>());
            ConfigureAuthBootstrap(GetComponent<AuthBootstrap>());
            ConfigurePlayerBootstrap(GetComponent<PlayerBootstrap>());
            ConfigureRuntimeBinder(GetComponent<RuntimeBinder>());
        }

        private void ConfigureNetworkBootstrap(NetworkBootstrap network)
        {
            if (network == null)
            {
                return;
            }

            SceneSpawnManager spawnManager = GetComponent<SceneSpawnManager>();
            Transform effectiveSpawnPoint = spawnManager != null ? spawnManager.PlayerSpawnPoint : null;
            string effectivePlayerTag = spawnManager != null ? spawnManager.PlayerTag : "Player";

            network.m_ForceClientMode = m_ForceClientMode;
            network.m_ClientModeTag = m_ClientModeTag;
            network.m_AvoidHostStartWhenPortIsInUse = m_AvoidHostStartWhenPortIsInUse;
            network.m_TryHostPortFallbackOnStartFailure = m_TryHostPortFallbackOnStartFailure;
            network.m_HostFallbackPortStart = m_HostFallbackPortStart;
            network.m_HostFallbackPortAttempts = m_HostFallbackPortAttempts;
            network.m_PlayerSpawnPoint = effectiveSpawnPoint;
            network.m_PlayerTag = effectivePlayerTag;
            network.m_PlayerSpawnSpacing = m_PlayerSpawnSpacing;
        }

        private void ConfigureAuthBootstrap(AuthBootstrap auth)
        {
            if (auth == null)
            {
                return;
            }

            auth.m_TimeoutSeconds = m_AuthGateTimeoutSeconds;
            auth.m_BlockNetworkStartUntilAuthenticated = m_BlockNetworkStartUntilAuthenticated;
            auth.m_RequireExplicitLoginEachSession = m_RequireExplicitLoginEachSession;
        }

        private void ConfigurePlayerBootstrap(PlayerBootstrap player)
        {
            if (player == null)
            {
                return;
            }

            SceneSpawnManager spawnManager = GetComponent<SceneSpawnManager>();
            Transform effectiveSpawnPoint = spawnManager != null ? spawnManager.PlayerSpawnPoint : null;
            string effectivePlayerTag = spawnManager != null ? spawnManager.PlayerTag : "Player";

            player.m_PlayerTag = effectivePlayerTag;
            player.m_PlayerSpawnPoint = effectiveSpawnPoint;
            player.m_AlignLocalPlayerToSpawnPoint = m_AlignLocalPlayerToSpawnPoint;
            player.m_PlayerSpawnSpacing = m_PlayerSpawnSpacing;
        }

        private void ConfigureSceneSpawnManager(SceneSpawnManager sceneSpawnManager)
        {
            if (sceneSpawnManager == null)
            {
                return;
            }
        }

        private void ConfigureDialoguePersistenceGateway(DialoguePersistenceGateway gateway)
        {
            if (gateway == null)
            {
                return;
            }
        }

        private void ConfigureDialogueMemoryWorker(DialogueMemoryWorker worker)
        {
            if (worker == null)
            {
                return;
            }
        }

        private void ConfigureRuntimeBinder(RuntimeBinder binder)
        {
            if (binder == null)
            {
                return;
            }

            binder.m_PrimaryNpc = m_PrimaryNpc;
        }

        private static int CountConfigured(Component component)
        {
            return component != null ? 1 : 0;
        }

        private T GetOrAddComponent<T>(ref int addedComponents, out T existing) where T : Component
        {
            existing = GetComponent<T>();
            if (existing != null)
            {
                return existing;
            }

            addedComponents++;
            return gameObject.AddComponent<T>();
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

        private readonly struct CompositionResult
        {
            public CompositionResult(int componentsAdded, int componentsConfigured)
            {
                ComponentsAdded = componentsAdded;
                ComponentsConfigured = componentsConfigured;
            }

            public int ComponentsAdded { get; }

            public int ComponentsConfigured { get; }
        }
    }
}
