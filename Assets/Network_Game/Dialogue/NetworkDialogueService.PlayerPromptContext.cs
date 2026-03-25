using System;
using Network_Game.Auth;
using Network_Game.Dialogue.Effects;
using UnityEngine;

namespace Network_Game.Dialogue
{
    public partial class NetworkDialogueService
    {
        // Runtime player-context prompt assembly.
        private PlayerPromptContextBinding ResolvePlayerPromptContextBinding(
            DialogueRequest request
        )
        {
            if (!m_EnablePlayerPromptContext)
            {
                return null;
            }

            ulong playerNetworkId = ResolvePlayerNetworkIdForRequest(request);
            if (playerNetworkId == 0)
            {
                return null;
            }

            return m_PlayerPromptContextByNetworkId.TryGetValue(playerNetworkId, out var binding)
                ? binding
                : null;
        }

        private string BuildPlayerContextPrompt(DialogueRequest request, GameObject listenerObject)
        {
            if (!m_EnablePlayerPromptContext)
            {
                return string.Empty;
            }

            PlayerPromptContextBinding binding = ResolvePlayerPromptContextBinding(request);
            PlayerIdentityBinding identity = ResolvePlayerIdentityForRequest(request);
            bool canUseLocalAuthFallback = !base.IsServer && base.IsClient;
            string nameId = identity?.NameId;
            if (string.IsNullOrWhiteSpace(nameId))
            {
                nameId = binding?.NameId ?? string.Empty;
            }
            if (string.IsNullOrWhiteSpace(nameId) && canUseLocalAuthFallback)
            {
                LocalPlayerAuthService authService = LocalPlayerAuthService.Instance;
                if (authService != null && authService.HasCurrentPlayer)
                {
                    nameId = authService.CurrentPlayer.NameId;
                }
            }

            if (string.IsNullOrWhiteSpace(nameId) && listenerObject != null)
            {
                nameId = listenerObject.name;
            }

            string customizationJson = identity?.CustomizationJson;
            if (string.IsNullOrWhiteSpace(customizationJson))
            {
                customizationJson = binding?.CustomizationJson ?? "{}";
            }
            if (string.IsNullOrWhiteSpace(customizationJson))
            {
                customizationJson = "{}";
            }

            if (IsPlaceholderPromptContextJson(customizationJson) && canUseLocalAuthFallback)
            {
                LocalPlayerAuthService authService = LocalPlayerAuthService.Instance;
                if (
                    authService != null
                    && authService.HasCurrentPlayer
                    && (
                        string.IsNullOrWhiteSpace(nameId)
                        || string.Equals(
                            authService.CurrentPlayer.NameId,
                            nameId,
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                )
                {
                    customizationJson = authService.GetCustomizationJson();
                }
            }

            customizationJson = NormalizePromptContextJson(nameId, customizationJson);

            int maxChars = Mathf.Max(64, m_MaxPlayerCustomizationChars);
            if (UseOpenAIRemote)
            {
                maxChars = Mathf.Min(maxChars, Mathf.Max(64, m_RemoteMaxPlayerCustomizationChars));
            }
            if (customizationJson.Length > maxChars)
            {
                customizationJson = customizationJson.Substring(0, maxChars).TrimEnd() + "...";
            }

            ulong playerNetworkId =
                identity?.PlayerNetworkId != 0
                    ? identity.PlayerNetworkId
                    : ResolvePlayerNetworkIdForRequest(request);
            ulong clientId = request.RequestingClientId;
            if (clientId == 0 && identity != null)
            {
                clientId = identity.ClientId;
            }

            string playerRole = clientId == 0 ? "host" : "client";

            NpcDialogueActor narrativeActor = ResolveDialogueActorForRequest(
                request,
                out _,
                out _,
                out _
            );
            string narrativeHints = BuildNarrativeHints(
                identity,
                narrativeActor?.Profile,
                customizationJson
            );
            string playerContextBlock =
                "[PlayerContext]\n"
                + $"name_id: {nameId}\n"
                + $"client_id: {clientId}\n"
                + $"player_network_id: {playerNetworkId}\n"
                + $"player_role: {playerRole}\n"
                + $"customization_json: {customizationJson}\n"
                + "Use this context to personalize details naturally without exposing internal formatting.";
            if (!string.IsNullOrEmpty(narrativeHints))
            {
                playerContextBlock += "\n\n" + narrativeHints;
            }
            return playerContextBlock;
        }

    }
}
