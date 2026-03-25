using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Network_Game.Diagnostics;
using Unity.Netcode;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Network_Game.Dialogue
{
    public partial class NetworkDialogueService
    {
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
            return text.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("timed out", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("temporar", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("connection reset", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("429", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("503", StringComparison.OrdinalIgnoreCase) >= 0;
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
            string text = string.IsNullOrWhiteSpace(rejectionReason)
                ? "request_rejected"
                : rejectionReason.Trim();
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
            return reason.IndexOf("request_rejected", StringComparison.OrdinalIgnoreCase) >= 0
                || reason.IndexOf("queue_full", StringComparison.OrdinalIgnoreCase) >= 0
                || reason.IndexOf("conversation_in_flight", StringComparison.OrdinalIgnoreCase) >= 0
                || reason.IndexOf("awaiting_user_message", StringComparison.OrdinalIgnoreCase) >= 0
                || reason.IndexOf("duplicate_prompt", StringComparison.OrdinalIgnoreCase) >= 0
                || reason.IndexOf("repeat_delay", StringComparison.OrdinalIgnoreCase) >= 0
                || reason.IndexOf("rate_limited", StringComparison.OrdinalIgnoreCase) >= 0
                || reason.IndexOf("invalid_", StringComparison.OrdinalIgnoreCase) >= 0
                || reason.IndexOf("participants_missing", StringComparison.OrdinalIgnoreCase) >= 0;
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
                return default;
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
            int index = Mathf.Clamp(
                Mathf.CeilToInt(num * orderedSamples.Count) - 1,
                0,
                orderedSamples.Count - 1
            );
            return orderedSamples[index];
        }

        private KeyValuePair<string, int>[] BuildRejectionCountsSnapshot()
        {
            if (m_RejectionReasonCounts.Count == 0)
            {
                return Array.Empty<KeyValuePair<string, int>>();
            }
            List<KeyValuePair<string, int>> list = new List<KeyValuePair<string, int>>(
                m_RejectionReasonCounts
            );
            list.Sort(
                (KeyValuePair<string, int> left, KeyValuePair<string, int> right) =>
                    right.Value.CompareTo(left.Value)
            );
            return list.ToArray();
        }

        private void TryLogPeriodicSummary()
        {
            if (m_SummaryLogIntervalSeconds <= 0f)
            {
                return;
            }
            float realtimeSinceStartup = Time.realtimeSinceStartup;
            if (
                (m_LastSummaryLogAt > 0f)
                && (realtimeSinceStartup - m_LastSummaryLogAt < m_SummaryLogIntervalSeconds)
            )
            {
                return;
            }

            m_LastSummaryLogAt = realtimeSinceStartup;
            DialogueStats stats = GetStats();
            string item = "none";
            int num = 0;
            if (stats.RejectionReasonCounts != null && stats.RejectionReasonCounts.Length != 0)
            {
                item = stats.RejectionReasonCounts[0].Key;
                num = stats.RejectionReasonCounts[0].Value;
            }
            NGLog.Info(
                "Dialogue",
                NGLog.Format(
                    "Summary",
                    ("enqueued", stats.TotalRequestsEnqueued),
                    ("finished", stats.TotalRequestsFinished),
                    ("completed", stats.TotalTerminalCompleted),
                    ("failed", stats.TotalTerminalFailed),
                    ("cancelled", stats.TotalTerminalCancelled),
                    ("rejected", stats.TotalTerminalRejected),
                    ("timeouts", stats.TimeoutCount),
                    ("successRate", stats.SuccessRate.ToString("P1")),
                    ("queueP95Ms", stats.QueueWaitHistogram.P95Ms.ToString("F0")),
                    ("modelP95Ms", stats.ModelExecutionHistogram.P95Ms.ToString("F0")),
                    ("topRejection", item),
                    ("topRejectionCount", num)
                )
            );
        }

        private void NotifyIfRequested(int requestId, DialogueRequestState state)
        {
            if (!state.Request.NotifyClient)
            {
                NGLog.Ready(
                    "Dialogue",
                    "client_notified",
                    ready: false,
                    CreateRequestTraceContext("client_notified", requestId, state.Request, state.FlowId),
                    (Object)this,
                    Network_Game.Diagnostics.LogLevel.Info,
                    "NotifyIfRequested",
                    BuildRequestData(state.Request, ("reason", "notify_disabled"))
                );
                EmitFlowTrace(
                    "client_notified",
                    "client_notified",
                    requestId,
                    state.Request,
                    success: false,
                    state.Status,
                    "notify_disabled",
                    state.FlowId
                );
                return;
            }
            if (!base.IsServer)
            {
                NGLog.Ready(
                    "Dialogue",
                    "client_notified",
                    ready: false,
                    CreateRequestTraceContext("client_notified", requestId, state.Request, state.FlowId),
                    (Object)this,
                    Network_Game.Diagnostics.LogLevel.Warning,
                    "NotifyIfRequested",
                    BuildRequestData(state.Request, ("reason", "not_server"))
                );
                EmitFlowTrace(
                    "client_notified",
                    "client_notified",
                    requestId,
                    state.Request,
                    success: false,
                    state.Status,
                    "not_server",
                    state.FlowId
                );
                return;
            }
            if (state.Request.RequestingClientId == 0L && !base.IsHost)
            {
                NGLog.Ready(
                    "Dialogue",
                    "client_notified",
                    ready: false,
                    CreateRequestTraceContext("client_notified", requestId, state.Request, state.FlowId),
                    (Object)this,
                    Network_Game.Diagnostics.LogLevel.Info,
                    "NotifyIfRequested",
                    BuildRequestData(state.Request, ("reason", "host_only"))
                );
                EmitFlowTrace(
                    "client_notified",
                    "client_notified",
                    requestId,
                    state.Request,
                    success: false,
                    state.Status,
                    "host_only",
                    state.FlowId
                );
                return;
            }
            NGLog.Publish(
                "Dialogue",
                "client_rpc_send",
                CreateRequestTraceContext("client_rpc_send", requestId, state.Request, state.FlowId),
                (Object)this,
                Network_Game.Diagnostics.LogLevel.Info,
                "NotifyIfRequested",
                BuildRequestData(
                    state.Request,
                    ("status", state.Status),
                    ("error", state.Error ?? string.Empty)
                )
            );
            EmitFlowTrace(
                "client_rpc_send",
                "client_rpc_send",
                requestId,
                state.Request,
                state.Status == DialogueStatus.Completed,
                state.Status,
                state.Error,
                state.FlowId
            );
            DialogueResponseClientRpc(
                requestId,
                state.Request.ClientRequestId,
                state.Status,
                state.ResponseText ?? string.Empty,
                state.Error ?? string.Empty,
                state.Request.ConversationKey ?? string.Empty,
                state.Request.SpeakerNetworkId,
                state.Request.ListenerNetworkId,
                state.Request.RequestingClientId,
                state.Request.IsUserInitiated,
                base.RpcTarget.Single(state.Request.RequestingClientId, RpcTargetUse.Temp)
            );
            NGLog.Ready(
                "Dialogue",
                "client_notified",
                ready: true,
                CreateRequestTraceContext("client_notified", requestId, state.Request, state.FlowId),
                (Object)this,
                Network_Game.Diagnostics.LogLevel.Info,
                "NotifyIfRequested",
                BuildRequestData(state.Request, ("status", state.Status))
            );
            EmitFlowTrace(
                "client_notified",
                "client_notified",
                requestId,
                state.Request,
                success: true,
                state.Status,
                state.Error,
                state.FlowId
            );
        }

        private void PublishDialogueTelemetry(int requestId, DialogueRequestState state)
        {
            if (state == null)
            {
                return;
            }

            float realtimeSinceStartup = Time.realtimeSinceStartup;
            float num = state.InferenceCompletedAt > 0f ? state.InferenceCompletedAt : realtimeSinceStartup;
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
            NetworkDialogueService.OnDialogueResponseTelemetry?.Invoke(
                new DialogueResponseTelemetry
                {
                    RequestId = requestId,
                    Status = state.Status,
                    Error = state.Error,
                    Request = state.Request,
                    RetryCount = Mathf.Max(0, state.RetryCount),
                    QueueLatencyMs = num2,
                    ModelLatencyMs = num3,
                    TotalLatencyMs = num4
                }
            );
            RecordExecutionTrace(
                "response_finalized",
                state.Status == DialogueStatus.Completed,
                state.Request,
                requestId,
                null,
                state.FlowId,
                state.Status.ToString(),
                null,
                null,
                0uL,
                0uL,
                state.Error,
                BuildExecutionTraceResponsePreview(state.ResponseText)
            );
            NGLog.Trigger(
                "Dialogue",
                "telemetry_published",
                CreateRequestTraceContext("telemetry_published", requestId, state.Request, state.FlowId),
                (Object)this,
                Network_Game.Diagnostics.LogLevel.Info,
                "PublishDialogueTelemetry",
                BuildRequestData(
                    state.Request,
                    ("status", state.Status),
                    ("queueLatencyMs", Mathf.RoundToInt(num2)),
                    ("modelLatencyMs", Mathf.RoundToInt(num3)),
                    ("totalLatencyMs", Mathf.RoundToInt(num4))
                )
            );
            EmitFlowTrace(
                "telemetry_published",
                "telemetry_published",
                requestId,
                state.Request,
                state.Status == DialogueStatus.Completed,
                state.Status,
                state.Error,
                state.FlowId
            );
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
            if ((Object)singleton == null || !singleton.IsListening || singleton.SpawnManager == null)
            {
                if (m_LogDebug)
                {
                    NGLog.Warn(
                        "Dialogue",
                        NGLog.Format(
                            "Broadcast skipped (network subsystem unavailable)",
                            ("speaker", speakerNetworkId)
                        )
                    );
                }
                return;
            }
            if (!singleton.SpawnManager.SpawnedObjects.TryGetValue(speakerNetworkId, out var value))
            {
                if (m_LogDebug)
                {
                    NGLog.Warn(
                        "Dialogue",
                        NGLog.Format("Broadcast skipped (speaker missing)", ("speaker", speakerNetworkId))
                    );
                }
                return;
            }
            NpcDialogueActor component = ((Component)value).GetComponent<NpcDialogueActor>();
            if ((Object)component != null)
            {
                if (m_LogDebug)
                {
                    NGLog.Debug(
                        "Dialogue",
                        NGLog.Format(
                            "Broadcast response skipped (speech bubble feature removed)",
                            ("speaker", speakerNetworkId),
                            ("chars", (text ?? string.Empty).Length),
                            ("mode", "NpcDialogueActor")
                        )
                    );
                }
            }
            else
            {
                NGLog.Warn(
                    "Dialogue",
                    NGLog.Format(
                        "Broadcast skipped (NpcDialogueActor missing)",
                        ("speaker", speakerNetworkId),
                        ("name", value.name)
                    )
                );
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
