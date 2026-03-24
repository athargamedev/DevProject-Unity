using System;
using System.Collections.Generic;
using Network_Game.Diagnostics;
using Unity.Netcode;
using UnityEngine;

namespace Network_Game.Behavior
{
    /// <summary>
    /// Owns the scene-start spawn plan for networked NPCs and other runtime objects.
    /// Player spawning remains on the Netcode connection-approval path and reads
    /// this component's player spawn contract.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-60)]
    public sealed class SceneSpawnManager : MonoBehaviour
    {
        private const string Category = "SceneSpawnManager";

        [Serializable]
        public struct InitialSpawnEntry
        {
            public string SpawnId;
            public GameObject Prefab;
            public Transform SpawnPoint;
            public Vector3 Position;
            public Vector3 RotationEuler;
            public bool Enabled;
            public bool SkipIfObjectWithTagExists;
            public string ExistingObjectTag;
        }

        [Header("Player Spawn Contract")]
        [SerializeField] private Transform m_PlayerSpawnPoint;
        [SerializeField] private string m_PlayerTag = "Player";

        [Header("Initial NPC Spawns")]
        [SerializeField] private InitialSpawnEntry[] m_NpcSpawnPlan;

        [Header("Initial NetworkObject Spawns")]
        [SerializeField] private InitialSpawnEntry[] m_InitialNetworkObjectSpawns;

        [Header("Legacy NPC Fallback")]
        [SerializeField] private GameObject[] m_LegacyNpcPrefabsToSpawn;

        [Header("Runtime")]
        [SerializeField] private bool m_SpawnOnHostStart = true;
        [SerializeField] private bool m_SpawnOnDedicatedServerStart = true;
        [SerializeField] private bool m_LogSpawnPlan = true;

        private bool m_InitialSpawnComplete;
        private bool m_SubscribedToBootstrapEvents;
        private readonly HashSet<string> m_ProcessedSpawnIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public Transform PlayerSpawnPoint => m_PlayerSpawnPoint;

        public string PlayerTag => string.IsNullOrWhiteSpace(m_PlayerTag) ? "Player" : m_PlayerTag;

        private void Awake()
        {
            ResolvePlayerSpawnPointReference();
        }

        private void OnEnable()
        {
            SubscribeToBootstrapEvents();
        }

        private void OnDisable()
        {
            UnsubscribeFromBootstrapEvents();
        }

        private void Start()
        {
            SubscribeToBootstrapEvents();

            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsServer || !manager.IsListening)
            {
                return;
            }

            bool shouldSpawn = manager.IsHost ? m_SpawnOnHostStart : m_SpawnOnDedicatedServerStart;
            if (shouldSpawn)
            {
                TrySpawnInitialSceneObjects(manager.IsHost ? "start_host" : "start_server");
            }
        }

        private void SubscribeToBootstrapEvents()
        {
            if (m_SubscribedToBootstrapEvents)
            {
                return;
            }

            NetworkBootstrapEvents eventsBridge = NetworkBootstrapEvents.Instance;
            if (eventsBridge == null)
            {
                return;
            }

            eventsBridge.OnHostStarted -= HandleHostStarted;
            eventsBridge.OnNetworkReady -= HandleNetworkReady;
            eventsBridge.OnHostStarted += HandleHostStarted;
            eventsBridge.OnNetworkReady += HandleNetworkReady;
            m_SubscribedToBootstrapEvents = true;
        }

        private void UnsubscribeFromBootstrapEvents()
        {
            if (!m_SubscribedToBootstrapEvents)
            {
                return;
            }

            NetworkBootstrapEvents eventsBridge = NetworkBootstrapEvents.Instance;
            if (eventsBridge == null)
            {
                m_SubscribedToBootstrapEvents = false;
                return;
            }

            eventsBridge.OnHostStarted -= HandleHostStarted;
            eventsBridge.OnNetworkReady -= HandleNetworkReady;
            m_SubscribedToBootstrapEvents = false;
        }

        public void ApplyLegacyDefaults(Transform playerSpawnPoint, string playerTag, GameObject[] legacyNpcPrefabs)
        {
            if (m_PlayerSpawnPoint == null && playerSpawnPoint != null)
            {
                m_PlayerSpawnPoint = playerSpawnPoint;
            }

            if (string.IsNullOrWhiteSpace(m_PlayerTag) && !string.IsNullOrWhiteSpace(playerTag))
            {
                m_PlayerTag = playerTag;
            }

            if ((m_LegacyNpcPrefabsToSpawn == null || m_LegacyNpcPrefabsToSpawn.Length == 0) && HasAnyPrefab(legacyNpcPrefabs))
            {
                m_LegacyNpcPrefabsToSpawn = legacyNpcPrefabs;
            }

            ResolvePlayerSpawnPointReference();
        }

