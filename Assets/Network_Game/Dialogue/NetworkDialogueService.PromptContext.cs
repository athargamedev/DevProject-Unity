using Network_Game.Diagnostics;

namespace Network_Game.Dialogue
{
    public partial class NetworkDialogueService
    {
        public bool TryGetPlayerIdentityByClientId(
            ulong clientId,
            out PlayerIdentitySnapshot snapshot
        )
        {
            snapshot = default;
            if (
                !m_PlayerIdentityByClientId.TryGetValue(clientId, out var identity)
                || identity == null
                || !identity.Enabled
            )
            {
                return false;
            }

            snapshot = ToSnapshot(identity);
            return true;
        }

        public bool TryGetPlayerIdentityByNetworkId(
            ulong playerNetworkId,
            out PlayerIdentitySnapshot snapshot
        )
        {
            snapshot = default;
            if (
                playerNetworkId == 0
                || !m_PlayerIdentityByNetworkId.TryGetValue(playerNetworkId, out var identity)
                || identity == null
                || !identity.Enabled
            )
            {
                return false;
            }

            snapshot = ToSnapshot(identity);
            return true;
        }

        public bool SetPlayerPromptContext(
            ulong playerNetworkId,
            string nameId,
            string customizationJson
        )
        {
            if (!IsServer)
            {
                NGLog.Warn("Dialogue", "SetPlayerPromptContext called on non-server.");
                return false;
            }

            if (playerNetworkId == 0)
            {
                return false;
            }

            string normalizedNameId = string.IsNullOrWhiteSpace(nameId)
                ? string.Empty
                : nameId.Trim();
            string normalizedCustomizationJson = NormalizePromptContextJson(
                normalizedNameId,
                customizationJson
            );

            PlayerPromptContextBinding binding = FindOrCreatePlayerPromptContextBinding(
                playerNetworkId
            );
            binding.NameId = normalizedNameId;
            binding.CustomizationJson = normalizedCustomizationJson;
            binding.Enabled = true;
            m_PlayerPromptContextByNetworkId[playerNetworkId] = binding;
            UpsertPlayerIdentity(
                ResolveOwnerClientIdForPlayerNetworkId(playerNetworkId),
                playerNetworkId,
                normalizedNameId,
                normalizedCustomizationJson
            );
            return true;
        }

        public bool ClearPlayerPromptContext(ulong playerNetworkId)
        {
            if (!IsServer || playerNetworkId == 0)
            {
                return false;
            }

            bool removedAny = false;
            removedAny |= m_PlayerPromptContextByNetworkId.Remove(playerNetworkId);
            if (m_PlayerPromptContextBindings != null)
            {
                for (int i = m_PlayerPromptContextBindings.Count - 1; i >= 0; i--)
                {
                    PlayerPromptContextBinding binding = m_PlayerPromptContextBindings[i];
                    if (binding == null || binding.PlayerNetworkId != playerNetworkId)
                    {
                        continue;
                    }

                    m_PlayerPromptContextBindings.RemoveAt(i);
                    removedAny = true;
                }
            }

            UpsertPlayerIdentity(
                ResolveOwnerClientIdForPlayerNetworkId(playerNetworkId),
                playerNetworkId,
                null,
                "{}"
            );

            return removedAny;
        }

        public bool SetPlayerPromptContextForClient(
            ulong clientId,
            string nameId,
            string customizationJson
        )
        {
            if (!TryGetPlayerNetworkObjectIdForClient(clientId, out ulong playerNetworkId))
            {
                return false;
            }

            bool applied = SetPlayerPromptContext(playerNetworkId, nameId, customizationJson);
            if (applied)
            {
                UpsertPlayerIdentity(clientId, playerNetworkId, nameId, customizationJson);
            }

            return applied;
        }

        public bool ClearPlayerPromptContextForClient(ulong clientId)
        {
            return TryGetPlayerNetworkObjectIdForClient(clientId, out ulong playerNetworkId)
                && ClearPlayerPromptContext(playerNetworkId);
        }

        public bool HasPlayerPromptContextBinding(ulong playerNetworkId)
        {
            return playerNetworkId != 0
                && m_PlayerPromptContextByNetworkId.TryGetValue(playerNetworkId, out var binding)
                && binding != null
                && binding.Enabled;
        }

        public bool RequestSetPlayerPromptContextFromClient(string nameId, string customizationJson)
        {
            if (IsServer || !IsClient)
            {
                return false;
            }

            SetPlayerPromptContextServerRpc(nameId ?? string.Empty, customizationJson ?? "{}");
            return true;
        }

        public bool RequestClearPlayerPromptContextFromClient()
        {
            if (IsServer || !IsClient)
            {
                return false;
            }

            ClearPlayerPromptContextServerRpc();
            return true;
        }
    }
}
