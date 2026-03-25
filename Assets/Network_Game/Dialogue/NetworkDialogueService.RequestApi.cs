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
        // Command-side request surface: enqueue, submit, and cancel.
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

        public void CancelRequest(int requestId)
        {
            if (m_Requests.TryGetValue(requestId, out var value))
            {
                value.Status = DialogueStatus.Cancelled;
                value.Error = "request_cancelled";
            }
        }
    }
}
