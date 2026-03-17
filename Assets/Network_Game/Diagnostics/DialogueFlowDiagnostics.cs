using System;
using System.Collections.Generic;
using System.Text;
using Network_Game.Dialogue;
using UnityEngine;
using DialogueStatus = Network_Game.Dialogue.NetworkDialogueService.DialogueStatus;

namespace Network_Game.Diagnostics
{
    /// <summary>
    /// Tracks correlated dialogue flow timelines and dumps slow or failed executions.
    /// </summary>
    [DefaultExecutionOrder(130)]
    public class DialogueFlowDiagnostics : MonoBehaviour
    {
        private const string Category = "DialogueFlow";

        private static DialogueFlowDiagnostics s_Instance;

        [Header("Flow Diagnostics")]
        [SerializeField]
        [Min(250f)]
        private float m_SlowFlowThresholdMs = 3500f;

        [SerializeField]
        [Min(4)]
        private int m_MaxRetainedCompletedFlows = 32;

        [SerializeField]
        [Min(8)]
        private int m_MaxTimelineEntriesPerFlow = 48;

        [SerializeField]
        private bool m_LogSlowFlows = true;

        [SerializeField]
        private bool m_LogFailedFlows = true;

        [SerializeField]
        private bool m_LogTimeoutFlows = true;

        [SerializeField]
        private bool m_LogNormalCompletions;

        private readonly Dictionary<string, FlowRecord> m_RecordsById =
            new Dictionary<string, FlowRecord>(StringComparer.Ordinal);

        private readonly Dictionary<string, string> m_AliasToRecordId =
            new Dictionary<string, string>(StringComparer.Ordinal);

        private readonly Queue<string> m_CompletedRecordIds = new Queue<string>();

        private sealed class FlowRecord
        {
            public readonly string RecordId;
            public readonly List<TimelineEntry> Timeline = new List<TimelineEntry>();

            public string FlowId;
            public string ConversationKey;
            public string LastError;
            public DialogueStatus LastStatus = DialogueStatus.Pending;
            public int RequestId = -1;
            public int ClientRequestId;
            public int RetryCount;
            public int TrimmedEntries;
            public ulong ClientId;
            public ulong SpeakerNetworkId;
            public ulong ListenerNetworkId;
            public float CreatedAtMs = -1f;
            public float LastTimestampMs = -1f;
            public float QueueLatencyMs = -1f;
            public float ModelLatencyMs = -1f;
            public float TotalLatencyMs = -1f;
            public bool HasTelemetry;
            public bool IsTerminal;
            public bool Dumped;
            public bool IsRetained;

            public FlowRecord(string recordId)
            {
                RecordId = recordId;
            }
        }

        private readonly struct TimelineEntry
        {
            public readonly string EventName;
            public readonly string Phase;
            public readonly DialogueStatus Status;
            public readonly bool Success;
            public readonly string Error;
            public readonly float TimestampMs;

            public TimelineEntry(
                string eventName,
                string phase,
                DialogueStatus status,
                bool success,
                string error,
                float timestampMs
            )
            {
                EventName = eventName ?? string.Empty;
                Phase = phase ?? string.Empty;
                Status = status;
                Success = success;
                Error = error ?? string.Empty;
                TimestampMs = timestampMs;
            }
        }

        private void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                NGLog.Warn(
                    Category,
                    NGLog.Format(
                        "Duplicate dialogue flow diagnostics destroyed",
                        ("object", (object)gameObject.name)
                    ),
                    this
                );
                Destroy(this);
                return;
            }

