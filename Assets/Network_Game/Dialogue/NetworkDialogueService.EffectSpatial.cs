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

    }
}
