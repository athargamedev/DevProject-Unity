using System;
using System.Text;
using Network_Game.Diagnostics;
using UnityEngine;

namespace Network_Game.Dialogue
{
    public partial class NetworkDialogueService
    {
        // Read-side request surface: response consumption, stats, and identity reports.
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
    }
}
