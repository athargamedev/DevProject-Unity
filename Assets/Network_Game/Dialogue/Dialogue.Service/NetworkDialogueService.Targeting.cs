using System;
using System.Collections.Generic;
using Network_Game.Diagnostics;
using Network_Game.Dialogue.Effects;
using Unity.Netcode;
using UnityEngine;

namespace Network_Game.Dialogue
{
    public partial class NetworkDialogueService
    {
        // Network participant resolution and requester-first target selection.
        private GameObject ResolveSpawnedObject(ulong networkObjectId)
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (
                networkObjectId == 0
                || manager == null
                || !manager.IsListening
                || manager.SpawnManager == null
            )
            {
                return null;
            }

            if (
                manager.SpawnManager.SpawnedObjects.TryGetValue(
                    networkObjectId,
                    out NetworkObject networkObject
                )
            )
            {
                return networkObject.gameObject;
            }

            return null;
        }

        private NpcDialogueActor ResolveDialogueActor(ulong speakerNetworkId)
        {
            GameObject speakerObject = ResolveSpawnedObject(speakerNetworkId);
            if (speakerObject == null)
            {
                return null;
            }

            return speakerObject.GetComponent<NpcDialogueActor>();
        }

        private NpcDialogueActor ResolveDialogueActorForRequest(
            DialogueRequest request,
            out ulong resolvedSpeakerNetworkId,
            out ulong resolvedListenerNetworkId,
            out bool usedListenerFallback
        )
        {
            resolvedSpeakerNetworkId = request.SpeakerNetworkId;
            resolvedListenerNetworkId = request.ListenerNetworkId;
            usedListenerFallback = false;

            NpcDialogueActor speakerActor = ResolveDialogueActor(request.SpeakerNetworkId);
            if (speakerActor != null && speakerActor.Profile == null)
            {
                speakerActor.TryAutoAssignProfileFromName();
            }
            if (speakerActor != null && speakerActor.Profile != null)
            {
                return speakerActor;
            }

            NpcDialogueActor listenerActor = ResolveDialogueActor(request.ListenerNetworkId);
            if (listenerActor != null && listenerActor.Profile == null)
            {
                listenerActor.TryAutoAssignProfileFromName();
            }
            if (listenerActor != null && listenerActor.Profile != null)
            {
                resolvedSpeakerNetworkId = request.ListenerNetworkId;
                resolvedListenerNetworkId = request.SpeakerNetworkId;
                usedListenerFallback = request.SpeakerNetworkId != request.ListenerNetworkId;
                if (m_LogDebug)
                {
                    NGLog.Debug(
                        "Dialogue",
                        NGLog.Format(
                            "Resolved NPC speaker fallback",
                            ("originalSpeaker", request.SpeakerNetworkId),
                            ("originalListener", request.ListenerNetworkId),
                            ("resolvedSpeaker", resolvedSpeakerNetworkId),
                            ("resolvedListener", resolvedListenerNetworkId)
                        )
                    );
                }
                return listenerActor;
            }

            if (speakerActor != null)
            {
                return speakerActor;
            }

            if (listenerActor != null)
            {
                resolvedSpeakerNetworkId = request.ListenerNetworkId;
                resolvedListenerNetworkId = request.SpeakerNetworkId;
                usedListenerFallback = request.SpeakerNetworkId != request.ListenerNetworkId;
                return listenerActor;
            }

            return null;
        }

        private ulong ResolvePlayerNetworkIdForRequest(DialogueRequest request)
        {
            // Prefer the requesting client's player for per-player targeting/context.
            if (
                (request.RequestingClientId != 0 || request.IsUserInitiated)
                && TryGetPlayerNetworkObjectIdForClient(
                    request.RequestingClientId,
                    out ulong requesterPlayerNetworkId
                )
            )
            {
                return requesterPlayerNetworkId;
            }

            if (IsConnectedClientPlayerObject(request.ListenerNetworkId))
            {
                return request.ListenerNetworkId;
            }

            if (IsConnectedClientPlayerObject(request.SpeakerNetworkId))
            {
                return request.SpeakerNetworkId;
            }

            return 0;
        }

        private ulong ResolvePreferredListenerTargetNetworkObjectId(DialogueRequest request)
        {
            ulong resolvedPlayerNetworkId = ResolvePlayerNetworkIdForRequest(request);
            if (resolvedPlayerNetworkId != 0)
            {
                return resolvedPlayerNetworkId;
            }

            if (request.ListenerNetworkId != 0)
            {
                return request.ListenerNetworkId;
            }

            return request.SpeakerNetworkId;
        }

