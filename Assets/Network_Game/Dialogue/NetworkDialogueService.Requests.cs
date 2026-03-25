using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Network_Game.Diagnostics;
using Network_Game.Dialogue.Effects;
using Unity.Netcode;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

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
        
        	private bool TryValidateRequestForEnqueue(DialogueRequest request, out string rejectionReason)
        	{
        		return CanAcceptRequest(request, out rejectionReason);
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
        		foreach (KeyValuePair<ulong, PlayerIdentityBinding> item in m_PlayerIdentityByClientId)
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
        
        	public string ResolveConversationKey(ulong speakerNetworkId, ulong listenerNetworkId, ulong requestingClientId, string conversationKeyOverride = null)
        	{
        		if (!string.IsNullOrWhiteSpace(conversationKeyOverride))
        		{
        			return conversationKeyOverride.Trim();
        		}
        		if (speakerNetworkId != 0L && listenerNetworkId != 0)
        		{
        			ulong num = Math.Min(speakerNetworkId, listenerNetworkId);
        			ulong num2 = Math.Max(speakerNetworkId, listenerNetworkId);
        			return $"{num}:{num2}";
        		}
        		if (speakerNetworkId != 0)
        		{
        			return $"actor:{speakerNetworkId}";
        		}
        		if (listenerNetworkId != 0)
        		{
        			return $"actor:{listenerNetworkId}";
        		}
        		return $"client:{requestingClientId}";
        	}
        
        	public bool TryGetAutoRequestBlockReason(string conversationKey, string prompt, bool blockRepeatedPrompt, float minRepeatDelaySeconds, bool requireUserReply, out string reason)
        	{
        		reason = null;
        		if (string.IsNullOrWhiteSpace(conversationKey))
        		{
        			return false;
        		}
        		string conversationKey2 = ResolveConversationKey(0uL, 0uL, 0uL, conversationKey);
        		ConversationState conversationStateForConversation = GetConversationStateForConversation(conversationKey2);
        		if (conversationStateForConversation.IsInFlight)
        		{
        			reason = "conversation_in_flight";
        			return true;
        		}
        		if (requireUserReply && conversationStateForConversation.AwaitingUserInput)
        		{
        			reason = "awaiting_user_message";
        			return true;
        		}
        		if (blockRepeatedPrompt && !string.IsNullOrWhiteSpace(prompt) && !string.IsNullOrWhiteSpace(conversationStateForConversation.LastCompletedPrompt) && string.Equals(conversationStateForConversation.LastCompletedPrompt, prompt, StringComparison.OrdinalIgnoreCase))
        		{
        			reason = "duplicate_prompt";
        			return true;
        		}
        		if (minRepeatDelaySeconds > 0f && conversationStateForConversation.LastCompletedAt > float.MinValue)
        		{
        			float num = Time.realtimeSinceStartup - conversationStateForConversation.LastCompletedAt;
        			if (num < minRepeatDelaySeconds)
        			{
        				reason = "repeat_delay";
        				return true;
        			}
        		}
        		return false;
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
        		List<DialogueHistoryEntry> historyForConversation = GetHistoryForConversation(conversationKey);
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
        
        	private async Task ProcessQueue()
        	{
        		if (m_IsProcessing)
        		{
        			return;
        		}
        		m_IsProcessing = true;
        		if (m_LogDebug)
        		{
        			NGLog.Debug("Dialogue", NGLog.Format("ProcessQueue start", ("pending", m_RequestQueue.Count)));
        		}
        		while (m_RequestQueue.Count > 0 || m_ActiveRequestIds.Count > 0)
        		{
        			bool startedWorker = false;
        			int maxWorkers = GetEffectiveMaxConcurrentRequests();
        			int requestId;
        			DialogueRequestState state;
        			while (m_ActiveRequestIds.Count < maxWorkers && TryDequeueNextRequestForExecution(out requestId, out state))
        			{
        				m_ActiveRequestIds.Add(requestId);
        				startedWorker = true;
        				NGLog.Transition("Dialogue", "request_enqueued", "request_dequeued", CreateRequestTraceContext("request_dequeued", requestId, state.Request, state.FlowId), (Object)(object)this, Network_Game.Diagnostics.LogLevel.Info, "ProcessQueue", BuildRequestData(state.Request, ("queueDepth", m_RequestQueue.Count), ("activeWorkers", m_ActiveRequestIds.Count)));
        				EmitFlowTrace("request_dequeued", "request_dequeued", requestId, state.Request, success: true, DialogueStatus.Pending, null, state.FlowId);
        				RunFireAndForget(
        					ExecuteRequestWorkerAsync(requestId, state),
        					$"execute_request_worker:{requestId}"
        				);
        				state = null;
        			}
        			if (!startedWorker)
        			{
        				await Task.Delay(GetQueueIdleDelayMs());
        			}
        		}
        		m_IsProcessing = false;
        		if (m_LogDebug)
        		{
        			NGLog.Debug("Dialogue", "ProcessQueue complete");
        		}
        	}
        
        	private bool TryDequeueNextRequestForExecution(out int requestId, out DialogueRequestState state)
        	{
        		requestId = -1;
        		state = null;
        		int count = m_RequestQueue.Count;
        		if (count <= 0)
        		{
        			return false;
        		}
        		float realtimeSinceStartup = Time.realtimeSinceStartup;
        		for (int i = 0; i < count; i++)
        		{
        			int num = m_RequestQueue.Dequeue();
        			if (!m_Requests.TryGetValue(num, out var value))
        			{
        				if (m_LogDebug)
        				{
        					NGLog.Warn("Dialogue", NGLog.Format("Missing request state", ("id", num)));
        				}
        			}
        			else if (value.Status != DialogueStatus.Completed && value.Status != DialogueStatus.Failed && value.Status != DialogueStatus.Cancelled)
        			{
        				if (!(value.NextAttemptAt > 0f) || !(realtimeSinceStartup < value.NextAttemptAt))
        				{
        					requestId = num;
        					state = value;
        					return true;
        				}
        				m_RequestQueue.Enqueue(num);
        			}
        		}
        		return false;
        	}
        
        	private int GetEffectiveMaxConcurrentRequests()
        	{
        		int num = Mathf.Clamp(m_MaxConcurrentRequests, 1, 8);
        		if (!m_AutoRaiseRemoteConcurrency || num > 1)
        		{
        			return num;
        		}
        		return 2;
        	}
        
        	private int GetQueueIdleDelayMs()
        	{
        		if (m_RequestQueue.Count <= 0)
        		{
        			return 10;
        		}
        		float realtimeSinceStartup = Time.realtimeSinceStartup;
        		float num = float.MaxValue;
        		foreach (int item in m_RequestQueue)
        		{
        			if (m_Requests.TryGetValue(item, out var value) && value != null && value.Status != DialogueStatus.Completed && value.Status != DialogueStatus.Failed && value.Status != DialogueStatus.Cancelled)
        			{
        				if (value.NextAttemptAt <= 0f || value.NextAttemptAt <= realtimeSinceStartup)
        				{
        					return 10;
        				}
        				float num2 = value.NextAttemptAt - realtimeSinceStartup;
        				if (num2 < num)
        				{
        					num = num2;
        				}
        			}
        		}
        		if (num == float.MaxValue)
        		{
        			return 10;
        		}
        		return Mathf.Clamp(Mathf.CeilToInt(num * 1000f), 10, 250);
        	}
        
        	private async Task ExecuteRequestWorkerAsync(int requestId, DialogueRequestState state)
        	{
        		if (state == null)
        		{
        			m_ActiveRequestIds.Remove(requestId);
        			return;
        		}
        		bool terminal = false;
        		string activeConversationKey = null;
        		try
        		{
        			if (state.Status == DialogueStatus.Cancelled)
        			{
        				state.Error = (string.IsNullOrWhiteSpace(state.Error) ? "request_cancelled" : state.Error);
        				terminal = true;
        				return;
        			}
        			float now = Time.realtimeSinceStartup;
        			if (state.FirstAttemptAt == float.MinValue)
        			{
        				state.FirstAttemptAt = now;
        			}
        			if (!(await EnsureWarmup()))
        			{
        				state.Status = DialogueStatus.Failed;
        				state.Error = (string.IsNullOrWhiteSpace(m_LastWarmupFailureReason) ? "LLM warmup unavailable." : m_LastWarmupFailureReason);
        				NGLog.Warn("Dialogue", NGLog.Format("Request failed during warmup", ("id", requestId), ("reason", state.Error), ("degraded", m_WarmupDegradedMode), ("retryIn", Mathf.Max(0f, m_NextWarmupRetryAt - Time.realtimeSinceStartup))));
        				terminal = true;
        				return;
        			}
        			if (m_RequestTimeoutSeconds > 0f)
        			{
        				float queueTimeoutSeconds = Mathf.Max(m_RequestTimeoutSeconds, m_RemoteMinRequestTimeoutSeconds);
        				float waitTime = Time.realtimeSinceStartup - state.EnqueuedAt;
        				if (waitTime > queueTimeoutSeconds)
        				{
        					if (!ShouldRetryTimeoutFailures() || !TryScheduleRetry(requestId, state, "timeout_in_queue"))
        					{
        						state.Status = DialogueStatus.Failed;
        						state.Error = "Dialogue request timed out in queue.";
        						TrackTimeout();
        						NGLog.Warn("Dialogue", $"Request timed out in queue | id={requestId} | wait={waitTime}");
        						terminal = true;
        					}
        					return;
        				}
        			}
        			BeginConversationRequest(requestId, state.Request);
        			state.Status = DialogueStatus.InProgress;
        			state.StartedAt = Time.realtimeSinceStartup;
        			AddLatencySample(m_QueueWaitSamplesMs, Mathf.Max(0f, (state.StartedAt - state.EnqueuedAt) * 1000f));
        			string key = ResolveConversationKey(state.Request.SpeakerNetworkId, state.Request.ListenerNetworkId, state.Request.RequestingClientId, state.Request.ConversationKey);
        			state.Request.ConversationKey = key;
        			activeConversationKey = key;
        			m_ActiveConversationKeys.Add(key);
        			NGLog.Transition("Dialogue", "request_dequeued", "worker_started", CreateRequestTraceContext("worker_started", requestId, state.Request, state.FlowId), (Object)(object)this, Network_Game.Diagnostics.LogLevel.Info, "ExecuteRequestWorkerAsync", BuildRequestData(state.Request, ("queueLatencyMs", Mathf.RoundToInt(Mathf.Max(0f, (state.StartedAt - state.EnqueuedAt) * 1000f)))));
        			EmitFlowTrace("worker_started", "worker_started", requestId, state.Request, success: true, DialogueStatus.InProgress, null, state.FlowId);
        			string result = null;
        			DialogueActionResponse pendingActionResponse = null;
        			IDialogueInferenceClient inferenceClient = ResolveInferenceClient();
        			if (inferenceClient == null)
        			{
        				state.Status = DialogueStatus.Failed;
        				state.Error = "Remote inference client unavailable.";
        				NGLog.Ready("Dialogue", "inference_completed", ready: false, CreateRequestTraceContext("inference_completed", requestId, state.Request, state.FlowId), (Object)(object)this, Network_Game.Diagnostics.LogLevel.Warning, "ExecuteRequestWorkerAsync", BuildRequestData(state.Request, ("reason", state.Error), ("backend", "unavailable")));
        				EmitFlowTrace("inference_completed", "inference_completed", requestId, state.Request, success: false, DialogueStatus.Failed, state.Error, state.FlowId);
        				terminal = true;
        				return;
        			}
        			List<DialogueHistoryEntry> history = GetHistoryForConversation(key);
        			List<DialogueInferenceMessage> inferenceHistory = BuildRemoteInferenceHistory(history);
        			string promptForRequest = ApplyRemoteUserPromptBudget(state.Request.Prompt ?? string.Empty);
        			string systemPromptForRequest = await BuildSystemPromptForRequestAsync(state.Request, inferenceHistory.Count == 0);
        			CancellationTokenSource openAiTimeoutCts = null;
        			try
        			{
        				float effectiveRequestTimeoutSeconds = (state.EffectiveTimeoutSeconds = GetEffectiveRequestTimeoutSeconds(systemPromptForRequest, promptForRequest));
        				NGLog.Trigger("Dialogue", "prompt_built", CreateRequestTraceContext("prompt_built", requestId, state.Request, state.FlowId), (Object)(object)this, Network_Game.Diagnostics.LogLevel.Info, "ExecuteRequestWorkerAsync", BuildRequestData(state.Request, ("backend", inferenceClient.BackendName ?? string.Empty), ("promptLen", promptForRequest.Length), ("systemLen", systemPromptForRequest?.Length ?? 0), ("historyCount", inferenceHistory.Count)));
        				EmitFlowTrace("prompt_built", "prompt_built", requestId, state.Request, success: true, DialogueStatus.InProgress, null, state.FlowId);
        				NGLog.Trigger("Dialogue", "inference_started", CreateRequestTraceContext("inference_started", requestId, state.Request, state.FlowId), (Object)(object)this, Network_Game.Diagnostics.LogLevel.Info, "ExecuteRequestWorkerAsync", BuildRequestData(state.Request, ("backend", inferenceClient.BackendName ?? string.Empty), ("timeoutSec", effectiveRequestTimeoutSeconds)));
        				EmitFlowTrace("inference_started", "inference_started", requestId, state.Request, success: true, DialogueStatus.InProgress, null, state.FlowId);
        				openAiTimeoutCts = ((effectiveRequestTimeoutSeconds > 0f) ? new CancellationTokenSource(TimeSpan.FromSeconds(effectiveRequestTimeoutSeconds)) : null);
        				DialogueInferenceRequestOptions requestOptions = BuildInferenceRequestOptions(state.Request, promptForRequest);
        				Task<string> chatTask = (Task<string>)((!(inferenceClient is OpenAIChatClient openAiClient)) ? ((Task)inferenceClient.ChatAsync(systemPromptForRequest, inferenceHistory, promptForRequest, addToHistory: false, openAiTimeoutCts?.Token ?? CancellationToken.None)) : ((Task)openAiClient.ChatWithOptionsAsync(systemPromptForRequest, inferenceHistory, promptForRequest, requestOptions, addToHistory: false, openAiTimeoutCts?.Token ?? CancellationToken.None)));
        				result = await chatTask;
        				if ((requestOptions?.PreferJsonResponse ?? false) && !string.IsNullOrWhiteSpace(result))
        				{
        					OpenAIChatClient.TryExtractActionResponse(result, out pendingActionResponse);
        					if (pendingActionResponse?.Speech != null)
        					{
        						result = pendingActionResponse.Speech;
        					}
        					NGLog.Debug("DialogueFX", NGLog.Format("ActionResponse parsed", ("speech", result ?? string.Empty), ("actionCount", (pendingActionResponse?.Actions?.Count).GetValueOrDefault()), ("rawLen", result?.Length ?? 0)));
        					try
        					{
        						NetworkDialogueService.OnDialogueActionResponse?.Invoke(state.Request.ClientRequestId, state.Request, pendingActionResponse);
        					}
        					catch (Exception ex)
        					{
        						NGLog.Warn("Dialogue", "OnDialogueActionResponse handler threw: " + ex.Message);
        					}
        				}
        				if (!inferenceClient.ManagesHistoryInternally && !string.IsNullOrWhiteSpace(result))
        				{
        					history.Add(new DialogueHistoryEntry("user", promptForRequest));
        					history.Add(new DialogueHistoryEntry("assistant", result));
        				}
        			}
        			catch (OperationCanceledException) when (openAiTimeoutCts?.IsCancellationRequested ?? false)
        			{
        				state.Status = DialogueStatus.Failed;
        				state.Error = "Dialogue request timed out.";
        				TrackTimeout();
        				if (!ShouldRetryTimeoutFailures() || !TryScheduleRetry(requestId, state, "timeout"))
        				{
        					MarkInferenceCompleted(state);
        					state.Status = DialogueStatus.Failed;
        					state.Error = BuildRetryExhaustedError("retry_exhausted_timeout", "Sorry, dialogue timed out. Please try again.");
        					NGLog.Ready("Dialogue", "inference_completed", ready: false, CreateRequestTraceContext("inference_completed", requestId, state.Request, state.FlowId), (Object)(object)this, Network_Game.Diagnostics.LogLevel.Warning, "ExecuteRequestWorkerAsync", BuildRequestData(state.Request, ("reason", state.Error)));
        					EmitFlowTrace("inference_completed", "inference_completed", requestId, state.Request, success: false, DialogueStatus.Failed, state.Error, state.FlowId);
        					terminal = true;
        				}
        				return;
        			}
        			catch (Exception ex3)
        			{
        				Exception ex4 = ex3;
        				bool transientException = IsTransientException(ex4);
        				if (!transientException || !TryScheduleRetry(requestId, state, "transient_exception"))
        				{
        					MarkInferenceCompleted(state);
        					state.Status = DialogueStatus.Failed;
        					state.Error = (transientException ? BuildRetryExhaustedError("retry_exhausted_transient_exception", "Sorry, dialogue is temporarily unavailable. Please try again.") : ("chat_exception_non_transient: " + ex4.Message));
        					NGLog.Ready("Dialogue", "inference_completed", ready: false, CreateRequestTraceContext("inference_completed", requestId, state.Request, state.FlowId), (Object)(object)this, Network_Game.Diagnostics.LogLevel.Error, "ExecuteRequestWorkerAsync", BuildRequestData(state.Request, ("reason", state.Error), ("transient", transientException)));
        					EmitFlowTrace("inference_completed", "inference_completed", requestId, state.Request, success: false, DialogueStatus.Failed, state.Error, state.FlowId);
        					terminal = true;
        				}
        				return;
        			}
        			finally
        			{
        				openAiTimeoutCts?.Dispose();
        			}
        			if (string.IsNullOrWhiteSpace(result))
        			{
        				if (!TryScheduleRetry(requestId, state, "empty_response"))
        				{
        					MarkInferenceCompleted(state);
        					state.Status = DialogueStatus.Failed;
        					state.Error = BuildRetryExhaustedError("retry_exhausted_empty_response", "Sorry, no response was generated. Please try again.");
        					NGLog.Ready("Dialogue", "inference_completed", ready: false, CreateRequestTraceContext("inference_completed", requestId, state.Request, state.FlowId), (Object)(object)this, Network_Game.Diagnostics.LogLevel.Warning, "ExecuteRequestWorkerAsync", BuildRequestData(state.Request, ("reason", state.Error), ("retry", state.RetryCount)));
        					EmitFlowTrace("inference_completed", "inference_completed", requestId, state.Request, success: false, DialogueStatus.Failed, state.Error, state.FlowId);
        					terminal = true;
        				}
        				return;
        			}
        			result = RewriteRefusalResponseForEffectCommands(state.Request, result);
        			MarkInferenceCompleted(state);
        			state.Status = DialogueStatus.Completed;
        			state.ResponseText = result;
        			List<DialogueHistoryEntry> historyToStore = history;
        			StoreHistoryForConversation(key, historyToStore);
        			NGLog.Ready("Dialogue", "inference_completed", ready: true, CreateRequestTraceContext("inference_completed", requestId, state.Request, state.FlowId), (Object)(object)this, Network_Game.Diagnostics.LogLevel.Info, "ExecuteRequestWorkerAsync", BuildRequestData(state.Request, ("responseLen", result.Length)));
        			EmitFlowTrace("inference_completed", "inference_completed", requestId, state.Request, success: true, DialogueStatus.Completed, null, state.FlowId);
        			terminal = true;
        			try
        			{
        				DispatchRawDialogueResponse(requestId, state.Request, state.Status, state.ResponseText);
        			}
        			catch (Exception ex3)
        			{
        				Exception ex5 = ex3;
        				NGLog.Warn("Dialogue", NGLog.Format("Raw dialogue callback failed; continuing", ("id", requestId), ("error", ex5.Message ?? string.Empty)));
        			}
        			TryApplyContextActionsSafe(speechText: string.IsNullOrWhiteSpace(state.Request.Prompt) ? state.ResponseText : (state.ResponseText + " " + state.Request.Prompt), request: state.Request, actions: pendingActionResponse?.Actions);
        			if (state.Request.Broadcast)
        			{
        				try
        				{
        					TryBroadcast(state.Request.SpeakerNetworkId, state.ResponseText, state.Request.BroadcastDuration);
        				}
        				catch (Exception ex3)
        				{
        					Exception ex6 = ex3;
        					NGLog.Warn("DialogueFX", NGLog.Format("Broadcast failed; response already completed", ("id", requestId), ("error", ex6.Message ?? string.Empty)));
        				}
        			}
        			NGLog.Trigger("Dialogue", "effects_applied", CreateRequestTraceContext("effects_applied", requestId, state.Request, state.FlowId), (Object)(object)this, Network_Game.Diagnostics.LogLevel.Info, "ExecuteRequestWorkerAsync", BuildRequestData(state.Request, ("broadcast", state.Request.Broadcast), ("responseLen", (state.ResponseText != null) ? state.ResponseText.Length : 0)));
        			EmitFlowTrace("effects_applied", "effects_applied", requestId, state.Request, success: true, state.Status, null, state.FlowId);
        		}
        		finally
        		{
        			m_ActiveRequestIds.Remove(requestId);
        			if (!string.IsNullOrWhiteSpace(activeConversationKey))
        			{
        				m_ActiveConversationKeys.Remove(activeConversationKey);
        			}
        			if (terminal)
        			{
        				FinalizeTerminalRequest(requestId, state, state.Status == DialogueStatus.Completed);
        				TrackTerminalStatus(state);
        				if (state.StartedAt > 0f)
        				{
        					AddLatencySample(valueMs: Mathf.Max(0f, (((state.InferenceCompletedAt > 0f) ? state.InferenceCompletedAt : Time.realtimeSinceStartup) - state.StartedAt) * 1000f), samples: m_ModelExecutionSamplesMs);
        				}
        				PublishDialogueTelemetry(requestId, state);
        				NotifyIfRequested(requestId, state);
        				NGLog.Ready("Dialogue", "request_completed", state.Status == DialogueStatus.Completed, CreateRequestTraceContext("request_completed", requestId, state.Request, state.FlowId), (Object)(object)this, (state.Status == DialogueStatus.Completed) ? Network_Game.Diagnostics.LogLevel.Info : Network_Game.Diagnostics.LogLevel.Warning, "ExecuteRequestWorkerAsync", BuildRequestData(state.Request, ("status", state.Status), ("error", state.Error ?? string.Empty), ("retry", state.RetryCount)));
        				EmitFlowTrace("request_completed", "request_completed", requestId, state.Request, state.Status == DialogueStatus.Completed, state.Status, state.Error, state.FlowId);
        			}
        			TryLogPeriodicSummary();
        			if (m_RequestQueue.Count > 0 && !m_IsProcessing)
        			{
        				RunFireAndForget(ProcessQueue(), "process_queue");
        			}
        		}
        	}

        	private async void RunFireAndForget(Task task, string operation)
        	{
        		if (task == null)
        		{
        			return;
        		}
        		try
        		{
        			await task;
        		}
        		catch (Exception ex)
        		{
        			NGLog.Warn("Dialogue", NGLog.Format("Async operation failed", ("operation", operation ?? "unknown"), ("error", ex.GetBaseException().Message ?? ex.Message ?? "unknown")));
        		}
        	}
         
        	private bool TryScheduleRetry(int requestId, DialogueRequestState state, string reason)
        	{
        		if (state == null)
        		{
        			return false;
        		}
        		if (state.Status == DialogueStatus.Completed || state.Status == DialogueStatus.Cancelled)
        		{
        			return false;
        		}
        		if (state.RetryCount >= Mathf.Max(0, m_MaxRetries))
        		{
        			NGLog.Ready("Dialogue", "retry_scheduled", ready: false, CreateRequestTraceContext("retry_scheduled", requestId, state.Request, state.FlowId), (Object)(object)this, Network_Game.Diagnostics.LogLevel.Warning, "TryScheduleRetry", BuildRequestData(state.Request, ("retry", state.RetryCount), ("maxRetries", m_MaxRetries), ("reason", reason)));
        			EmitFlowTrace("retry_scheduled", "retry_scheduled", requestId, state.Request, success: false, state.Status, reason, state.FlowId);
        			return false;
        		}
        		state.RetryCount++;
        		state.Status = DialogueStatus.Pending;
        		state.ResponseText = null;
        		state.StartedAt = 0f;
        		state.InferenceCompletedAt = 0f;
        		state.EffectiveTimeoutSeconds = 0f;
        		float num = ((m_RetryJitterSeconds > 0f) ? Random.Range(0f, m_RetryJitterSeconds) : 0f);
        		float num2 = Mathf.Max(0f, m_RetryBackoffSeconds) * (float)state.RetryCount + num;
        		state.NextAttemptAt = Time.realtimeSinceStartup + num2;
        		state.EnqueuedAt = state.NextAttemptAt;
        		state.Error = null;
        		m_RequestQueue.Enqueue(requestId);
        		NGLog.Ready("Dialogue", "retry_scheduled", ready: true, CreateRequestTraceContext("retry_scheduled", requestId, state.Request, state.FlowId), (Object)(object)this, Network_Game.Diagnostics.LogLevel.Warning, "TryScheduleRetry", BuildRequestData(state.Request, ("retry", state.RetryCount), ("maxRetries", m_MaxRetries), ("delaySeconds", num2), ("reason", reason)));
        		EmitFlowTrace("retry_scheduled", "retry_scheduled", requestId, state.Request, success: true, state.Status, reason, state.FlowId);
        		return true;
        	}
        
        	private bool ShouldRetryTimeoutFailures()
        	{
        		return UseOpenAIRemote;
        	}
        
        	private string BuildRetryExhaustedError(string code, string friendlyMessage)
        	{
        		return code + ": " + friendlyMessage;
        	}
        
        	private string RewriteRefusalResponseForEffectCommands(DialogueRequest request, string responseText)
        	{
        		if (string.IsNullOrWhiteSpace(responseText))
        		{
        			return responseText;
        		}
        		if (!LooksLikeModelRefusal(responseText))
        		{
        			return responseText;
        		}
        		string text = request.Prompt ?? string.Empty;
        		PlayerSpecialEffectMode playerSpecialEffectMode = ResolvePlayerSpecialEffectMode(text, string.Empty, null);
        		object obj;
        		switch (playerSpecialEffectMode)
        		{
        		case PlayerSpecialEffectMode.None:
        			return responseText;
        		default:
        			obj = "As you wish. You return to view.";
        			break;
        		case PlayerSpecialEffectMode.FloorDissolve:
        			obj = "As you wish. The floor fades from sight.";
        			break;
        		case PlayerSpecialEffectMode.Dissolve:
        			obj = "As you wish. You fade from sight.";
        			break;
        		}
        		string result = (string)obj;
        		NGLog.Warn("DialogueFX", NGLog.Format("Rewrote model refusal for effect command", ("mode", playerSpecialEffectMode.ToString()), ("prompt", text), ("original", responseText)));
        		return result;
        	}
        
        	private PlayerSpecialEffectMode ResolvePlayerSpecialEffectMode(string promptText, string responseText, List<EffectIntent> intents)
        	{
        		if (intents != null)
        		{
        			for (int i = 0; i < intents.Count; i++)
        			{
        				EffectIntent effectIntent = intents[i];
        				string rawTagName = effectIntent.rawTagName;
        				if (string.IsNullOrWhiteSpace(rawTagName))
        				{
        					continue;
        				}
        				string text = ResolveSpecialIntentTargetHint(effectIntent);
        				string text2 = rawTagName.Trim().ToLowerInvariant();
        				if (text2.Contains("dissolve") || text2.Contains("vanish"))
        				{
        					if (LooksLikeFloorTargetHint(text))
        					{
        						return PlayerSpecialEffectMode.FloorDissolve;
        					}
        					if (string.IsNullOrWhiteSpace(text) || LooksLikePlayerTargetHint(text))
        					{
        						return PlayerSpecialEffectMode.Dissolve;
        					}
        				}
        				else if ((text2.Contains("respawn") || text2.Contains("revive")) && (string.IsNullOrWhiteSpace(text) || LooksLikePlayerTargetHint(text)))
        				{
        					return PlayerSpecialEffectMode.Respawn;
        				}
        			}
        		}
        		if (string.IsNullOrWhiteSpace(responseText))
        		{
        			return PlayerSpecialEffectMode.None;
        		}
        		string text3 = responseText.ToLowerInvariant();
        		if (text3.Contains("[effect:") && (text3.Contains("dissolve") || text3.Contains("vanish")))
        		{
        			if (LooksLikeFloorTargetHint(responseText) || LooksLikeFloorTargetHint(promptText))
        			{
        				return PlayerSpecialEffectMode.FloorDissolve;
        			}
        			return PlayerSpecialEffectMode.Dissolve;
        		}
        		if (text3.Contains("[effect:") && (text3.Contains("respawn") || text3.Contains("revive")))
        		{
        			return PlayerSpecialEffectMode.Respawn;
        		}
        		return PlayerSpecialEffectMode.None;
        	}
        
        	private static string ResolveSpecialIntentTargetHint(EffectIntent intent)
        	{
        		if (intent == null)
        		{
        			return string.Empty;
        		}
        		return (!string.IsNullOrWhiteSpace(intent.anchor)) ? intent.anchor : intent.target;
        	}
        
        	private static bool LooksLikePlayerTargetHint(string targetHint)
        	{
        		if (string.IsNullOrWhiteSpace(targetHint))
        		{
        			return false;
        		}
        		string text = targetHint.Trim().ToLowerInvariant();
        		if (IsPlayerTargetToken(text) || LooksLikeExplicitPlayerTargetToken(text) || IsPlayerHeadAlias(text) || IsPlayerFeetAlias(text))
        		{
        			return true;
        		}
        		bool result;
        		switch (text)
        		{
        		case "self":
        		case "npc":
        		case "caster":
        		case "speaker":
        		case "listener":
        			result = true;
        			break;
        		default:
        			result = false;
        			break;
        		}
        		return result;
        	}
        
        	private static bool LooksLikeFloorTargetHint(string targetHint)
        	{
        		if (string.IsNullOrWhiteSpace(targetHint))
        		{
        			return false;
        		}
        		string text = targetHint.Trim().ToLowerInvariant();
        		if (IsGroundAlias(text))
        		{
        			return true;
        		}
        		return text.Contains("role:floor", StringComparison.Ordinal) || text.Contains("role:terrain", StringComparison.Ordinal) || text.Contains("semantic:floor", StringComparison.Ordinal) || text.Contains("semantic:terrain", StringComparison.Ordinal) || text.Contains("all floor", StringComparison.Ordinal) || text.Contains("all floors", StringComparison.Ordinal) || text.Contains("floors", StringComparison.Ordinal) || text.Contains("floor", StringComparison.Ordinal) || text.Contains("ground", StringComparison.Ordinal) || text.Contains("terrain", StringComparison.Ordinal) || text.Contains("stairs", StringComparison.Ordinal) || text.Contains("stair", StringComparison.Ordinal);
        	}
        
        	private static float ResolveSpecialEffectDurationSeconds(ParticleParameterExtractor.ParticleParameterIntent parameterIntent, float baseDurationSeconds = 5f)
        	{
        		if (parameterIntent.HasExplicitDurationSeconds)
        		{
        			return Mathf.Clamp(parameterIntent.ExplicitDurationSeconds, 0.4f, 20f);
        		}
        		float num = Mathf.Clamp(parameterIntent.DurationMultiplier, 0.35f, 3f);
        		return Mathf.Clamp(baseDurationSeconds * num, 0.4f, 20f);
        	}
        
        	private void AdjustIntentsForProbeMode(DialogueRequest request, ref List<EffectIntent> catalogIntents, ref bool hasCatalogIntents)
        	{
        		if (catalogIntents == null || catalogIntents.Count == 0)
        		{
        			hasCatalogIntents = false;
        			return;
        		}
        		catalogIntents = catalogIntents.Where((EffectIntent intent) => intent != null && !LooksLikePlaceholderEffectTag(intent.rawTagName)).ToList();
        		if (catalogIntents.Count == 0)
        		{
        			NGLog.Info("DialogueFX", NGLog.Format("Probe intents filtered out (placeholder/example tags)", ("requestId", request.ClientRequestId)));
        			hasCatalogIntents = false;
        			return;
        		}
        		if (catalogIntents.Count > 1)
        		{
        			catalogIntents = new List<EffectIntent>(1) { catalogIntents[0] };
        		}
        		hasCatalogIntents = true;
        	}
        
        	private bool ApplyPlayerSpecialEffects(DialogueRequest request, ParticleParameterExtractor.ParticleParameterIntent parameterIntent, PlayerSpecialEffectMode specialEffectMode)
        	{
        		if (specialEffectMode == PlayerSpecialEffectMode.None)
        		{
        			return false;
        		}
        		ulong num = ResolvePreferredListenerTargetNetworkObjectId(request);
        		if (specialEffectMode != PlayerSpecialEffectMode.FloorDissolve && num == 0)
        		{
        			string actionId = BuildActionId(request, 0, "special_effect", specialEffectMode.ToString(), num);
        			RecordActionValidationResult(request, 0, actionId, "special_effect", specialEffectMode.ToString(), "rejected", success: false, "invalid_listener_target", null, 0uL);
        			if (m_LogDebug)
        			{
        				NGLog.Warn("DialogueFX", NGLog.Format("Special effect skipped (invalid listener target)", ("mode", specialEffectMode.ToString()), ("requestId", request.ClientRequestId)));
        			}
        			return false;
        		}
        		switch (specialEffectMode)
        		{
        		case PlayerSpecialEffectMode.Dissolve:
        		{
        			float num3 = ResolveSpecialEffectDurationSeconds(parameterIntent);
        			string actionId4 = BuildActionId(request, 0, "special_effect", "dissolve", num);
        			RecordActionValidationResult(request, 0, actionId4, "special_effect", "dissolve", "validated", success: true, null, null, num, null, null, null, 0f, 0f, num3, num3);
        			RecordReplicationTrace("rpc_sent", "client_rpc", success: true, request, 0, actionId4, "dissolve", "dissolve", 0uL, num, num3.ToString("F2"));
        			ApplyDissolveEffectClientRpc(num, num3, actionId4);
        			RecordExecutionTrace("effect_dispatch", success: true, request, 0, actionId4, null, "special_effect", "dissolve", "dissolve", 0uL, num);
        			NGLog.Info("DialogueFX", NGLog.Format("Special effect applied", ("mode", "dissolve"), ("target", num), ("duration", num3.ToString("F2"))));
        			return true;
        		}
        		case PlayerSpecialEffectMode.FloorDissolve:
        		{
        			float num2 = ResolveSpecialEffectDurationSeconds(parameterIntent, 8f);
        			string actionId3 = BuildActionId(request, 0, "special_effect", "floor_dissolve", 0uL);
        			RecordActionValidationResult(request, 0, actionId3, "special_effect", "floor_dissolve", "validated", success: true, null, "ground", 0uL, null, "Area", null, 0f, 0f, num2, num2);
        			RecordReplicationTrace("rpc_sent", "client_rpc", success: true, request, 0, actionId3, "dissolve", "floor_dissolve", 0uL, 0uL, num2.ToString("F2"));
        			ApplyFloorDissolveEffectClientRpc(num2, actionId3);
        			RecordExecutionTrace("effect_dispatch", success: true, request, 0, actionId3, null, "special_effect", "dissolve", "floor_dissolve", 0uL, 0uL);
        			NGLog.Info("DialogueFX", NGLog.Format("Special effect applied", ("mode", "floor_dissolve"), ("duration", num2.ToString("F2"))));
        			return true;
        		}
        		case PlayerSpecialEffectMode.Respawn:
        		{
        			string actionId2 = BuildActionId(request, 0, "special_effect", "respawn", num);
        			RecordActionValidationResult(request, 0, actionId2, "special_effect", "respawn", "validated", success: true, null, null, num);
        			RecordReplicationTrace("rpc_sent", "client_rpc", success: true, request, 0, actionId2, "respawn", "respawn", 0uL, num);
        			ApplyRespawnEffectClientRpc(num, actionId2);
        			RecordExecutionTrace("effect_dispatch", success: true, request, 0, actionId2, null, "special_effect", "respawn", "respawn", 0uL, num);
        			NGLog.Info("DialogueFX", NGLog.Format("Special effect applied", ("mode", "respawn"), ("target", num)));
        			return true;
        		}
        		default:
        			return false;
        		}
        	}
        
        	private static bool LooksLikeModelRefusal(string text)
        	{
        		if (string.IsNullOrWhiteSpace(text))
        		{
        			return false;
        		}
        		string text2 = text.ToLowerInvariant();
        		if (text2.Contains("can't assist") || text2.Contains("cannot assist"))
        		{
        			return true;
        		}
        		if (text2.Contains("can't help") || text2.Contains("cannot help"))
        		{
        			return true;
        		}
        		return text2.Contains("i'm sorry") && (text2.Contains("can't") || text2.Contains("cannot") || text2.Contains("unable"));
        	}
        
        	private void FinalizeTerminalRequest(int requestId, DialogueRequestState requestState, bool completed)
        	{
        		if (requestState != null && !requestState.CompletionIssued)
        		{
        			requestState.CompletionIssued = true;
        			CompleteConversationRequest(requestId, requestState, completed);
        		}
        	}
        
        	private bool IsTransientException(Exception ex)
        	{
        		if (ex == null)
        		{
        			return false;
        		}
        		if (ex is TimeoutException || ex is TaskCanceledException || ex is OperationCanceledException)
        		{
        			return true;
        		}
        		string text = ex.Message ?? string.Empty;
        		return text.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("timed out", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("temporar", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("connection reset", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("429", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("503", StringComparison.OrdinalIgnoreCase) >= 0;
        	}
        
        	private void TrackTerminalStatus(DialogueRequestState state)
        	{
        		m_TotalRequestsFinished++;
        		switch (state.Status)
        		{
        		case DialogueStatus.Completed:
        			m_TotalTerminalCompleted++;
        			break;
        		case DialogueStatus.Cancelled:
        			m_TotalTerminalCancelled++;
        			break;
        		case DialogueStatus.Failed:
        			m_TotalTerminalFailed++;
        			if (IsRejectedReason(state.Error))
        			{
        				TrackRejected(state.Error);
        			}
        			break;
        		}
        	}
        
        	private void TrackRejected(string rejectionReason)
        	{
        		string text = (string.IsNullOrWhiteSpace(rejectionReason) ? "request_rejected" : rejectionReason.Trim());
        		m_TotalTerminalRejected++;
        		m_RejectionReasonOrder.Enqueue(text);
        		if (m_RejectionReasonCounts.TryGetValue(text, out var value))
        		{
        			m_RejectionReasonCounts[text] = value + 1;
        		}
        		else
        		{
        			m_RejectionReasonCounts[text] = 1;
        		}
        		while (m_RejectionReasonOrder.Count > m_RejectionReasonWindow)
        		{
        			string key = m_RejectionReasonOrder.Dequeue();
        			if (m_RejectionReasonCounts.TryGetValue(key, out var value2))
        			{
        				value2--;
        				if (value2 <= 0)
        				{
        					m_RejectionReasonCounts.Remove(key);
        				}
        				else
        				{
        					m_RejectionReasonCounts[key] = value2;
        				}
        			}
        		}
        	}
        
        	private static bool IsRejectedReason(string reason)
        	{
        		if (string.IsNullOrWhiteSpace(reason))
        		{
        			return false;
        		}
        		return reason.IndexOf("request_rejected", StringComparison.OrdinalIgnoreCase) >= 0 || reason.IndexOf("queue_full", StringComparison.OrdinalIgnoreCase) >= 0 || reason.IndexOf("conversation_in_flight", StringComparison.OrdinalIgnoreCase) >= 0 || reason.IndexOf("awaiting_user_message", StringComparison.OrdinalIgnoreCase) >= 0 || reason.IndexOf("duplicate_prompt", StringComparison.OrdinalIgnoreCase) >= 0 || reason.IndexOf("repeat_delay", StringComparison.OrdinalIgnoreCase) >= 0 || reason.IndexOf("rate_limited", StringComparison.OrdinalIgnoreCase) >= 0 || reason.IndexOf("invalid_", StringComparison.OrdinalIgnoreCase) >= 0 || reason.IndexOf("participants_missing", StringComparison.OrdinalIgnoreCase) >= 0;
        	}
        
        	private void TrackTimeout()
        	{
        		m_TimeoutCount++;
        	}
        
        	private void AddLatencySample(Queue<float> samples, float valueMs)
        	{
        		samples.Enqueue(Mathf.Max(0f, valueMs));
        		while (samples.Count > m_LatencySampleWindow)
        		{
        			samples.Dequeue();
        		}
        	}
        
        	private static LatencyHistogram BuildLatencyHistogram(IEnumerable<float> samples)
        	{
        		List<float> list = new List<float>();
        		float num = 0f;
        		float num2 = float.MaxValue;
        		float num3 = 0f;
        		int num4 = 0;
        		int num5 = 0;
        		int num6 = 0;
        		int num7 = 0;
        		int num8 = 0;
        		int num9 = 0;
        		foreach (float sample in samples)
        		{
        			list.Add(sample);
        			num += sample;
        			num2 = Math.Min(num2, sample);
        			num3 = Math.Max(num3, sample);
        			if (sample < 100f)
        			{
        				num4++;
        			}
        			else if (sample < 250f)
        			{
        				num5++;
        			}
        			else if (sample < 500f)
        			{
        				num6++;
        			}
        			else if (sample < 1000f)
        			{
        				num7++;
        			}
        			else if (sample < 2000f)
        			{
        				num8++;
        			}
        			else
        			{
        				num9++;
        			}
        		}
        		if (list.Count == 0)
        		{
        			return default(LatencyHistogram);
        		}
        		list.Sort();
        		return new LatencyHistogram
        		{
        			SampleCount = list.Count,
        			TotalMs = num,
        			MinMs = num2,
        			MaxMs = num3,
        			P50Ms = Percentile(list, 0.5f),
        			P95Ms = Percentile(list, 0.95f),
        			Under100Ms = num4,
        			Under250Ms = num5,
        			Under500Ms = num6,
        			Under1000Ms = num7,
        			Under2000Ms = num8,
        			Over2000Ms = num9
        		};
        	}
        
        	private static float Percentile(List<float> orderedSamples, float percentile)
        	{
        		if (orderedSamples == null || orderedSamples.Count == 0)
        		{
        			return 0f;
        		}
        		float num = Mathf.Clamp01(percentile);
        		int index = Mathf.Clamp(Mathf.CeilToInt(num * (float)orderedSamples.Count) - 1, 0, orderedSamples.Count - 1);
        		return orderedSamples[index];
        	}
        
        	private KeyValuePair<string, int>[] BuildRejectionCountsSnapshot()
        	{
        		if (m_RejectionReasonCounts.Count == 0)
        		{
        			return Array.Empty<KeyValuePair<string, int>>();
        		}
        		List<KeyValuePair<string, int>> list = new List<KeyValuePair<string, int>>(m_RejectionReasonCounts);
        		list.Sort((KeyValuePair<string, int> left, KeyValuePair<string, int> right) => right.Value.CompareTo(left.Value));
        		return list.ToArray();
        	}
        
        	private void TryLogPeriodicSummary()
        	{
        		if (m_SummaryLogIntervalSeconds <= 0f)
        		{
        			return;
        		}
        		float realtimeSinceStartup = Time.realtimeSinceStartup;
        		if (!(m_LastSummaryLogAt > 0f) || !(realtimeSinceStartup - m_LastSummaryLogAt < m_SummaryLogIntervalSeconds))
        		{
        			m_LastSummaryLogAt = realtimeSinceStartup;
        			DialogueStats stats = GetStats();
        			string item = "none";
        			int num = 0;
        			if (stats.RejectionReasonCounts != null && stats.RejectionReasonCounts.Length != 0)
        			{
        				item = stats.RejectionReasonCounts[0].Key;
        				num = stats.RejectionReasonCounts[0].Value;
        			}
        			NGLog.Info("Dialogue", NGLog.Format("Summary", ("enqueued", stats.TotalRequestsEnqueued), ("finished", stats.TotalRequestsFinished), ("completed", stats.TotalTerminalCompleted), ("failed", stats.TotalTerminalFailed), ("cancelled", stats.TotalTerminalCancelled), ("rejected", stats.TotalTerminalRejected), ("timeouts", stats.TimeoutCount), ("successRate", stats.SuccessRate.ToString("P1")), ("queueP95Ms", stats.QueueWaitHistogram.P95Ms.ToString("F0")), ("modelP95Ms", stats.ModelExecutionHistogram.P95Ms.ToString("F0")), ("topRejection", item), ("topRejectionCount", num)));
        		}
        	}
        
        	private async Task<bool> EnsureWarmup()
        	{
        		if (m_WarmupTask != null && (m_WarmupTask.IsFaulted || m_WarmupTask.IsCanceled))
        		{
        			string staleReason = ExtractWarmupFailureReason(m_WarmupTask);
        			m_WarmupTask = null;
        			m_LastWarmupFailureReason = staleReason;
        			if (m_LogDebug)
        			{
        				NGLog.Warn("Dialogue", NGLog.Format("Cleared stale warmup task", ("reason", staleReason)));
        			}
        		}
        		float now = Time.realtimeSinceStartup;
        		if (m_WarmupTask == null && m_NextWarmupRetryAt > now)
        		{
        			float retryIn = m_NextWarmupRetryAt - now;
        			if (m_WarmupDegradedMode)
        			{
        				m_LastWarmupFailureReason = $"Warmup degraded mode active; retry in {retryIn:0.0}s.";
        			}
        			return false;
        		}
        		if (m_WarmupTask == null)
        		{
        			if (m_LogDebug)
        			{
        				NGLog.Debug("Dialogue", "Starting LLM warmup.");
        			}
        			IDialogueInferenceClient inferenceClient = ResolveInferenceClient();
        			if (inferenceClient == null)
        			{
        				throw new Exception("Remote inference client unavailable.");
        			}
        			float remoteProbeTimeoutSeconds = Mathf.Clamp((m_WarmupTimeoutSeconds > 0f) ? m_WarmupTimeoutSeconds : 10f, 5f, 20f);
        			m_WarmupTask = Task.Run(async delegate
        			{
        				using CancellationTokenSource warmupCts = new CancellationTokenSource(TimeSpan.FromSeconds(remoteProbeTimeoutSeconds));
        				if (!(await inferenceClient.CheckConnectionAsync(warmupCts.Token)))
        				{
        					throw new Exception("OpenAI-compatible warmup probe failed at " + GetRemoteEndpointLabel());
        				}
        			});
        		}
        		Task activeTask = m_WarmupTask;
        		float effectiveWarmupTimeoutSeconds = ((m_WarmupTimeoutSeconds > 0f) ? Mathf.Min(m_WarmupTimeoutSeconds, 20f) : 20f);
        		if (effectiveWarmupTimeoutSeconds > 0f && await Task.WhenAny(new Task[2]
        		{
        			activeTask,
        			Task.Delay(TimeSpan.FromSeconds(effectiveWarmupTimeoutSeconds))
        		}) != activeTask)
        		{
        			return HandleWarmupFailure($"Warmup timed out after {effectiveWarmupTimeoutSeconds:0.0}s.");
        		}
        		try
        		{
        			await activeTask;
        			m_WarmupTask = activeTask;
        			m_WarmupConsecutiveFailures = 0;
        			m_WarmupDegradedMode = false;
        			m_NextWarmupRetryAt = 0f;
        			m_LastWarmupFailureReason = string.Empty;
        			return true;
        		}
        		catch (Exception)
        		{
        			return HandleWarmupFailure(ExtractWarmupFailureReason(activeTask));
        		}
        	}
        
        	private bool HandleWarmupFailure(string reason)
        	{
        		m_WarmupTask = null;
        		m_WarmupConsecutiveFailures++;
        		m_LastWarmupFailureReason = (string.IsNullOrWhiteSpace(reason) ? "Warmup failed with unknown reason." : reason);
        		float num = Mathf.Max(0f, m_WarmupRetryCooldownSeconds);
        		m_NextWarmupRetryAt = Time.realtimeSinceStartup + num;
        		if (m_WarmupConsecutiveFailures >= Mathf.Max(1, m_DegradedWarmupFailureThreshold))
        		{
        			m_WarmupDegradedMode = true;
        			NGLog.Error("Dialogue", NGLog.Format("Warmup degraded mode enabled", ("failures", m_WarmupConsecutiveFailures), ("reason", m_LastWarmupFailureReason), ("retryIn", num)));
        		}
        		else
        		{
        			NGLog.Warn("Dialogue", NGLog.Format("Warmup failed", ("failures", m_WarmupConsecutiveFailures), ("reason", m_LastWarmupFailureReason), ("retryIn", num)));
        		}
        		return false;
        	}
        
        	private static string ExtractWarmupFailureReason(Task task)
        	{
        		if (task == null)
        		{
        			return "Warmup task missing.";
        		}
        		if (task.IsCanceled)
        		{
        			return "Warmup task canceled.";
        		}
        		if (!task.IsFaulted)
        		{
        			return "Warmup task failed.";
        		}
        		Exception ex = task.Exception?.GetBaseException();
        		return string.IsNullOrWhiteSpace(ex?.Message) ? "Warmup task faulted." : ("Warmup task faulted: " + ex.Message);
        	}
        
        	private void EnsureOpenAIChatClient()
        	{
        		if (m_OpenAIChatClient != null)
        		{
        			SyncOpenAIChatClientParams();
        			return;
        		}
        		m_OpenAIChatClient = new OpenAIChatClient();
        		SyncOpenAIChatClientParams();
        		NGLog.Info("Dialogue", NGLog.Format("OpenAI chat client initialized", ("host", m_OpenAIChatClient.Host), ("port", m_OpenAIChatClient.Port), ("model", string.IsNullOrEmpty(ResolveConfiguredRemoteModelName()) ? "(auto)" : ResolveConfiguredRemoteModelName())));
        	}
        
        	private IDialogueInferenceClient ResolveInferenceClient()
        	{
        		EnsureOpenAIChatClient();
        		return m_OpenAIChatClient;
        	}
        
        	public OpenAIChatClient CreateConfiguredOpenAIChatClient()
        	{
        		OpenAIChatClient openAIChatClient = new OpenAIChatClient();
        		openAIChatClient.ApplyConfig(BuildInferenceRuntimeConfig());
        		return openAIChatClient;
        	}
        
        	public DialogueInferenceRuntimeConfig CreateInferenceRuntimeConfigSnapshot()
        	{
        		return BuildInferenceRuntimeConfig();
        	}
        
        	private void SyncOpenAIChatClientParams()
        	{
        		if (m_OpenAIChatClient != null)
        		{
        			m_OpenAIChatClient.ApplyConfig(BuildInferenceRuntimeConfig());
        		}
        	}
        
        	private DialogueInferenceRuntimeConfig BuildInferenceRuntimeConfig()
        	{
        		DialogueBackendConfig dialogueBackendConfig = GetDialogueBackendConfig();
        		if ((Object)(object)dialogueBackendConfig != (Object)null)
        		{
        			return new DialogueInferenceRuntimeConfig
        			{
        				Host = dialogueBackendConfig.Host,
        				Port = dialogueBackendConfig.Port,
        				ApiKey = ResolveRemoteApiKey(),
        				Model = ResolveConfiguredRemoteModelName(),
        				Temperature = dialogueBackendConfig.Temperature,
        				MaxTokens = dialogueBackendConfig.MaxTokens,
        				TopP = dialogueBackendConfig.TopP,
        				FrequencyPenalty = dialogueBackendConfig.FrequencyPenalty,
        				PresencePenalty = dialogueBackendConfig.PresencePenalty,
        				Seed = dialogueBackendConfig.Seed,
        				TopK = dialogueBackendConfig.TopK,
        				RepeatPenalty = dialogueBackendConfig.RepeatPenalty,
        				MinP = dialogueBackendConfig.MinP,
        				TypicalP = dialogueBackendConfig.TypicalP,
        				RepeatLastN = dialogueBackendConfig.RepeatLastN,
        				Mirostat = dialogueBackendConfig.Mirostat,
        				MirostatTau = dialogueBackendConfig.MirostatTau,
        				MirostatEta = dialogueBackendConfig.MirostatEta,
        				NProbs = dialogueBackendConfig.NProbs,
        				IgnoreEos = dialogueBackendConfig.IgnoreEos,
        				CachePrompt = dialogueBackendConfig.CachePrompt,
        				Grammar = (string.IsNullOrWhiteSpace(dialogueBackendConfig.Grammar) ? null : dialogueBackendConfig.Grammar),
        				StopSequences = ResolveConfiguredRemoteStopSequences()
        			};
        		}
        		return new DialogueInferenceRuntimeConfig
        		{
        			Host = "127.0.0.1",
        			Port = 7002,
        			ApiKey = ResolveRemoteApiKey(),
        			Model = ResolveConfiguredRemoteModelName(),
        			StopSequences = ResolveConfiguredRemoteStopSequences()
        		};
        	}
        
        	private string ResolveConfiguredRemoteModelName()
        	{
        		DialogueBackendConfig dialogueBackendConfig = GetDialogueBackendConfig();
        		if ((Object)(object)dialogueBackendConfig != (Object)null)
        		{
        			string model = dialogueBackendConfig.Model;
        			if (!string.IsNullOrWhiteSpace(model))
        			{
        				if (string.Equals(model, "auto", StringComparison.OrdinalIgnoreCase) || string.Equals(model, "(auto)", StringComparison.OrdinalIgnoreCase))
        				{
        					return string.Empty;
        				}
        				return model;
        			}
        		}
        		string text = ((m_RemoteModelName == null) ? string.Empty : m_RemoteModelName.Trim());
        		if (string.IsNullOrWhiteSpace(text))
        		{
        			return string.Empty;
        		}
        		if (string.Equals(text, "auto", StringComparison.OrdinalIgnoreCase) || string.Equals(text, "(auto)", StringComparison.OrdinalIgnoreCase))
        		{
        			return string.Empty;
        		}
        		return text;
        	}
        
        	private string ResolveRemoteApiKey()
        	{
        		if (TryGetApiKeyFromLmStudioFile(out var apiKey))
        		{
        			return apiKey;
        		}
        		if (TryGetApiKeyFromEnvironment("LMSTUDIO_API_KEY", out apiKey))
        		{
        			return apiKey;
        		}
        		if (TryGetApiKeyFromEnvironment("OPENAI_API_KEY", out apiKey))
        		{
        			return apiKey;
        		}
        		if ((Object)(object)GetDialogueBackendConfig() != (Object)null && !string.IsNullOrWhiteSpace(m_DialogueBackendConfig.ApiKey))
        		{
        			return m_DialogueBackendConfig.ApiKey;
        		}
        		return string.Empty;
        	}
        
        	private string[] ResolveConfiguredRemoteStopSequences()
        	{
        		DialogueBackendConfig dialogueBackendConfig = GetDialogueBackendConfig();
        		if ((Object)(object)dialogueBackendConfig != (Object)null)
        		{
        			string[] stopSequences = dialogueBackendConfig.GetStopSequences();
        			if (stopSequences != null && stopSequences.Length != 0)
        			{
        				return stopSequences;
        			}
        		}
        		return (m_RemoteStopSequences != null && m_RemoteStopSequences.Length != 0) ? m_RemoteStopSequences : null;
        	}
        
        	private static bool TryGetApiKeyFromEnvironment(string varName, out string apiKey)
        	{
        		apiKey = Environment.GetEnvironmentVariable(varName);
        		if (string.IsNullOrWhiteSpace(apiKey))
        		{
        			apiKey = string.Empty;
        			return false;
        		}
        		apiKey = apiKey.Trim();
        		return true;
        	}
        
        	private static bool TryGetApiKeyFromLmStudioFile(out string apiKey)
        	{
        		apiKey = string.Empty;
        		try
        		{
        			string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        			if (string.IsNullOrWhiteSpace(folderPath))
        			{
        				return false;
        			}
        			string path = Path.Combine(folderPath, ".lmstudio", "lms-key");
        			if (!File.Exists(path))
        			{
        				return false;
        			}
        			string text = File.ReadAllText(path);
        			if (string.IsNullOrWhiteSpace(text))
        			{
        				return false;
        			}
        			apiKey = text.Trim();
        			return true;
        		}
        		catch
        		{
        			return false;
        		}
        	}
        
        	private string BuildWarmupStateLabel()
        	{
        		if (m_WarmupTask != null && !m_WarmupTask.IsCompleted)
        		{
        			return "InProgress";
        		}
        		if (m_WarmupDegradedMode)
        		{
        			return "Degraded";
        		}
        		if (m_WarmupConsecutiveFailures > 0)
        		{
        			return "RetryCooldown";
        		}
        		if (m_WarmupTask != null && m_WarmupTask.IsCompleted && !m_WarmupTask.IsFaulted && !m_WarmupTask.IsCanceled)
        		{
        			return "Ready";
        		}
        		return "Idle";
        	}
        
        	private List<DialogueHistoryEntry> GetHistoryInternal(string key)
        	{
        		if (!m_Histories.TryGetValue(key, out var value))
        		{
        			value = new List<DialogueHistoryEntry>();
        			m_Histories[key] = value;
        		}
        		return value;
        	}
        
        	private List<DialogueHistoryEntry> GetHistory(string key)
        	{
        		return GetHistoryInternal(key);
        	}
        
        	private List<DialogueHistoryEntry> GetHistoryForConversation(string conversationKey)
        	{
        		return GetHistory(conversationKey);
        	}
        
        	private void RegisterClientRequestLookup(int requestId, DialogueRequest request)
        	{
        		if (request.ClientRequestId > 0)
        		{
        			ClientRequestLookupKey key = new ClientRequestLookupKey(request.ClientRequestId, request.RequestingClientId);
        			m_RequestIdsByScopedClientRequest[key] = requestId;
        			if (!m_RequestIdsByClientRequestId.TryGetValue(request.ClientRequestId, out var value))
        			{
        				value = new List<int>(1);
        				m_RequestIdsByClientRequestId[request.ClientRequestId] = value;
        			}
        			value.Add(requestId);
        		}
        	}
        
        	private void UnregisterClientRequestLookup(int requestId, DialogueRequest request)
        	{
        		if (request.ClientRequestId <= 0)
        		{
        			return;
        		}
        		ClientRequestLookupKey key = new ClientRequestLookupKey(request.ClientRequestId, request.RequestingClientId);
        		if (m_RequestIdsByScopedClientRequest.TryGetValue(key, out var value) && value == requestId)
        		{
        			m_RequestIdsByScopedClientRequest.Remove(key);
        		}
        		if (!m_RequestIdsByClientRequestId.TryGetValue(request.ClientRequestId, out var value2))
        		{
        			return;
        		}
        		for (int num = value2.Count - 1; num >= 0; num--)
        		{
        			if (value2[num] == requestId)
        			{
        				value2.RemoveAt(num);
        				break;
        			}
        		}
        		if (value2.Count == 0)
        		{
        			m_RequestIdsByClientRequestId.Remove(request.ClientRequestId);
        		}
        	}
        
        	private bool TryGetRequestIdByClientRequestId(int clientRequestId, ulong requestingClientId, out int requestId)
        	{
        		requestId = -1;
        		if (clientRequestId <= 0)
        		{
        			return false;
        		}
        		if (requestingClientId != ulong.MaxValue)
        		{
        			ClientRequestLookupKey key = new ClientRequestLookupKey(clientRequestId, requestingClientId);
        			if (m_RequestIdsByScopedClientRequest.TryGetValue(key, out var value))
        			{
        				if (m_Requests.ContainsKey(value))
        				{
        					requestId = value;
        					return true;
        				}
        				m_RequestIdsByScopedClientRequest.Remove(key);
        			}
        		}
        		if (!m_RequestIdsByClientRequestId.TryGetValue(clientRequestId, out var value2))
        		{
        			return false;
        		}
        		for (int num = value2.Count - 1; num >= 0; num--)
        		{
        			int num2 = value2[num];
        			if (!m_Requests.TryGetValue(num2, out var value3) || value3 == null)
        			{
        				value2.RemoveAt(num);
        			}
        			else if (requestingClientId == ulong.MaxValue || value3.Request.RequestingClientId == requestingClientId)
        			{
        				requestId = num2;
        				return true;
        			}
        		}
        		if (value2.Count == 0)
        		{
        			m_RequestIdsByClientRequestId.Remove(clientRequestId);
        		}
        		return false;
        	}
        
        	private List<DialogueInferenceMessage> BuildRemoteInferenceHistory(List<DialogueHistoryEntry> fullHistory)
        	{
        		if (fullHistory == null || fullHistory.Count == 0)
        		{
        			return new List<DialogueInferenceMessage>();
        		}
        		int num = Mathf.Max(0, m_RemoteMaxHistoryMessages);
        		int num2 = Mathf.Max(1, m_RemoteHistoryHardCapMessages);
        		num = Mathf.Min(num, num2);
        		if (num <= 0)
        		{
        			return new List<DialogueInferenceMessage>();
        		}
        		int num3 = Mathf.Min(num, fullHistory.Count);
        		int num4 = fullHistory.Count - num3;
        		int maxChars = Mathf.Clamp(m_RemoteHistoryMessageCharBudget, 64, 1024);
        		List<DialogueInferenceMessage> list = new List<DialogueInferenceMessage>(num3);
        		for (int i = num4; i < fullHistory.Count; i++)
        		{
        			DialogueHistoryEntry dialogueHistoryEntry = fullHistory[i];
        			if (dialogueHistoryEntry != null)
        			{
        				string text = TrimPromptSegment(dialogueHistoryEntry.Content, maxChars);
        				if (!string.IsNullOrWhiteSpace(text))
        				{
        					list.Add(new DialogueInferenceMessage(NormalizeHistoryRole(dialogueHistoryEntry.Role), text));
        				}
        			}
        		}
        		return list;
        	}
        
        	private static string NormalizeHistoryRole(string role)
        	{
        		if (string.IsNullOrWhiteSpace(role))
        		{
        			return "user";
        		}
        		string text = role.Trim().ToLowerInvariant();
        		if (text == "user" || text == "assistant" || text == "system")
        		{
        			return text;
        		}
        		return text.Contains("assistant", StringComparison.Ordinal) ? "assistant" : "user";
        	}
        
        	private static string TrimPromptSegment(string content, int maxChars)
        	{
        		if (string.IsNullOrWhiteSpace(content))
        		{
        			return string.Empty;
        		}
        		string text = content.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
        		if (text.Length <= maxChars)
        		{
        			return text;
        		}
        		maxChars = Mathf.Max(64, maxChars);
        		int num = Mathf.Clamp(Mathf.FloorToInt((float)maxChars * 0.72f), 48, maxChars - 24);
        		int num2 = maxChars - num - 12;
        		if (num2 < 12)
        		{
        			num2 = 12;
        			num = maxChars - num2 - 12;
        		}
        		return text.Substring(0, num).TrimEnd() + " [..] " + text.Substring(text.Length - num2).TrimStart();
        	}
        
        	private string ApplyRemoteUserPromptBudget(string prompt)
        	{
        		if (!UseOpenAIRemote || string.IsNullOrWhiteSpace(prompt))
        		{
        			return prompt;
        		}
        		int num = Mathf.Clamp(m_RemoteUserPromptCharBudget, 64, 1024);
        		string text = TrimPromptSegment(prompt, num);
        		if (m_LogDebug && text.Length < prompt.Length)
        		{
        			NGLog.Warn("Dialogue", NGLog.Format("Trimmed remote user prompt", ("fromChars", prompt.Length), ("toChars", text.Length), ("budget", num)));
        		}
        		return text;
        	}
        
        	private void TrimHistory(string key, int maxMessages)
        	{
        		if (maxMessages > 0 && m_Histories.TryGetValue(key, out var value) && value.Count > maxMessages)
        		{
        			int count = value.Count - maxMessages;
        			value.RemoveRange(0, count);
        		}
        	}
        
        	private void StoreHistoryInternal(string key, List<DialogueHistoryEntry> history)
        	{
        		if (history != null)
        		{
        			m_Histories[key] = history;
        			TrimHistory(key, m_MaxHistoryMessages);
        		}
        	}
        
        	private void StoreHistory(string key, List<DialogueHistoryEntry> history)
        	{
        		StoreHistoryInternal(key, history);
        	}
        
        	private void StoreHistoryForConversation(string conversationKey, List<DialogueHistoryEntry> history)
        	{
        		StoreHistory(conversationKey, history);
        	}
        
        	private string BuildConversationKey(DialogueRequest request)
        	{
        		return ResolveConversationKey(request.SpeakerNetworkId, request.ListenerNetworkId, request.RequestingClientId, request.ConversationKey);
        	}
        
        	private ConversationState GetConversationState(string conversationKey)
        	{
        		string key = ResolveConversationKey(0uL, 0uL, 0uL, conversationKey);
        		if (!m_ConversationStates.TryGetValue(key, out var value))
        		{
        			value = new ConversationState();
        			m_ConversationStates[key] = value;
        		}
        		return value;
        	}
        
        	private ConversationState GetConversationStateForConversation(string conversationKey)
        	{
        		return GetConversationState(conversationKey);
        	}
        
        	private void BeginConversationRequest(int requestId, DialogueRequest request)
        	{
        		ConversationState conversationStateForConversation = GetConversationStateForConversation(request.ConversationKey);
        		conversationStateForConversation.HasOutstandingRequest = true;
        		conversationStateForConversation.OutstandingRequestId = requestId;
        		conversationStateForConversation.IsInFlight = true;
        		conversationStateForConversation.ActiveRequestId = requestId;
        		if (request.IsUserInitiated)
        		{
        			conversationStateForConversation.AwaitingUserInput = false;
        		}
        	}
        
        	private void CompleteConversationRequest(int requestId, DialogueRequestState requestState, bool completed)
        	{
        		if (requestState == null)
        		{
        			return;
        		}
        		string conversationKey = BuildConversationKey(requestState.Request);
        		ConversationState conversationStateForConversation = GetConversationStateForConversation(conversationKey);
        		if (conversationStateForConversation.ActiveRequestId == requestId || conversationStateForConversation.IsInFlight)
        		{
        			conversationStateForConversation.IsInFlight = false;
        			conversationStateForConversation.ActiveRequestId = -1;
        		}
        		if (conversationStateForConversation.OutstandingRequestId == requestId || conversationStateForConversation.HasOutstandingRequest)
        		{
        			conversationStateForConversation.HasOutstandingRequest = false;
        			conversationStateForConversation.OutstandingRequestId = -1;
        		}
        		if (completed)
        		{
        			conversationStateForConversation.LastCompletedPrompt = requestState.Request.Prompt;
        			conversationStateForConversation.LastCompletedAt = Time.realtimeSinceStartup;
        			conversationStateForConversation.AssistantMessageCount++;
        			if (requestState.Request.RequireUserReply)
        			{
        				conversationStateForConversation.AwaitingUserInput = true;
        			}
        		}
        	}
        
        	private bool CanAcceptRequest(DialogueRequest request, out string reason)
        	{
        		reason = null;
        		ConversationState conversationStateForConversation = GetConversationStateForConversation(request.ConversationKey);
        		if (request.IsUserInitiated)
        		{
        			conversationStateForConversation.AwaitingUserInput = false;
        		}
        		if (!CanAcceptAuthForRequest(request, out reason))
        		{
        			return false;
        		}
        		if (conversationStateForConversation.HasOutstandingRequest || conversationStateForConversation.IsInFlight)
        		{
        			reason = "conversation_in_flight";
        			return false;
        		}
        		if (request.RequireUserReply && conversationStateForConversation.AwaitingUserInput)
        		{
        			reason = "awaiting_user_message";
        			return false;
        		}
        		if (request.BlockRepeatedPrompt && !string.IsNullOrWhiteSpace(request.Prompt) && !string.IsNullOrWhiteSpace(conversationStateForConversation.LastCompletedPrompt) && string.Equals(conversationStateForConversation.LastCompletedPrompt, request.Prompt, StringComparison.OrdinalIgnoreCase))
        		{
        			reason = "duplicate_prompt";
        			return false;
        		}
        		if (request.MinRepeatDelaySeconds > 0f && conversationStateForConversation.LastCompletedAt > float.MinValue)
        		{
        			float num = Time.realtimeSinceStartup - conversationStateForConversation.LastCompletedAt;
        			if (num < request.MinRepeatDelaySeconds)
        			{
        				reason = "repeat_delay";
        				return false;
        			}
        		}
        		if (request.RequestingClientId == 0)
        		{
        			return true;
        		}
        		if (m_MaxRequestsPerClient > 0)
        		{
        			int num2 = 0;
        			foreach (DialogueRequestState value2 in m_Requests.Values)
        			{
        				if (value2.Request.RequestingClientId == request.RequestingClientId && (value2.Status == DialogueStatus.Pending || value2.Status == DialogueStatus.InProgress))
        				{
        					num2++;
        				}
        			}
        			if (num2 >= m_MaxRequestsPerClient)
        			{
        				reason = "rate_limited_active";
        				return false;
        			}
        		}
        		if (m_MinSecondsBetweenRequests > 0f)
        		{
        			if (m_LastRequestTimeByClient.TryGetValue(request.RequestingClientId, out var value))
        			{
        				float num3 = Time.realtimeSinceStartup - value;
        				if (num3 < m_MinSecondsBetweenRequests)
        				{
        					reason = "rate_limited_interval";
        					return false;
        				}
        			}
        			m_LastRequestTimeByClient[request.RequestingClientId] = Time.realtimeSinceStartup;
        		}
        		return true;
        	}
        
        	private bool CanAcceptAuthForRequest(DialogueRequest request, out string rejectionReason)
        	{
        		PlayerIdentitySnapshot snapshot;
        		return NetworkDialogueAuthGate.CanAccept(m_RequireAuthenticatedPlayers, request.IsUserInitiated, request.RequestingClientId, (ulong clientId) => TryGetPlayerIdentityByClientId(clientId, out snapshot), out rejectionReason);
        	}
        
        	private void NotifyIfRequested(int requestId, DialogueRequestState state)
        	{
        		if (!state.Request.NotifyClient)
        		{
        			NGLog.Ready("Dialogue", "client_notified", ready: false, CreateRequestTraceContext("client_notified", requestId, state.Request, state.FlowId), (Object)(object)this, Network_Game.Diagnostics.LogLevel.Info, "NotifyIfRequested", BuildRequestData(state.Request, ("reason", "notify_disabled")));
        			EmitFlowTrace("client_notified", "client_notified", requestId, state.Request, success: false, state.Status, "notify_disabled", state.FlowId);
        			return;
        		}
        		if (!base.IsServer)
        		{
        			NGLog.Ready("Dialogue", "client_notified", ready: false, CreateRequestTraceContext("client_notified", requestId, state.Request, state.FlowId), (Object)(object)this, Network_Game.Diagnostics.LogLevel.Warning, "NotifyIfRequested", BuildRequestData(state.Request, ("reason", "not_server")));
        			EmitFlowTrace("client_notified", "client_notified", requestId, state.Request, success: false, state.Status, "not_server", state.FlowId);
        			return;
        		}
        		if (state.Request.RequestingClientId == 0L && !base.IsHost)
        		{
        			NGLog.Ready("Dialogue", "client_notified", ready: false, CreateRequestTraceContext("client_notified", requestId, state.Request, state.FlowId), (Object)(object)this, Network_Game.Diagnostics.LogLevel.Info, "NotifyIfRequested", BuildRequestData(state.Request, ("reason", "host_only")));
        			EmitFlowTrace("client_notified", "client_notified", requestId, state.Request, success: false, state.Status, "host_only", state.FlowId);
        			return;
        		}
        		NGLog.Publish("Dialogue", "client_rpc_send", CreateRequestTraceContext("client_rpc_send", requestId, state.Request, state.FlowId), (Object)(object)this, Network_Game.Diagnostics.LogLevel.Info, "NotifyIfRequested", BuildRequestData(state.Request, ("status", state.Status), ("error", state.Error ?? string.Empty)));
        		EmitFlowTrace("client_rpc_send", "client_rpc_send", requestId, state.Request, state.Status == DialogueStatus.Completed, state.Status, state.Error, state.FlowId);
        		DialogueResponseClientRpc(requestId, state.Request.ClientRequestId, state.Status, state.ResponseText ?? string.Empty, state.Error ?? string.Empty, state.Request.ConversationKey ?? string.Empty, state.Request.SpeakerNetworkId, state.Request.ListenerNetworkId, state.Request.RequestingClientId, state.Request.IsUserInitiated, base.RpcTarget.Single(state.Request.RequestingClientId, RpcTargetUse.Temp));
        		NGLog.Ready("Dialogue", "client_notified", ready: true, CreateRequestTraceContext("client_notified", requestId, state.Request, state.FlowId), (Object)(object)this, Network_Game.Diagnostics.LogLevel.Info, "NotifyIfRequested", BuildRequestData(state.Request, ("status", state.Status)));
        		EmitFlowTrace("client_notified", "client_notified", requestId, state.Request, success: true, state.Status, state.Error, state.FlowId);
        	}
        
        	private void PublishDialogueTelemetry(int requestId, DialogueRequestState state)
        	{
        		if (state != null)
        		{
        			float realtimeSinceStartup = Time.realtimeSinceStartup;
        			float num = ((state.InferenceCompletedAt > 0f) ? state.InferenceCompletedAt : realtimeSinceStartup);
        			float num2 = 0f;
        			float num3 = 0f;
        			float num4 = 0f;
        			if (state.StartedAt > 0f)
        			{
        				num2 = Mathf.Max(0f, (state.StartedAt - state.EnqueuedAt) * 1000f);
        				num3 = Mathf.Max(0f, (num - state.StartedAt) * 1000f);
        			}
        			if (state.FirstAttemptAt != float.MinValue)
        			{
        				num4 = Mathf.Max(0f, (realtimeSinceStartup - state.FirstAttemptAt) * 1000f);
        			}
        			else if (state.EnqueuedAt > 0f)
        			{
        				num4 = Mathf.Max(0f, (realtimeSinceStartup - state.EnqueuedAt) * 1000f);
        			}
        			NetworkDialogueService.OnDialogueResponseTelemetry?.Invoke(new DialogueResponseTelemetry
        			{
        				RequestId = requestId,
        				Status = state.Status,
        				Error = state.Error,
        				Request = state.Request,
        				RetryCount = Mathf.Max(0, state.RetryCount),
        				QueueLatencyMs = num2,
        				ModelLatencyMs = num3,
        				TotalLatencyMs = num4
        			});
        			RecordExecutionTrace("response_finalized", state.Status == DialogueStatus.Completed, state.Request, requestId, null, state.FlowId, state.Status.ToString(), null, null, 0uL, 0uL, state.Error, BuildExecutionTraceResponsePreview(state.ResponseText));
        			NGLog.Trigger("Dialogue", "telemetry_published", CreateRequestTraceContext("telemetry_published", requestId, state.Request, state.FlowId), (Object)(object)this, Network_Game.Diagnostics.LogLevel.Info, "PublishDialogueTelemetry", BuildRequestData(state.Request, ("status", state.Status), ("queueLatencyMs", Mathf.RoundToInt(num2)), ("modelLatencyMs", Mathf.RoundToInt(num3)), ("totalLatencyMs", Mathf.RoundToInt(num4))));
        			EmitFlowTrace("telemetry_published", "telemetry_published", requestId, state.Request, state.Status == DialogueStatus.Completed, state.Status, state.Error, state.FlowId);
        		}
        	}
        
        	private void MarkInferenceCompleted(DialogueRequestState state)
        	{
        		if (state != null && !(state.StartedAt <= 0f) && !(state.InferenceCompletedAt > 0f))
        		{
        			state.InferenceCompletedAt = Time.realtimeSinceStartup;
        		}
        	}
        
        	private void TryBroadcast(ulong speakerNetworkId, string text, float duration)
        	{
        		if (speakerNetworkId == 0L || string.IsNullOrWhiteSpace(text))
        		{
        			if (m_LogDebug)
        			{
        				NGLog.Warn("Dialogue", NGLog.Format("Broadcast skipped", ("speaker", speakerNetworkId)));
        			}
        			return;
        		}
        		NetworkManager singleton = NetworkManager.Singleton;
        		if ((Object)(object)singleton == (Object)null || !singleton.IsListening || singleton.SpawnManager == null)
        		{
        			if (m_LogDebug)
        			{
        				NGLog.Warn("Dialogue", NGLog.Format("Broadcast skipped (network subsystem unavailable)", ("speaker", speakerNetworkId)));
        			}
        			return;
        		}
        		if (!singleton.SpawnManager.SpawnedObjects.TryGetValue(speakerNetworkId, out var value))
        		{
        			if (m_LogDebug)
        			{
        				NGLog.Warn("Dialogue", NGLog.Format("Broadcast skipped (speaker missing)", ("speaker", speakerNetworkId)));
        			}
        			return;
        		}
        		NpcDialogueActor component = ((Component)value).GetComponent<NpcDialogueActor>();
        		if ((Object)(object)component != (Object)null)
        		{
        			if (m_LogDebug)
        			{
        				NGLog.Debug("Dialogue", NGLog.Format("Broadcast response skipped (speech bubble feature removed)", ("speaker", speakerNetworkId), ("chars", (text ?? string.Empty).Length), ("mode", "NpcDialogueActor")));
        			}
        		}
        		else
        		{
        			NGLog.Warn("Dialogue", NGLog.Format("Broadcast skipped (NpcDialogueActor missing)", ("speaker", speakerNetworkId), ("name", ((Object)value).name)));
        		}
        	}
        
        	private string BuildBroadcastPreviewText(string text)
        	{
        		if (string.IsNullOrWhiteSpace(text))
        		{
        			return string.Empty;
        		}
        		string text2 = text.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
        		if (m_BroadcastSingleLinePreview)
        		{
        			text2 = text2.Replace('\n', ' ');
        			while (text2.Contains("  ", StringComparison.Ordinal))
        			{
        				text2 = text2.Replace("  ", " ", StringComparison.Ordinal);
        			}
        		}
        		int num = Math.Max(40, m_BroadcastMaxCharacters);
        		if (text2.Length > num)
        		{
        			text2 = text2.Substring(0, num).TrimEnd() + "...";
        		}
        		return text2;
        	}
        
    }
}
