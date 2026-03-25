using System;
using System.Collections.Generic;
using Network_Game.Diagnostics;
using UnityEngine;

namespace Network_Game.Dialogue
{
    public partial class NetworkDialogueService
    {
        private List<DialogueHistoryEntry> GetHistoryInternal(string key)
        {
            if (!m_Histories.TryGetValue(key, out List<DialogueHistoryEntry> history))
            {
                history = new List<DialogueHistoryEntry>();
                m_Histories[key] = history;
            }

            return history;
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
                ClientRequestLookupKey key = new ClientRequestLookupKey(
                    request.ClientRequestId,
                    request.RequestingClientId
                );
                m_RequestIdsByScopedClientRequest[key] = requestId;
                if (!m_RequestIdsByClientRequestId.TryGetValue(request.ClientRequestId, out List<int> ids))
                {
                    ids = new List<int>(1);
                    m_RequestIdsByClientRequestId[request.ClientRequestId] = ids;
                }

                ids.Add(requestId);
            }
        }

        private void UnregisterClientRequestLookup(int requestId, DialogueRequest request)
        {
            if (request.ClientRequestId <= 0)
            {
                return;
            }

            ClientRequestLookupKey key = new ClientRequestLookupKey(
                request.ClientRequestId,
                request.RequestingClientId
            );
            if (
                m_RequestIdsByScopedClientRequest.TryGetValue(key, out int scopedRequestId)
                && scopedRequestId == requestId
            )
            {
                m_RequestIdsByScopedClientRequest.Remove(key);
            }

            if (!m_RequestIdsByClientRequestId.TryGetValue(request.ClientRequestId, out List<int> ids))
            {
                return;
            }

            for (int i = ids.Count - 1; i >= 0; i--)
            {
                if (ids[i] == requestId)
                {
                    ids.RemoveAt(i);
                    break;
                }
            }

            if (ids.Count == 0)
            {
                m_RequestIdsByClientRequestId.Remove(request.ClientRequestId);
            }
        }

        private bool TryGetRequestIdByClientRequestId(
            int clientRequestId,
            ulong requestingClientId,
            out int requestId
        )
        {
            requestId = -1;
            if (clientRequestId <= 0)
            {
                return false;
            }

            if (requestingClientId != ulong.MaxValue)
            {
                ClientRequestLookupKey key = new ClientRequestLookupKey(
                    clientRequestId,
                    requestingClientId
                );
                if (m_RequestIdsByScopedClientRequest.TryGetValue(key, out int scopedRequestId))
                {
                    if (m_Requests.ContainsKey(scopedRequestId))
                    {
                        requestId = scopedRequestId;
                        return true;
                    }

                    m_RequestIdsByScopedClientRequest.Remove(key);
                }
            }

            if (!m_RequestIdsByClientRequestId.TryGetValue(clientRequestId, out List<int> ids))
            {
                return false;
            }

            for (int i = ids.Count - 1; i >= 0; i--)
            {
                int candidateRequestId = ids[i];
                if (!m_Requests.TryGetValue(candidateRequestId, out DialogueRequestState state) || state == null)
                {
                    ids.RemoveAt(i);
                }
                else if (
                    requestingClientId == ulong.MaxValue
                    || state.Request.RequestingClientId == requestingClientId
                )
                {
                    requestId = candidateRequestId;
                    return true;
                }
            }

            if (ids.Count == 0)
            {
                m_RequestIdsByClientRequestId.Remove(clientRequestId);
            }

            return false;
        }

        private List<DialogueInferenceMessage> BuildRemoteInferenceHistory(
            List<DialogueHistoryEntry> fullHistory
        )
        {
            if (fullHistory == null || fullHistory.Count == 0)
            {
                return new List<DialogueInferenceMessage>();
            }

            int maxMessages = Mathf.Max(0, m_RemoteMaxHistoryMessages);
            int hardCapMessages = Mathf.Max(1, m_RemoteHistoryHardCapMessages);
            maxMessages = Mathf.Min(maxMessages, hardCapMessages);
            if (maxMessages <= 0)
            {
                return new List<DialogueInferenceMessage>();
            }

            int takeCount = Mathf.Min(maxMessages, fullHistory.Count);
            int startIndex = fullHistory.Count - takeCount;
            int maxChars = Mathf.Clamp(m_RemoteHistoryMessageCharBudget, 64, 1024);
            var history = new List<DialogueInferenceMessage>(takeCount);
            for (int i = startIndex; i < fullHistory.Count; i++)
            {
                DialogueHistoryEntry entry = fullHistory[i];
                if (entry == null)
                {
                    continue;
                }

                string trimmed = TrimPromptSegment(entry.Content, maxChars);
                if (!string.IsNullOrWhiteSpace(trimmed))
                {
                    history.Add(
                        new DialogueInferenceMessage(NormalizeHistoryRole(entry.Role), trimmed)
                    );
                }
            }

            return history;
        }

