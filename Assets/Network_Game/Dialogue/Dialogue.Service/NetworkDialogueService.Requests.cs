using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Network_Game.Diagnostics;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Network_Game.Dialogue
{
    public partial class NetworkDialogueService
    {
        // Queue orchestration and worker scheduling.
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
    }
}
