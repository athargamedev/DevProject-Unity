using System;
using System.Text;
using Network_Game.Diagnostics;
using Network_Game.Dialogue.Effects;
using Unity.Netcode;
using UnityEngine;

namespace Network_Game.Dialogue
{
    public partial class NetworkDialogueService
    {
        private enum EffectSpatialType
        {
            Ambient,
            Projectile,
            Area,
            Attached,
        }

        private struct EffectSpatialPolicy
        {
            public EffectSpatialType Type;
            public bool GroundSnap;
            public bool RequireLineOfSight;
            public string CollisionPolicyHint;
            public float ClearanceScale;
            public bool UseFallback;
            public float FallbackDistance;
            public bool PreferTargetOrigin;
            public float TargetHeightOffset;
        }

        private void LogEffectTargetResolution(
            string flow,
            string tag,
            string requestedTarget,
            ulong resolvedTargetNetworkObjectId,
            GameObject resolvedTargetObject,
            ulong speakerNetworkObjectId
        )
        {
            if (!m_LogDebug)
            {
                return;
            }

            NGLog.Debug(
                "DialogueFX",
                NGLog.Format(
                    "Effect target resolution",
                    ("flow", flow ?? string.Empty),
                    ("tag", tag ?? string.Empty),
                    ("requested", requestedTarget ?? string.Empty),
                    ("speaker", speakerNetworkObjectId),
                    ("resolvedId", resolvedTargetNetworkObjectId),
                    (
                        "resolvedObject",
                        resolvedTargetObject != null ? resolvedTargetObject.name : "<null>"
                    )
                )
            );
        }

        private static bool LooksLikePlaceholderEffectTag(string rawTagName)
        {
            if (string.IsNullOrWhiteSpace(rawTagName))
            {
                return true;
            }

            string trimmed = rawTagName.Trim();
            if (trimmed == "..." || trimmed == "â€¦")
            {
                return true;
            }

            string lower = trimmed.ToLowerInvariant();
            return lower.Contains("...")
                || lower.Contains("effectname")
                || lower.Contains("your_effect")
                || lower.Contains("example");
        }

        private static bool TryParseEffectSpatialTypeHint(
            string placementHint,
            out EffectSpatialType type
        )
        {
            type = EffectSpatialType.Ambient;
            if (string.IsNullOrWhiteSpace(placementHint))
            {
                return false;
            }

            string normalized = placementHint
                .Trim()
                .Replace("-", "_")
                .Replace(" ", "_")
                .ToLowerInvariant();
            switch (normalized)
            {
                case "projectile":
                case "bolt":
                case "missile":
                case "shot":
                    type = EffectSpatialType.Projectile;
                    return true;
                case "area":
                case "aoe":
                case "explosion":
                case "blast":
                    type = EffectSpatialType.Area;
                    return true;
                case "attached":
                case "attach":
                case "self":
                case "aura":
                    type = EffectSpatialType.Attached;
                    return true;
                case "ambient":
                case "world":
                case "scene":
                    type = EffectSpatialType.Ambient;
                    return true;
                default:
                    return false;
            }
        }

