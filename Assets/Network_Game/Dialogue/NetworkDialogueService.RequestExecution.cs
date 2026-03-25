using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Network_Game.Diagnostics;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Network_Game.Dialogue
{
    public partial class NetworkDialogueService
    {
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
    }
}