        private GameObject ResolvePreferredListenerTargetObject(DialogueRequest request)
        {
            ulong networkObjectId = ResolvePreferredListenerTargetNetworkObjectId(request);
            return networkObjectId != 0 ? ResolveSpawnedObject(networkObjectId) : null;
        }

        private bool TryResolveExplicitPlayerTargetNetworkObjectId(
            string targetHint,
            DialogueRequest request,
            out ulong playerNetworkObjectId
        )
        {
            playerNetworkObjectId = 0;
            if (string.IsNullOrWhiteSpace(targetHint))
            {
                return false;
            }

            string lower = targetHint.Trim().ToLowerInvariant();
            if (TryExtractExplicitPlayerQualifier(lower, out string qualifier))
            {
                lower = qualifier;
            }

            string compact = lower.Replace("_", string.Empty)
                .Replace("-", string.Empty)
                .Replace(" ", string.Empty);

            if (
                compact is "requester"
                    or "requestingplayer"
                    or "currentplayer"
                    or "localplayer"
                || lower is "player:requester" or "player:self" or "player:me"
            )
            {
                playerNetworkObjectId = ResolvePlayerNetworkIdForRequest(request);
                return playerNetworkObjectId != 0;
            }

            if (
                compact is "host" or "hostplayer" or "server" or "serverplayer"
                || lower is "player:host" or "player:server"
            )
            {
                return TryGetPlayerNetworkObjectIdForClient(
                    NetworkManager.ServerClientId,
                    out playerNetworkObjectId
                );
            }

            if (TryParseClientPlayerTargetToken(lower, out ulong targetClientId))
            {
                return TryGetPlayerNetworkObjectIdForClient(
                    targetClientId,
                    out playerNetworkObjectId
                );
            }

            if (TryParseOrderedPlayerTargetToken(lower, out int orderedIndex))
            {
                return TryResolveOrderedPlayerTargetNetworkObjectId(
                    orderedIndex,
                    out playerNetworkObjectId
                );
            }

            return false;
        }

        private static bool TryExtractExplicitPlayerQualifier(string lower, out string qualifier)
        {
            qualifier = string.Empty;
            if (string.IsNullOrWhiteSpace(lower))
            {
                return false;
            }

            string[] suffixes =
            {
                " head",
                " hair",
                " face",
                " feet",
                " foot",
                " toes",
                " legs",
                " ground",
                " floor",
                " terrain",
            };

            for (int i = 0; i < suffixes.Length; i++)
            {
                string suffix = suffixes[i];
                if (!lower.EndsWith(suffix, StringComparison.Ordinal) || lower.Length <= suffix.Length)
                {
                    continue;
                }

                qualifier = lower.Substring(0, lower.Length - suffix.Length).Trim();
                return !string.IsNullOrWhiteSpace(qualifier);
            }

            return false;
        }

        private bool TryResolveOrderedPlayerTargetNetworkObjectId(
            int orderedIndex,
            out ulong playerNetworkObjectId
        )
        {
            playerNetworkObjectId = 0;
            if (orderedIndex <= 0)
            {
                return false;
            }

            List<EffectTargetResolverService.PlayerTarget> orderedPlayers =
                new List<EffectTargetResolverService.PlayerTarget>(8);
            EffectTargetResolverService.GetOrderedPlayers(orderedPlayers, includeDead: true);
            for (int i = 0; i < orderedPlayers.Count; i++)
            {
                EffectTargetResolverService.PlayerTarget playerTarget = orderedPlayers[i];
                if (
                    playerTarget == null
                    || playerTarget.OrderedIndex != orderedIndex
                    || playerTarget.NetworkObject == null
                )
                {
                    continue;
                }

                playerNetworkObjectId = playerTarget.NetworkObject.NetworkObjectId;
                return playerNetworkObjectId != 0;
            }

            return false;
        }

        private bool TryResolveSingleConnectedPlayerFallback(out ulong playerNetworkId)
        {
            playerNetworkId = 0;
            if (
                NetworkManager.Singleton == null
                || NetworkManager.Singleton.ConnectedClients == null
                || NetworkManager.Singleton.ConnectedClients.Count == 0
            )
            {
                return false;
            }

            ulong resolved = 0;
            int matchedCount = 0;
            foreach (var client in NetworkManager.Singleton.ConnectedClients.Values)
            {
                if (
                    TryGetPlayerNetworkObjectIdForClient(
                        client.ClientId,
                        out ulong candidatePlayerNetworkId
                    )
                )
                {
                    matchedCount++;
                    resolved = candidatePlayerNetworkId;
                    if (matchedCount > 1)
                    {
                        break;
                    }
                }
            }

            if (matchedCount == 1 && resolved != 0)
            {
                playerNetworkId = resolved;
                return true;
            }

            return false;
        }

