using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Network_Game.Auth;
using Network_Game.Combat;
using Network_Game.Core;
using Network_Game.Diagnostics;
using Network_Game.Dialogue.Effects;

using Network_Game.Dialogue.Persistence;
using Newtonsoft.Json.Linq;
using Unity.Netcode;
using UnityEngine;
using ChatMessage = Network_Game.Dialogue.DialogueHistoryEntry;
using NGLogLevel = Network_Game.Diagnostics.LogLevel;

namespace Network_Game.Dialogue
{
    /// <summary>
    /// Server-authoritative dialogue service that routes remote dialogue requests and stores per-conversation history.
    /// </summary>
    [DefaultExecutionOrder(-450)]
    public partial class NetworkDialogueService : NetworkBehaviour, IDialoguePromptContextBridge
    {
        private const string DialogueCategory = "Dialogue";

        [Serializable]
        public struct DialogueRequest
        {
            public string Prompt;
            public string ConversationKey;
            public ulong SpeakerNetworkId;
            public ulong ListenerNetworkId;
            public ulong RequestingClientId;
            public bool Broadcast;
            public float BroadcastDuration;
            public bool NotifyClient;
            public int ClientRequestId;
            public bool IsUserInitiated;
            public bool BlockRepeatedPrompt;
            public float MinRepeatDelaySeconds;
            public bool RequireUserReply;
        }

        private const int GameplayProbeClientRequestIdMin = 99400;
        private const int EffectProbeMaxResponseTokens = 96;
        private const int AnimationProbeMaxResponseTokens = 96;
        private const int AnimationIntentMaxResponseTokens = 128;
        private const int PowerIntentMaxResponseTokens = 220;
        private const string DefaultRemoteHost = "127.0.0.1";
        private const int DefaultRemotePort = 7002;
        private static int s_NextDiagnosticActionOrdinal;

        public enum DialogueStatus
        {
            Pending,
            InProgress,
            Completed,
            Failed,
            Cancelled,
        }

        public struct DialogueResponse
        {
            public int RequestId;
            public DialogueStatus Status;
            public string ResponseText;
            public string Error;
            public DialogueRequest Request;
        }

        public struct DialogueResponseTelemetry
        {
            public int RequestId;
            public DialogueStatus Status;
            public string Error;
            public DialogueRequest Request;
            public int RetryCount;
            public float QueueLatencyMs;
            public float ModelLatencyMs;
            public float TotalLatencyMs;
        }

        public struct DialogueFlowTraceEvent
        {
            public string FlowId;
            public string EventName;
            public string Phase;
            public int RequestId;
            public int ClientRequestId;
            public ulong ClientId;
            public ulong SpeakerNetworkId;
            public ulong ListenerNetworkId;
            public string ConversationKey;
            public DialogueStatus Status;
            public bool Success;
            public string Error;
            public float TimestampMs;
        }

        public struct PlayerIdentitySnapshot
        {
            public ulong ClientId;
            public ulong PlayerNetworkId;
            public string NameId;
            public string CustomizationJson;
            public string LastUpdatedUtc;
        }

        public struct DialogueStats
        {
            public int PendingCount;
            public int ActiveCount;
            public int HistoryCount;
            public bool HasLlmAgent;
            public bool IsServer;
            public bool IsClient;
            public string WarmupState;
            public bool WarmupInProgress;
            public bool WarmupDegraded;
            public int WarmupFailureCount;
            public float WarmupRetryInSeconds;
            public string WarmupLastFailureReason;
            public int TotalTerminalCompleted;
            public int TotalTerminalFailed;
            public int TotalTerminalCancelled;
            public int TotalTerminalRejected;
            public int TotalRequestsEnqueued;
            public int TotalRequestsFinished;
            public int TimeoutCount;
            public float TimeoutRate;
            public float SuccessRate;
            public LatencyHistogram QueueWaitHistogram;
            public LatencyHistogram ModelExecutionHistogram;
            public KeyValuePair<string, int>[] RejectionReasonCounts;
        }

        [Serializable]
        public struct LatencyHistogram
        {
            public int SampleCount;
            public float TotalMs;
            public float MinMs;
            public float MaxMs;
            public float P50Ms;
            public float P95Ms;
            public int Under100Ms;
            public int Under250Ms;
            public int Under500Ms;
            public int Under1000Ms;
            public int Under2000Ms;
            public int Over2000Ms;
        }

        private class DialogueRequestState
        {
            public DialogueRequest Request;
            public string FlowId;
            public DialogueStatus Status;
            public string ResponseText;
            public string Error;
            public float EnqueuedAt;
            public float StartedAt;
            public float InferenceCompletedAt;
            public float EffectiveTimeoutSeconds;
            public int RetryCount;
            public float FirstAttemptAt = float.MinValue;
            public float NextAttemptAt;
            public bool CompletionIssued;
        }

        private class ConversationState
        {
            public bool HasOutstandingRequest;
            public int OutstandingRequestId = -1;
            public bool IsInFlight;
            public int ActiveRequestId = -1;
            public string LastCompletedPrompt;
            public float LastCompletedAt = float.MinValue;
            public bool AwaitingUserInput;
            public int UserMessageCount;
            public int AssistantMessageCount;
        }

