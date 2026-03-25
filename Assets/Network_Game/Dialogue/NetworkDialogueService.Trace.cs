using System;
using System.Threading;
using Network_Game.Diagnostics;
using UnityEngine;

namespace Network_Game.Dialogue
{
    public partial class NetworkDialogueService
    {
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
    }
}
