using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Network_Game.Diagnostics;
using Network_Game.Dialogue.Effects;
using Unity.Netcode;
using UnityEngine;

namespace Network_Game.Dialogue
{
    public partial class NetworkDialogueService
    {
private void ApplyContextEffects(DialogueRequest request, string responseText)
{
    if (!m_EnableContextSceneEffects)
    {
        NGLog.Info("DialogueFX", "Skip effects (disabled in NetworkDialogueService).");
        return;
    }

    if (string.IsNullOrWhiteSpace(responseText))
    {
        NGLog.Info("DialogueFX", "Skip effects (empty response text).");
        return;
    }


    bool isGameplayProbe = IsGameplayProbeRequest(request, request.Prompt);
    NpcDialogueActor actor = ResolveDialogueActorForRequest(
        request,
        out ulong resolvedSpeakerNetworkId,
        out ulong resolvedListenerNetworkId,
        out bool usedListenerFallback
    );

    DialogueRequest normalizedRequest = request;
    if (usedListenerFallback)
    {
        normalizedRequest.SpeakerNetworkId = resolvedSpeakerNetworkId;
        normalizedRequest.ListenerNetworkId = resolvedListenerNetworkId;
    }

    NpcDialogueProfile profile = actor != null ? actor.Profile : null;
    bool missingActorProfile = actor == null || profile == null;
    if (missingActorProfile && !isGameplayProbe)
    {
        NGLog.Warn(
            "DialogueFX",
            NGLog.Format(
                "Skip effects (actor missing)",
                ("speaker", request.SpeakerNetworkId),
                ("listener", request.ListenerNetworkId),
                ("resolvedSpeaker", resolvedSpeakerNetworkId),
                ("resolvedListener", resolvedListenerNetworkId)
            )
        );
        return;
    }

    if (missingActorProfile && isGameplayProbe)
    {
        NGLog.Warn(
            "DialogueFX",
            NGLog.Format(
                "Probe mode fallback (actor/profile missing); continuing with parser-only dispatch",
                ("speaker", request.SpeakerNetworkId),
                ("listener", request.ListenerNetworkId),
                ("resolvedSpeaker", resolvedSpeakerNetworkId),
                ("resolvedListener", resolvedListenerNetworkId)
            )
        );
    }

    string effectContext = BuildEffectContextText(request.Prompt, responseText);

    ParticleParameterExtractor.ParticleParameterIntent parameterIntent =
        profile != null && profile.EnableDynamicEffectParameters
            ? ParticleParameterExtractor.Extract(effectContext)
            : ParticleParameterExtractor.ParticleParameterIntent.Default;

    List<EffectIntent> catalogIntents = EffectParser.ExtractIntents(
        responseText,
        actor,
        stripTags: false
    );

    bool hasCatalogIntents = catalogIntents != null && catalogIntents.Count > 0;
    if (isGameplayProbe)
    {
        AdjustIntentsForProbeMode(
            request,
            ref catalogIntents,
            ref hasCatalogIntents
        );
    }

    PlayerSpecialEffectMode specialEffectMode = ResolvePlayerSpecialEffectMode(
        promptText: request.Prompt,
        responseText: responseText,
        intents: catalogIntents
    );

    PlayerIdentityBinding targetPlayerIdentity = ResolvePlayerIdentityForRequest(
        normalizedRequest
    );
    PlayerEffectModifier playerMod = BuildPlayerEffectModifier(targetPlayerIdentity);

    bool hasPlayerSpecialEffect = specialEffectMode != PlayerSpecialEffectMode.None;

    if (!hasCatalogIntents && !hasPlayerSpecialEffect)
    {
        if (m_LogDebug)
        {
            NGLog.Debug(
                "DialogueFX",
                NGLog.Format(
                    "No effects matched",
                    ("speaker", normalizedRequest.SpeakerNetworkId),
                    (
                        "promptSnippet",
                        request.Prompt?.Length > 60
                            ? request.Prompt.Substring(0, 60) + "..."
                            : request.Prompt ?? string.Empty
                    )
                )
            );
        }
        return;
    }

    if (hasPlayerSpecialEffect)
    {
        bool specialApplied = ApplyPlayerSpecialEffects(
            normalizedRequest,
            parameterIntent,
            specialEffectMode
        );
        if (
            specialApplied
            && (
                specialEffectMode == PlayerSpecialEffectMode.Dissolve
                || specialEffectMode == PlayerSpecialEffectMode.FloorDissolve
            )
        )
        {
            hasCatalogIntents = false;
            if (m_LogDebug)
            {
                NGLog.Debug(
                    "DialogueFX",
                    "Suppressed prefab/catalog effects due to dissolve priority."
                );
            }
        }
    }

    GameObject speakerObject = ResolveSpawnedObject(normalizedRequest.SpeakerNetworkId);
    GameObject listenerObject = ResolveSpawnedObject(normalizedRequest.ListenerNetworkId);
    ResolveEffectSpatialContext(
        effectContext,
        speakerObject,
        listenerObject,
        out Vector3 effectOrigin,
        out Vector3 effectForward,
        out string effectAnchorLabel
    );

    EnsureSceneEffectsController();
    if (m_SceneEffectsController == null)
    {
        NGLog.Warn(
            "DialogueFX",
            NGLog.Format(
                "Skip effects (scene effects controller missing)",
                ("speaker", normalizedRequest.SpeakerNetworkId),
                ("listener", normalizedRequest.ListenerNetworkId)
            )
        );
        return;
    }

    if (hasCatalogIntents)
    {
        ApplyEffectParserIntents(
            catalogIntents,
            normalizedRequest,
            effectOrigin,
            effectForward,
            playerModOverride: playerMod,
            probeTrace: isGameplayProbe
        );
    }
}

private void ApplyEffectParserIntents(
    List<EffectIntent> intents,
    DialogueRequest request,
    Vector3 effectOrigin,
    Vector3 effectForward,
    PlayerEffectModifier? playerModOverride = null,
    bool probeTrace = false
)
{
    PlayerEffectModifier playerMod = playerModOverride ?? PlayerEffectModifier.Neutral;
    for (int i = 0; i < intents.Count; i++)
    {
        EffectIntent intent = intents[index: i];
        if (probeTrace)
        {
            NGLog.Info(
                category: "DialogueFX",
                message: NGLog.Format(
                    message: "Probe intent evaluate",
                    ("requestId", request.ClientRequestId),
                    ("index", i),
                    ("tag", intent.rawTagName ?? string.Empty),
                    ("valid", intent.isValid),
                    (
                        "definition",
                        intent.definition != null ? intent.definition.effectTag : "<none>"
                    ),
                    ("target", intent.target ?? string.Empty),
                    ("placement", intent.placementType ?? string.Empty)
                )
            );
        }

        if (!intent.isValid)
        {
            // Tag not in catalog â€” skip. Register profile powers as EffectDefinitions in the catalog.
            if (m_LogDebug)
            {
                NGLog.Debug(
                    "DialogueFX",
                    $"[EffectParser] Skipping unresolved intent '{intent.rawTagName}'."
                );
            }
            continue;
        }

        EffectDefinition def = intent.definition;

        string prefabName =
            def.effectPrefab != null ? def.effectPrefab.name : def.effectTag;
        float intensity = Mathf.Clamp(
            intent.intensity > 0f ? intent.intensity : 1f,
            0.1f,
            4f
        );
        float intensityScale = Mathf.Clamp(Mathf.Pow(intensity, 0.5f), 0.5f, 2.25f);
        float scale = Mathf.Clamp(intent.GetEffectiveScale() * intensityScale, 0.1f, 50f);
        float duration = Mathf.Clamp(intent.GetEffectiveDuration(), 0.1f, 45f);
        Color color = intent.GetEffectiveColor();
        bool useColorOverride = color != Color.white && color != default;
        ulong targetNetworkObjectId = ResolvePreferredListenerTargetNetworkObjectId(
            request
        );
        float projectileSpeed = Mathf.Clamp(
            Mathf.Max(0.1f, intent.GetEffectiveSpeed()),
            0.1f,
            120f
        );
        float damageRadius = Mathf.Clamp(
            Mathf.Max(0.1f, intent.GetEffectiveRadius()),
            0.1f,
            40f
        );
        float damageAmount = Mathf.Clamp(
            Mathf.Max(0f, def.damageAmount) * intensity,
            0f,
            400f
        );

        // Apply LLM-supplied emotion multiplier to damage and scale
        if (!string.IsNullOrWhiteSpace(intent.emotion))
        {
            float emotionMul = ParticleParameterExtractor.EmotionKeywordToMultiplier(
                intent.emotion
            );
            if (!Mathf.Approximately(emotionMul, 1f))
            {
                scale = Mathf.Clamp(scale * Mathf.Max(1f, emotionMul), 0.1f, 50f);
                damageAmount = Mathf.Clamp(
                    damageAmount * Mathf.Max(1f, emotionMul),
                    0f,
                    400f
                );
            }
        }

        // Apply LLM-supplied damage multiplier when explicitly set
        if (intent.damage > 0f)
        {
            damageAmount = Mathf.Clamp(damageAmount * intent.damage, 0f, 400f);
        }

        string requestedTarget = !string.IsNullOrWhiteSpace(intent.anchor)
            ? intent.anchor
            : intent.target;
        requestedTarget = ResolveTargetHintForDefinition(requestedTarget, def);
        string resolvedPlacementHint = ResolvePlacementHintForDefinition(
            intent.placementType,
            def
        );

        // Apply player-specific modifiers from customization (server-authoritative)
        scale = Mathf.Clamp(scale * playerMod.EffectSizeScale, 0.1f, 50f);
        duration = Mathf.Clamp(duration * playerMod.EffectDurationScale, 0.1f, 45f);
        damageAmount = Mathf.Clamp(
            damageAmount * playerMod.DamageScaleReceived * playerMod.AggressionBias,
            0f,
            400f
        );
        if (playerMod.IsShielded)
        {
            damageAmount = 0f;
        }
        if (!useColorOverride && playerMod.PreferredColor.HasValue)
        {
            color = playerMod.PreferredColor.Value;
            useColorOverride = true;
        }

        float requestedScale = scale;
        float requestedDuration = duration;
        float requestedDamageRadius = damageRadius;
        float requestedDamageAmount = damageAmount;
        scale = Mathf.Clamp(scale, def.minScale, def.maxScale);
        duration = Mathf.Clamp(duration, def.minDuration, def.maxDuration);
        damageRadius = Mathf.Clamp(damageRadius, def.minRadius, def.maxRadius);
        damageAmount = Mathf.Clamp(damageAmount, 0f, 400f);

        Vector3 spawnForward = effectForward;
        Vector3 spawnPos = effectOrigin + effectForward * 1.5f;
        GameObject resolvedTargetObject = ResolvePreferredListenerTargetObject(request);
        if (
            TryResolveEffectIntentTarget(
                requestedTarget,
                request,
                effectOrigin,
                effectForward,
                out ulong resolvedTargetNetworkObjectId,
                out Vector3 resolvedSpawnPos,
                out Vector3 resolvedSpawnForward,
                out GameObject resolvedTargetGameObject
            )
        )
        {
            targetNetworkObjectId = resolvedTargetNetworkObjectId;
            spawnPos = resolvedSpawnPos;
            spawnForward = resolvedSpawnForward;
            resolvedTargetObject = resolvedTargetGameObject;
        }

        NGLog.Debug(
            "DialogueFX",
            $"Effect spawn decision | tag={intent.rawTagName}, spawnPos=({spawnPos.x:F2},{spawnPos.y:F2},{spawnPos.z:F2}), targetObj={resolvedTargetObject?.name ?? "null"}"
        );

        // DEBUG: Log all effect parameters
        Effects.EffectVfxDebugger.LogParticleState(
            def.effectPrefab?.GetComponentInChildren<ParticleSystem>(),
            $"BEFORE_SPAWN_{intent.rawTagName}"
        );

        string actionId = BuildActionId(
            request,
            0,
            "prefab_power",
            prefabName,
            targetNetworkObjectId
        );

        EffectSpatialType intentSpatialType = ResolveEffectSpatialType(
            resolvedPlacementHint,
            intent.rawTagName,
            requestedTarget,
            def.enableHoming,
            projectileSpeed,
            damageRadius,
            scale,
            def.affectPlayerOnly
        );
        string collisionPolicyHint = intent.collisionPolicy;
        bool? groundSnapHint = intent.groundSnap;
        bool? requireLineOfSightHint = intent.requireLineOfSight;
        if (probeTrace)
        {
            // Probe runs should validate effect spawning capability even when strict LoS
            // would reject a placement in crowded scenes.
            collisionPolicyHint = "relaxed";
            requireLineOfSightHint = false;
        }

        EffectSpatialPolicy intentSpatialPolicy = BuildEffectSpatialPolicy(
            intentSpatialType,
            def.enableGameplayDamage,
            collisionPolicyHint,
            groundSnapHint,
            requireLineOfSightHint
        );
        DialogueEffectSpatialResolver.ResolveResult spatial =
            ResolveSpatialPlacementForPower(
                intent.rawTagName,
                request,
                spawnPos,
                spawnForward,
                effectOrigin,
                effectForward,
                scale,
                damageRadius,
                intentSpatialPolicy,
                enableGameplayDamage: def.enableGameplayDamage,
                targetObject: resolvedTargetObject
            );
        if (!spatial.IsValid)
        {
            RecordActionValidationResult(
                request,
                0,
                actionId,
                actionKind: "prefab_power",
                actionName: prefabName,
                decision: "rejected",
                success: false,
                reason: "spatial_invalid",
                requestedTargetHint: requestedTarget,
                resolvedTargetNetworkObjectId: targetNetworkObjectId,
                requestedPlacementHint: resolvedPlacementHint,
                resolvedSpatialType: intentSpatialType.ToString(),
                spatialReason: spatial.Reason ?? "invalid",
                requestedScale: requestedScale,
                appliedScale: scale,
                requestedDuration: requestedDuration,
                appliedDuration: duration,
                requestedDamageRadius: requestedDamageRadius,
                appliedDamageRadius: damageRadius,
                requestedDamageAmount: requestedDamageAmount,
                appliedDamageAmount: damageAmount
            );
            NGLog.Warn(
                "DialogueFX",
                NGLog.Format(
                    "EffectParser intent skipped (spatial invalid)",
                    ("tag", intent.rawTagName),
                    ("reason", spatial.Reason ?? "invalid")
                )
            );
            if (probeTrace)
            {
                NGLog.Warn(
                    "DialogueFX",
                    NGLog.Format(
                        "Probe catalog intent skipped",
                        ("requestId", request.ClientRequestId),
                        ("tag", intent.rawTagName ?? string.Empty),
                        ("reason", spatial.Reason ?? "invalid")
                    )
                );
            }
            continue;
        }

        bool clamped =
            !Mathf.Approximately(requestedScale, scale)
            || !Mathf.Approximately(requestedDuration, duration)
            || !Mathf.Approximately(requestedDamageRadius, damageRadius)
            || !Mathf.Approximately(requestedDamageAmount, damageAmount);
        RecordActionValidationResult(
            request,
            0,
            actionId,
            actionKind: "prefab_power",
            actionName: prefabName,
            decision: clamped ? "validated_clamped" : "validated",
            success: true,
            requestedTargetHint: requestedTarget,
            resolvedTargetNetworkObjectId: targetNetworkObjectId,
            requestedPlacementHint: resolvedPlacementHint,
            resolvedSpatialType: intentSpatialType.ToString(),
            spatialReason: spatial.Reason ?? "ok",
            requestedScale: requestedScale,
            appliedScale: scale,
            requestedDuration: requestedDuration,
            appliedDuration: duration,
            requestedDamageRadius: requestedDamageRadius,
            appliedDamageRadius: damageRadius,
            requestedDamageAmount: requestedDamageAmount,
            appliedDamageAmount: damageAmount
        );

        LogEffectTargetResolution(
            "catalog_intent",
            intent.rawTagName,
            requestedTarget,
            targetNetworkObjectId,
            resolvedTargetObject,
            request.SpeakerNetworkId
        );

        RecordExecutionTrace(
            stage: "effect_dispatch",
            success: true,
            request,
            0,
            actionId: actionId,
            stageDetail: "catalog_intent",
            effectType: "prefab_power",
            effectName: prefabName,
            sourceNetworkObjectId: request.SpeakerNetworkId,
            targetNetworkObjectId: targetNetworkObjectId,
            responsePreview: intent.rawTagName
        );
        RecordReplicationTrace(
            stage: "rpc_sent",
            networkPath: "client_rpc",
            success: true,
            request,
            0,
            actionId: actionId,
            effectType: "prefab_power",
            effectName: prefabName,
            sourceNetworkObjectId: request.SpeakerNetworkId,
            targetNetworkObjectId: targetNetworkObjectId,
            detail: intent.rawTagName
        );
        ApplyPrefabPowerEffectClientRpc(
            prefabName,
            spatial.Position,
            spatial.Forward,
            scale,
            duration,
            new Vector4(color.r, color.g, color.b, color.a),
            useColorOverride,
            def.enableGameplayDamage,
            def.enableHoming,
            projectileSpeed,
            def.homingTurnRateDegrees,
            damageAmount,
            damageRadius,
            def.affectPlayerOnly,
            def.damageType ?? "effect",
            targetNetworkObjectId,
            request.SpeakerNetworkId,
            attachToTarget: intentSpatialType == EffectSpatialType.Attached,
            fitToTargetMesh: intentSpatialType == EffectSpatialType.Attached
                && def.preferFitTargetMesh,
            serverSpawnTimeSeconds: ResolveServerEffectTimeSeconds(),
            effectSeed: ResolveEffectSeed(),
            actionId: actionId
        );
        if (def.enableSurfaceMaterialOverride)
        {
            ApplySurfaceMaterialEffectClientRpc(
                def.effectTag ?? prefabName,
                spatial.Position,
                duration,
                request.SpeakerNetworkId,
                targetNetworkObjectId,
                actionId
            );
        }

        NGLog.Info(
            "DialogueFX",
            NGLog.Format(
                "EffectParser dispatch",
                ("tag", intent.rawTagName),
                ("prefab", prefabName),
                ("scale", scale),
                ("duration", duration),
                ("intensity", intensity),
                ("radius", damageRadius),
                ("speed", projectileSpeed),
                ("target_raw", requestedTarget ?? string.Empty),
                ("speaker", request.SpeakerNetworkId),
                ("target", targetNetworkObjectId),
                ("gameplay", def.enableGameplayDamage),
                ("homing", def.enableHoming),
                ("spatialType", intentSpatialType.ToString()),
                ("spatial", spatial.Reason ?? "ok")
            )
        );
        if (probeTrace)
        {
            NGLog.Info(
                "DialogueFX",
                NGLog.Format(
                    "Probe catalog intent dispatch",
                    ("requestId", request.ClientRequestId),
                    ("tag", intent.rawTagName ?? string.Empty),
                    ("prefab", prefabName ?? string.Empty),
                    ("target", targetNetworkObjectId)
                )
            );
        }
    }
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

    // Allow LLM explicit target to override definition default
    // Only use definition default when parser doesn't specify a target
    string effectiveHint = parserTargetHint;
    if (string.IsNullOrWhiteSpace(parserTargetHint) 
        || parserTargetHint.Equals("auto", StringComparison.OrdinalIgnoreCase)
        || parserTargetHint.Equals("default", StringComparison.OrdinalIgnoreCase))
    {
        effectiveHint = definition.targetType switch
        {
            EffectTargetType.Player => "player",
            EffectTargetType.Floor => "ground",
            EffectTargetType.Npc => "npc",
            EffectTargetType.WorldPoint => "world",
            _ => parserTargetHint
        };
    }
    
    // Log when LLM overrides definition target (for debugging)
    if (!string.IsNullOrWhiteSpace(parserTargetHint) 
        && definition.targetType != EffectTargetType.Auto
        && !parserTargetHint.Equals(effectiveHint, StringComparison.OrdinalIgnoreCase))
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
        default:
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

    // Anchor aliases are emitted by LLMs as "Target: head/feet/ground".
    // Treat them as player-relative placement targets.
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
            // Ground targets should not remain bound to player network IDs.
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
        var networkObject = objectTarget.GetComponentInParent<NetworkObject>();
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

private static void LogPrefabPowerSuccess(
    string prompt,
    string response,
    string powerName,
    string prefabName,
    float scale,
    float duration,
    float damage,
    float damageRadius,
    bool gameplay,
    bool homing,
    ulong sourceId,
    ulong targetId
)
{
    try
    {
        string logDir = Path.Combine(Application.persistentDataPath, "DialogueLogs");
        if (!Directory.Exists(logDir))
        {
            Directory.CreateDirectory(logDir);
        }

        string logPath = Path.Combine(logDir, "prefab_power_success.jsonl");
        string timestamp = DateTime.UtcNow.ToString("o");
        string safePrompt = EscapeJsonString(prompt ?? string.Empty);
        string safeResponse = EscapeJsonString(
            response != null && response.Length > 300
                ? response.Substring(0, 300)
                : response ?? string.Empty
        );
        string safePower = EscapeJsonString(powerName ?? string.Empty);
        string safePrefab = EscapeJsonString(prefabName ?? string.Empty);

        string json = string.Concat(
            "{\"ts\":\"",
            timestamp,
            "\",\"power\":\"",
            safePower,
            "\",\"prefab\":\"",
            safePrefab,
            "\",\"scale\":",
            scale.ToString("F2"),
            ",\"duration\":",
            duration.ToString("F2"),
            ",\"damage\":",
            damage.ToString("F2"),
            ",\"damageRadius\":",
            damageRadius.ToString("F2"),
            ",\"gameplay\":",
            gameplay ? "true" : "false",
            ",\"homing\":",
            homing ? "true" : "false",
            ",\"source\":",
            sourceId.ToString(),
            ",\"target\":",
            targetId.ToString(),
            ",\"prompt\":\"",
            safePrompt,
            "\",\"response\":\"",
            safeResponse,
            "\"}"
        );

        File.AppendAllText(logPath, json + "\n", Encoding.UTF8);
    }
    catch (Exception ex)
    {
        NGLog.Warn("DialogueFX", $"Failed to log prefab power success: {ex.Message}");
    }
}

[Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
private void ApplyDissolveEffectClientRpc(
    ulong targetNetworkObjectId,
    float durationSeconds,
    string actionId = ""
)
{
    RecordLocalEffectReceipt(
        "dissolve",
        "dissolve",
        actionId: actionId,
        targetNetworkObjectId: targetNetworkObjectId
    );
    EnsurePlayerDissolveController();
    if (m_PlayerDissolveController == null)
    {
        return;
    }

    m_PlayerDissolveController.ApplyDissolveEffect(targetNetworkObjectId, durationSeconds, actionId);
}

[Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
private void ApplyFloorDissolveEffectClientRpc(float durationSeconds, string actionId = "")
{
    RecordLocalEffectReceipt("dissolve", "floor_dissolve", actionId: actionId);
    EnsureSceneEffectsController();
    if (m_SceneEffectsController == null)
    {
        return;
    }

    m_SceneEffectsController.ApplyFloorDissolveEffect(durationSeconds, actionId);
}

[Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
private void ApplyRespawnEffectClientRpc(ulong targetNetworkObjectId, string actionId = "")
{
    RecordLocalEffectReceipt(
        "respawn",
        "respawn",
        actionId: actionId,
        targetNetworkObjectId: targetNetworkObjectId
    );
    EnsureSceneEffectsController();
    if (m_SceneEffectsController == null)
    {
        return;
    }

    m_SceneEffectsController.ApplyRespawnEffect(targetNetworkObjectId, actionId);
}

[Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
private void ApplySurfaceMaterialEffectClientRpc(
    string effectTag,
    Vector3 referencePosition,
    float durationSeconds,
    ulong sourceNetworkObjectId,
    ulong targetNetworkObjectId,
    string actionId = ""
)
{
    RecordLocalEffectReceipt(
        "surface_material",
        effectTag ?? "surface_material",
        actionId: actionId,
        sourceNetworkObjectId: sourceNetworkObjectId,
        targetNetworkObjectId: targetNetworkObjectId
    );
    EnsureSceneEffectsController();
    if (m_SceneEffectsController == null)
    {
        return;
    }

    m_SceneEffectsController.ApplySurfaceMaterialEffect(
        effectTag,
        referencePosition,
        durationSeconds,
        sourceNetworkObjectId,
        targetNetworkObjectId,
        actionId
    );
}

[Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
private void ApplyPrefabPowerEffectClientRpc(
    string prefabName,
    Vector3 position,
    Vector3 forward,
    float scale,
    float durationSeconds,
    Vector4 color,
    bool useColorOverride,
    bool enableGameplayDamage,
    bool enableHoming,
    float projectileSpeed,
    float homingTurnRateDegrees,
    float damageAmount,
    float damageRadius,
    bool affectPlayerOnly,
    string damageType,
    ulong targetNetworkObjectId,
    ulong sourceNetworkObjectId,
    bool attachToTarget,
    bool fitToTargetMesh,
    float serverSpawnTimeSeconds,
    uint effectSeed,
    string actionId = ""
)
{
    RecordLocalEffectReceipt(
        "prefab_power",
        prefabName,
        actionId: actionId,
        sourceNetworkObjectId: sourceNetworkObjectId,
        targetNetworkObjectId: targetNetworkObjectId
    );
    EnsureSceneEffectsController();
    if (m_SceneEffectsController == null)
    {
        return;
    }

    var effectColor = new Color(color.x, color.y, color.z, color.w);
    m_SceneEffectsController.ApplyPrefabPower(
        prefabName,
        position,
        forward,
        scale,
        durationSeconds,
        effectColor,
        useColorOverride,
        enableGameplayDamage,
        enableHoming,
        projectileSpeed,
        homingTurnRateDegrees,
        damageAmount,
        damageRadius,
        affectPlayerOnly,
        damageType,
        sourceNetworkObjectId: sourceNetworkObjectId,
        targetNetworkObjectId: targetNetworkObjectId,
        attachToTarget: attachToTarget,
        fitToTargetMesh: fitToTargetMesh,
        serverSpawnTimeSeconds: serverSpawnTimeSeconds,
        effectSeed: effectSeed,
        actionId: actionId
    );
}

private static float ResolveServerEffectTimeSeconds()
{
    NetworkManager networkManager = NetworkManager.Singleton;
    if (networkManager == null || !networkManager.IsListening)
    {
        return Time.time;
    }

    return networkManager.ServerTime.TimeAsFloat;
}

private static uint ResolveEffectSeed()
{
    return (uint)UnityEngine.Random.Range(1, int.MaxValue);
}

private static float ResolveExplicitDurationSeconds(
    ParticleParameterExtractor.ParticleParameterIntent parameterIntent,
    float min,
    float max
)
{
    if (!parameterIntent.HasExplicitDurationSeconds)
    {
        return -1f;
    }

    return Mathf.Clamp(parameterIntent.ExplicitDurationSeconds, min, max);
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

private static Vector3 ResolveEffectForward(
    GameObject speakerObject,
    GameObject listenerObject
)
{
    if (speakerObject == null)
    {
        return Vector3.forward;
    }

    if (listenerObject != null)
    {
        Vector3 toListener =
            listenerObject.transform.position - speakerObject.transform.position;
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

private static bool TryResolveObjectAnchor(
    string effectContext,
    out GameObject targetObject
)
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
    DialogueSemanticTag[] tags =
        UnityEngine.Object.FindObjectsOfType<DialogueSemanticTag>();
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

private static bool ContainsAnyKeyword(string source, string[] keywords)
{
    if (string.IsNullOrWhiteSpace(source) || keywords == null || keywords.Length == 0)
    {
        return false;
    }

    string lower = source.ToLowerInvariant();
    for (int i = 0; i < keywords.Length; i++)
    {
        string keyword = keywords[i];
        if (string.IsNullOrWhiteSpace(keyword))
        {
            continue;
        }

        if (lower.Contains(keyword.Trim().ToLowerInvariant()))
        {
            return true;
        }
    }

    return false;
}

private static readonly string[] GroundFreezeKeywords = new[]
{
    "freeze",
    "freez",
    "freexe",
    "frezze",
    "frozen",
    "frost",
    "ice",
    "glacial",
    "icy",
    "freeze solid",
    "congelar",
    "gelar",
    "gelo",
    "frozen ground",
};

private static readonly string[] GroundSurfaceKeywords = new[]
{
    "ground",
    "grond",
    "grnd",
    "floor",
    "fllor",
    "flor",
    "terrain",
    "arena",
    "soil",
    "field",
    "land",
    "chao",
    "chÃ£o",
    "piso",
    "solo",
};

private static bool IsGroundFreezePrompt(string prompt)
{
    return ContainsAnyKeyword(prompt, GroundFreezeKeywords)
        && ContainsAnyKeyword(prompt, GroundSurfaceKeywords);
}

private static EffectDefinition PickPromptMatchedPower(
    NpcDialogueProfile profile,
    string promptContext,
    string preferredElement = null,
    float aggressionBias = 1f
)
{
    if (profile == null) return null;

    EffectDefinition[] defs = profile.Effects;
    if (defs == null || defs.Length == 0) return null;

    if (IsGroundFreezePrompt(promptContext))
    {
        EffectDefinition groundFreeze = TryFindGroundFreezeProfilePower(profile);
        if (groundFreeze != null) return groundFreeze;
    }

    string lowerContext = string.IsNullOrWhiteSpace(promptContext)
        ? string.Empty
        : promptContext.Trim().ToLowerInvariant();

    EffectDefinition best = null;
    int bestScore = int.MinValue;
    for (int i = 0; i < defs.Length; i++)
    {
        EffectDefinition def = defs[i];
        if (def == null || !def.enabled || def.effectPrefab == null) continue;

        int score = 0;
        if (ContainsAnyKeyword(lowerContext, def.keywords))        score += 120;
        if (ContainsAnyKeyword(lowerContext, def.creativeTriggers)) score += 80;
        if (!string.IsNullOrWhiteSpace(def.effectTag)
            && lowerContext.Contains(def.effectTag.Trim().ToLowerInvariant()))
            score += 60;
        if (!string.IsNullOrWhiteSpace(preferredElement)
            && string.Equals(preferredElement, def.element, StringComparison.OrdinalIgnoreCase))
            score += 15;
        if (aggressionBias > 1.15f && IsCombatPowerKeywords(def))
            score += Mathf.RoundToInt((aggressionBias - 1f) * 60f);
        else if (aggressionBias < 0.85f && IsCombatPowerKeywords(def))
            score -= Mathf.RoundToInt((1f - aggressionBias) * 40f);

        if (score > bestScore) { bestScore = score; best = def; }
    }

    return best != null && bestScore > 0 ? best : PickRandomEnabledPower(profile, preferredElement);
}

private static bool LooksLikeFreezeIntent(EffectIntent intent, EffectDefinition definition)
{
    if (intent == null)
    {
        return false;
    }

    string raw = intent.rawTagName ?? string.Empty;
    string target = intent.target ?? string.Empty;
    string anchor = intent.anchor ?? string.Empty;
    string tag = definition != null ? definition.effectTag : string.Empty;

    if (
        ContainsAnyKeyword(raw, GroundFreezeKeywords)
        || ContainsAnyKeyword(tag, GroundFreezeKeywords)
        || ContainsAnyKeyword(target, GroundFreezeKeywords)
        || ContainsAnyKeyword(anchor, GroundFreezeKeywords)
    )
    {
        return true;
    }

    if (definition?.alternativeTags != null)
    {
        for (int i = 0; i < definition.alternativeTags.Length; i++)
        {
            string alt = definition.alternativeTags[i];
            if (ContainsAnyKeyword(alt, GroundFreezeKeywords))
            {
                return true;
            }
        }
    }

    return false;
}

private static bool LooksProjectileLike(EffectIntent intent, EffectDefinition definition)
{
    if (
        intent != null
        && !string.IsNullOrWhiteSpace(intent.placementType)
        && intent.placementType.IndexOf("projectile", StringComparison.OrdinalIgnoreCase)
            >= 0
    )
    {
        return true;
    }

    if (definition == null)
    {
        return false;
    }

    if (definition.enableHoming || definition.projectileSpeed >= 8f)
    {
        return true;
    }

    string lowerTag = (definition.effectTag ?? string.Empty).ToLowerInvariant();
    return lowerTag.Contains("lance")
        || lowerTag.Contains("bolt")
        || lowerTag.Contains("missile")
        || lowerTag.Contains("projectile")
        || lowerTag.Contains("arrow")
        || lowerTag.Contains("fireball");
}

private static bool ShouldForceGroundFreezeForIntent(
    DialogueRequest request,
    EffectIntent intent,
    EffectDefinition definition
)
{
    if (intent == null)
    {
        return false;
    }

    bool promptGroundFreeze = IsGroundFreezePrompt(request.Prompt);
    string requestedTarget = !string.IsNullOrWhiteSpace(intent.anchor)
        ? intent.anchor
        : intent.target;
    string requestedTargetLower = string.IsNullOrWhiteSpace(requestedTarget)
        ? string.Empty
        : requestedTarget.Trim().ToLowerInvariant();
    bool explicitGroundTarget = IsGroundAlias(requestedTargetLower);
    bool freezeIntent = LooksLikeFreezeIntent(intent, definition);

    if (!freezeIntent)
    {
        return false;
    }

    if (promptGroundFreeze)
    {
        return true;
    }

    if (explicitGroundTarget && LooksProjectileLike(intent, definition))
    {
        return true;
    }

    return false;
}

private static void EnforceGroundFreezeIntentParameters(EffectIntent intent)
{
    if (intent == null)
    {
        return;
    }

    intent.anchor = "ground";
    intent.target = "ground";
    intent.placementType = "area";
    intent.groundSnap = true;
    if (!intent.requireLineOfSight.HasValue)
    {
        intent.requireLineOfSight = false;
    }
}

private static EffectDefinition TryFindGroundFreezeProfilePower(NpcDialogueProfile profile)
{
    if (profile == null || profile.Effects == null || profile.Effects.Length == 0)
    {
        return null;
    }

    EffectDefinition best = null;
    int bestScore = int.MinValue;
    for (int i = 0; i < profile.Effects.Length; i++)
    {
        EffectDefinition def = profile.Effects[i];
        if (def == null || !def.enabled || def.effectPrefab == null)
        {
            continue;
        }

        int score = 0;
        if (LooksLikeGroundFreezePower(def))
        {
            score += 120;
        }

        if (!def.enableHoming && def.projectileSpeed <= 6f)
        {
            score += 24;
        }

        if (def.damageRadius >= 2f)
        {
            score += 16;
        }

        if (score > bestScore)
        {
            bestScore = score;
            best = def;
        }
    }

    return bestScore >= 120 ? best : null;
}

private static bool LooksLikeGroundFreezePower(EffectDefinition def)
{
    if (def == null)
    {
        return false;
    }

    string powerName = def.effectTag ?? string.Empty;
    string prefabName = def.effectPrefab != null ? def.effectPrefab.name : string.Empty;
    bool hasGroundSignal =
        ContainsAnyKeyword(powerName, GroundSurfaceKeywords)
        || ContainsAnyKeyword(prefabName, GroundSurfaceKeywords)
        || ContainsAnyKeyword(
            powerName,
            new[] { "fog", "mist", "aoe", "field", "burst", "break" }
        )
        || ContainsAnyKeyword(
            prefabName,
            new[] { "fog", "mist", "aoe", "field", "burst", "break" }
        )
        || ContainsAnyKeyword(
            string.Join(" ", def.keywords ?? Array.Empty<string>()),
            GroundSurfaceKeywords
        )
        || ContainsAnyKeyword(
            string.Join(" ", def.creativeTriggers ?? Array.Empty<string>()),
            GroundSurfaceKeywords
        );

    bool hasFreezeSignal =
        ContainsAnyKeyword(powerName, GroundFreezeKeywords)
        || ContainsAnyKeyword(prefabName, GroundFreezeKeywords)
        || ContainsAnyKeyword(def.element, new[] { "ice", "frost", "cold", "water" })
        || ContainsAnyKeyword(
            string.Join(" ", def.keywords ?? Array.Empty<string>()),
            GroundFreezeKeywords
        )
        || ContainsAnyKeyword(
            string.Join(" ", def.creativeTriggers ?? Array.Empty<string>()),
            GroundFreezeKeywords
        );

    bool hasGroundFogSignal =
        ContainsAnyKeyword(
            powerName,
            new[] { "groundfog", "ground fog", "frost field", "ice field" }
        )
        || ContainsAnyKeyword(
            prefabName,
            new[] { "groundfog", "ground fog", "frost field", "ice field" }
        )
        || ContainsAnyKeyword(powerName, new[] { "fog", "mist" })
        || ContainsAnyKeyword(prefabName, new[] { "fog", "mist" });

    return hasGroundSignal
        && (hasFreezeSignal || hasGroundFogSignal || def.damageRadius >= 1f);
}

private static readonly HashSet<string> s_CombatKeywords = new(
    StringComparer.OrdinalIgnoreCase
)
{
    "damage",
    "attack",
    "strike",
    "blast",
    "hit",
    "hurt",
    "harm",
    "kill",
    "destroy",
    "burn",
    "freeze",
    "shock",
    "explode",
    "fire",
    "lightning",
    "curse",
    "wound",
    "slash",
};

private static bool IsCombatPowerKeywords(EffectDefinition def)
{
    if (def == null)
        return false;
    if (def.keywords != null)
        foreach (string kw in def.keywords)
            if (!string.IsNullOrEmpty(kw) && s_CombatKeywords.Contains(kw))
                return true;
    if (!string.IsNullOrEmpty(def.effectTag))
        foreach (string kw in s_CombatKeywords)
            if (def.effectTag.Contains(kw, StringComparison.OrdinalIgnoreCase))
                return true;
    return false;
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
        Vector3 clamped = new Vector3(
            Mathf.Clamp(referencePosition.x, bounds.min.x, bounds.max.x),
            bounds.max.y + 0.03f,
            Mathf.Clamp(referencePosition.z, bounds.min.z, bounds.max.z)
        );
        return clamped;
    }

    Vector3 origin = ResolveEffectOrigin(groundObject);
    origin.y += 0.03f;
    return origin;
}

private static readonly string[] PowerRequestPhrases = new[]
{
    "power",
    "attack",
    "cast",
    "spell",
    "ability",
    "skill",
    "use your",
    "show me",
    "demonstrate",
    "blast",
    "strike",
    "shoot",
    "hit me",
    "fire at",
    "throw",
    "launch",
    "unleash",
    "summon",
    "conjure",
    "invoke",
    "hurl",
    "smite",
    "ultimate",
    "special",
    "vfx",
    "effect",
    "fx",
    "poder",
    "habilidade",
    "ataque",
};

private static bool LooksLikePowerRequest(string prompt)
{
    if (string.IsNullOrWhiteSpace(prompt))
    {
        return false;
    }

    string lower = prompt.ToLowerInvariant();
    for (int i = 0; i < PowerRequestPhrases.Length; i++)
    {
        if (lower.Contains(PowerRequestPhrases[i]))
        {
            return true;
        }
    }

    return false;
}

private static EffectDefinition PickRandomEnabledPower(
    NpcDialogueProfile profile,
    string preferredElement = null
)
{
    EffectDefinition[] defs = profile?.Effects;
    if (defs == null || defs.Length == 0) return null;

    var candidates = new List<EffectDefinition>(defs.Length);
    for (int i = 0; i < defs.Length; i++)
        if (defs[i] != null && defs[i].enabled && defs[i].effectPrefab != null)
            candidates.Add(defs[i]);

    if (candidates.Count == 0) return null;

    if (!string.IsNullOrWhiteSpace(preferredElement))
    {
        var weighted = new List<EffectDefinition>(candidates.Count * 2);
        for (int i = 0; i < candidates.Count; i++)
        {
            EffectDefinition d = candidates[i];
            weighted.Add(d);
            if (string.Equals(d.element, preferredElement, StringComparison.OrdinalIgnoreCase))
            { weighted.Add(d); weighted.Add(d); } // 3Ã— weight
        }
        return weighted[UnityEngine.Random.Range(0, weighted.Count)];
    }
    return candidates[UnityEngine.Random.Range(0, candidates.Count)];
}

private static EffectDefinition TryMatchProfilePowerByName(
    NpcDialogueProfile profile,
    string tagName
)
{
    if (profile == null || string.IsNullOrWhiteSpace(tagName)) return null;
    EffectDefinition[] defs = profile.Effects;
    if (defs == null || defs.Length == 0) return null;

    string normalizedTag = tagName.Trim().ToLowerInvariant();
    string compactTag = CompactEffectMatchToken(normalizedTag);

    for (int i = 0; i < defs.Length; i++)
    {
        EffectDefinition def = defs[i];
        if (def == null || !def.enabled || def.effectPrefab == null) continue;

        string lowerTag = def.effectTag?.Trim().ToLowerInvariant() ?? string.Empty;
        string compactDefTag = CompactEffectMatchToken(lowerTag);
        string lowerPrefab = def.effectPrefab.name.Trim().ToLowerInvariant();
        string compactPrefab = CompactEffectMatchToken(lowerPrefab);

        if (lowerTag.Equals(normalizedTag, StringComparison.Ordinal)) return def;
        if (lowerPrefab.Equals(normalizedTag, StringComparison.Ordinal)) return def;
        if (lowerTag.Length > 2 && (normalizedTag.Contains(lowerTag) || lowerTag.Contains(normalizedTag))) return def;
        if (compactTag.Length > 2
            && (compactDefTag.Contains(compactTag) || compactTag.Contains(compactDefTag)
                || compactPrefab.Contains(compactTag) || compactTag.Contains(compactPrefab)))
            return def;
        if (HasLooseTagMatch(normalizedTag, compactTag, def.keywords)
            || HasLooseTagMatch(normalizedTag, compactTag, def.creativeTriggers))
            return def;
    }
    return null;
}

private static bool HasLooseTagMatch(
    string normalizedTag,
    string compactTag,
    string[] candidates
)
{
    if (candidates == null || candidates.Length == 0)
    {
        return false;
    }

    for (int i = 0; i < candidates.Length; i++)
    {
        string candidate = candidates[i];
        if (string.IsNullOrWhiteSpace(candidate))
        {
            continue;
        }

        string lowerCandidate = candidate.Trim().ToLowerInvariant();
        if (
            lowerCandidate.Equals(normalizedTag, System.StringComparison.Ordinal)
            || lowerCandidate.Contains(normalizedTag, System.StringComparison.Ordinal)
            || normalizedTag.Contains(lowerCandidate, System.StringComparison.Ordinal)
        )
        {
            return true;
        }

        string compactCandidate = CompactEffectMatchToken(lowerCandidate);
        if (
            compactCandidate.Length > 2
            && (
                compactCandidate.Equals(compactTag, System.StringComparison.Ordinal)
                || compactCandidate.Contains(compactTag, System.StringComparison.Ordinal)
                || compactTag.Contains(compactCandidate, System.StringComparison.Ordinal)
            )
        )
        {
            return true;
        }
    }

    return false;
}

private static string CompactEffectMatchToken(string value)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return string.Empty;
    }

    var sb = new StringBuilder(value.Length);
    for (int i = 0; i < value.Length; i++)
    {
        char c = value[i];
        if (char.IsLetterOrDigit(c))
        {
            sb.Append(char.ToLowerInvariant(c));
        }
    }

    return sb.ToString();
}

private void TryApplyPromptOnlyPowerFallback(DialogueRequest request, string failureReason)
{
    if (!m_EnableContextSceneEffects)
    {
        return;
    }

    if (!request.IsUserInitiated || !LooksLikePowerRequest(request.Prompt))
    {
        return;
    }

    NpcDialogueActor actor = ResolveDialogueActorForRequest(
        request,
        out ulong resolvedSpeakerNetworkId,
        out ulong resolvedListenerNetworkId,
        out _
    );
    NpcDialogueProfile profile = actor != null ? actor.Profile : null;
    if (profile == null)
    {
        return;
    }

    PlayerIdentityBinding fallbackIdentity = ResolvePlayerIdentityForRequest(request);
    PlayerEffectModifier fallbackMod = BuildPlayerEffectModifier(fallbackIdentity);
    EffectDefinition fallback = PickPromptMatchedPower(
        profile,
        request.Prompt,
        fallbackMod.PreferredElement,
        fallbackMod.AggressionBias
    );
    if (fallback == null)
    {
        return;
    }

    string prefabName = fallback.effectPrefab != null
        ? fallback.effectPrefab.name
        : fallback.effectTag ?? string.Empty;
    float durationSeconds = Mathf.Max(0.5f, fallback.defaultDuration);
    float scale = Mathf.Max(0.1f, fallback.defaultScale);
    Vector3 spawnOffset = fallback.spawnOffset;
    bool spawnInFront = fallback.spawnInFrontOfNpc;
    float forwardDistance = Mathf.Max(0f, fallback.forwardDistance);
    Color color = fallback.defaultColor;
    bool useColorOverride =
        fallback.allowCustomColor && fallback.defaultColor != Color.white;
    string powerName = fallback.effectTag ?? string.Empty;
    bool enableGameplayDamage = fallback.enableGameplayDamage;
    bool enableHoming = fallback.enableHoming;
    float projectileSpeed = Mathf.Max(0.1f, fallback.projectileSpeed);
    float homingTurnRateDegrees = Mathf.Max(0f, fallback.homingTurnRateDegrees);
    float damageAmount = Mathf.Max(0f, fallback.damageAmount);
    float damageRadius = Mathf.Max(0.1f, fallback.damageRadius);
    bool affectPlayerOnly = fallback.affectPlayerOnly;
    string damageType = string.IsNullOrWhiteSpace(fallback.damageType)
        ? "effect"
        : fallback.damageType.Trim();
    GameObject speakerObject = ResolveSpawnedObject(resolvedSpeakerNetworkId);
    GameObject listenerObject = ResolveSpawnedObject(resolvedListenerNetworkId);
    ResolveEffectSpatialContext(
        request.Prompt,
        speakerObject,
        listenerObject,
        out Vector3 effectOrigin,
        out Vector3 effectForward,
        out _
    );

    Vector3 spawnPos = effectOrigin + spawnOffset;
    if (spawnInFront)
    {
        spawnPos += effectForward * forwardDistance;
    }

    ulong targetNetworkObjectId = resolvedListenerNetworkId;
    string actionId = BuildActionId(
        request,
        0,
        "prefab_power",
        prefabName,
        targetNetworkObjectId
    );
    GameObject fallbackTargetObject = ResolveSpawnedObject(targetNetworkObjectId);
    EffectSpatialType fallbackPowerSpatialType = ResolveEffectSpatialType(
        placementHint: null,
        effectName: string.IsNullOrWhiteSpace(powerName) ? prefabName : powerName,
        targetHint: fallbackTargetObject != null ? fallbackTargetObject.name : "player",
        enableHoming: enableHoming,
        projectileSpeed: projectileSpeed,
        damageRadius: damageRadius,
        scale: scale,
        affectPlayerOnly: affectPlayerOnly
    );
    EffectSpatialPolicy fallbackPowerSpatialPolicy = BuildEffectSpatialPolicy(
        fallbackPowerSpatialType,
        enableGameplayDamage,
        collisionPolicyHintOverride: null
    );
    DialogueEffectSpatialResolver.ResolveResult fallbackSpatial =
        ResolveSpatialPlacementForPower(
            powerName,
            request,
            spawnPos,
            effectForward,
            effectOrigin,
            effectForward,
            scale,
            damageRadius,
            fallbackPowerSpatialPolicy,
            enableGameplayDamage: enableGameplayDamage,
            targetObject: fallbackTargetObject
        );
    if (!fallbackSpatial.IsValid)
    {
        RecordActionValidationResult(
            request,
            0,
            actionId,
            actionKind: "prefab_power",
            actionName: prefabName,
            decision: "rejected",
            success: false,
            reason: "spatial_invalid",
            requestedTargetHint: fallbackTargetObject != null ? fallbackTargetObject.name : "player",
            resolvedTargetNetworkObjectId: targetNetworkObjectId,
            resolvedSpatialType: fallbackPowerSpatialType.ToString(),
            spatialReason: fallbackSpatial.Reason ?? "invalid",
            requestedScale: scale,
            appliedScale: scale,
            requestedDuration: durationSeconds,
            appliedDuration: durationSeconds,
            requestedDamageRadius: damageRadius,
            appliedDamageRadius: damageRadius,
            requestedDamageAmount: damageAmount,
            appliedDamageAmount: damageAmount
        );
        NGLog.Warn(
            "DialogueFX",
            NGLog.Format(
                "Prompt-only power fallback skipped (spatial invalid)",
                ("power", powerName),
                ("prefab", prefabName),
                ("reason", fallbackSpatial.Reason ?? "invalid")
            )
        );
        return;
    }

    RecordActionValidationResult(
        request,
        0,
        actionId,
        actionKind: "prefab_power",
        actionName: prefabName,
        decision: "validated",
        success: true,
        requestedTargetHint: fallbackTargetObject != null ? fallbackTargetObject.name : "player",
        resolvedTargetNetworkObjectId: targetNetworkObjectId,
        resolvedSpatialType: fallbackPowerSpatialType.ToString(),
        spatialReason: fallbackSpatial.Reason ?? "ok",
        requestedScale: scale,
        appliedScale: scale,
        requestedDuration: durationSeconds,
        appliedDuration: durationSeconds,
        requestedDamageRadius: damageRadius,
        appliedDamageRadius: damageRadius,
        requestedDamageAmount: damageAmount,
        appliedDamageAmount: damageAmount
    );

    ApplyPrefabPowerEffectClientRpc(
        prefabName,
        fallbackSpatial.Position,
        fallbackSpatial.Forward,
        scale,
        durationSeconds,
        new Vector4(color.r, color.g, color.b, color.a),
        useColorOverride,
        enableGameplayDamage,
        enableHoming,
        projectileSpeed,
        homingTurnRateDegrees,
        damageAmount,
        damageRadius,
        affectPlayerOnly,
        damageType,
        targetNetworkObjectId,
        resolvedSpeakerNetworkId,
        attachToTarget: fallbackPowerSpatialType == EffectSpatialType.Attached,
        fitToTargetMesh: fallbackPowerSpatialType == EffectSpatialType.Attached,
        serverSpawnTimeSeconds: ResolveServerEffectTimeSeconds(),
        effectSeed: ResolveEffectSeed(),
        actionId: actionId
    );
    if (fallback.enableSurfaceMaterialOverride)
    {
        ApplySurfaceMaterialEffectClientRpc(
            fallback.effectTag ?? prefabName,
            fallbackSpatial.Position,
            durationSeconds,
            resolvedSpeakerNetworkId,
            targetNetworkObjectId,
            actionId
        );
    }
    RecordExecutionTrace(
        stage: "effect_dispatch",
        success: true,
        request,
        0,
        actionId: actionId,
        stageDetail: "prompt_only_fallback",
        effectType: "prefab_power",
        effectName: prefabName,
        sourceNetworkObjectId: resolvedSpeakerNetworkId,
        targetNetworkObjectId: targetNetworkObjectId,
        responsePreview: powerName
    );
    RecordReplicationTrace(
        stage: "rpc_sent",
        networkPath: "client_rpc",
        success: true,
        request,
        0,
        actionId: actionId,
        effectType: "prefab_power",
        effectName: prefabName,
        sourceNetworkObjectId: resolvedSpeakerNetworkId,
        targetNetworkObjectId: targetNetworkObjectId,
        detail: powerName
    );

    NGLog.Warn(
        "DialogueFX",
        NGLog.Format(
            "Applied prompt-only power fallback",
            ("power", powerName),
            ("prefab", prefabName),
            ("speaker", resolvedSpeakerNetworkId),
            ("listener", resolvedListenerNetworkId),
            ("spatialType", fallbackPowerSpatialType.ToString()),
            ("spatial", fallbackSpatial.Reason ?? "ok"),
            ("reason", failureReason ?? "unknown")
        )
    );
}

private static float ClampDynamicMultiplier(float value, NpcDialogueProfile profile)
{
    float min = 0.6f;
    float max = 2f;
    if (profile != null)
    {
        min = Mathf.Clamp(profile.DynamicEffectMinMultiplier, 0.25f, 1f);
        max = Mathf.Clamp(profile.DynamicEffectMaxMultiplier, 1f, 10f);
        if (max < min)
        {
            max = min;
        }
    }

    if (float.IsNaN(value) || float.IsInfinity(value))
    {
        return 1f;
    }

    return Mathf.Clamp(value, min, max);
}

private static Color ApplyDynamicColorOverride(
    Color baseColor,
    ParticleParameterExtractor.ParticleParameterIntent parameterIntent,
    float blend = 0.78f
)
{
    if (!parameterIntent.HasColorOverride)
    {
        return baseColor;
    }

    float t = Mathf.Clamp01(blend);
    Color color = Color.Lerp(baseColor, parameterIntent.ColorOverride, t);
    color.a = baseColor.a;
    return color;
}

private static string BuildEffectContextText(string prompt, string response)
{
    if (string.IsNullOrWhiteSpace(prompt))
    {
        return response ?? string.Empty;
    }

    if (string.IsNullOrWhiteSpace(response))
    {
        return prompt;
    }

    return $"{prompt}\n{response}";
}

private static bool IsGameplayProbeRequest(DialogueRequest request, string promptText)
{
    if (request.ClientRequestId >= GameplayProbeClientRequestIdMin)
    {
        return true;
    }

    return !string.IsNullOrWhiteSpace(promptText)
        && promptText.IndexOf("Gameplay probe for ", StringComparison.OrdinalIgnoreCase)
            >= 0;
}




    }
}
