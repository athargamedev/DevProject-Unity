using System.Collections.Generic;
using System.Threading.Tasks;
using Network_Game.Diagnostics;
using Unity.Netcode;
using UnityEngine;
using NGLogLevel = Network_Game.Diagnostics.LogLevel;

namespace Network_Game.Dialogue
{
    public partial class NetworkDialogueService
    {
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void SetPlayerPromptContextServerRpc(
            string nameId,
            string customizationJson,
            RpcParams rpcParams = default
        )
        {
            if (
                !SetPlayerPromptContextForClient(
                    rpcParams.Receive.SenderClientId,
                    nameId,
                    customizationJson
                )
            )
            {
                NGLog.Warn(
                    "Dialogue",
                    NGLog.Format(
                        "Player prompt context RPC apply failed",
                        ("sender", rpcParams.Receive.SenderClientId)
                    )
                );
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void ClearPlayerPromptContextServerRpc(RpcParams rpcParams = default)
        {
            if (!ClearPlayerPromptContextForClient(rpcParams.Receive.SenderClientId))
            {
                NGLog.Warn(
                    "Dialogue",
                    NGLog.Format(
                        "Player prompt context RPC clear failed",
                        ("sender", rpcParams.Receive.SenderClientId)
                    )
                );
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void AppendMessageServerRpc(
            string conversationKey,
            string role,
            string content,
            RpcParams rpcParams = default
        )
        {
            if (string.IsNullOrWhiteSpace(conversationKey) || string.IsNullOrWhiteSpace(content))
            {
                return;
            }

            if (
                !IsConversationKeyVisibleToClient(conversationKey, rpcParams.Receive.SenderClientId)
            )
            {
                NGLog.Warn(
                    "Dialogue",
                    NGLog.Format(
                        "Append rejected (invalid conversation key)",
                        ("sender", rpcParams.Receive.SenderClientId),
                        ("key", conversationKey)
                    )
                );
                return;
            }

            AppendMessageInternal(conversationKey, role, content);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void RequestDialogueServerRpc(
            string prompt,
            string conversationKey,
            ulong speakerNetworkId,
            ulong listenerNetworkId,
            bool broadcast,
            float broadcastDuration,
            int clientRequestId,
            bool isUserInitiated,
            bool blockRepeatedPrompt,
            float minRepeatDelaySeconds,
            bool requireUserReply,
            RpcParams rpcParams = default
        )
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;
            var rpcRequest = new DialogueRequest
            {
                Prompt = prompt,
                ConversationKey = conversationKey ?? string.Empty,
                SpeakerNetworkId = speakerNetworkId,
                ListenerNetworkId = listenerNetworkId,
                RequestingClientId = senderClientId,
                Broadcast = broadcast,
                BroadcastDuration = broadcastDuration,
                NotifyClient = true,
                ClientRequestId = clientRequestId,
                IsUserInitiated = isUserInitiated,
                BlockRepeatedPrompt = blockRepeatedPrompt,
                MinRepeatDelaySeconds = minRepeatDelaySeconds,
                RequireUserReply = requireUserReply,
            };

            NGLog.Publish(
                DialogueCategory,
                "server_rpc_receive",
                CreateRequestTraceContext("server_rpc_receive", 0, rpcRequest),
                this,
                data: BuildRequestData(rpcRequest)
            );
            EmitFlowTrace("server_rpc_receive", "server_rpc_receive", 0, rpcRequest);

            string canonicalKey = ResolveConversationKey(
                speakerNetworkId,
                listenerNetworkId,
                senderClientId,
                null
            );

            if (!m_RequireAuthenticatedPlayers || !isUserInitiated)
            {
                if (
                    TryGetPlayerNetworkObjectIdForClient(
                        senderClientId,
                        out ulong senderPlayerNetworkId
                    )
                )
                {
                    UpsertPlayerIdentity(
                        senderClientId,
                        senderPlayerNetworkId,
                        $"client_{senderClientId}",
                        null
                    );
                }
            }

            if (
                !TryValidateClientDialogueParticipants(
                    senderClientId,
                    speakerNetworkId,
                    listenerNetworkId,
                    out string participantReason
                )
            )
            {
                NGLog.Ready(
                    DialogueCategory,
                    "request_validated",
                    false,
                    CreateRequestTraceContext("request_validated", -1, rpcRequest),
                    this,
                    NGLogLevel.Warning,
                    data:
                    BuildRequestData(
                        rpcRequest,
                        ("reason", (object)(participantReason ?? "unknown")),
                        ("resolvedKey", (object)canonicalKey)
                    )
                );
                EmitFlowTrace(
                    "request_validated",
                    "request_validated",
                    -1,
                    rpcRequest,
                    success: false,
                    status: DialogueStatus.Failed,
                    error: participantReason
                );
                SendRejectedDialogueResponseToClient(
                    senderClientId,
                    -1,
                    clientRequestId,
                    participantReason,
                    canonicalKey,
                    speakerNetworkId,
                    listenerNetworkId
                );
                return;
            }

            if (!string.IsNullOrWhiteSpace(conversationKey))
            {
                string requestedKey = ResolveConversationKey(
                    speakerNetworkId,
                    listenerNetworkId,
                    senderClientId,
                    conversationKey
                );
                string clientScopedPrefix = $"client:{senderClientId}:";
                if (requestedKey.StartsWith(clientScopedPrefix, System.StringComparison.OrdinalIgnoreCase))
                {
                    canonicalKey = requestedKey;
                }
                else if (!string.Equals(requestedKey, canonicalKey, System.StringComparison.Ordinal))
                {
                    NGLog.Warn(
                        "Dialogue",
                        NGLog.Format(
                            "Ignoring client conversation override",
                            ("sender", senderClientId),
                            ("requested", requestedKey),
                            ("canonical", canonicalKey)
                        )
                    );
                }
            }

            var request = new DialogueRequest
            {
                Prompt = prompt,
                ConversationKey = canonicalKey,
                SpeakerNetworkId = speakerNetworkId,
                ListenerNetworkId = listenerNetworkId,
                RequestingClientId = senderClientId,
                Broadcast = broadcast,
                BroadcastDuration = broadcastDuration,
                NotifyClient = true,
                ClientRequestId = clientRequestId,
                IsUserInitiated = isUserInitiated,
                BlockRepeatedPrompt = blockRepeatedPrompt,
                MinRepeatDelaySeconds = minRepeatDelaySeconds,
                RequireUserReply = requireUserReply,
            };

            if (TryEnqueueRequest(request, out int requestId, out string rejectionReason))
            {
                return;
            }

            NGLog.Ready(
                DialogueCategory,
                "request_rejected",
                false,
                CreateRequestTraceContext("request_rejected", requestId, request),
                this,
                NGLogLevel.Warning,
                data: BuildRequestData(request, ("reason", (object)(rejectionReason ?? "unknown")))
            );
            EmitFlowTrace(
                "request_rejected",
                "request_rejected",
                requestId,
                request,
                success: false,
                status: DialogueStatus.Failed,
                error: rejectionReason
            );
            SendRejectedDialogueResponseToClient(
                senderClientId,
                requestId,
                clientRequestId,
                rejectionReason,
                canonicalKey,
                speakerNetworkId,
                listenerNetworkId,
                isUserInitiated
            );
        }

        [Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
        private void DialogueResponseClientRpc(
            int requestId,
            int clientRequestId,
            DialogueStatus status,
            string responseText,
            string error,
            string conversationKey,
            ulong speakerNetworkId,
            ulong listenerNetworkId,
            ulong requestingClientId,
            bool isUserInitiated,
            RpcParams rpcParams = default
        )
        {
            var responseRequest = new DialogueRequest
            {
                ConversationKey = conversationKey ?? string.Empty,
                ClientRequestId = clientRequestId,
                SpeakerNetworkId = speakerNetworkId,
                ListenerNetworkId = listenerNetworkId,
                RequestingClientId = requestingClientId,
                IsUserInitiated = isUserInitiated,
            };

            NGLog.Publish(
                DialogueCategory,
                "client_rpc_receive",
                CreateRequestTraceContext("client_rpc_receive", requestId, responseRequest),
                this,
                data:
                BuildRequestData(
                    responseRequest,
                    ("status", (object)status),
                    ("error", (object)(error ?? string.Empty))
                )
            );
            EmitFlowTrace(
                "client_rpc_receive",
                "client_rpc_receive",
                requestId,
                responseRequest,
                success: status == DialogueStatus.Completed,
                status: status,
                error: error
            );
            var response = new DialogueResponse
            {
                RequestId = requestId,
                Status = status,
                ResponseText = responseText,
                Error = error,
                Request = responseRequest,
            };

            OnDialogueResponse?.Invoke(response);
        }

        /// <summary>
        /// Analyzes a runtime error or log message using the LLM to suggest fixes.
        /// Bypasses the standard dialogue queue for immediate analysis.
        /// </summary>
        public async Task<string> AnalyzeDebugLog(string logContext, string errorMessage)
        {
            string debugSystemPrompt =
                @"You are an expert Unity C# engineer and debugger.
Your task is to analyze the provided runtime log and error message.
Identify the root cause and suggest a specific code fix.
Format your response as a concise summary followed by a code block if applicable.
Do not roleplay as a character.";

            string userPrompt =
                $"Analyze this Unity error:\nContext:\n{logContext}\n\nError:\n{errorMessage}";

            try
            {
                IDialogueInferenceClient inferenceClient = ResolveInferenceClient();
                if (inferenceClient == null)
                {
                    return "LLM backend not ready.";
                }

                return await inferenceClient.ChatAsync(
                    debugSystemPrompt,
                    new List<DialogueInferenceMessage>(),
                    userPrompt,
                    addToHistory: false
                );
            }
            catch (System.Exception ex)
            {
                return $"Analysis failed: {ex.Message}";
            }
        }

        #region Enhanced Context Helpers

        /// <summary>
        /// Advance the conversation exchange counter for an NPC.
        /// Call this after each dialogue exchange.
        /// </summary>
        public void AdvanceConversationExchange(NpcDialogueActor actor)
        {
            actor?.NextExchange();
        }

        /// <summary>
        /// Set the story beat for an NPC's conversation.
        /// </summary>
        public void SetConversationStoryBeat(NpcDialogueActor actor, string beat)
        {
            actor?.SetStoryBeat(beat);
        }

        /// <summary>
        /// Begin a new tracked conversation.
        /// </summary>
        public void BeginTrackedConversation(
            NpcDialogueActor actor,
            string conversationKey,
            string storyBeat = "greeting"
        )
        {
            actor?.BeginConversation(conversationKey, storyBeat);
        }

        /// <summary>
        /// Get telemetry statistics for effect decisions.
        /// </summary>
        public Effects.EffectUsageStatistics GetEffectTelemetryStatistics()
        {
            return Effects.EffectDecisionTelemetry.GetStatistics();
        }

        /// <summary>
        /// Generate a telemetry report for recent effect decisions.
        /// </summary>
        public string GenerateEffectTelemetryReport(int decisionCount = 20)
        {
            return Effects.EffectDecisionTelemetry.GenerateReport(decisionCount);
        }

        #endregion

#if UNITY_EDITOR
        // ─── Editor-only test injection ───────────────────────────────────────────
        // Bypass the LLM entirely and inject a DialogueActionResponse directly into
        
        // verify that dispatch/animation/effect code works without LLM latency.
        public void InjectTestActions(
            ulong speakerNetId,
            ulong listenerNetId,
            List<DialogueAction> actions
        )
        {
            var request = new DialogueRequest
            {
                SpeakerNetworkId = speakerNetId,
                ListenerNetworkId = listenerNetId,
                RequestingClientId = NetworkManager.Singleton != null
                    ? NetworkManager.Singleton.LocalClientId
                    : 0UL,
            };
            TryApplyContextActionsSafe(request, actions, string.Empty);
        }
#endif
    }
}
