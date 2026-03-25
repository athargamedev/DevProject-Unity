using System;
using System.Collections.Generic;
using Network_Game.Diagnostics;
using Unity.Netcode;

namespace Network_Game.Dialogue
{
    public partial class NetworkDialogueService
    {
        private void RebuildPlayerPromptContextLookup()
        {
            m_PlayerPromptContextByNetworkId.Clear();
            if (m_PlayerPromptContextBindings == null)
            {
                m_PlayerPromptContextBindings = new List<PlayerPromptContextBinding>();
                return;
            }

            for (int i = 0; i < m_PlayerPromptContextBindings.Count; i++)
            {
                PlayerPromptContextBinding binding = m_PlayerPromptContextBindings[i];
                if (binding == null || !binding.Enabled || binding.PlayerNetworkId == 0)
                {
                    continue;
                }

                binding.NameId = string.IsNullOrWhiteSpace(binding.NameId)
                    ? string.Empty
                    : binding.NameId.Trim();
                binding.CustomizationJson = string.IsNullOrWhiteSpace(binding.CustomizationJson)
                    ? "{}"
                    : binding.CustomizationJson.Trim();
                m_PlayerPromptContextByNetworkId[binding.PlayerNetworkId] = binding;
            }
        }

        private void RebuildPlayerIdentityLookup()
        {
            m_PlayerIdentityByClientId.Clear();
            m_PlayerIdentityByNetworkId.Clear();
            if (m_PlayerIdentityBindings == null)
            {
                m_PlayerIdentityBindings = new List<PlayerIdentityBinding>();
                return;
            }

            for (int i = 0; i < m_PlayerIdentityBindings.Count; i++)
            {
                PlayerIdentityBinding binding = m_PlayerIdentityBindings[i];
                if (binding == null || !binding.Enabled)
                {
                    continue;
                }

                binding.NameId = string.IsNullOrWhiteSpace(binding.NameId)
                    ? string.Empty
                    : binding.NameId.Trim();
                binding.CustomizationJson = NormalizePromptContextJson(
                    binding.NameId,
                    binding.CustomizationJson
                );
                if (string.IsNullOrWhiteSpace(binding.LastUpdatedUtc))
                {
                    binding.LastUpdatedUtc = DateTime.UtcNow.ToString("o");
                }

                if (binding.ClientId != ulong.MaxValue)
                {
                    m_PlayerIdentityByClientId[binding.ClientId] = binding;
                }

                if (binding.PlayerNetworkId != 0)
                {
                    m_PlayerIdentityByNetworkId[binding.PlayerNetworkId] = binding;
                }
            }
        }

        private void SynthesizeIdentityBindingsFromRuntimeBindings()
        {
            foreach (var promptBinding in m_PlayerPromptContextByNetworkId.Values)
            {
                if (
                    promptBinding == null
                    || !promptBinding.Enabled
                    || promptBinding.PlayerNetworkId == 0
                )
                {
                    continue;
                }

                UpsertPlayerIdentity(
                    ResolveOwnerClientIdForPlayerNetworkId(promptBinding.PlayerNetworkId),
                    promptBinding.PlayerNetworkId,
                    promptBinding.NameId,
                    promptBinding.CustomizationJson
                );
            }
        }

        private PlayerPromptContextBinding FindOrCreatePlayerPromptContextBinding(
            ulong playerNetworkId
        )
        {
            if (m_PlayerPromptContextBindings == null)
            {
                m_PlayerPromptContextBindings = new List<PlayerPromptContextBinding>();
            }

            for (int i = 0; i < m_PlayerPromptContextBindings.Count; i++)
            {
                PlayerPromptContextBinding existing = m_PlayerPromptContextBindings[i];
                if (existing == null || existing.PlayerNetworkId != playerNetworkId)
                {
                    continue;
                }

                return existing;
            }

            var created = new PlayerPromptContextBinding
            {
                PlayerNetworkId = playerNetworkId,
                NameId = string.Empty,
                CustomizationJson = "{}",
                Enabled = true,
            };
            m_PlayerPromptContextBindings.Add(created);
            return created;
        }

        private PlayerIdentityBinding FindOrCreatePlayerIdentityBinding(
            ulong clientId,
            ulong playerNetworkId
        )
        {
            if (m_PlayerIdentityBindings == null)
            {
                m_PlayerIdentityBindings = new List<PlayerIdentityBinding>();
            }

            if (
                clientId != ulong.MaxValue
                && m_PlayerIdentityByClientId.TryGetValue(clientId, out var byClient)
            )
            {
                return byClient;
            }

            if (
                playerNetworkId != 0
                && m_PlayerIdentityByNetworkId.TryGetValue(playerNetworkId, out var byPlayer)
            )
            {
                return byPlayer;
            }

            for (int i = 0; i < m_PlayerIdentityBindings.Count; i++)
            {
                PlayerIdentityBinding existing = m_PlayerIdentityBindings[i];
                if (existing == null)
                {
                    continue;
                }

                if (
                    (clientId != ulong.MaxValue && existing.ClientId == clientId)
                    || (playerNetworkId != 0 && existing.PlayerNetworkId == playerNetworkId)
                )
                {
                    return existing;
                }
            }

            var created = new PlayerIdentityBinding
            {
                ClientId = clientId,
                PlayerNetworkId = playerNetworkId,
                NameId = string.Empty,
                CustomizationJson = "{}",
                Enabled = true,
                LastUpdatedUtc = DateTime.UtcNow.ToString("o"),
            };
            m_PlayerIdentityBindings.Add(created);
            return created;
        }