        private static string NormalizeHistoryRole(string role)
        {
            if (string.IsNullOrWhiteSpace(role))
            {
                return "user";
            }

            string normalized = role.Trim().ToLowerInvariant();
            if (normalized == "user" || normalized == "assistant" || normalized == "system")
            {
                return normalized;
            }

            return normalized.Contains("assistant", StringComparison.Ordinal)
                ? "assistant"
                : "user";
        }

        private static string TrimPromptSegment(string content, int maxChars)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return string.Empty;
            }

            string normalized = content.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
            if (normalized.Length <= maxChars)
            {
                return normalized;
            }

            maxChars = Mathf.Max(64, maxChars);
            int headChars = Mathf.Clamp(Mathf.FloorToInt(maxChars * 0.72f), 48, maxChars - 24);
            int tailChars = maxChars - headChars - 12;
            if (tailChars < 12)
            {
                tailChars = 12;
                headChars = maxChars - tailChars - 12;
            }

            return normalized.Substring(0, headChars).TrimEnd()
                + " [..] "
                + normalized.Substring(normalized.Length - tailChars).TrimStart();
        }

        private string ApplyRemoteUserPromptBudget(string prompt)
        {
            if (!UseOpenAIRemote || string.IsNullOrWhiteSpace(prompt))
            {
                return prompt;
            }

            int maxChars = Mathf.Clamp(m_RemoteUserPromptCharBudget, 64, 1024);
            string trimmed = TrimPromptSegment(prompt, maxChars);
            if (m_LogDebug && trimmed.Length < prompt.Length)
            {
                NGLog.Warn(
                    "Dialogue",
                    NGLog.Format(
                        "Trimmed remote user prompt",
                        ("fromChars", prompt.Length),
                        ("toChars", trimmed.Length),
                        ("budget", maxChars)
                    )
                );
            }

            return trimmed;
        }

        private void TrimHistory(string key, int maxMessages)
        {
            if (
                maxMessages > 0
                && m_Histories.TryGetValue(key, out List<DialogueHistoryEntry> history)
                && history.Count > maxMessages
            )
            {
                int removeCount = history.Count - maxMessages;
                history.RemoveRange(0, removeCount);
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

        private void StoreHistoryForConversation(
            string conversationKey,
            List<DialogueHistoryEntry> history
        )
        {
            StoreHistory(conversationKey, history);
        }

        private string BuildConversationKey(DialogueRequest request)
        {
            return ResolveConversationKey(
                request.SpeakerNetworkId,
                request.ListenerNetworkId,
                request.RequestingClientId,
                request.ConversationKey
            );
        }

        private ConversationState GetConversationState(string conversationKey)
        {
            string key = ResolveConversationKey(0UL, 0UL, 0UL, conversationKey);
            if (!m_ConversationStates.TryGetValue(key, out ConversationState state))
            {
                state = new ConversationState();
                m_ConversationStates[key] = state;
            }

            return state;
        }

        private ConversationState GetConversationStateForConversation(string conversationKey)
        {
            return GetConversationState(conversationKey);
        }

        private void BeginConversationRequest(int requestId, DialogueRequest request)
        {
            ConversationState state = GetConversationStateForConversation(request.ConversationKey);
            state.HasOutstandingRequest = true;
            state.OutstandingRequestId = requestId;
            state.IsInFlight = true;
            state.ActiveRequestId = requestId;
            if (request.IsUserInitiated)
            {
                state.AwaitingUserInput = false;
            }
        }

        private void CompleteConversationRequest(
            int requestId,
            DialogueRequestState requestState,
            bool completed
        )
        {
            if (requestState == null)
            {
                return;
            }

            string conversationKey = BuildConversationKey(requestState.Request);
            ConversationState state = GetConversationStateForConversation(conversationKey);
            if (state.ActiveRequestId == requestId || state.IsInFlight)
            {
                state.IsInFlight = false;
                state.ActiveRequestId = -1;
            }

            if (state.OutstandingRequestId == requestId || state.HasOutstandingRequest)
            {
                state.HasOutstandingRequest = false;
                state.OutstandingRequestId = -1;
            }

            if (completed)
            {
                state.LastCompletedPrompt = requestState.Request.Prompt;
                state.LastCompletedAt = Time.realtimeSinceStartup;
                state.AssistantMessageCount++;
                if (requestState.Request.RequireUserReply)
                {
                    state.AwaitingUserInput = true;
                }
            }
        }
    }
}
