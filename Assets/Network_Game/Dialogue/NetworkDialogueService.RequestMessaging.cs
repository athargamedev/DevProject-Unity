using Network_Game.Diagnostics;
using Unity.Netcode;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Network_Game.Dialogue
{
    public partial class NetworkDialogueService
    {
        // Local rejection publishing, client failure routing, and manual history appends.
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