        private static bool TextHasToken(string source, string token)
        {
            return !string.IsNullOrWhiteSpace(source)
                && !string.IsNullOrWhiteSpace(token)
                && source.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string ResolvePlacementHintForDefinition(
            string parserPlacementHint,
            EffectDefinition definition
        )
        {
            if (!string.IsNullOrWhiteSpace(parserPlacementHint))
            {
                return parserPlacementHint;
            }

            if (definition == null)
            {
                return parserPlacementHint;
            }

            switch (definition.placementMode)
            {
                case EffectPlacementMode.AttachMesh:
                    return "attached";
                case EffectPlacementMode.GroundAoe:
                    return "area";
                case EffectPlacementMode.SkyVolume:
                    return "ambient";
                case EffectPlacementMode.Projectile:
                    return "projectile";
                default:
                    return parserPlacementHint;
            }
        }

        private static string ResolveTargetHintForDefinition(
            string parserTargetHint,
            EffectDefinition definition
        )
        {
            if (definition == null)
            {
                return parserTargetHint;
            }

            string effectiveHint = parserTargetHint;
            if (
                string.IsNullOrWhiteSpace(parserTargetHint)
                || parserTargetHint.Equals("auto", StringComparison.OrdinalIgnoreCase)
                || parserTargetHint.Equals("default", StringComparison.OrdinalIgnoreCase)
            )
            {
                effectiveHint = definition.targetType switch
                {
                    EffectTargetType.Player => "player",
                    EffectTargetType.Floor => "ground",
                    EffectTargetType.Npc => "npc",
                    EffectTargetType.WorldPoint => "world",
                    _ => parserTargetHint,
                };
            }

            if (
                !string.IsNullOrWhiteSpace(parserTargetHint)
                && definition.targetType != EffectTargetType.Auto
                && !parserTargetHint.Equals(effectiveHint, StringComparison.OrdinalIgnoreCase)
            )
            {
                NGLog.Debug(
                    "DialogueFX",
                    $"Target override: LLM specified '{parserTargetHint}' vs definition default '{definition.targetType}'"
                );
            }

            return effectiveHint;
        }

        private EffectSpatialType ResolveEffectSpatialType(
            string placementHint,
            string effectName,
            string targetHint,
            bool enableHoming,
            float projectileSpeed,
            float damageRadius,
            float scale,
            bool affectPlayerOnly
        )
        {
            if (TryParseEffectSpatialTypeHint(placementHint, out EffectSpatialType hinted))
            {
                return hinted;
            }

            bool likelyProjectile =
                enableHoming
                || projectileSpeed >= 12f
                || TextHasToken(effectName, "projectile")
                || TextHasToken(effectName, "missile")
                || TextHasToken(effectName, "bolt")
                || TextHasToken(effectName, "arrow")
                || TextHasToken(effectName, "lance")
                || TextHasToken(effectName, "orb");

            bool likelyArea =
                damageRadius >= 2.5f
                || scale >= 2.4f
                || TextHasToken(effectName, "explosion")
                || TextHasToken(effectName, "blast")
                || TextHasToken(effectName, "shockwave")
                || TextHasToken(effectName, "nova")
                || TextHasToken(effectName, "storm")
                || TextHasToken(effectName, "rain");

            bool targetIsActor = LooksLikePlayerTargetHint(targetHint);

            if (!likelyProjectile && targetIsActor && (affectPlayerOnly || damageRadius <= 1.25f))
            {
                return EffectSpatialType.Attached;
            }

            if (likelyProjectile)
            {
                return EffectSpatialType.Projectile;
            }

            if (likelyArea)
            {
                return EffectSpatialType.Area;
            }

            return EffectSpatialType.Ambient;
        }

        private EffectSpatialPolicy BuildEffectSpatialPolicy(
            EffectSpatialType type,
            bool enableGameplayDamage,
            string collisionPolicyHintOverride,
            bool? groundSnapOverride = null,
            bool? requireLineOfSightOverride = null
        )
        {
            EffectSpatialPolicy policy = new EffectSpatialPolicy
            {
                Type = type,
                GroundSnap = true,
                RequireLineOfSight = false,
                CollisionPolicyHint = "relaxed",
                ClearanceScale = 0.2f,
                UseFallback = true,
                FallbackDistance = Mathf.Max(0.25f, m_EffectFallbackDistance),
                PreferTargetOrigin = false,
                TargetHeightOffset = 0f,
            };

            switch (type)
            {
                case EffectSpatialType.Projectile:
                    policy.GroundSnap = false;
                    policy.RequireLineOfSight = true;
                    policy.CollisionPolicyHint = "strict";
                    policy.ClearanceScale = 0.12f;
                    policy.UseFallback = false;
                    policy.FallbackDistance = Mathf.Max(0.5f, m_EffectFallbackDistance * 0.75f);
                    break;
                case EffectSpatialType.Area:
                    policy.GroundSnap = true;
                    policy.RequireLineOfSight = enableGameplayDamage;
                    policy.CollisionPolicyHint = enableGameplayDamage ? "strict" : "relaxed";
                    policy.ClearanceScale = 0.28f;
                    policy.UseFallback = true;
                    break;
                case EffectSpatialType.Attached:
                    policy.GroundSnap = false;
                    policy.RequireLineOfSight = false;
                    policy.CollisionPolicyHint = "allow_overlap";
                    policy.ClearanceScale = 0.08f;
                    policy.UseFallback = true;
                    policy.FallbackDistance = Mathf.Max(0.5f, m_EffectFallbackDistance * 0.8f);
                    policy.PreferTargetOrigin = true;
                    policy.TargetHeightOffset = 0.2f;
                    break;
            }

            if (groundSnapOverride.HasValue)
            {
                policy.GroundSnap = groundSnapOverride.Value;
            }

            if (requireLineOfSightOverride.HasValue)
            {
                policy.RequireLineOfSight = requireLineOfSightOverride.Value;
            }

            if (!string.IsNullOrWhiteSpace(collisionPolicyHintOverride))
            {
                policy.CollisionPolicyHint = collisionPolicyHintOverride;
            }

            return policy;
        }

        private DialogueEffectSpatialResolver.CollisionPolicy ResolveCollisionPolicy(
            string collisionPolicyHint,
            bool enableGameplayDamage
        )
        {
            if (
                !string.IsNullOrWhiteSpace(collisionPolicyHint)
                && DialogueEffectSpatialResolver.TryParseCollisionPolicy(
                    collisionPolicyHint,
                    out DialogueEffectSpatialResolver.CollisionPolicy parsed
                )
            )
            {
                return parsed;
            }

            return enableGameplayDamage
                ? DialogueEffectSpatialResolver.CollisionPolicy.Strict
                : DialogueEffectSpatialResolver.CollisionPolicy.Relaxed;
        }

        private DialogueEffectSpatialResolver.ResolveResult ResolveSpatialPlacementForPower(
            string label,
            DialogueRequest request,
            Vector3 desiredPosition,
            Vector3 desiredForward,
            Vector3 fallbackOrigin,
            Vector3 fallbackForward,
            float scale,
            float damageRadius,
            EffectSpatialPolicy policy,
            bool enableGameplayDamage,
            GameObject targetObject
        )
        {
            Vector3 normalizedForward =
                desiredForward.sqrMagnitude > 0.0001f
                    ? desiredForward.normalized
                    : (
                        fallbackForward.sqrMagnitude > 0.0001f
                            ? fallbackForward.normalized
                            : Vector3.forward
                    );

            if (!m_EnableSpatialEffectResolver)
            {
                return new DialogueEffectSpatialResolver.ResolveResult
                {
                    IsValid = true,
                    Position = desiredPosition,
                    Forward = normalizedForward,
                    Reason = "resolver_disabled",
                };
            }

            GameObject speakerObject = ResolveSpawnedObject(request.SpeakerNetworkId);
            GameObject listenerObject = ResolvePreferredListenerTargetObject(request);

            if (policy.PreferTargetOrigin && targetObject != null)
            {
                desiredPosition =
                    ResolveEffectOrigin(targetObject) + Vector3.up * policy.TargetHeightOffset;
                normalizedForward = ResolveEffectForward(speakerObject, targetObject);
            }

            Vector3 lineOfSightOrigin =
                speakerObject != null ? ResolveEffectOrigin(speakerObject) : fallbackOrigin;

            float clearance = Mathf.Clamp(
                Mathf.Max(
                    m_EffectCollisionClearanceRadius,
                    scale * Mathf.Max(0.05f, policy.ClearanceScale),
                    damageRadius * Mathf.Max(0.05f, policy.ClearanceScale)
                ),
                0.05f,
                4f
            );

            var resolveRequest = new DialogueEffectSpatialResolver.ResolveRequest
            {
                DesiredPosition = desiredPosition,
                DesiredForward = normalizedForward,
                FallbackOrigin = fallbackOrigin,
                FallbackForward = fallbackForward,
                FallbackForwardDistance = Mathf.Max(0.25f, policy.FallbackDistance),
                UseFallback = policy.UseFallback,
                GroundSnap = policy.GroundSnap,
                GroundProbeUp = Mathf.Max(0.05f, m_EffectGroundProbeUp),
                GroundProbeDown = Mathf.Max(0.05f, m_EffectGroundProbeDown),
                GroundOffset = Mathf.Max(0f, m_EffectGroundOffset),
                GroundMask = m_EffectGroundMask,
                ClearanceRadius = clearance,
                CollisionMask = m_EffectCollisionMask,
                CollisionPolicy = ResolveCollisionPolicy(
                    policy.CollisionPolicyHint,
                    enableGameplayDamage
                ),
                RequireLineOfSight = policy.RequireLineOfSight,
                LineOfSightOrigin = lineOfSightOrigin,
                LineOfSightMask = m_EffectLineOfSightMask,
                IgnoreRootA = speakerObject != null ? speakerObject.transform.root : null,
                IgnoreRootB = listenerObject != null ? listenerObject.transform.root : null,
                IgnoreRootC = targetObject != null ? targetObject.transform.root : null,
            };

            DialogueEffectSpatialResolver.ResolveResult resolved =
                DialogueEffectSpatialResolver.Resolve(resolveRequest);
            if (
                m_LogDebug
                && (resolved.CollisionAdjusted || resolved.UsedFallback || !resolved.IsValid)
            )
            {
                NGLog.Debug(
                    "DialogueFX",
                    NGLog.Format(
                        "Spatial resolve",
                        ("label", label ?? string.Empty),
                        ("valid", resolved.IsValid),
                        ("reason", resolved.Reason ?? string.Empty),
                        ("fallback", resolved.UsedFallback),
                        ("collisionAdjusted", resolved.CollisionAdjusted),
                        ("groundSnapped", resolved.GroundSnapped),
                        ("x", resolved.Position.x),
                        ("y", resolved.Position.y),
                        ("z", resolved.Position.z)
                    )
                );
            }

            return resolved;
        }

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

        private static bool IsPlayerTargetToken(string lower)
        {
            if (string.IsNullOrWhiteSpace(lower))
            {
                return false;
            }

            return lower
                    is "player"
                        or "listener"
                        or "me"
                        or "myself"
                        or "target"
                        or "hero"
                        or "user"
                || lower.Equals("role:player", StringComparison.Ordinal)
                || lower.Equals("semantic:player", StringComparison.Ordinal)
                || lower.StartsWith("id:player", StringComparison.Ordinal)
                || lower.StartsWith("semantic:player:", StringComparison.Ordinal)
                || lower.StartsWith("player:", StringComparison.Ordinal);
        }

        private static bool LooksLikeExplicitPlayerTargetToken(string lower)
        {
            if (string.IsNullOrWhiteSpace(lower))
            {
                return false;
            }

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
                    or "host"
                    or "hostplayer"
                    or "server"
                    or "serverplayer"
                || lower is "player:requester"
                    or "player:self"
                    or "player:me"
                    or "player:host"
                    or "player:server"
            )
            {
                return true;
            }

            return TryParseOrderedPlayerTargetToken(lower, out _)
                || TryParseClientPlayerTargetToken(lower, out _);
        }