        [Serializable]
        private class PlayerPromptContextBinding
        {
            public ulong PlayerNetworkId;
            public string NameId = string.Empty;
            public string CustomizationJson = "{}";
            public bool Enabled = true;
        }

        [Serializable]
        private class PlayerIdentityBinding
        {
            public ulong ClientId;
            public ulong PlayerNetworkId;
            public string NameId = string.Empty;
            public string CustomizationJson = "{}";
            public string LastUpdatedUtc = string.Empty;
            public bool Enabled = true;
        }

        private readonly struct ClientRequestLookupKey : IEquatable<ClientRequestLookupKey>
        {
            public readonly int ClientRequestId;
            public readonly ulong RequestingClientId;

            public ClientRequestLookupKey(int clientRequestId, ulong requestingClientId)
            {
                ClientRequestId = clientRequestId;
                RequestingClientId = requestingClientId;
            }

            public bool Equals(ClientRequestLookupKey other)
            {
                return ClientRequestId == other.ClientRequestId
                    && RequestingClientId == other.RequestingClientId;
            }

            public override bool Equals(object obj)
            {
                return obj is ClientRequestLookupKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (ClientRequestId * 397) ^ RequestingClientId.GetHashCode();
                }
            }
        }

        private bool TryDispatchStructuredSpecialEffect(
            DialogueAction action,
            DialogueRequest request,
            ParticleParameterExtractor.ParticleParameterIntent parameterIntent,
            string speechText
        )
        {
            if (action == null || string.IsNullOrWhiteSpace(action.Type))
            {
                return false;
            }

            bool isEffectAction = string.Equals(
                action.Type,
                "EFFECT",
                StringComparison.OrdinalIgnoreCase
            );
            bool isPatchWithoutFields = string.Equals(
                    action.Type,
                    "PATCH",
                    StringComparison.OrdinalIgnoreCase
                )
                && !PatchHasFields(action);
            if (!isEffectAction && !isPatchWithoutFields)
            {
                return false;
            }

            PlayerSpecialEffectMode specialMode = ResolveStructuredActionSpecialEffectMode(
                action,
                speechText
            );
            if (specialMode == PlayerSpecialEffectMode.None)
            {
                return false;
            }

            string targetHint = ResolveStructuredActionTargetHint(action);
            ulong targetNetworkObjectId = ResolveStructuredActionTargetNetworkObjectId(
                action,
                request
            );

            switch (specialMode)
            {
                case PlayerSpecialEffectMode.Dissolve:
                {
                    if (targetNetworkObjectId == 0)
                    {
                        NGLog.Warn(
                            "DialogueFX",
                            NGLog.Format(
                                "Structured special effect skipped (invalid dissolve target)",
                                ("tag", action.Tag ?? string.Empty),
                                ("target", targetHint ?? string.Empty)
                            )
                        );
                        return true;
                    }

                    float durationSeconds = ResolveSpecialEffectDurationSeconds(
                        parameterIntent,
                        5f
                    );
                    string actionId = BuildActionId(
                        request,
                        0,
                        "special_effect",
                        "dissolve",
                        targetNetworkObjectId
                    );
                    RecordActionValidationResult(
                        request,
                        0,
                        actionId,
                        actionKind: "special_effect",
                        actionName: "dissolve",
                        decision: "validated",
                        success: true,
                        requestedTargetHint: targetHint,
                        resolvedTargetNetworkObjectId: targetNetworkObjectId,
                        requestedDuration: durationSeconds,
                        appliedDuration: durationSeconds
                    );
                    RecordReplicationTrace(
                        stage: "rpc_sent",
                        networkPath: "client_rpc",
                        success: true,
                        request,
                        0,
                        actionId: actionId,
                        effectType: "dissolve",
                        effectName: "dissolve",
                        targetNetworkObjectId: targetNetworkObjectId,
                        detail: durationSeconds.ToString("F2")
                    );
                    ApplyDissolveEffectClientRpc(targetNetworkObjectId, durationSeconds, actionId);
                    RecordExecutionTrace(
                        stage: "effect_dispatch",
                        success: true,
                        request,
                        0,
                        actionId: actionId,
                        stageDetail: "structured_special_effect",
                        effectType: "dissolve",
                        effectName: "dissolve",
                        targetNetworkObjectId: targetNetworkObjectId,
                        responsePreview: speechText
                    );
                    NGLog.Info(
                        "DialogueFX",
                        NGLog.Format(
                            "Structured action normalized to special effect",
                            ("fromType", action.Type ?? string.Empty),
                            ("mode", "dissolve"),
                            ("targetHint", targetHint ?? string.Empty),
                            ("target", targetNetworkObjectId),
                            ("duration", durationSeconds.ToString("F2"))
                        )
                    );
                    return true;
                }
                case PlayerSpecialEffectMode.FloorDissolve:
                {
                    float durationSeconds = ResolveSpecialEffectDurationSeconds(
                        parameterIntent,
                        8f
                    );
                    string actionId = BuildActionId(
                        request,
                        0,
                        "special_effect",
                        "floor_dissolve",
                        0UL
                    );
                    RecordActionValidationResult(
                        request,
                        0,
                        actionId,
                        actionKind: "special_effect",
                        actionName: "floor_dissolve",
                        decision: "validated",
                        success: true,
                        requestedTargetHint: targetHint,
                        requestedDuration: durationSeconds,
                        appliedDuration: durationSeconds,
                        resolvedSpatialType: "Area"
                    );
                    RecordReplicationTrace(
                        stage: "rpc_sent",
                        networkPath: "client_rpc",
                        success: true,
                        request,
                        0,
                        actionId: actionId,
                        effectType: "dissolve",
                        effectName: "floor_dissolve",
                        detail: durationSeconds.ToString("F2")
                    );
                    ApplyFloorDissolveEffectClientRpc(durationSeconds, actionId);
                    RecordExecutionTrace(
                        stage: "effect_dispatch",
                        success: true,
                        request,
                        0,
                        actionId: actionId,
                        stageDetail: "structured_special_effect",
                        effectType: "dissolve",
                        effectName: "floor_dissolve",
                        responsePreview: speechText
                    );
                    NGLog.Info(
                        "DialogueFX",
                        NGLog.Format(
                            "Structured action normalized to special effect",
                            ("fromType", action.Type ?? string.Empty),
                            ("mode", "floor_dissolve"),
                            ("targetHint", targetHint ?? string.Empty),
                            ("duration", durationSeconds.ToString("F2"))
                        )
                    );
                    return true;
                }
                case PlayerSpecialEffectMode.Respawn:
                {
                    if (targetNetworkObjectId == 0)
                    {
                        NGLog.Warn(
                            "DialogueFX",
                            NGLog.Format(
                                "Structured special effect skipped (invalid respawn target)",
                                ("tag", action.Tag ?? string.Empty),
                                ("target", targetHint ?? string.Empty)
                            )
                        );
                        return true;
                    }

                    string actionId = BuildActionId(
                        request,
                        0,
                        "special_effect",
                        "respawn",
                        targetNetworkObjectId
                    );
                    RecordActionValidationResult(
                        request,
                        0,
                        actionId,
                        actionKind: "special_effect",
                        actionName: "respawn",
                        decision: "validated",
                        success: true,
                        requestedTargetHint: targetHint,
                        resolvedTargetNetworkObjectId: targetNetworkObjectId
                    );
                    RecordReplicationTrace(
                        stage: "rpc_sent",
                        networkPath: "client_rpc",
                        success: true,
                        request,
                        0,
                        actionId: actionId,
                        effectType: "respawn",
                        effectName: "respawn",
                        targetNetworkObjectId: targetNetworkObjectId
                    );
                    ApplyRespawnEffectClientRpc(targetNetworkObjectId, actionId);
                    RecordExecutionTrace(
                        stage: "effect_dispatch",
                        success: true,
                        request,
                        0,
                        actionId: actionId,
                        stageDetail: "structured_special_effect",
                        effectType: "respawn",
                        effectName: "respawn",
                        targetNetworkObjectId: targetNetworkObjectId,
                        responsePreview: speechText
                    );
                    NGLog.Info(
                        "DialogueFX",
                        NGLog.Format(
                            "Structured action normalized to special effect",
                            ("fromType", action.Type ?? string.Empty),
                            ("mode", "respawn"),
                            ("targetHint", targetHint ?? string.Empty),
                            ("target", targetNetworkObjectId)
                        )
                    );
                    return true;
                }
            }

            return false;
        }