        private void HandleHostStarted()
        {
            if (!m_SpawnOnHostStart)
            {
                return;
            }

            TrySpawnInitialSceneObjects("host_started");
        }

        private void HandleNetworkReady(NetworkManager manager)
        {
            if (manager == null || !manager.IsServer)
            {
                return;
            }

            bool shouldSpawn = manager.IsHost ? m_SpawnOnHostStart : m_SpawnOnDedicatedServerStart;
            if (!shouldSpawn)
            {
                return;
            }

            TrySpawnInitialSceneObjects(manager.IsHost ? "network_ready_host" : "network_ready_server");
        }

        private void TrySpawnInitialSceneObjects(string reason)
        {
            if (m_InitialSpawnComplete)
            {
                return;
            }

            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsListening || !manager.IsServer)
            {
                NGLog.Warn(Category, "Spawn manager skipped initial scene spawn because the server is not ready", this);
                return;
            }

            ResolvePlayerSpawnPointReference();

            int spawnedCount = 0;
            int skippedCount = 0;

            if (m_LogSpawnPlan)
            {
                NGLog.Trigger(
                    Category,
                    "initial_spawn_begin",
                    CreateTraceContext("scene_spawn"),
                    this,
                    data: new[]
                    {
                        ("reason", (object)(reason ?? string.Empty)),
                        ("npcPlanCount", (object)(m_NpcSpawnPlan?.Length ?? 0)),
                        ("networkPlanCount", (object)(m_InitialNetworkObjectSpawns?.Length ?? 0)),
                        ("legacyNpcCount", (object)(m_LegacyNpcPrefabsToSpawn?.Length ?? 0)),
                    }
                );
            }

            spawnedCount += SpawnEntries(m_NpcSpawnPlan, defaultExistingTag: "NPC", ref skippedCount);
            if (spawnedCount == 0 && skippedCount == 0)
            {
                spawnedCount += SpawnLegacyNpcFallback(ref skippedCount);
            }

            spawnedCount += SpawnEntries(m_InitialNetworkObjectSpawns, defaultExistingTag: string.Empty, ref skippedCount);

            m_InitialSpawnComplete = true;