        private static bool TryParseOrderedPlayerTargetToken(string lower, out int orderedIndex)
        {
            orderedIndex = 0;
            if (string.IsNullOrWhiteSpace(lower))
            {
                return false;
            }

            if (
                lower.Length > 1
                && lower[0] == 'p'
                && int.TryParse(lower.Substring(1), out orderedIndex)
                && orderedIndex > 0
            )
            {
                return true;
            }

            if (
                lower.StartsWith("player", StringComparison.Ordinal)
                && lower.Length > "player".Length
                && int.TryParse(lower.Substring("player".Length), out orderedIndex)
                && orderedIndex > 0
            )
            {
                return true;
            }

            string[] prefixes = { "player:", "role:player:", "semantic:player:" };
            for (int i = 0; i < prefixes.Length; i++)
            {
                string prefix = prefixes[i];
                if (
                    lower.StartsWith(prefix, StringComparison.Ordinal)
                    && int.TryParse(lower.Substring(prefix.Length), out orderedIndex)
                    && orderedIndex > 0
                )
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryParseClientPlayerTargetToken(string lower, out ulong clientId)
        {
            clientId = 0;
            if (string.IsNullOrWhiteSpace(lower))
            {
                return false;
            }

            string[] prefixes = { "client:", "player:client:" };
            for (int i = 0; i < prefixes.Length; i++)
            {
                string prefix = prefixes[i];
                if (
                    lower.StartsWith(prefix, StringComparison.Ordinal)
                    && ulong.TryParse(lower.Substring(prefix.Length), out clientId)
                )
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsGroundAlias(string lower)
        {
            if (string.IsNullOrWhiteSpace(lower))
            {
                return false;
            }

            return lower
                    is "ground"
                        or "floor"
                        or "terrain"
                        or "grounded"
                        or "fllor"
                        or "flor"
                        or "grond"
                || lower.EndsWith(" ground", StringComparison.Ordinal)
                || lower.EndsWith(" floor", StringComparison.Ordinal)
                || lower.EndsWith(" terrain", StringComparison.Ordinal)
                || lower.Contains("on ground", StringComparison.Ordinal)
                || lower.Contains("at ground", StringComparison.Ordinal)
                || lower.Contains("on floor", StringComparison.Ordinal)
                || lower.Contains("on fllor", StringComparison.Ordinal)
                || lower.Contains("at fllor", StringComparison.Ordinal)
                || lower.Contains("at feet", StringComparison.Ordinal)
                || lower.Contains("under feet", StringComparison.Ordinal);
        }

        private static bool IsPlayerHeadAlias(string lower)
        {
            if (string.IsNullOrWhiteSpace(lower))
            {
                return false;
            }

            return lower is "head" or "hair" or "face"
                || lower.EndsWith(" head", StringComparison.Ordinal)
                || lower.EndsWith(" hair", StringComparison.Ordinal)
                || lower.EndsWith(" face", StringComparison.Ordinal)
                || lower.Contains("player head", StringComparison.Ordinal)
                || lower.Contains("on head", StringComparison.Ordinal)
                || lower.Contains("at head", StringComparison.Ordinal)
                || lower.Contains("player hair", StringComparison.Ordinal);
        }

        private static bool IsPlayerFeetAlias(string lower)
        {
            if (string.IsNullOrWhiteSpace(lower))
            {
                return false;
            }

            return lower is "feet" or "foot" or "toes" or "legs" or "atfeet"
                || lower.EndsWith(" feet", StringComparison.Ordinal)
                || lower.EndsWith(" foot", StringComparison.Ordinal)
                || lower.EndsWith(" toes", StringComparison.Ordinal)
                || lower.EndsWith(" legs", StringComparison.Ordinal)
                || lower.Contains("player feet", StringComparison.Ordinal)
                || lower.Contains("at feet", StringComparison.Ordinal)
                || lower.Contains("on feet", StringComparison.Ordinal)
                || lower.Contains("under player", StringComparison.Ordinal);
        }

        private static bool TryGetObjectBounds(GameObject obj, out Bounds bounds)
        {
            bounds = default;
            if (obj == null)
            {
                return false;
            }

            bool hasBounds = false;
            Renderer[] renderers = obj.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            if (!hasBounds)
            {
                Collider[] colliders = obj.GetComponentsInChildren<Collider>(true);
                for (int i = 0; i < colliders.Length; i++)
                {
                    Collider collider = colliders[i];
                    if (collider == null)
                    {
                        continue;
                    }

                    if (!hasBounds)
                    {
                        bounds = collider.bounds;
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(collider.bounds);
                    }
                }
            }

            return hasBounds;
        }

        private static void ResolveEffectSpatialContext(
            string effectContext,
            GameObject speakerObject,
            GameObject listenerObject,
            out Vector3 origin,
            out Vector3 forward,
            out string anchorLabel
        )
        {
            origin = ResolveEffectOrigin(speakerObject);
            forward = ResolveEffectForward(speakerObject, listenerObject);
            anchorLabel = "npc";

            if (TryResolveObjectAnchor(effectContext, out GameObject objectAnchor))
            {
                Vector3 objectOrigin = ResolveEffectOrigin(objectAnchor);
                if (speakerObject != null)
                {
                    origin = ResolveEffectOrigin(speakerObject);
                    Vector3 toObject = objectOrigin - origin;
                    toObject.y = 0f;
                    if (toObject.sqrMagnitude > 0.0001f)
                    {
                        forward = toObject.normalized;
                    }
                    else
                    {
                        forward = ResolveEffectForward(speakerObject, objectAnchor);
                    }
                }
                else
                {
                    origin = objectOrigin - objectAnchor.transform.forward * 0.5f;
                    forward =
                        objectAnchor.transform.forward.sqrMagnitude > 0.0001f
                            ? objectAnchor.transform.forward.normalized
                            : Vector3.forward;
                }

                anchorLabel = $"object:{objectAnchor.name}";
                return;
            }

            if (WantsPlayerAnchor(effectContext) && listenerObject != null)
            {
                origin = ResolveEffectOrigin(listenerObject);
                forward = ResolveEffectForward(speakerObject, listenerObject);
                anchorLabel = "player";
            }
        }

        private static Vector3 ResolveEffectOrigin(GameObject speakerObject)
        {
            if (speakerObject == null)
            {
                return Vector3.zero;
            }

            Vector3 origin = speakerObject.transform.position;
            Collider collider = speakerObject.GetComponentInChildren<Collider>();
            if (collider != null)
            {
                origin.y = collider.bounds.center.y;
            }

            return origin;
        }

        private static Vector3 ResolveEffectForward(GameObject speakerObject, GameObject listenerObject)
        {
            if (speakerObject == null)
            {
                return Vector3.forward;
            }

            if (listenerObject != null)
            {
                Vector3 toListener = listenerObject.transform.position - speakerObject.transform.position;
                toListener.y = 0f;
                if (toListener.sqrMagnitude > 0.0001f)
                {
                    return toListener.normalized;
                }
            }

            Vector3 forward = speakerObject.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = Vector3.forward;
            }

            return forward.normalized;
        }

        private static bool WantsPlayerAnchor(string effectContext)
        {
            if (string.IsNullOrWhiteSpace(effectContext))
            {
                return false;
            }

            string lower = effectContext.ToLowerInvariant();
            return lower.Contains("at player", StringComparison.Ordinal)
                || lower.Contains("on player", StringComparison.Ordinal)
                || lower.Contains("around player", StringComparison.Ordinal)
                || lower.Contains("player position", StringComparison.Ordinal)
                || lower.Contains("at me", StringComparison.Ordinal)
                || lower.Contains("on me", StringComparison.Ordinal)
                || lower.Contains("around me", StringComparison.Ordinal)
                || lower.Contains("my position", StringComparison.Ordinal);
        }

        private static bool TryResolveObjectAnchor(string effectContext, out GameObject targetObject)
        {
            targetObject = null;
            if (!TryExtractObjectAnchorName(effectContext, out string objectName))
            {
                return false;
            }

            targetObject = FindSceneObjectByName(objectName);
            return targetObject != null;
        }

        private static bool TryExtractObjectAnchorName(
            string effectContext,
            out string objectAnchorName
        )
        {
            objectAnchorName = string.Empty;
            if (string.IsNullOrWhiteSpace(effectContext))
            {
                return false;
            }

            string lower = effectContext.ToLowerInvariant();
            string[] markers =
            {
                "at object ",
                "on object ",
                "near object ",
                "around object ",
                "at the object ",
                "on the object ",
                "near the object ",
                "around the object ",
                "at wall ",
                "on wall ",
                "near wall ",
                "around wall ",
                "at the wall ",
                "on the wall ",
                "near the wall ",
                "around the wall ",
            };

            for (int i = 0; i < markers.Length; i++)
            {
                string marker = markers[i];
                int markerIndex = lower.IndexOf(marker, StringComparison.Ordinal);
                if (markerIndex < 0)
                {
                    continue;
                }

                int start = markerIndex + marker.Length;
                if (start >= effectContext.Length)
                {
                    continue;
                }

                int end = effectContext.Length;
                for (int c = start; c < effectContext.Length; c++)
                {
                    char ch = effectContext[c];
                    if (
                        ch == '\n'
                        || ch == '\r'
                        || ch == ','
                        || ch == '.'
                        || ch == ';'
                        || ch == '!'
                        || ch == '?'
                    )
                    {
                        end = c;
                        break;
                    }
                }

                string candidate = effectContext.Substring(start, end - start).Trim();
                int connector = candidate.IndexOf(" for ", StringComparison.OrdinalIgnoreCase);
                if (connector >= 0)
                {
                    candidate = candidate.Substring(0, connector).Trim();
                }

                connector = candidate.IndexOf(" with ", StringComparison.OrdinalIgnoreCase);
                if (connector >= 0)
                {
                    candidate = candidate.Substring(0, connector).Trim();
                }

                connector = candidate.IndexOf(" and ", StringComparison.OrdinalIgnoreCase);
                if (connector >= 0)
                {
                    candidate = candidate.Substring(0, connector).Trim();
                }

                candidate = candidate.Trim('"', '\'');
                if (candidate.StartsWith("the ", StringComparison.OrdinalIgnoreCase))
                {
                    candidate = candidate.Substring(4).Trim();
                }

                if (!string.IsNullOrWhiteSpace(candidate) && candidate.Length >= 2)
                {
                    objectAnchorName = candidate;
                    return true;
                }
            }

            return false;
        }

        private static GameObject FindSceneObjectByName(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return null;
            }

            string trimmed = objectName.Trim();
            if (trimmed.Length < 2)
            {
                return null;
            }

            if (DialogueSceneTargetRegistry.TryResolveSceneObject(trimmed, out GameObject cachedTarget))
            {
                return cachedTarget;
            }

            if (TryFindSceneObjectBySemanticTag(trimmed, out GameObject semanticMatch))
            {
                return semanticMatch;
            }

            GameObject exact = GameObject.Find(trimmed);
            if (exact != null)
            {
                return exact;
            }

#if UNITY_2023_1_OR_NEWER
            Transform[] transforms = UnityEngine.Object.FindObjectsByType<Transform>(
                findObjectsInactive: FindObjectsInactive.Exclude
            );
#else
            Transform[] transforms = UnityEngine.Object.FindObjectsOfType<Transform>();
#endif

            GameObject partialMatch = null;
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate == null || candidate.gameObject == null)
                {
                    continue;
                }

                string candidateName = candidate.name ?? string.Empty;
                if (candidateName.Length == 0)
                {
                    continue;
                }

                if (string.Equals(candidateName, trimmed, StringComparison.OrdinalIgnoreCase))
                {
                    return candidate.gameObject;
                }

                if (
                    partialMatch == null
                    && candidateName.IndexOf(trimmed, StringComparison.OrdinalIgnoreCase) >= 0
                )
                {
                    partialMatch = candidate.gameObject;
                }
            }

            return partialMatch;
        }