        private PlayerSpecialEffectMode ResolveStructuredActionSpecialEffectMode(
            DialogueAction action,
            string speechText
        )
        {
            if (action == null)
            {
                return PlayerSpecialEffectMode.None;
            }

            string targetHint = ResolveStructuredActionTargetHint(action);
            string semanticText =
                string.Join(
                    " ",
                    action.Type ?? string.Empty,
                    action.Tag ?? string.Empty,
                    targetHint ?? string.Empty,
                    speechText ?? string.Empty
                ).ToLowerInvariant();

            if (
                semanticText.Contains("floor_dissolve", StringComparison.Ordinal)
                || (LooksLikeFloorTargetHint(targetHint)
                    && (
                        semanticText.Contains("dissolve", StringComparison.Ordinal)
                        || semanticText.Contains("vanish", StringComparison.Ordinal)
                    ))
            )
            {
                return PlayerSpecialEffectMode.FloorDissolve;
            }

            if (
                semanticText.Contains("dissolve", StringComparison.Ordinal)
                || semanticText.Contains("vanish", StringComparison.Ordinal)
            )
            {
                return PlayerSpecialEffectMode.Dissolve;
            }

            if (
                semanticText.Contains("respawn", StringComparison.Ordinal)
                || semanticText.Contains("revive", StringComparison.Ordinal)
            )
            {
                return PlayerSpecialEffectMode.Respawn;
            }

            return PlayerSpecialEffectMode.None;
        }

