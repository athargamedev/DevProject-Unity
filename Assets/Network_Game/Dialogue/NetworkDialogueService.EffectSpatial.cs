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
        // Spatial policy inference and placement resolution.
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

    }
}