        private void UpsertPlayerIdentity(
            ulong clientId,
            ulong playerNetworkId,
            string nameId,
            string customizationJson
        )
        {
            PlayerIdentityBinding identity = FindOrCreatePlayerIdentityBinding(
                clientId,
                playerNetworkId
            );
            if (identity == null)
            {
                return;
            }

            ulong oldClientId = identity.ClientId;
            ulong oldPlayerNetworkId = identity.PlayerNetworkId;
            string oldNameId = identity.NameId ?? string.Empty;
            string oldCustomization = identity.CustomizationJson ?? "{}";

            if (clientId != ulong.MaxValue)
            {
                identity.ClientId = clientId;
            }

            if (playerNetworkId != 0)
            {
                identity.PlayerNetworkId = playerNetworkId;
            }

            if (!string.IsNullOrWhiteSpace(nameId))
            {
                string normalizedName = nameId.Trim();
                bool incomingIsPlaceholder = normalizedName.StartsWith(
                    "client_",
                    StringComparison.OrdinalIgnoreCase
                );
                if (string.IsNullOrWhiteSpace(identity.NameId) || !incomingIsPlaceholder)
                {
                    identity.NameId = normalizedName;
                }
            }

            if (!string.IsNullOrWhiteSpace(customizationJson))
            {
                identity.CustomizationJson = NormalizePromptContextJson(
                    identity.NameId,
                    customizationJson
                );
            }

            identity.Enabled = true;
            identity.LastUpdatedUtc = DateTime.UtcNow.ToString("o");

            if (identity.ClientId != ulong.MaxValue)
            {
                m_PlayerIdentityByClientId[identity.ClientId] = identity;
            }

            if (identity.PlayerNetworkId != 0)
            {
                m_PlayerIdentityByNetworkId[identity.PlayerNetworkId] = identity;
            }

            if (m_LogDebug)
            {
                bool changed =
                    oldClientId != identity.ClientId
                    || oldPlayerNetworkId != identity.PlayerNetworkId
                    || !string.Equals(oldNameId, identity.NameId, StringComparison.Ordinal)
                    || !string.Equals(
                        oldCustomization,
                        identity.CustomizationJson,
                        StringComparison.Ordinal
                    );

                if (changed)
                {
                    NGLog.Info(
                        "Dialogue",
                        NGLog.Format(
                            "Player identity updated",
                            ("clientId", identity.ClientId),
                            ("playerNetworkId", identity.PlayerNetworkId),
                            ("name_id", identity.NameId ?? string.Empty),
                            (
                                "hasCustomization",
                                !IsPlaceholderPromptContextJson(identity.CustomizationJson)
                            )
                        )
                    );
                }
            }
        }

        private PlayerIdentityBinding ResolvePlayerIdentityForRequest(DialogueRequest request)
        {
            if (request.RequestingClientId != 0)
            {
                if (
                    m_PlayerIdentityByClientId.TryGetValue(
                        request.RequestingClientId,
                        out PlayerIdentityBinding byClient
                    )
                    && byClient != null
                    && byClient.Enabled
                )
                {
                    return byClient;
                }
            }

            ulong playerNetworkId = ResolvePlayerNetworkIdForRequest(request);
            if (
                playerNetworkId != 0
                && m_PlayerIdentityByNetworkId.TryGetValue(
                    playerNetworkId,
                    out PlayerIdentityBinding byNetwork
                )
                && byNetwork != null
                && byNetwork.Enabled
            )
            {
                return byNetwork;
            }

            if (
                request.RequestingClientId != 0
                && TryGetPlayerNetworkObjectIdForClient(
                    request.RequestingClientId,
                    out ulong resolvedPlayerNetworkId
                )
            )
            {
                if (
                    m_PlayerIdentityByNetworkId.TryGetValue(
                        resolvedPlayerNetworkId,
                        out PlayerIdentityBinding fallback
                    )
                    && fallback != null
                    && fallback.Enabled
                )
                {
                    return fallback;
                }
            }

            return null;
        }

        private ulong ResolveOwnerClientIdForPlayerNetworkId(ulong playerNetworkId)
        {
            if (playerNetworkId == 0 || NetworkManager.Singleton?.SpawnManager == null)
            {
                return ulong.MaxValue;
            }

            if (
                !NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(
                    playerNetworkId,
                    out NetworkObject playerObject
                )
                || playerObject == null
            )
            {
                return ulong.MaxValue;
            }

            return playerObject.OwnerClientId;
        }

        private static PlayerIdentitySnapshot ToSnapshot(PlayerIdentityBinding identity)
        {
            return new PlayerIdentitySnapshot
            {
                ClientId = identity.ClientId,
                PlayerNetworkId = identity.PlayerNetworkId,
                NameId = identity.NameId ?? string.Empty,
                CustomizationJson = identity.CustomizationJson ?? "{}",
                LastUpdatedUtc = identity.LastUpdatedUtc ?? string.Empty,
            };
        }

    }
}