            s_Instance = this;
            NGLog.Lifecycle(Category, "awake", CreateTraceContext(null), this);
        }

        private void OnEnable()
        {
            NetworkDialogueService.OnDialogueFlowTrace += HandleFlowTrace;
            NetworkDialogueService.OnDialogueResponseTelemetry += HandleDialogueTelemetry;
            NetworkDialogueService.OnDialogueResponse += HandleDialogueResponse;
            NGLog.Subscribe(Category, "dialogue_events", CreateTraceContext(null), this);
        }

        private void OnDisable()
        {
            NetworkDialogueService.OnDialogueFlowTrace -= HandleFlowTrace;
            NetworkDialogueService.OnDialogueResponseTelemetry -= HandleDialogueTelemetry;
            NetworkDialogueService.OnDialogueResponse -= HandleDialogueResponse;
        }

        private void OnDestroy()
        {
            if (s_Instance == this)
            {
                s_Instance = null;
            }
        }

        private void HandleFlowTrace(NetworkDialogueService.DialogueFlowTraceEvent traceEvent)
        {
            FlowRecord record = ResolveRecord(
                traceEvent.FlowId,
                traceEvent.RequestId,
                traceEvent.ClientRequestId,
                traceEvent.ClientId
            );
            UpdateIdentifiers(record, traceEvent);
            AddTimelineEntry(record, traceEvent);

            if (string.Equals(traceEvent.EventName, "request_rejected", StringComparison.Ordinal))
            {
                record.IsTerminal = true;
                record.LastStatus = DialogueStatus.Failed;
                record.LastError = string.IsNullOrWhiteSpace(traceEvent.Error)
                    ? "request_rejected"
                    : traceEvent.Error;
                EvaluateAndDump(record, "request_rejected");
                MarkCompleted(record);
                return;
            }

            if (string.Equals(traceEvent.EventName, "request_completed", StringComparison.Ordinal))
            {
                record.IsTerminal = true;
                record.LastStatus = traceEvent.Status;
                record.LastError = traceEvent.Error ?? string.Empty;
                EvaluateAndDump(record, "request_completed");
                MarkCompleted(record);
            }
        }

        private void HandleDialogueTelemetry(NetworkDialogueService.DialogueResponseTelemetry telemetry)
        {
            NetworkDialogueService.DialogueRequest request = telemetry.Request;
            FlowRecord record = ResolveRecord(
                BuildFlowId(telemetry.RequestId, request),
                telemetry.RequestId,
                request.ClientRequestId,
                request.RequestingClientId
            );

            record.RequestId = telemetry.RequestId;
            record.ClientRequestId = request.ClientRequestId;
            record.ClientId = request.RequestingClientId;
            record.SpeakerNetworkId = request.SpeakerNetworkId;
            record.ListenerNetworkId = request.ListenerNetworkId;
            record.ConversationKey = request.ConversationKey ?? string.Empty;
            record.LastStatus = telemetry.Status;
            record.LastError = telemetry.Error ?? string.Empty;
            record.RetryCount = Mathf.Max(0, telemetry.RetryCount);
            record.QueueLatencyMs = telemetry.QueueLatencyMs;
            record.ModelLatencyMs = telemetry.ModelLatencyMs;
            record.TotalLatencyMs = telemetry.TotalLatencyMs;
            record.HasTelemetry = true;

            if (record.IsTerminal)
            {
                EvaluateAndDump(record, "telemetry");
                MarkCompleted(record);
            }
        }

        private void HandleDialogueResponse(NetworkDialogueService.DialogueResponse response)
        {
            NetworkDialogueService.DialogueRequest request = response.Request;
            FlowRecord record = ResolveRecord(
                BuildFlowId(response.RequestId, request),
                response.RequestId,
                request.ClientRequestId,
                request.RequestingClientId
            );

            record.RequestId = response.RequestId;
            record.ClientRequestId = request.ClientRequestId;
            record.ClientId = request.RequestingClientId;
            record.SpeakerNetworkId = request.SpeakerNetworkId;
            record.ListenerNetworkId = request.ListenerNetworkId;
            record.ConversationKey = request.ConversationKey ?? string.Empty;

            AddSyntheticTimelineEntry(
                record,
                "response_event",
                "response_event",
                response.Status,
                response.Status == DialogueStatus.Completed,
                response.Error
            );

            if (response.Status != DialogueStatus.Pending && response.Status != DialogueStatus.InProgress)
            {
                record.IsTerminal = true;
                record.LastStatus = response.Status;
                record.LastError = response.Error ?? string.Empty;
                EvaluateAndDump(record, "response_event");
                MarkCompleted(record);
            }
        }

        private FlowRecord ResolveRecord(
            string flowId,
            int requestId,
            int clientRequestId,
            ulong clientId
        )
        {
            string requestAlias = BuildRequestAlias(requestId);
            string clientAlias = BuildClientAlias(clientId, clientRequestId);
            string effectiveFlowId = string.IsNullOrWhiteSpace(flowId)
                ? BuildFlowId(requestId, clientId, clientRequestId)
                : flowId;

            FlowRecord record =
                FindRecordByAlias(requestAlias)
                ?? FindRecordByAlias(clientAlias)
                ?? FindRecordByAlias(effectiveFlowId);

            if (record == null)
            {
                string recordId =
                    !string.IsNullOrWhiteSpace(requestAlias)
                        ? requestAlias
                        : !string.IsNullOrWhiteSpace(clientAlias)
                            ? clientAlias
                            : !string.IsNullOrWhiteSpace(effectiveFlowId)
                                ? effectiveFlowId
                                : Guid.NewGuid().ToString("N");
                record = new FlowRecord(recordId) { FlowId = effectiveFlowId };
                m_RecordsById[record.RecordId] = record;
            }

            RegisterAlias(record, requestAlias);
            RegisterAlias(record, clientAlias);
            RegisterAlias(record, effectiveFlowId);

            if (string.IsNullOrWhiteSpace(record.FlowId))
            {
                record.FlowId = effectiveFlowId;
            }

            return record;
        }

        private void UpdateIdentifiers(
            FlowRecord record,
            NetworkDialogueService.DialogueFlowTraceEvent traceEvent
        )
        {
            if (record == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(traceEvent.FlowId))
            {
                record.FlowId = traceEvent.FlowId;
            }

            if (traceEvent.RequestId > 0)
            {
                record.RequestId = traceEvent.RequestId;
                RegisterAlias(record, BuildRequestAlias(traceEvent.RequestId));
            }

            if (traceEvent.ClientRequestId > 0)
            {
                record.ClientRequestId = traceEvent.ClientRequestId;
            }

            if (traceEvent.ClientId != 0)
            {
                record.ClientId = traceEvent.ClientId;
                RegisterAlias(
                    record,
                    BuildClientAlias(traceEvent.ClientId, traceEvent.ClientRequestId)
                );
            }

            if (traceEvent.SpeakerNetworkId != 0)
            {
                record.SpeakerNetworkId = traceEvent.SpeakerNetworkId;
            }

            if (traceEvent.ListenerNetworkId != 0)
            {
                record.ListenerNetworkId = traceEvent.ListenerNetworkId;
            }

            if (!string.IsNullOrWhiteSpace(traceEvent.ConversationKey))
            {
                record.ConversationKey = traceEvent.ConversationKey;
            }

            record.LastStatus = traceEvent.Status;
            if (!string.IsNullOrWhiteSpace(traceEvent.Error))
            {
                record.LastError = traceEvent.Error;
            }
        }

        private void AddTimelineEntry(
            FlowRecord record,
            NetworkDialogueService.DialogueFlowTraceEvent traceEvent
        )
        {
            AddSyntheticTimelineEntry(
                record,
                traceEvent.EventName,
                traceEvent.Phase,
                traceEvent.Status,
                traceEvent.Success,
                traceEvent.Error,
                traceEvent.TimestampMs
            );
        }

        private void AddSyntheticTimelineEntry(
            FlowRecord record,
            string eventName,
            string phase,
            DialogueStatus status,
            bool success,
            string error,
            float timestampMs = -1f
        )
        {
            if (record == null)
            {
                return;
            }

            float effectiveTimestamp = timestampMs >= 0f
                ? timestampMs
                : Time.realtimeSinceStartup * 1000f;

            if (record.CreatedAtMs < 0f)
            {
                record.CreatedAtMs = effectiveTimestamp;
            }

            record.LastTimestampMs = Mathf.Max(record.LastTimestampMs, effectiveTimestamp);

            if (record.Timeline.Count >= Mathf.Max(8, m_MaxTimelineEntriesPerFlow))
            {
                record.Timeline.RemoveAt(0);
                record.TrimmedEntries++;
            }

            record.Timeline.Add(
                new TimelineEntry(
                    eventName,
                    phase,
                    status,
                    success,
                    error,
                    effectiveTimestamp
                )
            );
        }

        private void EvaluateAndDump(FlowRecord record, string source)
        {
            if (record == null || record.Dumped)
            {
                return;
            }

            float totalLatencyMs = GetTotalLatencyMs(record);
            bool timedOut = IsTimeoutReason(record.LastError);
            bool failed =
                record.LastStatus == DialogueStatus.Failed
                || record.LastStatus == DialogueStatus.Cancelled
                || timedOut;
            bool slow = totalLatencyMs >= m_SlowFlowThresholdMs;

            if (timedOut && !m_LogTimeoutFlows)
            {
                return;
            }

            if (failed && !timedOut && !m_LogFailedFlows)
            {
                return;
            }

            if (!failed && slow && !m_LogSlowFlows)
            {
                return;
            }

            if (!failed && !slow && !m_LogNormalCompletions)
            {
                return;
            }

            string reason = timedOut
                ? "timeout"
                : failed
                    ? "failed"
                    : slow
                        ? "slow"
                        : "completed";

            TraceContext traceContext = CreateTraceContext(record);
            var data = new (string key, object value)[]
            {
                ("reason", reason),
                ("source", source ?? string.Empty),
                ("status", record.LastStatus),
                ("error", record.LastError ?? string.Empty),
                ("retry", record.RetryCount),
                ("key", record.ConversationKey ?? string.Empty),
                ("queueMs", Mathf.RoundToInt(Mathf.Max(0f, record.QueueLatencyMs))),
                ("modelMs", Mathf.RoundToInt(Mathf.Max(0f, record.ModelLatencyMs))),
                ("totalMs", Mathf.RoundToInt(Mathf.Max(0f, totalLatencyMs))),
                ("timeline", record.Timeline.Count),
            };

            if (failed || timedOut)
            {
                NGLog.Trigger(
                    Category,
                    "flow_dump",
                    traceContext,
                    this,
                    LogLevel.Warning,
                    data: data
                );
            }
            else
            {
                NGLog.Trigger(Category, "flow_dump", traceContext, this, data: data);
            }

            NGLog.Info(
                Category,
                NGLog.Format(
                    "Timeline",
                    ("flowId", (object)(record.FlowId ?? record.RecordId)),
                    ("events", (object)BuildTimelineSummary(record))
                ),
                this
            );

            record.Dumped = true;
        }

        private void MarkCompleted(FlowRecord record)
        {
            if (record == null || record.IsRetained)
            {
                return;
            }

            record.IsRetained = true;
            m_CompletedRecordIds.Enqueue(record.RecordId);
            PruneCompletedRecords();
        }

        private void PruneCompletedRecords()
        {
            int maxRecords = Mathf.Max(4, m_MaxRetainedCompletedFlows);
            while (m_CompletedRecordIds.Count > maxRecords)
            {
                string recordId = m_CompletedRecordIds.Dequeue();
                if (!m_RecordsById.TryGetValue(recordId, out FlowRecord record))
                {
                    continue;
                }

                RemoveAlias(record.FlowId, recordId);
                RemoveAlias(BuildRequestAlias(record.RequestId), recordId);
                RemoveAlias(BuildClientAlias(record.ClientId, record.ClientRequestId), recordId);
                m_RecordsById.Remove(recordId);
            }
        }

        private FlowRecord FindRecordByAlias(string alias)
        {
            if (string.IsNullOrWhiteSpace(alias))
            {
                return null;
            }

            if (!m_AliasToRecordId.TryGetValue(alias, out string recordId))
            {
                return null;
            }

            return m_RecordsById.TryGetValue(recordId, out FlowRecord record) ? record : null;
        }

        private void RegisterAlias(FlowRecord record, string alias)
        {
            if (record == null || string.IsNullOrWhiteSpace(alias))
            {
                return;
            }

            m_AliasToRecordId[alias] = record.RecordId;
        }

        private void RemoveAlias(string alias, string recordId)
        {
            if (string.IsNullOrWhiteSpace(alias))
            {
                return;
            }

            if (m_AliasToRecordId.TryGetValue(alias, out string existingRecordId)
                && string.Equals(existingRecordId, recordId, StringComparison.Ordinal))
            {
                m_AliasToRecordId.Remove(alias);
            }
        }

        private float GetTotalLatencyMs(FlowRecord record)
        {
            if (record == null)
            {
                return 0f;
            }

            if (record.HasTelemetry && record.TotalLatencyMs >= 0f)
            {
                return record.TotalLatencyMs;
            }

            if (record.CreatedAtMs < 0f || record.LastTimestampMs < 0f)
            {
                return 0f;
            }

            return Mathf.Max(0f, record.LastTimestampMs - record.CreatedAtMs);
        }

        private string BuildTimelineSummary(FlowRecord record)
        {
            if (record == null || record.Timeline.Count == 0)
            {
                return "empty";
            }

            var builder = new StringBuilder();
            if (record.TrimmedEntries > 0)
            {
                builder.Append("trimmed:");
                builder.Append(record.TrimmedEntries);
                builder.Append(" | ");
            }

            float baseTimestamp = record.CreatedAtMs >= 0f
                ? record.CreatedAtMs
                : record.Timeline[0].TimestampMs;

            for (int i = 0; i < record.Timeline.Count; i++)
            {
                TimelineEntry entry = record.Timeline[i];
                if (i > 0)
                {
                    builder.Append(" -> ");
                }

                builder.Append(entry.EventName);
                builder.Append('@');
                builder.Append(Mathf.RoundToInt(Mathf.Max(0f, entry.TimestampMs - baseTimestamp)));
                builder.Append("ms");

                if (!entry.Success)
                {
                    builder.Append('!');
                }

                if (!string.IsNullOrWhiteSpace(entry.Error))
                {
                    builder.Append('(');
                    builder.Append(entry.Error);
                    builder.Append(')');
                }
            }

            return builder.ToString();
        }

        private TraceContext CreateTraceContext(FlowRecord record)
        {
            return new TraceContext(
                bootId: SceneWorkflowDiagnostics.ActiveBootId,
                flowId: record != null ? record.FlowId ?? record.RecordId : string.Empty,
                requestId: record != null ? record.RequestId : 0,
                clientRequestId: record != null ? record.ClientRequestId : 0,
                clientId: record != null ? record.ClientId : 0,
                phase: "dialogue_flow",
                script: nameof(DialogueFlowDiagnostics)
            );
        }

        private static string BuildFlowId(
            int requestId,
            NetworkDialogueService.DialogueRequest request
        )
        {
            return BuildFlowId(requestId, request.RequestingClientId, request.ClientRequestId);
        }

        private static string BuildFlowId(int requestId, ulong clientId, int clientRequestId)
        {
            if (requestId > 0)
            {
                return $"dialogue-{requestId}";
            }

            if (clientId != 0 && clientRequestId > 0)
            {
                return $"dialogue-client-{clientId}-{clientRequestId}";
            }

            return "dialogue-pending";
        }

        private static string BuildRequestAlias(int requestId)
        {
            return requestId > 0 ? $"request:{requestId}" : string.Empty;
        }

        private static string BuildClientAlias(ulong clientId, int clientRequestId)
        {
            return clientId != 0 && clientRequestId > 0
                ? $"client:{clientId}:{clientRequestId}"
                : string.Empty;
        }

        private static bool IsTimeoutReason(string error)
        {
            return !string.IsNullOrWhiteSpace(error)
                && error.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
