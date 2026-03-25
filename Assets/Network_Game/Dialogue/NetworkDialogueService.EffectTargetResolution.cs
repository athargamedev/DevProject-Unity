using System;
using Unity.Netcode;
using UnityEngine;

namespace Network_Game.Dialogue
{
    public partial class NetworkDialogueService
    {
        // Target-object resolution for effect intents.
        private bool TryResolveEffectIntentTarget(
            string targetText,
            DialogueRequest request,
            Vector3 fallbackOrigin,
            Vector3 fallbackForward,
            out ulong targetNetworkObjectId,
            out Vector3 spawnPos,
            out Vector3 spawnForward,
            out GameObject resolvedTargetObject
        )
        {
            targetNetworkObjectId = ResolvePreferredListenerTargetNetworkObjectId(request);
            spawnPos = fallbackOrigin + fallbackForward * 1.5f;
            spawnForward = fallbackForward;
            resolvedTargetObject = ResolvePreferredListenerTargetObject(request);

            if (string.IsNullOrWhiteSpace(targetText))
            {
                return false;
            }

            string cleanedTarget = targetText.Trim().Trim('"', '\'');
            if (cleanedTarget.Length == 0)
            {
                return false;
            }

            string lower = cleanedTarget.ToLowerInvariant();
            string[] prefixes = { "at ", "on ", "near ", "around ", "to ", "target " };
            for (int i = 0; i < prefixes.Length; i++)
            {
                string prefix = prefixes[i];
                if (lower.StartsWith(prefix, StringComparison.Ordinal))
                {
                    cleanedTarget = cleanedTarget.Substring(prefix.Length).Trim();
                    lower = cleanedTarget.ToLowerInvariant();
                    break;
                }
            }

            if (ulong.TryParse(cleanedTarget, out ulong explicitTargetId) && explicitTargetId != 0)
            {
                targetNetworkObjectId = explicitTargetId;
                GameObject explicitTargetObject = ResolveSpawnedObject(explicitTargetId);
                resolvedTargetObject = explicitTargetObject;
                if (explicitTargetObject != null)
                {
                    spawnPos = ResolveEffectOrigin(explicitTargetObject);
                    GameObject speakerObject = ResolveSpawnedObject(request.SpeakerNetworkId);
                    spawnForward = ResolveEffectForward(speakerObject, explicitTargetObject);
                }

                return true;
            }

            if (IsPlayerHeadAlias(lower) || IsPlayerFeetAlias(lower) || IsGroundAlias(lower))
            {
                bool usesExplicitPlayerToken = LooksLikeExplicitPlayerTargetToken(lower);
                ulong resolvedPlayerNetworkId = 0;
                if (usesExplicitPlayerToken)
                {
                    if (
                        !TryResolveExplicitPlayerTargetNetworkObjectId(
                            lower,
                            request,
                            out resolvedPlayerNetworkId
                        )
                    )
                    {
                        targetNetworkObjectId = 0UL;
                        resolvedTargetObject = null;
                        return false;
                    }
                }
                else
                {
                    resolvedPlayerNetworkId = ResolvePlayerNetworkIdForRequest(request);
                }

                if (resolvedPlayerNetworkId != 0)
                {
                    targetNetworkObjectId = resolvedPlayerNetworkId;
                }
                else
                {
                    targetNetworkObjectId = ResolvePreferredListenerTargetNetworkObjectId(request);
                }

                GameObject listenerObject = ResolveSpawnedObject(targetNetworkObjectId);
                GameObject speakerObject = ResolveSpawnedObject(request.SpeakerNetworkId);

                if (IsGroundAlias(lower))
                {
                    GameObject semanticGround =
                        FindSceneObjectByName("role:floor")
                        ?? FindSceneObjectByName("role:terrain")
                        ?? FindSceneObjectByName("ground")
                        ?? FindSceneObjectByName("floor");
                    if (semanticGround != null)
                    {
                        NetworkObject semanticGroundNetworkObject =
                            semanticGround.GetComponentInParent<NetworkObject>();
                        targetNetworkObjectId =
                            semanticGroundNetworkObject != null
                            && semanticGroundNetworkObject.IsSpawned
                                ? semanticGroundNetworkObject.NetworkObjectId
                                : 0UL;
                        resolvedTargetObject = semanticGround;
                        Vector3 groundReference =
                            listenerObject != null
                                ? listenerObject.transform.position
                                : fallbackOrigin;
                        spawnPos = ResolveGroundPlacementNearReference(
                            semanticGround,
                            groundReference
                        );
                        spawnForward = ResolveEffectForward(
                            speakerObject,
                            listenerObject != null ? listenerObject : semanticGround
                        );
                        return true;
                    }
                }

                if (IsGroundAlias(lower))
                {
                    targetNetworkObjectId = 0UL;
                }

                resolvedTargetObject = listenerObject;
                if (listenerObject != null)
                {
                    Vector3 anchorPosition = ResolveEffectOrigin(listenerObject);
                    if (
                        TryGetObjectBounds(listenerObject, out Bounds listenerBounds)
                        && listenerBounds.size.sqrMagnitude > 0.0001f
                    )
                    {
                        if (IsPlayerHeadAlias(lower))
                        {
                            anchorPosition = new Vector3(
                                listenerBounds.center.x,
                                listenerBounds.max.y + 0.06f,
                                listenerBounds.center.z
                            );
                        }
                        else if (IsPlayerFeetAlias(lower) || IsGroundAlias(lower))
                        {
                            anchorPosition = new Vector3(
                                listenerBounds.center.x,
                                listenerBounds.min.y + 0.03f,
                                listenerBounds.center.z
                            );
                        }
                    }

                    spawnPos = anchorPosition;
                    spawnForward = ResolveEffectForward(speakerObject, listenerObject);
                }

                return true;
            }

            if (LooksLikeExplicitPlayerTargetToken(lower))
            {
                if (
                    !TryResolveExplicitPlayerTargetNetworkObjectId(
                        lower,
                        request,
                        out ulong explicitPlayerTargetNetworkObjectId
                    )
                )
                {
                    targetNetworkObjectId = 0UL;
                    resolvedTargetObject = null;
                    return false;
                }

                targetNetworkObjectId = explicitPlayerTargetNetworkObjectId;
                GameObject explicitPlayerTargetObject = ResolveSpawnedObject(
                    explicitPlayerTargetNetworkObjectId
                );
                resolvedTargetObject = explicitPlayerTargetObject;
                if (explicitPlayerTargetObject != null)
                {
                    spawnPos = ResolveEffectOrigin(explicitPlayerTargetObject);
                    GameObject speakerObject = ResolveSpawnedObject(request.SpeakerNetworkId);
                    spawnForward = ResolveEffectForward(
                        speakerObject,
                        explicitPlayerTargetObject
                    );
                }

                return true;
            }

            if (IsPlayerTargetToken(lower))
            {
                ulong resolvedPlayerNetworkId = ResolvePlayerNetworkIdForRequest(request);
                if (resolvedPlayerNetworkId != 0)
                {
                    targetNetworkObjectId = resolvedPlayerNetworkId;
                }
                else
                {
                    targetNetworkObjectId = ResolvePreferredListenerTargetNetworkObjectId(request);
                }

                GameObject listenerObject = ResolveSpawnedObject(targetNetworkObjectId);
                resolvedTargetObject = listenerObject;
                if (listenerObject != null)
                {
                    spawnPos = ResolveEffectOrigin(listenerObject);
                    GameObject speakerObject = ResolveSpawnedObject(request.SpeakerNetworkId);
                    spawnForward = ResolveEffectForward(speakerObject, listenerObject);
                }

                return true;
            }

            if (lower is "self" or "npc" or "caster" or "speaker" or "enemy" or "boss")
            {
                targetNetworkObjectId = request.SpeakerNetworkId;
                GameObject speakerObject = ResolveSpawnedObject(request.SpeakerNetworkId);
                resolvedTargetObject = speakerObject;
                if (speakerObject != null)
                {
                    spawnPos = ResolveEffectOrigin(speakerObject);
                    spawnForward =
                        speakerObject.transform.forward.sqrMagnitude > 0.0001f
                            ? speakerObject.transform.forward.normalized
                            : fallbackForward;
                }

                return true;
            }

            GameObject objectTarget = FindSceneObjectByName(cleanedTarget);
            if (objectTarget != null)
            {
                resolvedTargetObject = objectTarget;
                spawnPos = ResolveEffectOrigin(objectTarget);
                GameObject speakerObject = ResolveSpawnedObject(request.SpeakerNetworkId);
                spawnForward = ResolveEffectForward(speakerObject, objectTarget);
                NetworkObject networkObject = objectTarget.GetComponentInParent<NetworkObject>();
                if (networkObject != null)
                {
                    targetNetworkObjectId = networkObject.NetworkObjectId;
                }

                return true;
            }

            return false;
        }
    }
}
