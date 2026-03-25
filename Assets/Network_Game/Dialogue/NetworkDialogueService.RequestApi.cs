using System;
using System.Text;
using Network_Game.Diagnostics;
using Unity.Netcode;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Network_Game.Dialogue
{
    public partial class NetworkDialogueService
    {
        public int EnqueueRequest(DialogueRequest request)
        {
            int requestId;
            string rejectionReason;
            return TryEnqueueRequest(request, out requestId, out rejectionReason) ? requestId : (-1);
        }

        public bool TryEnqueueRequest(DialogueRequest request, out int requestId, out string rejectionReason)
        {
            requestId = -1;
            rejectionReason = null;
            if (!base.IsServer)
            {
                NGLog.Ready("Dialogue", "request_enqueued", ready: false, CreateRequestTraceContext("request_validated", requestId, request), (Object)(object)this, Network_Game.Diagnostics.LogLevel.Warning, "TryEnqueueRequest", BuildRequestData(request, ("reason", "not_server")));
                EmitFlowTrace("request_enqueued", "request_validated", requestId, request, success: false, DialogueStatus.Failed, "not_server");
                rejectionReason = "not_server";
                return false;
            }
            if (m_Requests.Count >= m_MaxPendingRequests)
            {
                rejectionReason = "queue_full";
                TrackRejected(rejectionReason);
                NGLog.Ready("Dialogue", "request_enqueued", ready: false, CreateRequestTraceContext("request_validated", requestId, request), (Object)(object)this, Network_Game.Diagnostics.LogLevel.Warning, "TryEnqueueRequest", BuildRequestData(request, ("reason", rejectionReason)));
                EmitFlowTrace("request_enqueued", "request_validated", requestId, request, success: false, DialogueStatus.Failed, rejectionReason);
                return false;
            }
            request.ConversationKey = ResolveConversationKey(request.SpeakerNetworkId, request.ListenerNetworkId, request.RequestingClientId, request.ConversationKey);
            if (!TryValidateRequestForEnqueue(request, out var rejectionReason2))
            {
                rejectionReason = rejectionReason2;
                TrackRejected(rejectionReason);
                NGLog.Ready("Dialogue", "request_validated", ready: false, CreateRequestTraceContext("request_validated", requestId, request), (Object)(object)this, Network_Game.Diagnostics.LogLevel.Warning, "TryEnqueueRequest", BuildRequestData(request, ("reason", rejectionReason2 ?? "unknown")));
                EmitFlowTrace("request_validated", "request_validated", requestId, request, success: false, DialogueStatus.Failed, rejectionReason2);
                return false;
            }
            requestId = m_NextRequestId++;
            m_TotalRequestsEnqueued++;
            ConversationState conversationStateForConversation = GetConversationStateForConversation(request.ConversationKey);
            conversationStateForConversation.HasOutstandingRequest = true;
            conversationStateForConversation.OutstandingRequestId = requestId;
            m_Requests[requestId] = new DialogueRequestState
            {
                Request = request,
                FlowId = BuildFlowId(requestId, request),
                Status = DialogueStatus.Pending,
                EnqueuedAt = Time.realtimeSinceStartup
            };
            RegisterClientRequestLookup(requestId, request);
            m_RequestQueue.Enqueue(requestId);
            if (!m_IsProcessing)
            {
                RunFireAndForget(ProcessQueue(), "process_queue");
            }
            NGLog.Ready("Dialogue", "request_validated", ready: true, CreateRequestTraceContext("request_validated", requestId, request, m_Requests[requestId].FlowId), (Object)(object)this, Network_Game.Diagnostics.LogLevel.Info, "TryEnqueueRequest", BuildRequestData(request));
            EmitFlowTrace("request_validated", "request_validated", requestId, request, success: true, DialogueStatus.Pending, null, m_Requests[requestId].FlowId);
            NGLog.Ready("Dialogue", "request_enqueued", ready: true, CreateRequestTraceContext("request_enqueued", requestId, request, m_Requests[requestId].FlowId), (Object)(object)this, Network_Game.Diagnostics.LogLevel.Info, "TryEnqueueRequest", BuildRequestData(request, ("queueDepth", m_RequestQueue.Count), ("activeWorkers", m_ActiveRequestIds.Count)));
            EmitFlowTrace("request_enqueued", "request_enqueued", requestId, request, success: true, DialogueStatus.Pending, null, m_Requests[requestId].FlowId);
            return true;
        }

        public bool TryConsumeResponse(int requestId, out DialogueResponse response)
        {
            response = default(DialogueResponse);
            if (!m_Requests.TryGetValue(requestId, out var value))
            {
                return false;
            }
            if (value.Status == DialogueStatus.Completed || value.Status == DialogueStatus.Failed || value.Status == DialogueStatus.Cancelled)
            {
                response = new DialogueResponse
                {
                    RequestId = requestId,
                    Status = value.Status,
                    ResponseText = value.ResponseText,
                    Error = value.Error,
                    Request = value.Request
                };
                UnregisterClientRequestLookup(requestId, value.Request);
                m_Requests.Remove(requestId);
                return true;
            }
            return false;
        }

        public bool TryConsumeResponseByClientRequestId(int clientRequestId, out DialogueResponse response, ulong requestingClientId = ulong.MaxValue)
        {
            response = default(DialogueResponse);
            if (clientRequestId <= 0)
            {
                return false;
            }
            if (!TryGetRequestIdByClientRequestId(clientRequestId, requestingClientId, out var requestId))
            {
                return false;
            }
            if (!m_Requests.TryGetValue(requestId, out var value))
            {
                return false;
            }
            if (value.Status != DialogueStatus.Completed && value.Status != DialogueStatus.Failed && value.Status != DialogueStatus.Cancelled)
            {
                return false;
            }
            return TryConsumeResponse(requestId, out response);
        }

        public bool TryGetTerminalResponseByClientRequestId(int clientRequestId, out DialogueResponse response, ulong requestingClientId = ulong.MaxValue)
        {
            response = default(DialogueResponse);
            if (clientRequestId <= 0)
            {
                return false;
            }
            if (!TryGetRequestIdByClientRequestId(clientRequestId, requestingClientId, out var requestId))
            {
                return false;
            }
            if (!m_Requests.TryGetValue(requestId, out var value))
            {
                return false;
            }
            if (value.Status != DialogueStatus.Completed && value.Status != DialogueStatus.Failed && value.Status != DialogueStatus.Cancelled)
            {
                return false;
            }
            response = new DialogueResponse
            {
                RequestId = requestId,
                Status = value.Status,
                ResponseText = value.ResponseText,
                Error = value.Error,
                Request = value.Request
            };
            return true;
        }

        public DialogueStats GetStats()
        {
            int num = 0;
            foreach (DialogueRequestState value in m_Requests.Values)
            {
                if (value.Status == DialogueStatus.Pending || value.Status == DialogueStatus.InProgress)
                {
                    num++;
                }
            }
            int val = m_TotalTerminalCompleted + m_TotalTerminalFailed + m_TotalTerminalCancelled;
            int num2 = Math.Max(1, m_TotalTerminalCompleted + m_TotalTerminalFailed);
            return new DialogueStats
            {
                PendingCount = m_RequestQueue.Count,
                ActiveCount = num,
                HistoryCount = m_Histories.Count,
                HasLlmAgent = false,
                IsServer = base.IsServer,
                IsClient = base.IsClient,
                WarmupState = BuildWarmupStateLabel(),
                WarmupInProgress = (m_WarmupTask != null && !m_WarmupTask.IsCompleted),
                WarmupDegraded = m_WarmupDegradedMode,
                WarmupFailureCount = m_WarmupConsecutiveFailures,
                WarmupRetryInSeconds = Mathf.Max(0f, m_NextWarmupRetryAt - Time.realtimeSinceStartup),
                WarmupLastFailureReason = (m_LastWarmupFailureReason ?? string.Empty),
                TotalTerminalCompleted = m_TotalTerminalCompleted,
                TotalTerminalFailed = m_TotalTerminalFailed,
                TotalTerminalCancelled = m_TotalTerminalCancelled,
                TotalTerminalRejected = m_TotalTerminalRejected,
                TotalRequestsEnqueued = m_TotalRequestsEnqueued,
                TotalRequestsFinished = m_TotalRequestsFinished,
                TimeoutCount = m_TimeoutCount,
                TimeoutRate = (float)m_TimeoutCount / (float)num2,
                SuccessRate = (float)m_TotalTerminalCompleted / (float)Math.Max(1, val),
                QueueWaitHistogram = BuildLatencyHistogram(m_QueueWaitSamplesMs),
                ModelExecutionHistogram = BuildLatencyHistogram(m_ModelExecutionSamplesMs),
                RejectionReasonCounts = BuildRejectionCountsSnapshot()
            };
        }

        public bool IsClientRequestInFlight(int clientRequestId, ulong requestingClientId = ulong.MaxValue)
        {
            if (clientRequestId <= 0)
            {
                return false;
            }
            if (!TryGetRequestIdByClientRequestId(clientRequestId, requestingClientId, out var requestId))
            {
                return false;
            }
            if (!m_Requests.TryGetValue(requestId, out var value))
            {
                return false;
            }
            return value.Status == DialogueStatus.Pending || value.Status == DialogueStatus.InProgress;
        }

        [ContextMenu("Dialogue/Log Player Identity Report")]
        public void LogPlayerIdentityReport()
        {
            NGLog.Info("Dialogue", BuildPlayerIdentityReport());
        }

        public string BuildPlayerIdentityReport()
        {
            StringBuilder stringBuilder = new StringBuilder(256);
            stringBuilder.Append("Player identity bindings:");
            int num = 0;
            foreach (var item in m_PlayerIdentityByClientId)
            {
                PlayerIdentityBinding value = item.Value;
                if (value != null && value.Enabled)
                {
                    num++;
                    stringBuilder.Append(" [client=").Append(value.ClientId).Append(", playerNetId=")
                        .Append(value.PlayerNetworkId)
                        .Append(", name_id=")
                        .Append(string.IsNullOrWhiteSpace(value.NameId) ? "unknown" : value.NameId)
                        .Append("]");
                }
            }
            if (num == 0)
            {
                stringBuilder.Append(" none");
            }
            return stringBuilder.ToString();
        }

        public void RequestDialogue(DialogueRequest request)
        {
            NGLog.Trigger("Dialogue", "request_submit", CreateRequestTraceContext("request_submit", 0, request), (Object)(object)this, Network_Game.Diagnostics.LogLevel.Info, "RequestDialogue", BuildRequestData(request, ("notifyClient", request.NotifyClient), ("broadcast", request.Broadcast)));
            EmitFlowTrace("request_submit", "request_submit", 0, request);
            if (base.IsServer)
            {
                if (!TryEnqueueRequest(request, out var requestId, out var rejectionReason))
                {
                    string text = ResolveConversationKey(request.SpeakerNetworkId, request.ListenerNetworkId, request.RequestingClientId, request.ConversationKey);
                    NGLog.Ready("Dialogue", "request_rejected", ready: false, CreateRequestTraceContext("request_rejected", requestId, request), (Object)(object)this, Network_Game.Diagnostics.LogLevel.Warning, "RequestDialogue", BuildRequestData(request, ("reason", rejectionReason ?? "unknown"), ("resolvedKey", text)));
                    EmitFlowTrace("request_rejected", "request_rejected", requestId, request, success: false, DialogueStatus.Failed, rejectionReason);
                    if (request.NotifyClient)
                    {
                        PublishLocalRejection(requestId, request.ClientRequestId, text, rejectionReason, request.SpeakerNetworkId, request.ListenerNetworkId);
                    }
                }
            }
            else if (!base.IsClient)
            {
                NGLog.Ready("Dialogue", "request_submit", ready: false, CreateRequestTraceContext("request_submit", 0, request), (Object)(object)this, Network_Game.Diagnostics.LogLevel.Warning, "RequestDialogue", BuildRequestData(request, ("reason", "not_client_or_server")));
                EmitFlowTrace("request_submit", "request_submit", 0, request, success: false, DialogueStatus.Failed, "not_client_or_server");
                if (request.NotifyClient)
                {
                    PublishLocalRejection(0, request.ClientRequestId, request.ConversationKey, "not_server", request.SpeakerNetworkId, request.ListenerNetworkId);
                }
            }
            else
            {
                NGLog.Publish("Dialogue", "client_rpc_send", CreateRequestTraceContext("client_rpc_send", 0, request), (Object)(object)this, Network_Game.Diagnostics.LogLevel.Info, "RequestDialogue", BuildRequestData(request));
                EmitFlowTrace("client_rpc_send", "client_rpc_send", 0, request);
                RequestDialogueServerRpc(request.Prompt, request.ConversationKey, request.SpeakerNetworkId, request.ListenerNetworkId, request.Broadcast, request.BroadcastDuration, request.ClientRequestId, request.IsUserInitiated, request.BlockRepeatedPrompt, request.MinRepeatDelaySeconds, request.RequireUserReply);
            }
        }

        private static void DispatchRawDialogueResponse(int requestId, DialogueRequest request, DialogueStatus status, string responseText, string error = "")
        {
            NetworkDialogueService.OnRawDialogueResponse?.Invoke(new DialogueResponse
            {
                RequestId = requestId,
                Status = status,
                ResponseText = (responseText ?? string.Empty),
                Error = (error ?? string.Empty),
                Request = request
            });
        }

        public void CancelRequest(int requestId)
        {
            if (m_Requests.TryGetValue(requestId, out var value))
            {
                value.Status = DialogueStatus.Cancelled;
                value.Error = "request_cancelled";
            }
        }

        private void PublishLocalRejection(int requestId, int clientRequestId, string conversationKey, string rejectionReason, ulong speakerNetworkId, ulong listenerNetworkId)
        {
            DialogueRequest request = new DialogueRequest
            {
                ConversationKey = (conversationKey ?? string.Empty),
                ClientRequestId = clientRequestId,
                SpeakerNetworkId = speakerNetworkId,
                ListenerNetworkId = listenerNetworkId
            };
            NGLog.Ready("Dialogue", "request_rejected", ready: false, CreateRequestTraceContext("request_rejected", requestId, request), (Object)(object)this, Network_Game.Diagnostics.LogLevel.Warning, "PublishLocalRejection", BuildRequestData(request, ("reason", rejectionReason ?? "request_rejected")));
            EmitFlowTrace("request_rejected", "request_rejected", requestId, request, success: false, DialogueStatus.Failed, rejectionReason);
            DialogueResponse obj = new DialogueResponse
            {
                RequestId = requestId,
                Status = DialogueStatus.Failed,
                ResponseText = string.Empty,
                Error = (string.IsNullOrWhiteSpace(rejectionReason) ? "request_rejected" : rejectionReason),
                Request = request
            };
            NetworkDialogueService.OnDialogueResponse?.Invoke(obj);
            NetworkDialogueService.OnDialogueResponseTelemetry?.Invoke(new DialogueResponseTelemetry
            {
                RequestId = requestId,
                Status = DialogueStatus.Failed,
                Error = obj.Error,
                Request = obj.Request,
                RetryCount = 0,
                QueueLatencyMs = 0f,
                ModelLatencyMs = 0f,
                TotalLatencyMs = 0f
            });
            RecordExecutionTrace("request_rejected", success: false, request, requestId, null, null, "validation", null, null, 0uL, 0uL, obj.Error);
        }

        private void SendRejectedDialogueResponseToClient(ulong targetClientId, int requestId, int clientRequestId, string rejectionReason, string conversationKey, ulong speakerNetworkId, ulong listenerNetworkId, bool isUserInitiated = false)
        {
            DialogueRequest request = new DialogueRequest
            {
                ConversationKey = (conversationKey ?? string.Empty),
                ClientRequestId = clientRequestId,
                SpeakerNetworkId = speakerNetworkId,
                ListenerNetworkId = listenerNetworkId,
                RequestingClientId = targetClientId,
                IsUserInitiated = isUserInitiated,
                NotifyClient = true
            };
            NGLog.Publish("Dialogue", "client_rpc_send", CreateRequestTraceContext("client_rpc_send", requestId, request), (Object)(object)this, Network_Game.Diagnostics.LogLevel.Info, "SendRejectedDialogueResponseToClient", BuildRequestData(request, ("status", DialogueStatus.Failed), ("reason", rejectionReason ?? "request_rejected")));
            EmitFlowTrace("client_rpc_send", "client_rpc_send", requestId, request, success: false, DialogueStatus.Failed, rejectionReason);
            DialogueResponseClientRpc(requestId, clientRequestId, DialogueStatus.Failed, string.Empty, string.IsNullOrWhiteSpace(rejectionReason) ? "request_rejected" : rejectionReason, conversationKey ?? string.Empty, speakerNetworkId, listenerNetworkId, targetClientId, isUserInitiated, base.RpcTarget.Single(targetClientId, RpcTargetUse.Temp));
            RecordExecutionTrace("request_rejected", success: false, request, requestId, null, null, "client_rpc_send", null, null, 0uL, 0uL, string.IsNullOrWhiteSpace(rejectionReason) ? "request_rejected" : rejectionReason);
        }

        public bool AppendMessage(string conversationKey, string role, string content)
        {
            if (!base.IsServer)
            {
                if (!base.IsClient)
                {
                    NGLog.Warn("Dialogue", "AppendMessage called without client/server.");
                    return false;
                }
                AppendMessageServerRpc(conversationKey, role, content);
                return true;
            }
            return AppendMessageInternal(conversationKey, role, content);
        }

        private bool AppendMessageInternal(string conversationKey, string role, string content)
        {
            if (string.IsNullOrWhiteSpace(conversationKey) || string.IsNullOrWhiteSpace(content))
            {
                return false;
            }
            conversationKey = ResolveConversationKey(0uL, 0uL, 0uL, conversationKey);
            var historyForConversation = GetHistoryForConversation(conversationKey);
            string text = (string.IsNullOrWhiteSpace(role) ? "user" : role.Trim().ToLowerInvariant());
            historyForConversation.Add(new DialogueHistoryEntry(text, content));
            StoreHistoryForConversation(conversationKey, historyForConversation);
            ConversationState conversationStateForConversation = GetConversationStateForConversation(conversationKey);
            if (text == "user")
            {
                conversationStateForConversation.AwaitingUserInput = false;
                conversationStateForConversation.UserMessageCount++;
            }
            else if (text == "assistant")
            {
                conversationStateForConversation.AwaitingUserInput = true;
                conversationStateForConversation.AssistantMessageCount++;
            }
            return true;
        }
    }
}
