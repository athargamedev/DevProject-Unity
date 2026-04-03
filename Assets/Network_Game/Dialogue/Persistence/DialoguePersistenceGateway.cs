using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Network_Game.Diagnostics;
using Newtonsoft.Json.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Network_Game.Dialogue.Persistence
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-430)]
    public sealed class DialoguePersistenceGateway : MonoBehaviour
    {
        private const string Category = "DialoguePersistence";
        private const int DefaultEmbeddingDimensions = 768;
        private const string DefaultServiceRoleEnvVar = "SUPABASE_SERVICE_ROLE_KEY";
        private const string LegacyServiceRoleEnvVar = "SERVICE_ROLE_KEY";
        private const string SecretKeyEnvVar = "SECRET_KEY";

        [Serializable]
        private sealed class PlayerProfilePayload
        {
            public string p_player_key;
            public string p_player_handle;
            public string p_bio;
            public string p_long_term_summary;
            public JToken p_customization_json;
            public JToken p_metadata;
        }

        [Serializable]
        private sealed class PlayerRuntimeStatePayload
        {
            public string p_player_key;
            public string p_scene_name;
            public double? p_current_health;
            public double? p_max_health;
            public JToken p_position;
            public JToken p_status_flags;
            public JToken p_metadata;
        }

        [Serializable]
        private sealed class NpcProfilePayload
        {
            public string p_npc_key;
            public string p_display_name;
            public string p_bio;
            public string p_profile_asset_key;
            public JToken p_metadata;
        }

        [Serializable]
        private sealed class OpenSessionPayload
        {
            public string p_player_key;
            public string p_npc_key;
            public string p_conversation_key;
            public string p_scene_name;
            public JToken p_metadata;
        }

        [Serializable]
        private sealed class AppendMessagePayload
        {
            public Guid p_session_id;
            public string p_player_key;
            public string p_npc_key;
            public string p_speaker_role;
            public string p_speaker_key;
            public string p_content;
            public JToken p_metadata;
        }

        [Serializable]
        private sealed class MemoryJobPayload
        {
            public string p_player_key;
            public string p_npc_key;
            public Guid p_session_id;
            public string p_job_type;
            public JToken p_payload;
        }

        [Header("Supabase")]
        [SerializeField] private bool m_EnablePersistence = true;
        [SerializeField] private string m_BaseUrl = "http://127.0.0.1:54321";
        [SerializeField] private string m_ServiceKey = string.Empty;
        [SerializeField] private string m_ServiceKeyEnvironmentVariable = DefaultServiceRoleEnvVar;
        [SerializeField] private bool m_RequireLoopbackEndpoint = true;
        [SerializeField][Min(1f)] private float m_RequestTimeoutSeconds = 10f;
        [SerializeField][Min(64)] private int m_ExpectedEmbeddingDimensions = DefaultEmbeddingDimensions;

        [Header("Authority")]
        [SerializeField] private bool m_RecordDialogueTurns = true;
        [SerializeField] private bool m_RecordHealthSnapshots = true;
        [SerializeField][Min(0f)] private float m_MinHealthSyncIntervalSeconds = 0.75f;
        [SerializeField] private bool m_SyncNpcProfilesOnServerStart = true;
        [SerializeField][Min(0)] private int m_QueueMemoryJobEveryNpcReplies = 3;
        [SerializeField] private bool m_LogDebug;

        private readonly object m_SessionGate = new object();
        private readonly Dictionary<string, Guid> m_SessionIdByConversationKey = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> m_NpcReplyCountByConversationKey = new(StringComparer.Ordinal);
        private readonly Dictionary<string, float> m_LastHealthSyncAtByPlayerKey = new(StringComparer.OrdinalIgnoreCase);

        private SupabaseRpcClient m_Client;
        private NetworkManager m_SubscribedNetworkManager;
        private bool m_LoggedConfigurationWarning;

        private void Awake()
        {
            m_Client = new SupabaseRpcClient(m_BaseUrl, ResolveServiceKey());
            TryRegisterNetworkCallbacks();
        }

        private void Start()
        {
            TryRegisterNetworkCallbacks();
            if (m_SyncNpcProfilesOnServerStart && CanWriteAuthoritatively())
            {
                RunFireAndForget(SyncNpcProfilesAsync(), "sync_npc_profiles");
            }
        }

        private void OnEnable()
        {
            TryRegisterNetworkCallbacks();
            NetworkDialogueService.OnRawDialogueResponse += HandleRawDialogueResponse;
            CombatHealthV2.OnHealthChanged += HandleHealthChanged;
        }

        private void OnDisable()
        {
            NetworkDialogueService.OnRawDialogueResponse -= HandleRawDialogueResponse;
            CombatHealthV2.OnHealthChanged -= HandleHealthChanged;
            UnregisterNetworkCallbacks();
            ClearRuntimeCaches();
        }

        public Task<JToken> GetRecentDialogueContextAsync(
            string playerKey,
            string npcKey,
            int messageLimit = 6,
            int memoryLimit = 6,
            CancellationToken cancellationToken = default
        )
        {
            EnsureAuthoritativeServerAccess();
            EnsureConfigured();
            return m_Client.InvokeRpcAsync(
                "authoritative_get_recent_dialogue_context",
                new
                {
                    p_player_key = playerKey,
                    p_npc_key = npcKey,
                    p_message_limit = messageLimit,
                    p_memory_limit = memoryLimit,
                },
                cancellationToken
            );
        }

        public Task<JToken> ClaimNextMemoryJobAsync(
            string workerId,
            CancellationToken cancellationToken = default
        )
        {
            EnsureAuthoritativeServerAccess();
            EnsureConfigured();
            return m_Client.InvokeRpcAsync(
                "authoritative_claim_memory_job",
                new
                {
                    p_worker_id = workerId,
                },
                cancellationToken
            );
        }

        public Task<JToken> GetDialogueSessionTranscriptAsync(
            Guid sessionId,
            int messageLimit = 12,
            CancellationToken cancellationToken = default
        )
        {
            EnsureAuthoritativeServerAccess();
            EnsureConfigured();
            return m_Client.InvokeRpcAsync(
                "authoritative_get_dialogue_session_transcript",
                new
                {
                    p_session_id = sessionId,
                    p_message_limit = messageLimit,
                },
                cancellationToken
            );
        }

        public Task<JToken> UpsertDialogueMemoryAsync(
            string playerKey,
            string npcKey,
            Guid sessionId,
            string memoryScope,
            string summary,
            string memoryText,
            int importance,
            float[] embedding = null,
            JToken metadata = null,
            CancellationToken cancellationToken = default
        )
        {
            EnsureAuthoritativeServerAccess();
            EnsureConfigured();
            ValidateEmbeddingLength(embedding);
            return m_Client.InvokeRpcAsync(
                "authoritative_upsert_dialogue_memory",
                new
                {
                    p_player_key = playerKey,
                    p_npc_key = npcKey,
                    p_session_id = sessionId,
                    p_memory_scope = memoryScope,
                    p_summary = summary,
                    p_memory_text = memoryText,
                    p_importance = importance,
                    p_embedding = embedding != null ? JArray.FromObject(embedding) : null,
                    p_metadata = metadata,
                },
                cancellationToken
            );
        }

        public Task<JToken> MatchDialogueMemoriesAsync(
            string playerKey,
            string npcKey,
            float[] queryEmbedding,
            int matchCount = 4,
            float matchThreshold = 0.72f,
            CancellationToken cancellationToken = default
        )
        {
            EnsureAuthoritativeServerAccess();
            EnsureConfigured();
            ValidateEmbeddingLength(queryEmbedding);
            return m_Client.InvokeRpcAsync(
                "authoritative_match_dialogue_memories",
                new
                {
                    p_player_key = playerKey,
                    p_npc_key = npcKey,
                    p_query_embedding = queryEmbedding != null ? JArray.FromObject(queryEmbedding) : null,
                    p_match_count = matchCount,
                    p_match_threshold = matchThreshold,
                },
                cancellationToken
            );
        }

        public Task<JToken> SearchNpcKnowledgeAsync(
            string npcKey,
            float[] queryEmbedding,
            int matchCount = 3,
            float matchThreshold = 0.60f,
            CancellationToken cancellationToken = default
        )
        {
            EnsureAuthoritativeServerAccess();
            EnsureConfigured();
            ValidateEmbeddingLength(queryEmbedding);
            return m_Client.InvokeRpcAsync(
                "authoritative_search_npc_knowledge",
                new
                {
                    p_npc_key         = npcKey,
                    p_query_embedding = queryEmbedding != null ? JArray.FromObject(queryEmbedding) : null,
                    p_match_count     = matchCount,
                    p_match_threshold = matchThreshold,
                },
                cancellationToken
            );
        }

        public async Task<int> AwardPlayerXpAsync(
            string playerKey,
            int xpDelta,
            string reason = "quiz_answer",
            CancellationToken cancellationToken = default
        )
        {
            EnsureAuthoritativeServerAccess();
            EnsureConfigured();
            JToken result = await m_Client.InvokeRpcAsync(
                "authoritative_award_player_xp",
                new
                {
                    p_player_key = playerKey,
                    p_xp_delta   = xpDelta,
                    p_reason     = reason ?? "quiz_answer",
                },
                cancellationToken
            );
            return SupabaseRpcClient.ReadInt(result, "authoritative_award_player_xp");
        }

        public Task<JToken> UpdateMemoryJobStatusAsync(
            Guid jobId,
            string status,
            string error = null,
            JToken payloadPatch = null,
            CancellationToken cancellationToken = default
        )
        {
            EnsureAuthoritativeServerAccess();
            EnsureConfigured();
            return m_Client.InvokeRpcAsync(
                "authoritative_update_memory_job_status",
                new
                {
                    p_job_id = jobId,
                    p_status = status,
                    p_error = error,
                    p_payload_patch = payloadPatch,
                },
                cancellationToken
            );
        }

        public Task<JToken> RequeueStaleMemoryJobsAsync(
            int staleAfterSeconds,
            CancellationToken cancellationToken = default
        )
        {
            EnsureAuthoritativeServerAccess();
            EnsureConfigured();
            return m_Client.InvokeRpcAsync(
                "authoritative_requeue_stale_memory_jobs",
                new
                {
                    p_stale_after_seconds = staleAfterSeconds,
                },
                cancellationToken
            );
        }

        private void HandleRawDialogueResponse(NetworkDialogueService.DialogueResponse response)
        {
            if (!m_RecordDialogueTurns || response.Status != NetworkDialogueService.DialogueStatus.Completed)
            {
                return;
            }

            if (!CanWriteAuthoritatively())
            {
                return;
            }

            RunFireAndForget(PersistDialogueResponseAsync(response), "persist_dialogue_response");
        }

        private void HandleHealthChanged(
            CombatHealthV2 health,
            CombatHealthV2.HealthChangedEvent changeEvent
        )
        {
            if (!m_RecordHealthSnapshots || !CanWriteAuthoritatively() || health == null)
            {
                return;
            }

            if (!TryBuildPlayerDescriptor(health, out PlayerDescriptor player))
            {
                return;
            }

            float now = Time.realtimeSinceStartup;
            if (
                m_LastHealthSyncAtByPlayerKey.TryGetValue(player.PlayerKey, out float lastSync)
                && now - lastSync < m_MinHealthSyncIntervalSeconds
                && changeEvent.CurrentHealth > 0f
                && changeEvent.CurrentHealth < changeEvent.MaxHealth
            )
            {
                return;
            }

            m_LastHealthSyncAtByPlayerKey[player.PlayerKey] = now;
            RunFireAndForget(
                SyncPlayerRuntimeStateAsync(health, player, changeEvent.CurrentHealth, changeEvent.MaxHealth),
                "sync_player_runtime_state"
            );
        }

        private async Task PersistDialogueResponseAsync(NetworkDialogueService.DialogueResponse response)
        {
            try
            {
                EnsureConfigured();

                if (!TryResolveDialogueParticipants(response.Request, out PlayerDescriptor player, out NpcDescriptor npc))
                {
                    return;
                }

                await UpsertPlayerProfileAsync(player);
                await UpsertNpcProfileAsync(npc);

                string conversationKey = BuildConversationKey(response.Request);
                Guid sessionId = await GetOrOpenSessionAsync(conversationKey, player, npc, response.Request);

                await AppendDialogueMessageAsync(sessionId, player.PlayerKey, npc.NpcKey, "player", player.PlayerKey, response.Request.Prompt, BuildRequestMetadata(response));
                await AppendDialogueMessageAsync(sessionId, player.PlayerKey, npc.NpcKey, "npc", npc.NpcKey, response.ResponseText, BuildResponseMetadata(response));

                int npcReplyCount = IncrementNpcReplyCount(conversationKey);
                if (m_QueueMemoryJobEveryNpcReplies > 0 && npcReplyCount % m_QueueMemoryJobEveryNpcReplies == 0)
                {
                    await EnqueueMemoryJobAsync(sessionId, player.PlayerKey, npc.NpcKey, conversationKey, npcReplyCount);
                }
            }
            catch (Exception ex)
            {
                NGLog.Warn(Category, $"Dialogue persistence failed: {ex.Message}", this);
            }
        }

        private async Task SyncPlayerRuntimeStateAsync(
            CombatHealthV2 health,
            PlayerDescriptor player,
            float currentHealth,
            float maxHealth
        )
        {
            try
            {
                EnsureConfigured();
                await UpsertPlayerProfileAsync(player);

                JToken position = ToJToken(new PositionPayload
                {
                    x = health.transform.position.x,
                    y = health.transform.position.y,
                    z = health.transform.position.z,
                });

                JToken flags = ToJToken(new StatusFlagsPayload
                {
                    dead = currentHealth <= 0f,
                });

                _ = await InvokeAsync(
                    "authoritative_upsert_player_runtime_state",
                    new PlayerRuntimeStatePayload
                    {
                        p_player_key = player.PlayerKey,
                        p_scene_name = SceneManager.GetActiveScene().name,
                        p_current_health = currentHealth,
                        p_max_health = maxHealth,
                        p_position = position,
                        p_status_flags = flags,
                        p_metadata = ToJToken(new { source = nameof(DialoguePersistenceGateway) }),
                    }
                );
            }
            catch (Exception ex)
            {
                NGLog.Warn(Category, $"Runtime-state sync failed: {ex.Message}", this);
            }
        }

        private async Task SyncNpcProfilesAsync()
        {
            try
            {
                EnsureConfigured();
                NpcDialogueActor[] actors = FindObjectsByType<NpcDialogueActor>(FindObjectsInactive.Exclude);
                for (int i = 0; i < actors.Length; i++)
                {
                    NpcDialogueActor actor = actors[i];
                    if (actor == null)
                    {
                        continue;
                    }

                    NpcDescriptor npc = BuildNpcDescriptor(actor);
                    await UpsertNpcProfileAsync(npc);
                }
            }
            catch (Exception ex)
            {
                NGLog.Warn(Category, $"NPC profile sync failed: {ex.Message}", this);
            }
        }

        private async Task UpsertPlayerProfileAsync(PlayerDescriptor player)
        {
            _ = await InvokeAsync(
                "authoritative_upsert_player_profile",
                new PlayerProfilePayload
                {
                    p_player_key = player.PlayerKey,
                    p_player_handle = player.PlayerHandle,
                    p_bio = null,
                    p_long_term_summary = null,
                    p_customization_json = player.CustomizationJson,
                    p_metadata = ToJToken(new { source = nameof(DialoguePersistenceGateway) }),
                }
            );
        }

        private async Task UpsertNpcProfileAsync(NpcDescriptor npc)
        {
            _ = await InvokeAsync(
                "authoritative_upsert_npc_profile",
                new NpcProfilePayload
                {
                    p_npc_key = npc.NpcKey,
                    p_display_name = npc.DisplayName,
                    p_bio = npc.Bio,
                    p_profile_asset_key = npc.ProfileAssetKey,
                    p_metadata = ToJToken(new { source = nameof(DialoguePersistenceGateway) }),
                }
            );
        }

        private async Task<Guid> GetOrOpenSessionAsync(
            string conversationKey,
            PlayerDescriptor player,
            NpcDescriptor npc,
            NetworkDialogueService.DialogueRequest request
        )
        {
            lock (m_SessionGate)
            {
                if (m_SessionIdByConversationKey.TryGetValue(conversationKey, out Guid existing))
                {
                    return existing;
                }
            }

            JToken result = await InvokeAsync(
                "authoritative_open_dialogue_session",
                new OpenSessionPayload
                {
                    p_player_key = player.PlayerKey,
                    p_npc_key = npc.NpcKey,
                    p_conversation_key = conversationKey,
                    p_scene_name = SceneManager.GetActiveScene().name,
                    p_metadata = ToJToken(new
                    {
                        source = nameof(DialoguePersistenceGateway),
                        speaker_network_id = request.SpeakerNetworkId,
                        listener_network_id = request.ListenerNetworkId,
                        requesting_client_id = request.RequestingClientId,
                    }),
                }
            );

            string rawSessionId = SupabaseRpcClient.ReadString(result, "session_id");
            Guid sessionId = Guid.TryParse(rawSessionId, out Guid parsed) ? parsed : Guid.Empty;
            if (sessionId == Guid.Empty)
            {
                throw new InvalidOperationException("Supabase did not return a valid session_id.");
            }

            lock (m_SessionGate)
            {
                m_SessionIdByConversationKey[conversationKey] = sessionId;
            }

            return sessionId;
        }

        private async Task AppendDialogueMessageAsync(
            Guid sessionId,
            string playerKey,
            string npcKey,
            string speakerRole,
            string speakerKey,
            string content,
            JToken metadata
        )
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return;
            }

            _ = await InvokeAsync(
                "authoritative_append_dialogue_message",
                new AppendMessagePayload
                {
                    p_session_id = sessionId,
                    p_player_key = playerKey,
                    p_npc_key = npcKey,
                    p_speaker_role = speakerRole,
                    p_speaker_key = speakerKey,
                    p_content = content,
                    p_metadata = metadata,
                }
            );
        }

        private async Task EnqueueMemoryJobAsync(
            Guid sessionId,
            string playerKey,
            string npcKey,
            string conversationKey,
            int npcReplyCount
        )
        {
            _ = await InvokeAsync(
                "authoritative_enqueue_memory_job",
                new MemoryJobPayload
                {
                    p_player_key = playerKey,
                    p_npc_key = npcKey,
                    p_session_id = sessionId,
                    p_job_type = "summarize_turns",
                    p_payload = ToJToken(new
                    {
                        conversation_key = conversationKey,
                        npc_reply_count = npcReplyCount,
                        source = nameof(DialoguePersistenceGateway),
                    }),
                }
            );
        }

        private async Task<JToken> InvokeAsync<TPayload>(string functionName, TPayload payload)
        {
            EnsureAuthoritativeServerAccess();
            using CancellationTokenSource cts = new CancellationTokenSource(
                TimeSpan.FromSeconds(Mathf.Max(1f, m_RequestTimeoutSeconds))
            );
            return await m_Client.InvokeRpcAsync(functionName, payload, cts.Token);
        }

        private bool TryResolveDialogueParticipants(
            NetworkDialogueService.DialogueRequest request,
            out PlayerDescriptor player,
            out NpcDescriptor npc
        )
        {
            player = default;
            npc = default;

            NetworkDialogueService service = NetworkDialogueService.Instance;
            if (service == null)
            {
                return false;
            }

            if (!TryBuildPlayerDescriptor(request, service, out player))
            {
                return false;
            }

            npc = BuildNpcDescriptor(ResolveNpcActor(request.SpeakerNetworkId));
            return !string.IsNullOrWhiteSpace(npc.NpcKey);
        }

        private bool TryBuildPlayerDescriptor(
            NetworkDialogueService.DialogueRequest request,
            NetworkDialogueService service,
            out PlayerDescriptor descriptor
        )
        {
            descriptor = default;
            if (service.TryGetPlayerIdentityByClientId(request.RequestingClientId, out var byClient))
            {
                descriptor = BuildPlayerDescriptor(byClient.NameId, byClient.CustomizationJson, request.RequestingClientId);
                return true;
            }

            if (service.TryGetPlayerIdentityByNetworkId(request.ListenerNetworkId, out var byNetwork))
            {
                descriptor = BuildPlayerDescriptor(byNetwork.NameId, byNetwork.CustomizationJson, request.RequestingClientId);
                return true;
            }

            PlayerGameData data = PlayerDataManager.Instance?.GetPlayerData(request.RequestingClientId);
            if (data != null)
            {
                descriptor = new PlayerDescriptor(data.PlayerId, data.PlayerName, null);
                return true;
            }

            return false;
        }

        private bool TryBuildPlayerDescriptor(CombatHealthV2 health, out PlayerDescriptor descriptor)
        {
            descriptor = default;
            NetworkObject netObj = ResolveOwningNetworkObject(health);
            if (netObj == null)
            {
                return false;
            }

            NetworkDialogueService service = NetworkDialogueService.Instance;
            if (service != null && service.TryGetPlayerIdentityByClientId(netObj.OwnerClientId, out var snapshot))
            {
                descriptor = BuildPlayerDescriptor(snapshot.NameId, snapshot.CustomizationJson, netObj.OwnerClientId);
                return true;
            }

            PlayerGameData data = PlayerDataManager.Instance?.GetPlayerData(netObj.OwnerClientId);
            if (data != null)
            {
                descriptor = new PlayerDescriptor(data.PlayerId, data.PlayerName, null);
                return true;
            }

            return false;
        }

        private static PlayerDescriptor BuildPlayerDescriptor(
            string nameId,
            string customizationJson,
            ulong clientId
        )
        {
            string playerKey = !string.IsNullOrWhiteSpace(nameId) ? nameId.Trim() : $"player_{clientId}";
            JToken customization = TryParseJson(customizationJson);
            return new PlayerDescriptor(playerKey, playerKey, customization);
        }

        private static NpcDescriptor BuildNpcDescriptor(NpcDialogueActor actor)
        {
            if (actor == null)
            {
                return default;
            }

            NpcDialogueProfile profile = actor.Profile;
            string npcKey = !string.IsNullOrWhiteSpace(actor.ProfileId) ? actor.ProfileId : actor.gameObject.name;
            string displayName = profile != null && !string.IsNullOrWhiteSpace(profile.DisplayName)
                ? profile.DisplayName.Trim()
                : actor.gameObject.name;
            string bio = profile != null && !string.IsNullOrWhiteSpace(profile.Lore) ? profile.Lore : null;
            string profileAssetKey = profile != null ? profile.name : null;
            return new NpcDescriptor(npcKey, displayName, bio, profileAssetKey);
        }

        private static NpcDialogueActor ResolveNpcActor(ulong networkObjectId)
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager?.SpawnManager == null)
            {
                return null;
            }

            return manager.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out NetworkObject netObj)
                ? netObj.GetComponent<NpcDialogueActor>()
                : null;
        }

        private static NetworkObject ResolveOwningNetworkObject(CombatHealthV2 health)
        {
            if (health == null)
            {
                return null;
            }

            if (health.CachedNetworkObject != null)
            {
                return health.CachedNetworkObject;
            }

            return health.GetComponentInParent<NetworkObject>();
        }

        private string ResolveServiceKey()
        {
            if (!string.IsNullOrWhiteSpace(m_ServiceKey))
            {
                return m_ServiceKey.Trim();
            }

            string envName = string.IsNullOrWhiteSpace(m_ServiceKeyEnvironmentVariable)
                ? DefaultServiceRoleEnvVar
                : m_ServiceKeyEnvironmentVariable.Trim();
            string configured = Environment.GetEnvironmentVariable(envName);
            if (!string.IsNullOrWhiteSpace(configured))
            {
                return configured.Trim();
            }

            if (
                !string.Equals(envName, LegacyServiceRoleEnvVar, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(LegacyServiceRoleEnvVar))
            )
            {
                return Environment.GetEnvironmentVariable(LegacyServiceRoleEnvVar)?.Trim() ?? string.Empty;
            }

            string secretKey = Environment.GetEnvironmentVariable(SecretKeyEnvVar);
            if (!string.IsNullOrWhiteSpace(secretKey))
            {
                return secretKey.Trim();
            }

            return string.Empty;
        }

        private void EnsureConfigured()
        {
            if (!m_EnablePersistence)
            {
                throw new InvalidOperationException("Dialogue persistence is disabled.");
            }

            if (!IsLoopbackAllowed())
            {
                throw new InvalidOperationException("Supabase base URL must stay on loopback for this gateway.");
            }

            if (m_Client != null && m_Client.IsConfigured)
            {
                return;
            }

            m_Client = new SupabaseRpcClient(m_BaseUrl, ResolveServiceKey());
            if (m_Client.IsConfigured)
            {
                return;
            }

            if (!m_LoggedConfigurationWarning)
            {
                m_LoggedConfigurationWarning = true;
                NGLog.Warn(Category, "Supabase persistence is enabled but no service key is configured.", this);
            }

            throw new InvalidOperationException("Supabase service key is missing.");
        }

        private bool CanWriteAuthoritatively()
        {
            NetworkManager manager = NetworkManager.Singleton;
            return m_EnablePersistence && manager != null && manager.IsServer;
        }

        private void EnsureAuthoritativeServerAccess()
        {
            if (!CanWriteAuthoritatively())
            {
                throw new InvalidOperationException(
                    "Dialogue persistence RPC access is restricted to the authoritative server."
                );
            }
        }

        private void TryRegisterNetworkCallbacks()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || ReferenceEquals(manager, m_SubscribedNetworkManager))
            {
                return;
            }

            UnregisterNetworkCallbacks();
            manager.OnServerStarted += HandleServerStarted;
            manager.OnServerStopped += HandleServerStopped;
            m_SubscribedNetworkManager = manager;
        }

        private void UnregisterNetworkCallbacks()
        {
            if (m_SubscribedNetworkManager == null)
            {
                return;
            }

            m_SubscribedNetworkManager.OnServerStarted -= HandleServerStarted;
            m_SubscribedNetworkManager.OnServerStopped -= HandleServerStopped;
            m_SubscribedNetworkManager = null;
        }

        private void HandleServerStarted()
        {
            ClearRuntimeCaches();
            if (m_SyncNpcProfilesOnServerStart && CanWriteAuthoritatively())
            {
                RunFireAndForget(SyncNpcProfilesAsync(), "sync_npc_profiles");
            }
        }

        private void HandleServerStopped(bool _)
        {
            ClearRuntimeCaches();
        }

        private void ClearRuntimeCaches()
        {
            lock (m_SessionGate)
            {
                m_SessionIdByConversationKey.Clear();
                m_NpcReplyCountByConversationKey.Clear();
                m_LastHealthSyncAtByPlayerKey.Clear();
            }
        }

        private bool IsLoopbackAllowed()
        {
            if (!m_RequireLoopbackEndpoint)
            {
                return true;
            }

            if (!Uri.TryCreate(m_BaseUrl, UriKind.Absolute, out Uri uri))
            {
                return false;
            }

            return string.Equals(uri.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase);
        }

        private void ValidateEmbeddingLength(float[] embedding)
        {
            if (embedding == null)
            {
                return;
            }

            int expected = Mathf.Max(64, m_ExpectedEmbeddingDimensions);
            if (embedding.Length != expected)
            {
                throw new InvalidOperationException(
                    $"Embedding length {embedding.Length} does not match dialogue-memory schema dimension {expected}."
                );
            }
        }

        private int IncrementNpcReplyCount(string conversationKey)
        {
            lock (m_SessionGate)
            {
                m_NpcReplyCountByConversationKey.TryGetValue(conversationKey, out int count);
                count++;
                m_NpcReplyCountByConversationKey[conversationKey] = count;
                return count;
            }
        }

        private static string BuildConversationKey(NetworkDialogueService.DialogueRequest request)
        {
            return !string.IsNullOrWhiteSpace(request.ConversationKey)
                ? request.ConversationKey.Trim()
                : $"{request.ListenerNetworkId}:{request.SpeakerNetworkId}";
        }

        private static JToken BuildRequestMetadata(NetworkDialogueService.DialogueResponse response)
        {
            return ToJToken(new
            {
                source = nameof(DialoguePersistenceGateway),
                request_id = response.RequestId,
                client_request_id = response.Request.ClientRequestId,
                requesting_client_id = response.Request.RequestingClientId,
                is_user_initiated = response.Request.IsUserInitiated,
            });
        }

        private static JToken BuildResponseMetadata(NetworkDialogueService.DialogueResponse response)
        {
            return ToJToken(new
            {
                source = nameof(DialoguePersistenceGateway),
                request_id = response.RequestId,
                client_request_id = response.Request.ClientRequestId,
                status = response.Status.ToString(),
            });
        }

        private static JToken ToJToken<T>(T value)
        {
            return value == null ? JValue.CreateNull() : JToken.FromObject(value);
        }

        private static JToken TryParseJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            try
            {
                return JToken.Parse(json);
            }
            catch
            {
                return null;
            }
        }

        private void RunFireAndForget(Task task, string operation)
        {
            _ = task.ContinueWith(
                t =>
                {
                    if (t.Exception != null)
                    {
                        NGLog.Warn(Category, $"{operation} failed: {t.Exception.GetBaseException().Message}", this);
                    }
                    else if (m_LogDebug)
                    {
                        NGLog.Debug(Category, $"{operation} completed", this);
                    }
                },
                TaskScheduler.FromCurrentSynchronizationContext()
            );
        }

        private readonly struct PlayerDescriptor
        {
            public PlayerDescriptor(string playerKey, string playerHandle, JToken customizationJson)
            {
                PlayerKey = playerKey;
                PlayerHandle = playerHandle;
                CustomizationJson = customizationJson;
            }

            public string PlayerKey { get; }
            public string PlayerHandle { get; }
            public JToken CustomizationJson { get; }
        }

        private readonly struct NpcDescriptor
        {
            public NpcDescriptor(string npcKey, string displayName, string bio, string profileAssetKey)
            {
                NpcKey = npcKey;
                DisplayName = displayName;
                Bio = bio;
                ProfileAssetKey = profileAssetKey;
            }

            public string NpcKey { get; }
            public string DisplayName { get; }
            public string Bio { get; }
            public string ProfileAssetKey { get; }
        }

        private struct PositionPayload
        {
            public float x;
            public float y;
            public float z;
        }

        private struct StatusFlagsPayload
        {
            public bool dead;
        }
    }
}
