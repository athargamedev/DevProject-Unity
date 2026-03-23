using System.Collections.Generic;
using Network_Game.Diagnostics;
using Network_Game.Dialogue;
using Unity.Netcode;
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
        [SerializeField] private GameObject[] m_NpcPrefabsToSpawn;
        [SerializeField] private Transform m_PlayerSpawnPoint;
        [SerializeField] private string m_PlayerTag = "Player";
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

        [Header("Diagnostics")]
        [SerializeField] private bool m_AutoCreateLlmDebugAssistant;
        [SerializeField] private bool m_EnableDebugAssistantOnClients;

        private void Reset()
        {
            ResolveSceneReferences();
            SyncExistingComponents();
        }

        private void OnValidate()
        {
            if (m_HostFallbackPortAttempts < 1)
            {
                m_HostFallbackPortAttempts = 1;
            }

            ResolveSceneReferences();
            SyncExistingComponents();
        }

        private void Awake()
        {
            ResolveSceneReferences();
            NGLog.Lifecycle(
                Category,
                "awake",
                CreateTraceContext("scene_compose"),
                this,
                data: new[]
                {
                    ("hasSpawnPoint", (object)(m_PlayerSpawnPoint != null)),
                    ("playerTag", (object)(m_PlayerTag ?? string.Empty)),
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

        private void OnEnable()
        {
            var events = NetworkBootstrapEvents.Instance;
            if (events != null)
            {
                events.OnHostStarted += OnNetworkInitialized;
            }
        }

        private void OnDisable()
        {
            var events = NetworkBootstrapEvents.Instance;
            if (events != null)
            {
                events.OnHostStarted -= OnNetworkInitialized;
            }
        }

        private void OnNetworkInitialized()
        {
            // Spawn NPCs after network is initialized (host/server started)
            SpawnNpcsIfNeeded();
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

            PlayerBootstrap playerBootstrap = GetOrAddComponent(ref addedComponents, out PlayerBootstrap existingPlayer);
            ConfigurePlayerBootstrap(existingPlayer ?? playerBootstrap);
            configuredComponents++;

            RuntimeBinder runtimeBinder = GetOrAddComponent(ref addedComponents, out RuntimeBinder existingBinder);
            ConfigureRuntimeBinder(existingBinder ?? runtimeBinder);
            configuredComponents++;

            SceneWorkflowDiagnostics sceneWorkflow = GetOrAddComponent(ref addedComponents, out SceneWorkflowDiagnostics existingWorkflow);
            configuredComponents += CountConfigured(existingWorkflow ?? sceneWorkflow);

            DialogueFlowDiagnostics dialogueFlow = GetOrAddComponent(ref addedComponents, out DialogueFlowDiagnostics existingDialogueFlow);
            configuredComponents += CountConfigured(existingDialogueFlow ?? dialogueFlow);

            DialogueSceneTargetRegistry sceneTargetRegistry = GetOrAddComponent(ref addedComponents, out DialogueSceneTargetRegistry existingTargetRegistry);
            configuredComponents += CountConfigured(existingTargetRegistry ?? sceneTargetRegistry);

            DiagnosticBrainRuntime diagnosticBrainRuntime = GetOrAddComponent(ref addedComponents, out DiagnosticBrainRuntime existingDiagnosticBrainRuntime);
            configuredComponents += CountConfigured(existingDiagnosticBrainRuntime ?? diagnosticBrainRuntime);

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

        private void SpawnNpcsIfNeeded()
        {
            // Check if NPCs with "NPC" tag already exist in scene
            var existingNpcs = GameObject.FindGameObjectsWithTag("NPC");
            if (existingNpcs != null && existingNpcs.Length > 0)
            {
                NGLog.Trigger(
                    Category,
                    "npcs_already_in_scene",
                    CreateTraceContext("scene_compose"),
                    this,
                    data: new[] { ("count", (object)existingNpcs.Length) }
                );
                return;
            }

            // Get list of NPC prefabs to spawn
            var prefabsToSpawn = GetNpcPrefabsToSpawn();
            if (prefabsToSpawn == null || prefabsToSpawn.Count == 0)
            {
                NGLog.Warn(
                    Category,
                    "No NPC prefabs found to spawn",
                    this
                );
                return;
            }

            var networkManager = NetworkManager.Singleton;
            if (networkManager == null)
            {
                NGLog.Warn(
                    Category,
                    "NetworkManager not ready for NPC spawn",
                    this
                );
                return;
            }

            foreach (var npcPrefab in prefabsToSpawn)
            {
                if (npcPrefab == null)
                {
                    continue;
                }

                var networkObject = npcPrefab.GetComponent<NetworkObject>();
                if (networkObject == null)
                {
                    NGLog.Warn(
                        Category,
                        NGLog.Format("NPC prefab missing NetworkObject", ("prefab", npcPrefab.name)),
                        this
                    );
                    continue;
                }

                var spawned = Instantiate(npcPrefab, Vector3.zero, Quaternion.identity);
                var npcNetworkObject = spawned.GetComponent<NetworkObject>();
                
                // Spawn on network - defaults to server-authoritative
                npcNetworkObject.Spawn();
                
                NGLog.Trigger(
                    Category,
                    "npc_spawned",
                    CreateTraceContext("scene_compose"),
                    this,
                    data: new[]
                    {
                        ("prefab", (object)npcPrefab.name),
                        ("networkId", (object)npcNetworkObject.NetworkObjectId)
                    }
                );
            }
        }

        private List<GameObject> GetNpcPrefabsToSpawn()
        {
            // First try configured prefab list
            if (m_NpcPrefabsToSpawn != null && m_NpcPrefabsToSpawn.Length > 0)
            {
                var validPrefabs = new List<GameObject>();
                foreach (var prefab in m_NpcPrefabsToSpawn)
                {
                    if (prefab != null)
                    {
                        validPrefabs.Add(prefab);
                    }
                }
                return validPrefabs;
            }

            // Auto-discover NPC prefabs from Resources
            var discovered = new List<GameObject>();
            var npcPrefabs = Resources.LoadAll<GameObject>("");
            foreach (var prefab in npcPrefabs)
            {
                if (prefab != null && prefab.name.StartsWith("NPC_") && prefab.GetComponent<NetworkObject>() != null)
                {
                    discovered.Add(prefab);
                    NGLog.Trigger(
                        Category,
                        "npc_prefab_discovered",
                        CreateTraceContext("scene_compose"),
                        this,
                        data: new[] { ("prefab", (object)prefab.name) }
                    );
                }
            }

            // Also check Addressables path if available
            #if UNITY_EDITOR
            if (discovered.Count == 0)
            {
                // Try finding in prefab folders
                var guids = UnityEditor.AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Network_Game/ThirdPersonController/Prefabs" });
                foreach (var guid in guids)
                {
                    var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                    var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab != null && prefab.name.StartsWith("NPC_") && prefab.GetComponent<NetworkObject>() != null)
                    {
                        discovered.Add(prefab);
                    }
                }
            }
            #endif

            return discovered;
        }

        private void SyncExistingComponents()
        {
            ConfigureNetworkBootstrap(GetComponent<NetworkBootstrap>());
            ConfigureAuthBootstrap(GetComponent<AuthBootstrap>());
            ConfigurePlayerBootstrap(GetComponent<PlayerBootstrap>());
            ConfigureRuntimeBinder(GetComponent<RuntimeBinder>());
        }

        private void ResolveSceneReferences()
        {
            if (m_PlayerSpawnPoint == null)
            {
                GameObject spawnPointByName = GameObject.Find("SpawnPoint");
                if (spawnPointByName != null)
                {
                    m_PlayerSpawnPoint = spawnPointByName.transform;
                }
            }

            if (m_PlayerSpawnPoint == null)
            {
                try
                {
                    GameObject spawnPointByTag = GameObject.FindGameObjectWithTag("SpawnPoint");
                    if (spawnPointByTag != null)
                    {
                        m_PlayerSpawnPoint = spawnPointByTag.transform;
                    }
                }
                catch (UnityException)
                {
                }
            }
        }

        private void ConfigureNetworkBootstrap(NetworkBootstrap network)
        {
            if (network == null)
            {
                return;
            }

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

            player.m_PlayerTag = m_PlayerTag;
            player.m_PlayerSpawnPoint = m_PlayerSpawnPoint;
            player.m_AlignLocalPlayerToSpawnPoint = m_AlignLocalPlayerToSpawnPoint;
        }

        private void ConfigureRuntimeBinder(RuntimeBinder binder)
        {
            if (binder == null)
            {
                return;
            }

            binder.m_PrimaryNpc = m_PrimaryNpc;
            binder.m_AutoCreateLlmDebugAssistant = m_AutoCreateLlmDebugAssistant;
            binder.m_EnableDebugAssistantOnClients = m_EnableDebugAssistantOnClients;
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
