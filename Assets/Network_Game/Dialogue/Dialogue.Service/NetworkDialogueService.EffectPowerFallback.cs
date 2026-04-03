using System;
using System.Collections.Generic;
using System.Text;
using Network_Game.Diagnostics;
using Network_Game.Dialogue.Effects;
using UnityEngine;

namespace Network_Game.Dialogue
{
    public partial class NetworkDialogueService
    {
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

        private static readonly HashSet<string> s_CombatKeywords = new(StringComparer.OrdinalIgnoreCase)
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
            if (profile == null)
            {
                return null;
            }

            EffectDefinition[] defs = profile.Effects;
            if (defs == null || defs.Length == 0)
            {
                return null;
            }

            if (IsGroundFreezePrompt(promptContext))
            {
                EffectDefinition groundFreeze = TryFindGroundFreezeProfilePower(profile);
                if (groundFreeze != null)
                {
                    return groundFreeze;
                }
            }

            string lowerContext = string.IsNullOrWhiteSpace(promptContext)
                ? string.Empty
                : promptContext.Trim().ToLowerInvariant();

            EffectDefinition best = null;
            int bestScore = int.MinValue;
            for (int i = 0; i < defs.Length; i++)
            {
                EffectDefinition def = defs[i];
                if (def == null || !def.enabled || def.effectPrefab == null)
                {
                    continue;
                }

                int score = 0;
                if (ContainsAnyKeyword(lowerContext, def.keywords))
                {
                    score += 120;
                }

                if (ContainsAnyKeyword(lowerContext, def.creativeTriggers))
                {
                    score += 80;
                }

                if (
                    !string.IsNullOrWhiteSpace(def.effectTag)
                    && lowerContext.Contains(def.effectTag.Trim().ToLowerInvariant())
                )
                {
                    score += 60;
                }

                if (
                    !string.IsNullOrWhiteSpace(preferredElement)
                    && string.Equals(
                        preferredElement,
                        def.element,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    score += 15;
                }

                if (aggressionBias > 1.15f && IsCombatPowerKeywords(def))
                {
                    score += Mathf.RoundToInt((aggressionBias - 1f) * 60f);
                }
                else if (aggressionBias < 0.85f && IsCombatPowerKeywords(def))
                {
                    score -= Mathf.RoundToInt((1f - aggressionBias) * 40f);
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    best = def;
                }
            }

            return best != null && bestScore > 0
                ? best
                : PickRandomEnabledPower(profile, preferredElement);
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

        private static bool IsCombatPowerKeywords(EffectDefinition def)
        {
            if (def == null)
            {
                return false;
            }

            if (def.keywords != null)
            {
                foreach (string kw in def.keywords)
                {
                    if (!string.IsNullOrEmpty(kw) && s_CombatKeywords.Contains(kw))
                    {
                        return true;
                    }
                }
            }

            if (!string.IsNullOrEmpty(def.effectTag))
            {
                foreach (string kw in s_CombatKeywords)
                {
                    if (def.effectTag.Contains(kw, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

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
            if (defs == null || defs.Length == 0)
            {
                return null;
            }

            var candidates = new List<EffectDefinition>(defs.Length);
            for (int i = 0; i < defs.Length; i++)
            {
                if (defs[i] != null && defs[i].enabled && defs[i].effectPrefab != null)
                {
                    candidates.Add(defs[i]);
                }
            }

            if (candidates.Count == 0)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(preferredElement))
            {
                var weighted = new List<EffectDefinition>(candidates.Count * 2);
                for (int i = 0; i < candidates.Count; i++)
                {
                    EffectDefinition d = candidates[i];
                    weighted.Add(d);
                    if (
                        string.Equals(
                            d.element,
                            preferredElement,
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                    {
                        weighted.Add(d);
                        weighted.Add(d);
                    }
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
            if (profile == null || string.IsNullOrWhiteSpace(tagName))
            {
                return null;
            }

            EffectDefinition[] defs = profile.Effects;
            if (defs == null || defs.Length == 0)
            {
                return null;
            }

            string normalizedTag = tagName.Trim().ToLowerInvariant();
            string compactTag = CompactEffectMatchToken(normalizedTag);

            for (int i = 0; i < defs.Length; i++)
            {
                EffectDefinition def = defs[i];
                if (def == null || !def.enabled || def.effectPrefab == null)
                {
                    continue;
                }

                string lowerTag = def.effectTag?.Trim().ToLowerInvariant() ?? string.Empty;
                string compactDefTag = CompactEffectMatchToken(lowerTag);
                string lowerPrefab = def.effectPrefab.name.Trim().ToLowerInvariant();
                string compactPrefab = CompactEffectMatchToken(lowerPrefab);

                if (lowerTag.Equals(normalizedTag, StringComparison.Ordinal))
                {
                    return def;
                }

                if (lowerPrefab.Equals(normalizedTag, StringComparison.Ordinal))
                {
                    return def;
                }

                if (
                    lowerTag.Length > 2
                    && (
                        normalizedTag.Contains(lowerTag)
                        || lowerTag.Contains(normalizedTag)
                    )
                )
                {
                    return def;
                }

                if (
                    compactTag.Length > 2
                    && (
                        compactDefTag.Contains(compactTag)
                        || compactTag.Contains(compactDefTag)
                        || compactPrefab.Contains(compactTag)
                        || compactTag.Contains(compactPrefab)
                    )
                )
                {
                    return def;
                }

                if (
                    HasLooseTagMatch(normalizedTag, compactTag, def.keywords)
                    || HasLooseTagMatch(normalizedTag, compactTag, def.creativeTriggers)
                )
                {
                    return def;
                }
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
                    lowerCandidate.Equals(normalizedTag, StringComparison.Ordinal)
                    || lowerCandidate.Contains(normalizedTag, StringComparison.Ordinal)
                    || normalizedTag.Contains(lowerCandidate, StringComparison.Ordinal)
                )
                {
                    return true;
                }

                string compactCandidate = CompactEffectMatchToken(lowerCandidate);
                if (
                    compactCandidate.Length > 2
                    && (
                        compactCandidate.Equals(compactTag, StringComparison.Ordinal)
                        || compactCandidate.Contains(compactTag, StringComparison.Ordinal)
                        || compactTag.Contains(compactCandidate, StringComparison.Ordinal)
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
    }
}
