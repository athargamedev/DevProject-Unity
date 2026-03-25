using System;
using UnityEngine;

namespace Network_Game.Dialogue
{
    public partial class NetworkDialogueService
    {
        private bool TryValidateRequestForEnqueue(DialogueRequest request, out string rejectionReason)
        {
            return CanAcceptRequest(request, out rejectionReason);
        }

        public string ResolveConversationKey(
            ulong speakerNetworkId,
            ulong listenerNetworkId,
            ulong requestingClientId,
            string conversationKeyOverride = null
        )
        {
            if (!string.IsNullOrWhiteSpace(conversationKeyOverride))
            {
                return conversationKeyOverride.Trim();
            }
            if (speakerNetworkId != 0L && listenerNetworkId != 0)
            {
                ulong low = Math.Min(speakerNetworkId, listenerNetworkId);
                ulong high = Math.Max(speakerNetworkId, listenerNetworkId);
                return $"{low}:{high}";
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

        public bool TryGetAutoRequestBlockReason(
            string conversationKey,
            string prompt,
            bool blockRepeatedPrompt,
            float minRepeatDelaySeconds,
            bool requireUserReply,
            out string reason
        )
        {
            reason = null;
            if (string.IsNullOrWhiteSpace(conversationKey))
            {
                return false;
            }
            string resolvedConversationKey = ResolveConversationKey(0uL, 0uL, 0uL, conversationKey);
            ConversationState conversationState = GetConversationStateForConversation(
                resolvedConversationKey
            );
            if (conversationState.IsInFlight)
            {
                reason = "conversation_in_flight";
                return true;
            }
            if (requireUserReply && conversationState.AwaitingUserInput)
            {
                reason = "awaiting_user_message";
                return true;
            }
            if (
                blockRepeatedPrompt
                && !string.IsNullOrWhiteSpace(prompt)
                && !string.IsNullOrWhiteSpace(conversationState.LastCompletedPrompt)
                && string.Equals(
                    conversationState.LastCompletedPrompt,
                    prompt,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                reason = "duplicate_prompt";
                return true;
            }
            if (
                minRepeatDelaySeconds > 0f
                && conversationState.LastCompletedAt > float.MinValue
            )
            {
                float elapsed = Time.realtimeSinceStartup - conversationState.LastCompletedAt;
                if (elapsed < minRepeatDelaySeconds)
                {
                    reason = "repeat_delay";
                    return true;
                }
            }
            return false;
        }

        private bool CanAcceptRequest(DialogueRequest request, out string reason)
        {
            reason = null;
            ConversationState conversationState = GetConversationStateForConversation(
                request.ConversationKey
            );
            if (request.IsUserInitiated)
            {
                conversationState.AwaitingUserInput = false;
            }
            if (!CanAcceptAuthForRequest(request, out reason))
            {
                return false;
            }
            if (conversationState.HasOutstandingRequest || conversationState.IsInFlight)
            {
                reason = "conversation_in_flight";
                return false;
            }
            if (request.RequireUserReply && conversationState.AwaitingUserInput)
            {
                reason = "awaiting_user_message";
                return false;
            }
            if (
                request.BlockRepeatedPrompt
                && !string.IsNullOrWhiteSpace(request.Prompt)
                && !string.IsNullOrWhiteSpace(conversationState.LastCompletedPrompt)
                && string.Equals(
                    conversationState.LastCompletedPrompt,
                    request.Prompt,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                reason = "duplicate_prompt";
                return false;
            }
            if (
                request.MinRepeatDelaySeconds > 0f
                && conversationState.LastCompletedAt > float.MinValue
            )
            {
                float elapsed = Time.realtimeSinceStartup - conversationState.LastCompletedAt;
                if (elapsed < request.MinRepeatDelaySeconds)
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
                int activeForClient = 0;
                foreach (DialogueRequestState state in m_Requests.Values)
                {
                    if (
                        state.Request.RequestingClientId == request.RequestingClientId
                        && (
                            state.Status == DialogueStatus.Pending
                            || state.Status == DialogueStatus.InProgress
                        )
                    )
                    {
                        activeForClient++;
                    }
                }
                if (activeForClient >= m_MaxRequestsPerClient)
                {
                    reason = "rate_limited_active";
                    return false;
                }
            }
            if (m_MinSecondsBetweenRequests > 0f)
            {
                if (m_LastRequestTimeByClient.TryGetValue(request.RequestingClientId, out var lastTime))
                {
                    float elapsed = Time.realtimeSinceStartup - lastTime;
                    if (elapsed < m_MinSecondsBetweenRequests)
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
            return NetworkDialogueAuthGate.CanAccept(
                m_RequireAuthenticatedPlayers,
                request.IsUserInitiated,
                request.RequestingClientId,
                (ulong clientId) => TryGetPlayerIdentityByClientId(clientId, out snapshot),
                out rejectionReason
            );
        }
    }
}