        private bool IsConnectedClientPlayerObject(ulong networkObjectId)
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (
                networkObjectId == 0
                || manager == null
                || !manager.IsListening
                || manager.ConnectedClients == null
            )
            {
                return false;
            }

            foreach (var client in manager.ConnectedClients.Values)
            {
                if (client?.PlayerObject == null)
                {
                    continue;
                }

                if (client.PlayerObject.NetworkObjectId == networkObjectId)
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryGetPlayerNetworkObjectIdForClient(ulong clientId, out ulong playerNetworkId)
        {
            playerNetworkId = 0;
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsListening || manager.ConnectedClients == null)
            {
                return false;
            }

            if (
                manager.ConnectedClients.TryGetValue(clientId, out var client)
                && client?.PlayerObject != null
            )
            {
                playerNetworkId = client.PlayerObject.NetworkObjectId;
                return true;
            }

            return false;
        }

        private bool IsObjectOwnedByClient(ulong networkObjectId, ulong clientId)
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (
                networkObjectId == 0
                || manager == null
                || !manager.IsListening
                || manager.SpawnManager == null
            )
            {
                return false;
            }

            if (
                !manager.SpawnManager.SpawnedObjects.TryGetValue(
                    networkObjectId,
                    out NetworkObject networkObject
                )
            )
            {
                return false;
            }

            return networkObject != null && networkObject.OwnerClientId == clientId;
        }

        private bool TryValidateClientDialogueParticipants(
            ulong senderClientId,
            ulong speakerNetworkId,
            ulong listenerNetworkId,
            out string rejectionReason
        )
        {
            rejectionReason = null;
            bool hasSenderPlayer = TryGetPlayerNetworkObjectIdForClient(
                senderClientId,
                out ulong senderPlayerNetworkId
            );

            if (speakerNetworkId == 0 && listenerNetworkId == 0)
            {
                rejectionReason = "participants_missing";
                return false;
            }

            bool senderOwnsSpeaker = IsObjectOwnedByClient(speakerNetworkId, senderClientId);
            bool senderOwnsListener = IsObjectOwnedByClient(listenerNetworkId, senderClientId);
            if (!hasSenderPlayer && !senderOwnsSpeaker && !senderOwnsListener)
            {
                rejectionReason = "requester_player_missing";
                return false;
            }

            bool senderMatchesPlayer =
                hasSenderPlayer
                && (
                    speakerNetworkId == senderPlayerNetworkId
                    || listenerNetworkId == senderPlayerNetworkId
                );
            if (!senderMatchesPlayer && !senderOwnsSpeaker && !senderOwnsListener)
            {
                rejectionReason = "invalid_participants";
                return false;
            }

            return true;
        }

        private bool IsConversationKeyVisibleToClient(string conversationKey, ulong senderClientId)
        {
            if (string.IsNullOrWhiteSpace(conversationKey))
            {
                return false;
            }

            bool hasSenderPlayer = TryGetPlayerNetworkObjectIdForClient(
                senderClientId,
                out ulong senderPlayerNetworkId
            );

            string key = conversationKey.Trim();
            string clientScopePrefix = $"client:{senderClientId}";
            if (
                string.Equals(key, clientScopePrefix, StringComparison.OrdinalIgnoreCase)
                || key.StartsWith($"{clientScopePrefix}:", StringComparison.OrdinalIgnoreCase)
            )
            {
                return true;
            }

            if (
                key.StartsWith("actor:", StringComparison.OrdinalIgnoreCase)
                && ulong.TryParse(key.Substring("actor:".Length), out ulong actorId)
            )
            {
                return (hasSenderPlayer && actorId == senderPlayerNetworkId)
                    || IsObjectOwnedByClient(actorId, senderClientId);
            }

            int firstColon = key.IndexOf(':');
            int secondColon = firstColon >= 0 ? key.IndexOf(':', firstColon + 1) : -1;
            if (
                firstColon > 0
                && secondColon < 0
                && ulong.TryParse(key.Substring(0, firstColon), out ulong first)
                && ulong.TryParse(key.Substring(firstColon + 1), out ulong second)
            )
            {
                return (
                        hasSenderPlayer
                        && (first == senderPlayerNetworkId || second == senderPlayerNetworkId)
                    )
                    || IsObjectOwnedByClient(first, senderClientId)
                    || IsObjectOwnedByClient(second, senderClientId);
            }

            return false;
        }

    }
}
