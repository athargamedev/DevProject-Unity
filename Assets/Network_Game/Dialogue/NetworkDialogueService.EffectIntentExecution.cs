using System.Collections.Generic;
using Network_Game.Diagnostics;
using Network_Game.Dialogue.Effects;
using UnityEngine;

namespace Network_Game.Dialogue
{
    public partial class NetworkDialogueService
    {
        // Catalog intent execution and prefab effect dispatch.
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
    }
}
