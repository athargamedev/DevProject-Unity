using System;
using Network_Game.Combat;
using Network_Game.Diagnostics.Contracts;
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
                var playerIdentity = ProviderRegistry.PlayerIdentity;
                if (playerIdentity.HasCurrentPlayer)
                {
                    nameId = playerIdentity.CurrentPlayer.NameId;
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
                var playerIdentity = ProviderRegistry.PlayerIdentity;
                if (playerIdentity.HasCurrentPlayer)
                {
                    customizationJson = "{}"; // Provider handles actual data
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

            string playerGameDataBlock = BuildPlayerGameDataPromptBlock(
                request,
                nameId,
                listenerObject
            );
            if (!string.IsNullOrWhiteSpace(playerGameDataBlock))
            {
                playerContextBlock += "\n\n" + playerGameDataBlock;
            }

            if (!string.IsNullOrEmpty(narrativeHints))
            {
                playerContextBlock += "\n\n" + narrativeHints;
            }
            return playerContextBlock;
        }

        private static string BuildPlayerGameDataPromptBlock(
            DialogueRequest request,
            string resolvedPlayerKey,
            GameObject listenerObject
        )
        {
            PlayerRuntimeSnapshot runtimeState = ResolvePlayerRuntimeState(request, resolvedPlayerKey);
            CombatHealthV2 liveHealth = listenerObject != null
                ? listenerObject.GetComponentInChildren<CombatHealthV2>()
                : null;

            float? currentHealth = liveHealth != null
                ? liveHealth.CurrentHealth
                : runtimeState.IsValid ? runtimeState.CurrentHealth : null;
            float? maxHealth = liveHealth != null
                ? liveHealth.MaxHealth
                : runtimeState.IsValid ? runtimeState.MaxHealth : null;
            float? healthPercent =
                currentHealth.HasValue && maxHealth.HasValue && maxHealth.Value > 0f
                    ? currentHealth.Value / maxHealth.Value
                    : null;

            bool hasAnyData =
                currentHealth.HasValue
                || maxHealth.HasValue
                || runtimeState.IsValid;
            if (!hasAnyData)
            {
                return string.Empty;
            }

            string healthSummary = "unknown";
            if (healthPercent.HasValue)
            {
                float percent = Mathf.Clamp01(healthPercent.Value);
                if (percent <= 0.1f)
                {
                    healthSummary = "critical";
                }
                else if (percent <= 0.25f)
                {
                    healthSummary = "very low";
                }
                else if (percent <= 0.5f)
                {
                    healthSummary = "wounded";
                }
                else if (percent <= 0.85f)
                {
                    healthSummary = "healthy";
                }
                else
                {
                    healthSummary = "strong";
                }
            }

            string block =
                "[PlayerGameData]\n"
                + "Treat these values as authoritative gameplay state for personalization.\n"
                + $"player_key: {resolvedPlayerKey}\n"
                + $"current_health: {(currentHealth.HasValue ? currentHealth.Value.ToString("F0") : "unknown")}\n"
                + $"max_health: {(maxHealth.HasValue ? maxHealth.Value.ToString("F0") : "unknown")}\n"
                + $"health_percent: {(healthPercent.HasValue ? Mathf.RoundToInt(Mathf.Clamp01(healthPercent.Value) * 100f).ToString() + "%" : "unknown")}\n"
                + $"health_state: {healthSummary}";

            if (runtimeState.IsValid)
            {
                block +=
                    $"\nlevel: {(runtimeState.Level.HasValue ? runtimeState.Level.Value.ToString() : "unknown")}"
                    + $"\nexperience: {(runtimeState.Experience.HasValue ? runtimeState.Experience.Value.ToString() : "unknown")}"
                    + $"\ndeaths: {(runtimeState.Deaths.HasValue ? runtimeState.Deaths.Value.ToString() : "unknown")}"
                    + $"\neffects_survived: {(runtimeState.EffectsSurvived.HasValue ? runtimeState.EffectsSurvived.Value.ToString() : "unknown")}";
            }

            block +=
                "\nUse this only when it naturally helps the reply, for example reacting to low health or progression.";
            return block;
        }

        private static PlayerRuntimeSnapshot ResolvePlayerRuntimeState(
            DialogueRequest request,
            string resolvedPlayerKey
        )
        {
            IPlayerRuntimeStateProvider provider = ProviderRegistry.PlayerRuntimeState;
            if (provider == null)
            {
                return PlayerRuntimeSnapshot.Invalid;
            }

            if (!string.IsNullOrWhiteSpace(resolvedPlayerKey))
            {
                if (provider.TryGetPlayerRuntimeState(resolvedPlayerKey.Trim(), out var byKey))
                {
                    return byKey;
                }
            }

            return provider.TryGetPlayerRuntimeState(request.RequestingClientId, out var byClientId)
                ? byClientId
                : PlayerRuntimeSnapshot.Invalid;
        }

    }
}
