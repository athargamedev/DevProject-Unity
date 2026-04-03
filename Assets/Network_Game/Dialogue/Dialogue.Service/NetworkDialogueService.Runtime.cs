using Network_Game.Combat;
using Network_Game.Diagnostics;
using Network_Game.Dialogue.Effects;
using UnityEngine;

namespace Network_Game.Dialogue
{
    public partial class NetworkDialogueService
    {
        // MonoBehaviour lifecycle, backend config, and scene-level bootstrapping state.
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                NGLog.Warn("Dialogue", $"Duplicate instance detected. Disabling. ({this})");
                enabled = false;
                return;
            }

            Instance = this;
            DialoguePromptContextBridgeRegistry.Register(this);
            CacheDialogueBackendConfig();
            NormalizeRemoteRuntimeTuning();
            NGLog.Info("Dialogue", $"NetworkDialogueService initialized. ({this})");
            m_DefaultSystemPrompt = GetConfiguredSystemPrompt();

            ValidateLlmConfiguration();

            if (m_SceneEffectsController == null)
            {
#if UNITY_2023_1_OR_NEWER
                m_SceneEffectsController = FindAnyObjectByType<DialogueSceneEffectsController>(
                    FindObjectsInactive.Exclude
                );
#else
                m_SceneEffectsController = FindObjectOfType<DialogueSceneEffectsController>();
#endif
            }