        private static bool TryFindSceneObjectBySemanticTag(string query, out GameObject target)
        {
            target = null;
            if (string.IsNullOrWhiteSpace(query))
            {
                return false;
            }

            if (DialogueSceneTargetRegistry.TryResolveSceneObject(query, out target))
            {
                return target != null;
            }

#if UNITY_2023_1_OR_NEWER
            DialogueSemanticTag[] tags = UnityEngine.Object.FindObjectsByType<DialogueSemanticTag>(
                findObjectsInactive: FindObjectsInactive.Exclude
            );
#else
            DialogueSemanticTag[] tags = UnityEngine.Object.FindObjectsOfType<DialogueSemanticTag>();
#endif
            if (tags == null || tags.Length == 0)
            {
                return false;
            }

            string raw = query.Trim().Trim('"', '\'');
            if (raw.Length == 0)
            {
                return false;
            }

            string lower = raw.ToLowerInvariant();
            bool useRoleFilter = lower.StartsWith("role:", StringComparison.Ordinal);
            bool useIdFilter =
                lower.StartsWith("id:", StringComparison.Ordinal)
                || lower.StartsWith("semantic:", StringComparison.Ordinal);
            string filterValue = raw;
            if (useRoleFilter)
            {
                filterValue = raw.Substring("role:".Length).Trim();
            }
            else if (lower.StartsWith("id:", StringComparison.Ordinal))
            {
                filterValue = raw.Substring("id:".Length).Trim();
            }
            else if (lower.StartsWith("semantic:", StringComparison.Ordinal))
            {
                filterValue = raw.Substring("semantic:".Length).Trim();
            }

            if (filterValue.Length == 0)
            {
                return false;
            }

            string filterLower = filterValue.ToLowerInvariant();
            string filterNorm = NormalizeSemanticToken(filterLower);

            int bestScore = int.MinValue;
            DialogueSemanticTag bestTag = null;
            for (int i = 0; i < tags.Length; i++)
            {
                DialogueSemanticTag tag = tags[i];
                if (tag == null || tag.gameObject == null)
                {
                    continue;
                }

                int score = ScoreSemanticTagMatch(
                    tag,
                    raw,
                    lower,
                    filterLower,
                    filterNorm,
                    useRoleFilter,
                    useIdFilter
                );
                if (score > bestScore)
                {
                    bestScore = score;
                    bestTag = tag;
                }
            }

            if (bestTag == null || bestScore <= 0)
            {
                return false;
            }

            target = bestTag.gameObject;
            return true;
        }