        private static string ResolveStructuredActionTargetHint(DialogueAction action)
        {
            if (action == null)
            {
                return string.Empty;
            }

            if (
                string.Equals(action.Type, "PATCH", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(action.Tag)
            )
            {
                return action.Tag;
            }

            return !string.IsNullOrWhiteSpace(action.Target)
                ? action.Target
                : action.Tag ?? string.Empty;
        }

        private ulong ResolveStructuredActionTargetNetworkObjectId(
            DialogueAction action,
            DialogueRequest request
        )
        {
            string targetHint = ResolveStructuredActionTargetHint(action);
            ulong preferredListenerTargetNetworkObjectId =
                ResolvePreferredListenerTargetNetworkObjectId(request);
            if (string.IsNullOrWhiteSpace(targetHint))
            {
                return preferredListenerTargetNetworkObjectId != 0
                    ? preferredListenerTargetNetworkObjectId
                    : request.SpeakerNetworkId;
            }

            string lower = targetHint.Trim().ToLowerInvariant();
            if (LooksLikeFloorTargetHint(lower))
            {
                return 0UL;
            }

            if (LooksLikeExplicitPlayerTargetToken(lower))
            {
                return TryResolveExplicitPlayerTargetNetworkObjectId(
                    lower,
                    request,
                    out ulong explicitPlayerTargetNetworkObjectId
                )
                    ? explicitPlayerTargetNetworkObjectId
                    : 0UL;
            }

            if (lower is "self" or "npc" or "speaker" or "caster")
            {
                return request.SpeakerNetworkId != 0
                    ? request.SpeakerNetworkId
                    : request.ListenerNetworkId;
            }

            if (
                lower is "listener" or "player"
                || IsPlayerTargetToken(lower)
                || IsPlayerHeadAlias(lower)
                || IsPlayerFeetAlias(lower)
            )
            {
                return preferredListenerTargetNetworkObjectId != 0
                    ? preferredListenerTargetNetworkObjectId
                    : request.SpeakerNetworkId;
            }

            GameObject targetObject = GameObject.Find(targetHint);
            NetworkObject networkObject = targetObject != null
                ? targetObject.GetComponent<NetworkObject>()
                : null;
            if (networkObject != null)
            {
                return networkObject.NetworkObjectId;
            }

            return preferredListenerTargetNetworkObjectId != 0
                ? preferredListenerTargetNetworkObjectId
                : request.SpeakerNetworkId;
        }

        private static string BuildFlowId(int requestId, DialogueRequest request)
        {
            if (requestId > 0)
            {
                return $"dialogue-{requestId}";
            }

            if (request.RequestingClientId != 0 && request.ClientRequestId > 0)
            {
                return $"dialogue-client-{request.RequestingClientId}-{request.ClientRequestId}";
            }

            return "dialogue-pending";
        }

        private static string BuildActionId(
            DialogueRequest request,
            int requestId,
            string actionKind,
            string actionName,
            ulong targetNetworkObjectId
        )
        {
            string flowId = BuildFlowId(requestId, request);
            string normalizedKind = string.IsNullOrWhiteSpace(actionKind)
                ? "action"
                : actionKind.Trim().Replace(' ', '_').ToLowerInvariant();
            string normalizedName = string.IsNullOrWhiteSpace(actionName)
                ? "unnamed"
                : actionName.Trim().Replace(' ', '_').ToLowerInvariant();
            int ordinal = Interlocked.Increment(ref s_NextDiagnosticActionOrdinal);
            return $"{flowId}:{normalizedKind}:{normalizedName}:{targetNetworkObjectId}:{ordinal}";
        }

        private static (string key, object value)[] BuildRequestData(
            DialogueRequest request,
            params (string key, object value)[] extra
        )
        {
            int extraLength = extra != null ? extra.Length : 0;
            var values = new (string key, object value)[3 + extraLength];
            values[0] = ("key", request.ConversationKey ?? string.Empty);
            values[1] = ("speaker", request.SpeakerNetworkId);
            values[2] = ("listener", request.ListenerNetworkId);

            if (extraLength > 0)
            {
                Array.Copy(extra, 0, values, 3, extraLength);
            }

            return values;
        }

        private static TraceContext CreateRequestTraceContext(
            string phase,
            int requestId,
            DialogueRequest request,
            string flowId = null
        )
        {
            return new TraceContext(
                bootId: string.Empty,
                flowId: string.IsNullOrWhiteSpace(flowId) ? BuildFlowId(requestId, request) : flowId,
                requestId: requestId,
                clientRequestId: request.ClientRequestId,
                clientId: request.RequestingClientId,
                phase: phase,
                script: nameof(NetworkDialogueService)
            );
        }

        private static void EmitFlowTrace(
            string eventName,
            string phase,
            int requestId,
            DialogueRequest request,
            bool success = true,
            DialogueStatus status = DialogueStatus.Pending,
            string error = null,
            string flowId = null
        )
        {
            OnDialogueFlowTrace?.Invoke(
                new DialogueFlowTraceEvent
                {
                    FlowId = string.IsNullOrWhiteSpace(flowId)
                        ? BuildFlowId(requestId, request)
                        : flowId,
                    EventName = eventName ?? string.Empty,
                    Phase = phase ?? string.Empty,
                    RequestId = requestId,
                    ClientRequestId = request.ClientRequestId,
                    ClientId = request.RequestingClientId,
                    SpeakerNetworkId = request.SpeakerNetworkId,
                    ListenerNetworkId = request.ListenerNetworkId,
                    ConversationKey = request.ConversationKey ?? string.Empty,
                    Status = status,
                    Success = success,
                    Error = error ?? string.Empty,
                    TimestampMs = Time.realtimeSinceStartup * 1000f,
                }
            );
        }

        /// <summary>
        /// Per-player effect modifiers derived from the player's customization data.
        /// Applied server-side in ApplyContextEffects before ClampDynamicMultiplier.
        /// </summary>
        private struct PlayerEffectModifier
        {
            public float DamageScaleReceived; // >1 = more vulnerable, <1 = resistant
            public float EffectSizeScale; // player-specific visual scale
            public float EffectDurationScale; // e.g., cursed player gets longer effects
            public Color? PreferredColor; // from customization "color_theme"
            public string PreferredElement; // from customization "element_affinity"
            public bool IsShielded; // from customization "has_shield"
            public float AggressionBias; // scales all offensive effect multipliers

            public static PlayerEffectModifier Neutral =>
                new PlayerEffectModifier
                {
                    DamageScaleReceived = 1f,
                    EffectSizeScale = 1f,
                    EffectDurationScale = 1f,
                    PreferredColor = null,
                    PreferredElement = null,
                    IsShielded = false,
                    AggressionBias = 1f,
                };
        }

        private enum PlayerSpecialEffectMode
        {
            None,
            Dissolve,
            FloorDissolve,
            Respawn,
        }

        public static NetworkDialogueService Instance { get; private set; }
        public static event Action<DialogueResponse> OnDialogueResponse;
        public static event Action<DialogueResponse> OnRawDialogueResponse;
        public static event Action<DialogueResponseTelemetry> OnDialogueResponseTelemetry;
        public static event Action<DialogueFlowTraceEvent> OnDialogueFlowTrace;

        /// <summary>
        /// Fired after JSON action-response parsing completes. Carries the full
        /// structured response (speech + actions). Null <paramref name="actionResponse"/>
        /// means the response was not JSON or parsing failed.
        /// Parameters: clientRequestId, request, actionResponse (may be null).
        /// </summary>
        public static event Action<int, DialogueRequest, DialogueActionResponse> OnDialogueActionResponse;

        public bool UsesRemoteInference => true;
        public bool HasDialogueBackendConfig => GetDialogueBackendConfig() != null;
        public string ActiveInferenceBackendName => ResolveActiveInferenceBackendName();
        public string RemoteInferenceEndpoint => GetRemoteEndpointLabel();

        /// <summary>
        /// Check if LLM agent is ready for requests
        /// </summary>
        public bool IsLLMReady => true;

        private bool UseOpenAIRemote => true;

        /// <summary>
        /// Check if warmup is in degraded mode
        /// </summary>
        public bool IsWarmupDegraded => m_WarmupDegradedMode;

        /// <summary>
        /// Get warmup failure count
        /// </summary>
        public int WarmupFailureCount => m_WarmupConsecutiveFailures;

        /// <summary>
        /// Get pending request count
        /// </summary>
        public int PendingRequestCount => m_RequestQueue.Count;

        /// <summary>
        /// Get active request count
        /// </summary>
        public int ActiveRequestCount => m_ActiveRequestIds.Count;

        /// <summary>
        /// Get total requests enqueued
        /// </summary>
        public int TotalRequestsEnqueued => m_TotalRequestsEnqueued;

        /// <summary>
        /// Get total requests finished
        /// </summary>
        public int TotalRequestsFinished => m_TotalRequestsFinished;

        /// <summary>
        /// Get total completed (terminal success)
        /// </summary>
        public int TotalCompleted => m_TotalTerminalCompleted;

        /// <summary>
        /// Get total failed (terminal failure)
        /// </summary>
        public int TotalFailed => m_TotalTerminalFailed;

        /// <summary>
        /// Get timeout count
        /// </summary>
        public int TimeoutCount => m_TimeoutCount;

        /// <summary>
        /// Get all conversation keys with history
        /// </summary>
        public string[] GetConversationKeys() => GetAllHistory().Keys.ToArray();

        /// <summary>
        /// Get history for a specific conversation (public accessor)
        /// </summary>
        /// <param name="conversationKey">The conversation key</param>
        /// <returns>List of chat messages or null if not found</returns>
        public List<ChatMessage> GetHistoryPublic(string conversationKey)
        {
            return m_Histories.TryGetValue(conversationKey, out var history) ? history : null;
        }

        /// <summary>
        /// Get all history dictionary (for debugging)
        /// </summary>
        public Dictionary<string, List<ChatMessage>> GetAllHistory() => m_Histories;

        /// <summary>
        /// Clear history for a specific conversation
        /// </summary>
        public void ClearHistory(string conversationKey)
        {
            if (m_Histories.ContainsKey(conversationKey))
            {
                m_Histories.Remove(conversationKey);
            }
            if (m_ConversationStates.ContainsKey(conversationKey))
            {
                m_ConversationStates.Remove(conversationKey);
            }
        }

        /// <summary>
        /// Clear all pending requests (emergency use only)
        /// </summary>
        public void ClearPendingRequests()
        {
            m_RequestQueue.Clear();
            foreach (var id in m_ActiveRequestIds)
            {
                if (m_Requests.TryGetValue(id, out var state))
                {
                    state.Status = DialogueStatus.Cancelled;
                    state.Error = "cleared_by_mcp";
                }
            }
            m_ActiveRequestIds.Clear();
        }

        /// <summary>
        /// Get rejection reason counts
        /// </summary>
        public Dictionary<string, int> GetRejectionReasons() =>
            new Dictionary<string, int>(m_RejectionReasonCounts);

        [Header("Dialogue Backend")]
        [SerializeField]
        private DialogueBackendConfig m_DialogueBackendConfig;

        [Header("Persona")]
        [SerializeField]
        private bool m_EnablePersonaRouting = true;

        [SerializeField]
        [TextArea(3, 8)]
        private string m_DefaultSystemPromptOverride = "";

        [Header("Player Prompt Context")]
        [SerializeField]
        private bool m_EnablePlayerPromptContext = true;

        [Header("Persistent Memory Recall")]
        [SerializeField]
        private bool m_EnablePersistentMemoryRecall = true;
        [SerializeField]
        [Range(1, 8)]
        private int m_PersistentMemoryMaxRecentMessages = 4;
        [SerializeField]
        [Range(1, 8)]
        private int m_PersistentMemoryMaxSummaries = 4;
        [SerializeField]
        [Min(0.1f)]
        private float m_PersistentMemoryFetchTimeoutSeconds = 1.5f;
        [SerializeField]
        private bool m_EnablePersistentSemanticRecall = true;
        [SerializeField]
        [Range(1, 8)]
        private int m_PersistentSemanticRecallMaxMatches = 3;
        [SerializeField]
        [Range(0.1f, 1f)]
        private float m_PersistentSemanticRecallThreshold = 0.72f;

        [SerializeField]
        [Min(64)]
        private int m_MaxPlayerCustomizationChars = 720;

        [SerializeField]
        private List<PlayerPromptContextBinding> m_PlayerPromptContextBindings = new();

        [Header("Player Identity")]
        [SerializeField]
        private List<PlayerIdentityBinding> m_PlayerIdentityBindings = new();

        [SerializeField]
        private bool m_RequireAuthenticatedPlayers = true;

        [Header("Scene Effects")]
        [SerializeField]
        private bool m_EnableContextSceneEffects = true;

        [SerializeField]
        private DialogueSceneEffectsController m_SceneEffectsController;

        [SerializeField]
        private PlayerDissolveController m_PlayerDissolveController;

        [SerializeField]
        private SurfaceMaterialEffectController m_SurfaceMaterialEffectController;

        [Header("Effect Spatial Mapping")]
        [SerializeField]
        private bool m_EnableSpatialEffectResolver = true;

        [SerializeField]
        private LayerMask m_EffectGroundMask = ~0;

        [SerializeField]
        private LayerMask m_EffectCollisionMask = ~0;

        [SerializeField]
        private LayerMask m_EffectLineOfSightMask = ~0;

        [SerializeField]
        [Min(0.05f)]
        private float m_EffectGroundProbeUp = 4f;

        [SerializeField]
        [Min(0.05f)]
        private float m_EffectGroundProbeDown = 12f;

        [SerializeField]
        [Min(0f)]
        private float m_EffectGroundOffset = 0.04f;

        [SerializeField]
        [Min(0.05f)]
        private float m_EffectCollisionClearanceRadius = 0.45f;

        [SerializeField]
        [Min(0.25f)]
        private float m_EffectFallbackDistance = 1.5f;

        [SerializeField]
        private int m_MaxHistoryMessages = 20;

        [SerializeField]
        private int m_MaxPendingRequests = 32;

        [SerializeField]
        [Range(1, 8)]
        private int m_MaxConcurrentRequests = 1;

        [SerializeField]
        private bool m_AutoRaiseRemoteConcurrency = true;

        [SerializeField]
        private int m_MaxRequestsPerClient = 4;

        [SerializeField]
        private float m_MinSecondsBetweenRequests = 0.2f;

        [SerializeField]
        private float m_RequestTimeoutSeconds = 5f;

        [Header("Retry")]
        [SerializeField]
        [Min(0)]
        private int m_MaxRetries = 3;

        [SerializeField]
        [Min(0f)]
        private float m_RetryBackoffSeconds = 2f;

        [SerializeField]
        [Min(0f)]
        private float m_RetryJitterSeconds = 1f;

        [Header("Warmup")]
        [SerializeField]
        [Min(0f)]
        private float m_WarmupTimeoutSeconds = 10f;

        [SerializeField]
        [Min(1)]
        private int m_DegradedWarmupFailureThreshold = 3;

        [SerializeField]
        [Min(0f)]
        private float m_WarmupRetryCooldownSeconds = 5f;

        [Header("Broadcast")]
        [SerializeField]
        [Min(40)]
        private int m_BroadcastMaxCharacters = 180;

        [SerializeField]
        private bool m_BroadcastSingleLinePreview = true;

        [SerializeField]
        private bool m_LogDebug = true;

        private readonly Dictionary<int, DialogueRequestState> m_Requests = new();
        private readonly Dictionary<ClientRequestLookupKey, int> m_RequestIdsByScopedClientRequest =
            new();
        private readonly Dictionary<int, List<int>> m_RequestIdsByClientRequestId = new();
        private readonly Queue<int> m_RequestQueue = new();
        private readonly HashSet<int> m_ActiveRequestIds = new();
        private readonly HashSet<string> m_ActiveConversationKeys = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<ChatMessage>> m_Histories = new();
        private readonly Dictionary<string, ConversationState> m_ConversationStates = new(
            StringComparer.Ordinal
        );
        private readonly List<string> m_ConversationKeysCache = new();
        private readonly Dictionary<ulong, float> m_LastRequestTimeByClient = new();
        private readonly Dictionary<
            ulong,
            PlayerPromptContextBinding
        > m_PlayerPromptContextByNetworkId = new();
        private readonly Dictionary<ulong, PlayerIdentityBinding> m_PlayerIdentityByClientId =
            new();
        private readonly Dictionary<ulong, PlayerIdentityBinding> m_PlayerIdentityByNetworkId =
            new();
        private readonly Queue<float> m_QueueWaitSamplesMs = new();
        private readonly Queue<float> m_ModelExecutionSamplesMs = new();
        private readonly Dictionary<string, int> m_RejectionReasonCounts = new(
            StringComparer.OrdinalIgnoreCase
        );
        private readonly Queue<string> m_RejectionReasonOrder = new();
        private int m_TotalTerminalCompleted;
        private int m_TotalTerminalFailed;
        private int m_TotalTerminalCancelled;
        private int m_TotalTerminalRejected;
        private int m_TotalRequestsEnqueued;
        private int m_TotalRequestsFinished;
        private int m_TimeoutCount;
        private float m_LastSummaryLogAt;
        private const float kStuckCheckInterval = 1f;
        private const float kStuckConversationTimeout = 60f;
        private float m_LastStuckCheckTime;
        private int m_NextRequestId = 1;
        private bool m_IsProcessing;
        private Task m_WarmupTask;
        private int m_WarmupConsecutiveFailures;
        private float m_NextWarmupRetryAt;
        private bool m_WarmupDegradedMode;
        private string m_LastWarmupFailureReason = string.Empty;
        private string m_DefaultSystemPrompt = string.Empty;
        // Effect lookup is now per-NPC via NpcDialogueActor.TryGetEffect â€” no global catalog cache.

        [Header("Remote OpenAI")]
        [SerializeField]
        [Tooltip(
            "Model name for OpenAI-compatible remote servers. Leave empty if server has only one model loaded."
        )]
        private string m_RemoteModelName = "";

        [SerializeField]
        [Tooltip(
            "Stop sequences injected into every LM Studio request to prevent chat-template bleed. "
                + "Typical values for Llama-3 / Mistral LoRA models: [\"\u003c/s\u003e\", \"[INST]\", \"User:\", \"Assistant:\"]. "
                + "Leave empty to omit the stop field entirely (LM Studio uses its own template defaults)."
        )]
        private string[] m_RemoteStopSequences = new string[0];

        [SerializeField]
        [Min(120f)]
        [Tooltip(
            "Minimum timeout (seconds) used for remote OpenAI-compatible requests. Prevents premature cancellation during large prompt prefill."
        )]
        private float m_RemoteMinRequestTimeoutSeconds = 240f;

        [SerializeField]
        [Min(512)]
        [Tooltip(
            "Maximum system prompt characters sent to remote OpenAI-compatible backends. Long prompts are trimmed to reduce prefill latency."
        )]
        private int m_RemoteSystemPromptCharBudget = 8000;

        /// <summary>Effective system prompt character budget after clamping (read-only).</summary>
        public int RemoteSystemPromptCharBudget =>
            Mathf.Clamp(m_RemoteSystemPromptCharBudget, 512,
                Mathf.Clamp(m_RemoteSystemPromptHardCapChars, 512, 16000));

        [SerializeField]
        [Min(0)]
        [Tooltip(
            "Maximum prior chat messages forwarded to remote backends. Lower values reduce prompt prefill latency."
        )]
        private int m_RemoteMaxHistoryMessages = 6;

        [SerializeField]
        [Min(1)]
        [Tooltip(
            "Hard cap for remote history message count, applied after m_RemoteMaxHistoryMessages."
        )]
        private int m_RemoteHistoryHardCapMessages = 8;

        [SerializeField]
        [Min(64)]
        [Tooltip("Maximum characters per prior history message sent to remote backends.")]
        private int m_RemoteHistoryMessageCharBudget = 320;

        [SerializeField]
        [Min(64)]
        [Tooltip("Maximum characters for the current user prompt sent to remote backends.")]
        private int m_RemoteUserPromptCharBudget = 520;

        [SerializeField]
        [Range(32, 1024)]
        [Tooltip("Max tokens for standard player-visible remote dialogue turns.")]
        private int m_RemoteDialogueResponseMaxTokens = 192;

        [SerializeField]
        [Min(512)]
        [Tooltip("Absolute safety cap for remote system prompt size.")]
        private int m_RemoteSystemPromptHardCapChars = 3200;

        [SerializeField]
        [Min(64)]
        [Tooltip("Maximum player customization JSON characters included for remote prompts.")]
        private int m_RemoteMaxPlayerCustomizationChars = 220;

        private OpenAIChatClient m_OpenAIChatClient;
        private DialoguePersistenceGateway m_DialoguePersistenceGateway;
        private DialogueMemoryWorker m_DialogueMemoryWorker;

        [Header("Diagnostics")]
        [SerializeField]
        [Min(32)]
        private int m_LatencySampleWindow = 256;

        [SerializeField]
        [Min(16)]
        private int m_RejectionReasonWindow = 256;

        [SerializeField]
        [Min(5f)]
        private float m_SummaryLogIntervalSeconds = 30f;

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

        // EnsureEffectCatalog removed â€” effect lookup is now per-NPC via NpcDialogueActor.


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

        public bool TryGetPlayerIdentityByClientId(
            ulong clientId,
            out PlayerIdentitySnapshot snapshot
        )
        {
            snapshot = default;
            if (
                !m_PlayerIdentityByClientId.TryGetValue(clientId, out var identity)
                || identity == null
                || !identity.Enabled
            )
            {
                return false;
            }

            snapshot = ToSnapshot(identity);
            return true;
        }

        public bool TryGetPlayerIdentityByNetworkId(
            ulong playerNetworkId,
            out PlayerIdentitySnapshot snapshot
        )
        {
            snapshot = default;
            if (
                playerNetworkId == 0
                || !m_PlayerIdentityByNetworkId.TryGetValue(playerNetworkId, out var identity)
                || identity == null
                || !identity.Enabled
            )
            {
                return false;
            }

            snapshot = ToSnapshot(identity);
            return true;
        }

        public bool SetPlayerPromptContext(
            ulong playerNetworkId,
            string nameId,
            string customizationJson
        )
        {
            if (!IsServer)
            {
                NGLog.Warn("Dialogue", "SetPlayerPromptContext called on non-server.");
                return false;
            }

            if (playerNetworkId == 0)
            {
                return false;
            }

            string normalizedNameId = string.IsNullOrWhiteSpace(nameId)
                ? string.Empty
                : nameId.Trim();
            string normalizedCustomizationJson = NormalizePromptContextJson(
                normalizedNameId,
                customizationJson
            );

            PlayerPromptContextBinding binding = FindOrCreatePlayerPromptContextBinding(
                playerNetworkId
            );
            binding.NameId = normalizedNameId;
            binding.CustomizationJson = normalizedCustomizationJson;
            binding.Enabled = true;
            m_PlayerPromptContextByNetworkId[playerNetworkId] = binding;
            UpsertPlayerIdentity(
                ResolveOwnerClientIdForPlayerNetworkId(playerNetworkId),
                playerNetworkId,
                normalizedNameId,
                normalizedCustomizationJson
            );
            return true;
        }

        public bool ClearPlayerPromptContext(ulong playerNetworkId)
        {
            if (!IsServer || playerNetworkId == 0)
            {
                return false;
            }

            bool removedAny = false;
            removedAny |= m_PlayerPromptContextByNetworkId.Remove(playerNetworkId);
            if (m_PlayerPromptContextBindings != null)
            {
                for (int i = m_PlayerPromptContextBindings.Count - 1; i >= 0; i--)
                {
                    PlayerPromptContextBinding binding = m_PlayerPromptContextBindings[i];
                    if (binding == null || binding.PlayerNetworkId != playerNetworkId)
                    {
                        continue;
                    }

                    m_PlayerPromptContextBindings.RemoveAt(i);
                    removedAny = true;
                }
            }

            UpsertPlayerIdentity(
                ResolveOwnerClientIdForPlayerNetworkId(playerNetworkId),
                playerNetworkId,
                null,
                "{}"
            );

            return removedAny;
        }

        public bool SetPlayerPromptContextForClient(
            ulong clientId,
            string nameId,
            string customizationJson
        )
        {
            if (!TryGetPlayerNetworkObjectIdForClient(clientId, out ulong playerNetworkId))
            {
                return false;
            }

            bool applied = SetPlayerPromptContext(playerNetworkId, nameId, customizationJson);
            if (applied)
            {
                UpsertPlayerIdentity(clientId, playerNetworkId, nameId, customizationJson);
            }

            return applied;
        }

        public bool ClearPlayerPromptContextForClient(ulong clientId)
        {
            return TryGetPlayerNetworkObjectIdForClient(clientId, out ulong playerNetworkId)
                && ClearPlayerPromptContext(playerNetworkId);
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

        public bool HasPlayerPromptContextBinding(ulong playerNetworkId)
        {
            return playerNetworkId != 0
                && m_PlayerPromptContextByNetworkId.TryGetValue(playerNetworkId, out var binding)
                && binding != null
                && binding.Enabled;
        }

        public bool RequestSetPlayerPromptContextFromClient(string nameId, string customizationJson)
        {
            if (IsServer || !IsClient)
            {
                return false;
            }

            SetPlayerPromptContextServerRpc(nameId ?? string.Empty, customizationJson ?? "{}");
            return true;
        }

        public bool RequestClearPlayerPromptContextFromClient()
        {
            if (IsServer || !IsClient)
            {
                return false;
            }

            ClearPlayerPromptContextServerRpc();
            return true;
        }

    }
}