            RebuildPlayerPromptContextLookup();
            RebuildPlayerIdentityLookup();
            SynthesizeIdentityBindingsFromRuntimeBindings();
        }

        private void CacheDialogueBackendConfig()
        {
            GetDialogueBackendConfig();
        }

        private DialogueBackendConfig GetDialogueBackendConfig()
        {
            if (m_DialogueBackendConfig == null)
            {
                m_DialogueBackendConfig = GetComponent<DialogueBackendConfig>();
            }

            return m_DialogueBackendConfig;
        }

        private string GetConfiguredSystemPrompt()
        {
            DialogueBackendConfig backendConfig = GetDialogueBackendConfig();
            return backendConfig != null ? backendConfig.SystemPrompt : string.Empty;
        }

        private void SetConfiguredSystemPrompt(string prompt)
        {
            string resolvedPrompt = prompt ?? string.Empty;
            DialogueBackendConfig backendConfig = GetDialogueBackendConfig();
            if (backendConfig != null)
            {
                backendConfig.SetSystemPrompt(resolvedPrompt);
            }
        }

        private string GetRemoteEndpointLabel()
        {
            DialogueBackendConfig backendConfig = GetDialogueBackendConfig();
            if (backendConfig != null)
            {
                return $"{backendConfig.Host}:{backendConfig.Port}";
            }

            return $"{DefaultRemoteHost}:{DefaultRemotePort}";
        }

        private string ResolveActiveInferenceBackendName()
        {
            return "openai-compatible-remote";
        }

        private void EnsureSceneEffectsController()
        {
            if (m_SceneEffectsController != null)
            {
                return;
            }
#if UNITY_2023_1_OR_NEWER
            m_SceneEffectsController = FindAnyObjectByType<DialogueSceneEffectsController>(
                FindObjectsInactive.Exclude
            );
#else
            m_SceneEffectsController = FindObjectOfType<DialogueSceneEffectsController>();
#endif
        }

        private void EnsurePlayerDissolveController()
        {
            if (m_PlayerDissolveController != null)
            {
                return;
            }
#if UNITY_2023_1_OR_NEWER
            m_PlayerDissolveController = FindAnyObjectByType<PlayerDissolveController>(
                FindObjectsInactive.Exclude
            );
#else
            m_PlayerDissolveController = FindObjectOfType<PlayerDissolveController>();
#endif
        }

        private void EnsureSurfaceMaterialEffectController()
        {
            if (m_SurfaceMaterialEffectController != null)
            {
                return;
            }
#if UNITY_2023_1_OR_NEWER
            m_SurfaceMaterialEffectController = FindAnyObjectByType<SurfaceMaterialEffectController>(
                FindObjectsInactive.Exclude
            );
#else
            m_SurfaceMaterialEffectController = FindObjectOfType<SurfaceMaterialEffectController>();
#endif
        }

        private void NormalizeRemoteRuntimeTuning()
        {
            m_RemoteMaxHistoryMessages = Mathf.Clamp(m_RemoteMaxHistoryMessages, 0, 32);
            m_RemoteHistoryHardCapMessages = Mathf.Clamp(m_RemoteHistoryHardCapMessages, 1, 64);
            if (m_RemoteHistoryHardCapMessages < m_RemoteMaxHistoryMessages)
            {
                m_RemoteHistoryHardCapMessages = m_RemoteMaxHistoryMessages;
            }

            m_RemoteHistoryMessageCharBudget = Mathf.Clamp(
                m_RemoteHistoryMessageCharBudget,
                64,
                2048
            );
            m_RemoteUserPromptCharBudget = Mathf.Clamp(m_RemoteUserPromptCharBudget, 64, 2048);
            m_RemoteDialogueResponseMaxTokens = Mathf.Clamp(
                m_RemoteDialogueResponseMaxTokens,
                32,
                1024
            );
            m_RemoteSystemPromptCharBudget = Mathf.Clamp(
                m_RemoteSystemPromptCharBudget,
                512,
                24000
            );
            m_RemoteSystemPromptHardCapChars = Mathf.Clamp(
                m_RemoteSystemPromptHardCapChars,
                512,
                24000
            );
            if (m_RemoteSystemPromptHardCapChars < m_RemoteSystemPromptCharBudget)
            {
                m_RemoteSystemPromptHardCapChars = m_RemoteSystemPromptCharBudget;
            }
        }

        private void ValidateLlmConfiguration()
        {
            if (m_LogDebug)
            {
                NGLog.Info(
                    "Dialogue",
                    HasDialogueBackendConfig
                        ? "Dialogue backend configured for remote LM Studio inference."
                        : "Dialogue backend will use built-in remote defaults."
                );
            }
        }

        private void Update()
        {
            if (!IsServer)
            {
                return;
            }

            float now = Time.realtimeSinceStartup;
            if (now - m_LastStuckCheckTime < kStuckCheckInterval)
            {
                return;
            }
            m_LastStuckCheckTime = now;
            RecoverStuckConversations(now);
        }

        private void RecoverStuckConversations(float now)
        {
            // Snapshot keys to avoid modifying collection during iteration.
            m_ConversationKeysCache.Clear();
            m_ConversationKeysCache.AddRange(m_ConversationStates.Keys);
            for (int i = 0; i < m_ConversationKeysCache.Count; i++)
            {
                string conversationKey = m_ConversationKeysCache[i];
                if (
                    !m_ConversationStates.TryGetValue(
                        conversationKey,
                        out ConversationState conversationState
                    )
                )
                {
                    continue;
                }

                if (!conversationState.IsInFlight)
                {
                    continue;
                }

                int activeId = conversationState.ActiveRequestId;
                if (activeId < 0)
                {
                    conversationState.IsInFlight = false;
                    conversationState.ActiveRequestId = -1;
                    NGLog.Warn(
                        "Dialogue",
                        NGLog.Format(
                            "Recovered orphaned conversation (no active request)",
                            ("key", conversationKey)
                        )
                    );
                    continue;
                }

                if (!m_Requests.TryGetValue(activeId, out DialogueRequestState reqState))
                {
                    conversationState.IsInFlight = false;
                    conversationState.ActiveRequestId = -1;
                    NGLog.Warn(
                        "Dialogue",
                        NGLog.Format(
                            "Recovered orphaned conversation (request state missing)",
                            ("key", conversationKey),
                            ("activeId", activeId)
                        )
                    );
                    continue;
                }

                float elapsed = now - reqState.EnqueuedAt;
                float requestTimeoutSeconds =
                    reqState.EffectiveTimeoutSeconds > 0f
                        ? reqState.EffectiveTimeoutSeconds
                        : (
                            UseOpenAIRemote
                                ? Mathf.Max(
                                    m_RequestTimeoutSeconds,
                                    m_RemoteMinRequestTimeoutSeconds
                                )
                                : m_RequestTimeoutSeconds
                        );
                float stuckTimeoutSeconds = Mathf.Max(
                    kStuckConversationTimeout,
                    requestTimeoutSeconds + 20f
                );
                if (elapsed > stuckTimeoutSeconds)
                {
                    reqState.Status = DialogueStatus.Failed;
                    reqState.Error = "Request timed out (stuck recovery).";
                    CompleteConversationRequest(activeId, reqState, false);
                    NotifyIfRequested(activeId, reqState);
                    NGLog.Warn(
                        "Dialogue",
                        NGLog.Format(
                            "Force-recovered stuck request",
                            ("id", activeId),
                            ("key", conversationKey),
                            ("elapsed", elapsed),
                            ("stuckTimeout", stuckTimeoutSeconds)
                        )
                    );
                }
            }
        }

        public override void OnNetworkSpawn()
        {
            // Re-assert Instance when NGO completes the network spawn.
            // This handles the case where NGO destroys and re-creates the scene-placed
            // NetworkObject during scene reconciliation (GlobalObjectIdHash mismatch
            // recovery), which would have cleared Instance in OnDestroy.
            if (Instance == null || Instance == this)
            {
                Instance = this;
            }

            base.OnNetworkSpawn();
        }

        public override void OnDestroy()
        {
            DialoguePromptContextBridgeRegistry.Unregister(this);
            if (Instance == this)
            {
                Instance = null;
            }

            base.OnDestroy();
        }
    }
}