        private static int ScoreSemanticTagMatch(
            DialogueSemanticTag tag,
            string rawQuery,
            string lowerQuery,
            string filterLower,
            string filterNorm,
            bool useRoleFilter,
            bool useIdFilter
        )
        {
            string semanticId = tag.SemanticId ?? string.Empty;
            string semanticIdLower = semanticId.ToLowerInvariant();
            string semanticIdNorm = NormalizeSemanticToken(semanticIdLower);
            string display = tag.ResolveDisplayName(tag.gameObject) ?? string.Empty;
            string displayLower = display.ToLowerInvariant();
            string displayNorm = NormalizeSemanticToken(displayLower);
            string role = tag.RoleKey ?? string.Empty;
            string roleNorm = NormalizeSemanticToken(role);

            if (useRoleFilter && role != filterLower && roleNorm != filterNorm)
            {
                return 0;
            }

            if (useIdFilter && semanticIdLower != filterLower && semanticIdNorm != filterNorm)
            {
                return 0;
            }

            int score = 0;
            if (semanticIdLower == filterLower)
            {
                score = Mathf.Max(score, 320);
            }
            else if (semanticIdNorm.Length > 0 && semanticIdNorm == filterNorm)
            {
                score = Mathf.Max(score, 300);
            }

            if (displayLower == lowerQuery || displayLower == filterLower)
            {
                score = Mathf.Max(score, 260);
            }
            else if (
                displayNorm.Length > 0
                && (displayNorm == NormalizeSemanticToken(lowerQuery) || displayNorm == filterNorm)
            )
            {
                score = Mathf.Max(score, 235);
            }
            else if (displayLower.IndexOf(filterLower, StringComparison.Ordinal) >= 0)
            {
                score = Mathf.Max(score, 150);
            }

            if (role == filterLower || roleNorm == filterNorm)
            {
                score = Mathf.Max(score, 180);
            }

            string[] aliases = tag.Aliases;
            if (aliases != null)
            {
                for (int i = 0; i < aliases.Length; i++)
                {
                    string alias = aliases[i];
                    if (string.IsNullOrWhiteSpace(alias))
                    {
                        continue;
                    }

                    string aliasLower = alias.Trim().ToLowerInvariant();
                    string aliasNorm = NormalizeSemanticToken(aliasLower);
                    if (aliasLower == lowerQuery || aliasLower == filterLower)
                    {
                        score = Mathf.Max(score, 240);
                        break;
                    }

                    if (
                        aliasNorm.Length > 0
                        && (
                            aliasNorm == NormalizeSemanticToken(lowerQuery)
                            || aliasNorm == filterNorm
                        )
                    )
                    {
                        score = Mathf.Max(score, 220);
                        break;
                    }
                }
            }

            return score;
        }

        private static string NormalizeSemanticToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(token.Length);
            for (int i = 0; i < token.Length; i++)
            {
                char c = token[i];
                if (char.IsLetterOrDigit(c))
                {
                    builder.Append(char.ToLowerInvariant(c));
                }
            }

            return builder.ToString();
        }

        private static Vector3 ResolveGroundPlacementNearReference(
            GameObject groundObject,
            Vector3 referencePosition
        )
        {
            if (groundObject == null)
            {
                return referencePosition;
            }

            if (TryGetObjectBounds(groundObject, out Bounds bounds))
            {
                return new Vector3(
                    Mathf.Clamp(referencePosition.x, bounds.min.x, bounds.max.x),
                    bounds.max.y + 0.03f,
                    Mathf.Clamp(referencePosition.z, bounds.min.z, bounds.max.z)
                );
            }

            Vector3 origin = ResolveEffectOrigin(groundObject);
            origin.y += 0.03f;
            return origin;
        }
    }
}