            NGLog.Ready(
                Category,
                "initial_scene_objects_spawned",
                true,
                CreateTraceContext("scene_spawn"),
                this,
                data: new[]
                {
                    ("reason", (object)(reason ?? string.Empty)),
                    ("spawned", (object)spawnedCount),
                    ("skipped", (object)skippedCount),
                    ("playerSpawnPoint", (object)(m_PlayerSpawnPoint != null ? m_PlayerSpawnPoint.name : string.Empty)),
                }
            );
        }

        private int SpawnEntries(InitialSpawnEntry[] entries, string defaultExistingTag, ref int skippedCount)
        {
            if (entries == null || entries.Length == 0)
            {
                return 0;
            }

            int spawnedCount = 0;
            for (int i = 0; i < entries.Length; i++)
            {
                if (TrySpawnEntry(entries[i], i, defaultExistingTag, out string skipReason))
                {
                    spawnedCount++;
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(skipReason))
                {
                    skippedCount++;
                }
            }

            return spawnedCount;
        }

        private int SpawnLegacyNpcFallback(ref int skippedCount)
        {
            if (m_LegacyNpcPrefabsToSpawn == null || m_LegacyNpcPrefabsToSpawn.Length == 0)
            {
                return 0;
            }

            int spawnedCount = 0;
            for (int i = 0; i < m_LegacyNpcPrefabsToSpawn.Length; i++)
            {
                GameObject prefab = m_LegacyNpcPrefabsToSpawn[i];
                InitialSpawnEntry entry = new InitialSpawnEntry
                {
                    SpawnId = prefab != null ? prefab.name : $"legacy_npc_{i}",
                    Prefab = prefab,
                    SpawnPoint = null,
                    Position = Vector3.zero,
                    RotationEuler = Vector3.zero,
                    Enabled = true,
                    SkipIfObjectWithTagExists = true,
                    ExistingObjectTag = "NPC",
                };

                if (TrySpawnEntry(entry, i, "NPC", out string skipReason))
                {
                    spawnedCount++;
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(skipReason))
                {
                    skippedCount++;
                }
            }

            return spawnedCount;
        }

        private bool TrySpawnEntry(InitialSpawnEntry entry, int index, string defaultExistingTag, out string skipReason)
        {
            skipReason = null;

            if (!entry.Enabled)
            {
                skipReason = "disabled";
                return false;
            }

            if (entry.Prefab == null)
            {
                skipReason = "missing_prefab";
                NGLog.Warn(
                    Category,
                    NGLog.Format("Initial spawn entry skipped because prefab is missing", ("index", index)),
                    this
                );
                return false;
            }

            NetworkObject prefabNetworkObject = entry.Prefab.GetComponent<NetworkObject>();
            if (prefabNetworkObject == null)
            {
                skipReason = "missing_network_object";
                NGLog.Warn(
                    Category,
                    NGLog.Format("Initial spawn entry skipped because prefab has no NetworkObject", ("prefab", entry.Prefab.name)),
                    this
                );
                return false;
            }

            string entryId = BuildSpawnEntryId(entry, index);
            if (!m_ProcessedSpawnIds.Add(entryId))
            {
                skipReason = "already_processed";
                return false;
            }

            string existingTag = string.IsNullOrWhiteSpace(entry.ExistingObjectTag)
                ? defaultExistingTag
                : entry.ExistingObjectTag;
            if (entry.SkipIfObjectWithTagExists && HasExistingObjectWithTag(existingTag))
            {
                skipReason = "existing_tag_present";
                NGLog.Trigger(
                    Category,
                    "initial_spawn_skipped_existing_tag",
                    CreateTraceContext("scene_spawn"),
                    this,
                    data: new[]
                    {
                        ("entryId", (object)entryId),
                        ("tag", (object)(existingTag ?? string.Empty)),
                    }
                );
                return false;
            }

            Transform spawnPoint = entry.SpawnPoint;
            Vector3 position = spawnPoint != null ? spawnPoint.position : entry.Position;
            Quaternion rotation = spawnPoint != null ? spawnPoint.rotation : Quaternion.Euler(entry.RotationEuler);

            GameObject instance = Instantiate(entry.Prefab, position, rotation);
            NetworkObject spawnedNetworkObject = instance.GetComponent<NetworkObject>();
            spawnedNetworkObject.Spawn();

            NGLog.Trigger(
                Category,
                "initial_object_spawned",
                CreateTraceContext("scene_spawn"),
                this,
                data: new[]
                {
                    ("entryId", (object)entryId),
                    ("prefab", (object)entry.Prefab.name),
                    ("networkId", (object)spawnedNetworkObject.NetworkObjectId),
                }
            );

            return true;
        }

        private void ResolvePlayerSpawnPointReference()
        {
            if (m_PlayerSpawnPoint != null)
            {
                return;
            }

            GameObject spawnByName = GameObject.Find("SpawnPoint");
            if (spawnByName != null)
            {
                m_PlayerSpawnPoint = spawnByName.transform;
                return;
            }

            try
            {
                GameObject spawnByTag = GameObject.FindGameObjectWithTag("SpawnPoint");
                if (spawnByTag != null)
                {
                    m_PlayerSpawnPoint = spawnByTag.transform;
                }
            }
            catch (UnityException)
            {
            }
        }

        private static bool HasAnyPrefab(GameObject[] prefabs)
        {
            if (prefabs == null || prefabs.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < prefabs.Length; i++)
            {
                if (prefabs[i] != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasExistingObjectWithTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                return false;
            }

            try
            {
                GameObject[] objects = GameObject.FindGameObjectsWithTag(tag);
                return objects != null && objects.Length > 0;
            }
            catch (UnityException)
            {
                return false;
            }
        }

        private static string BuildSpawnEntryId(InitialSpawnEntry entry, int index)
        {
            if (!string.IsNullOrWhiteSpace(entry.SpawnId))
            {
                return entry.SpawnId.Trim();
            }

            return entry.Prefab != null ? $"{entry.Prefab.name}_{index}" : $"spawn_{index}";
        }

        private static TraceContext CreateTraceContext(
            string phase,
            [System.Runtime.CompilerServices.CallerMemberName] string caller = null
        )
        {
            return new TraceContext(
                phase: phase,
                script: nameof(SceneSpawnManager),
                callback: caller
            );
        }
    }
}
