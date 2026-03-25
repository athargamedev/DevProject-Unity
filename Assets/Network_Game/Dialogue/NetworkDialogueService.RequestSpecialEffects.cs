using System;
using System.Collections.Generic;
using System.Linq;
using Network_Game.Diagnostics;
using Network_Game.Dialogue.Effects;
using UnityEngine;

namespace Network_Game.Dialogue
{
    public partial class NetworkDialogueService
    {
        private string RewriteRefusalResponseForEffectCommands(
            DialogueRequest request,
            string responseText
        )
        {
            if (string.IsNullOrWhiteSpace(responseText))
            {
                return responseText;
            }
            if (!LooksLikeModelRefusal(responseText))
            {
                return responseText;
            }

            string promptText = request.Prompt ?? string.Empty;
            PlayerSpecialEffectMode specialEffectMode = ResolvePlayerSpecialEffectMode(
                promptText,
                string.Empty,
                null
            );
            string rewrittenResponse;
            switch (specialEffectMode)
            {
                case PlayerSpecialEffectMode.None:
                    return responseText;
                case PlayerSpecialEffectMode.FloorDissolve:
                    rewrittenResponse = "As you wish. The floor fades from sight.";
                    break;
                case PlayerSpecialEffectMode.Dissolve:
                    rewrittenResponse = "As you wish. You fade from sight.";
                    break;
                default:
                    rewrittenResponse = "As you wish. You return to view.";
                    break;
            }

            NGLog.Warn(
                "DialogueFX",
                NGLog.Format(
                    "Rewrote model refusal for effect command",
                    ("mode", specialEffectMode.ToString()),
                    ("prompt", promptText),
                    ("original", responseText)
                )
            );
            return rewrittenResponse;
        }

        private PlayerSpecialEffectMode ResolvePlayerSpecialEffectMode(
            string promptText,
            string responseText,
            List<EffectIntent> intents
        )
        {
            if (intents != null)
            {
                for (int i = 0; i < intents.Count; i++)
                {
                    EffectIntent effectIntent = intents[i];
                    string rawTagName = effectIntent.rawTagName;
                    if (string.IsNullOrWhiteSpace(rawTagName))
                    {
                        continue;
                    }

                    string targetHint = ResolveSpecialIntentTargetHint(effectIntent);
                    string normalizedTag = rawTagName.Trim().ToLowerInvariant();
                    if (normalizedTag.Contains("dissolve") || normalizedTag.Contains("vanish"))
                    {
                        if (LooksLikeFloorTargetHint(targetHint))
                        {
                            return PlayerSpecialEffectMode.FloorDissolve;
                        }
                        if (
                            string.IsNullOrWhiteSpace(targetHint)
                            || LooksLikePlayerTargetHint(targetHint)
                        )
                        {
                            return PlayerSpecialEffectMode.Dissolve;
                        }
                    }
                    else if (
                        (normalizedTag.Contains("respawn") || normalizedTag.Contains("revive"))
                        && (
                            string.IsNullOrWhiteSpace(targetHint)
                            || LooksLikePlayerTargetHint(targetHint)
                        )
                    )
                    {
                        return PlayerSpecialEffectMode.Respawn;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(responseText))
            {
                return PlayerSpecialEffectMode.None;
            }

            string normalizedResponse = responseText.ToLowerInvariant();
            if (
                normalizedResponse.Contains("[effect:")
                && (normalizedResponse.Contains("dissolve") || normalizedResponse.Contains("vanish"))
            )
            {
                if (
                    LooksLikeFloorTargetHint(responseText)
                    || LooksLikeFloorTargetHint(promptText)
                )
                {
                    return PlayerSpecialEffectMode.FloorDissolve;
                }
                return PlayerSpecialEffectMode.Dissolve;
            }
            if (
                normalizedResponse.Contains("[effect:")
                && (normalizedResponse.Contains("respawn") || normalizedResponse.Contains("revive"))
            )
            {
                return PlayerSpecialEffectMode.Respawn;
            }
            return PlayerSpecialEffectMode.None;
        }

        private static string ResolveSpecialIntentTargetHint(EffectIntent intent)
        {
            if (intent == null)
            {
                return string.Empty;
            }
            return !string.IsNullOrWhiteSpace(intent.anchor) ? intent.anchor : intent.target;
        }

        private static bool LooksLikePlayerTargetHint(string targetHint)
        {
            if (string.IsNullOrWhiteSpace(targetHint))
            {
                return false;
            }

            string normalizedTarget = targetHint.Trim().ToLowerInvariant();
            if (
                IsPlayerTargetToken(normalizedTarget)
                || LooksLikeExplicitPlayerTargetToken(normalizedTarget)
                || IsPlayerHeadAlias(normalizedTarget)
                || IsPlayerFeetAlias(normalizedTarget)
            )
            {
                return true;
            }

            switch (normalizedTarget)
            {
                case "self":
                case "npc":
                case "caster":
                case "speaker":
                case "listener":
                    return true;
                default:
                    return false;
            }
        }

        private static bool LooksLikeFloorTargetHint(string targetHint)
        {
            if (string.IsNullOrWhiteSpace(targetHint))
            {
                return false;
            }

            string normalizedTarget = targetHint.Trim().ToLowerInvariant();
            if (IsGroundAlias(normalizedTarget))
            {
                return true;
            }

            return normalizedTarget.Contains("role:floor", StringComparison.Ordinal)
                || normalizedTarget.Contains("role:terrain", StringComparison.Ordinal)
                || normalizedTarget.Contains("semantic:floor", StringComparison.Ordinal)
                || normalizedTarget.Contains("semantic:terrain", StringComparison.Ordinal)
                || normalizedTarget.Contains("all floor", StringComparison.Ordinal)
                || normalizedTarget.Contains("all floors", StringComparison.Ordinal)
                || normalizedTarget.Contains("floors", StringComparison.Ordinal)
                || normalizedTarget.Contains("floor", StringComparison.Ordinal)
                || normalizedTarget.Contains("ground", StringComparison.Ordinal)
                || normalizedTarget.Contains("terrain", StringComparison.Ordinal)
                || normalizedTarget.Contains("stairs", StringComparison.Ordinal)
                || normalizedTarget.Contains("stair", StringComparison.Ordinal);
        }

        private static float ResolveSpecialEffectDurationSeconds(
            ParticleParameterExtractor.ParticleParameterIntent parameterIntent,
            float baseDurationSeconds = 5f
        )
        {
            if (parameterIntent.HasExplicitDurationSeconds)
            {
                return Mathf.Clamp(parameterIntent.ExplicitDurationSeconds, 0.4f, 20f);
            }
            float durationScale = Mathf.Clamp(parameterIntent.DurationMultiplier, 0.35f, 3f);
            return Mathf.Clamp(baseDurationSeconds * durationScale, 0.4f, 20f);
        }

        private void AdjustIntentsForProbeMode(
            DialogueRequest request,
            ref List<EffectIntent> catalogIntents,
            ref bool hasCatalogIntents
        )
        {
            if (catalogIntents == null || catalogIntents.Count == 0)
            {
                hasCatalogIntents = false;
                return;
            }

            catalogIntents = catalogIntents
                .Where(intent => intent != null && !LooksLikePlaceholderEffectTag(intent.rawTagName))
                .ToList();
            if (catalogIntents.Count == 0)
            {
                NGLog.Info(
                    "DialogueFX",
                    NGLog.Format(
                        "Probe intents filtered out (placeholder/example tags)",
                        ("requestId", request.ClientRequestId)
                    )
                );
                hasCatalogIntents = false;
                return;
            }
            if (catalogIntents.Count > 1)
            {
                catalogIntents = new List<EffectIntent>(1) { catalogIntents[0] };
            }
            hasCatalogIntents = true;
        }

        private bool ApplyPlayerSpecialEffects(
            DialogueRequest request,
            ParticleParameterExtractor.ParticleParameterIntent parameterIntent,
            PlayerSpecialEffectMode specialEffectMode
        )
        {
            if (specialEffectMode == PlayerSpecialEffectMode.None)
            {
                return false;
            }

            ulong targetNetworkObjectId = ResolvePreferredListenerTargetNetworkObjectId(request);
            if (
                specialEffectMode != PlayerSpecialEffectMode.FloorDissolve
                && targetNetworkObjectId == 0
            )
            {
                string actionId = BuildActionId(
                    request,
                    0,
                    "special_effect",
                    specialEffectMode.ToString(),
                    targetNetworkObjectId
                );
                RecordActionValidationResult(
                    request,
                    0,
                    actionId,
                    "special_effect",
                    specialEffectMode.ToString(),
                    "rejected",
                    success: false,
                    "invalid_listener_target",
                    null,
                    0uL
                );
                if (m_LogDebug)
                {
                    NGLog.Warn(
                        "DialogueFX",
                        NGLog.Format(
                            "Special effect skipped (invalid listener target)",
                            ("mode", specialEffectMode.ToString()),
                            ("requestId", request.ClientRequestId)
                        )
                    );
                }
                return false;
            }

            switch (specialEffectMode)
            {
                case PlayerSpecialEffectMode.Dissolve:
                {
                    float durationSeconds = ResolveSpecialEffectDurationSeconds(parameterIntent);
                    string actionId = BuildActionId(
                        request,
                        0,
                        "special_effect",
                        "dissolve",
                        targetNetworkObjectId
                    );
                    RecordActionValidationResult(
                        request,
                        0,
                        actionId,
                        "special_effect",
                        "dissolve",
                        "validated",
                        success: true,
                        null,
                        null,
                        targetNetworkObjectId,
                        null,
                        null,
                        null,
                        0f,
                        0f,
                        durationSeconds,
                        durationSeconds
                    );
                    RecordReplicationTrace(
                        "rpc_sent",
                        "client_rpc",
                        success: true,
                        request,
                        0,
                        actionId,
                        "dissolve",
                        "dissolve",
                        0uL,
                        targetNetworkObjectId,
                        durationSeconds.ToString("F2")
                    );
                    ApplyDissolveEffectClientRpc(targetNetworkObjectId, durationSeconds, actionId);
                    RecordExecutionTrace(
                        "effect_dispatch",
                        success: true,
                        request,
                        0,
                        actionId,
                        null,
                        "special_effect",
                        "dissolve",
                        "dissolve",
                        0uL,
                        targetNetworkObjectId
                    );
                    NGLog.Info(
                        "DialogueFX",
                        NGLog.Format(
                            "Special effect applied",
                            ("mode", "dissolve"),
                            ("target", targetNetworkObjectId),
                            ("duration", durationSeconds.ToString("F2"))
                        )
                    );
                    return true;
                }
                case PlayerSpecialEffectMode.FloorDissolve:
                {
                    float durationSeconds = ResolveSpecialEffectDurationSeconds(parameterIntent, 8f);
                    string actionId = BuildActionId(
                        request,
                        0,
                        "special_effect",
                        "floor_dissolve",
                        0uL
                    );
                    RecordActionValidationResult(
                        request,
                        0,
                        actionId,
                        "special_effect",
                        "floor_dissolve",
                        "validated",
                        success: true,
                        null,
                        "ground",
                        0uL,
                        null,
                        "Area",
                        null,
                        0f,
                        0f,
                        durationSeconds,
                        durationSeconds
                    );
                    RecordReplicationTrace(
                        "rpc_sent",
                        "client_rpc",
                        success: true,
                        request,
                        0,
                        actionId,
                        "dissolve",
                        "floor_dissolve",
                        0uL,
                        0uL,
                        durationSeconds.ToString("F2")
                    );
                    ApplyFloorDissolveEffectClientRpc(durationSeconds, actionId);
                    RecordExecutionTrace(
                        "effect_dispatch",
                        success: true,
                        request,
                        0,
                        actionId,
                        null,
                        "special_effect",
                        "dissolve",
                        "floor_dissolve",
                        0uL,
                        0uL
                    );
                    NGLog.Info(
                        "DialogueFX",
                        NGLog.Format(
                            "Special effect applied",
                            ("mode", "floor_dissolve"),
                            ("duration", durationSeconds.ToString("F2"))
                        )
                    );
                    return true;
                }
                case PlayerSpecialEffectMode.Respawn:
                {
                    string actionId = BuildActionId(
                        request,
                        0,
                        "special_effect",
                        "respawn",
                        targetNetworkObjectId
                    );
                    RecordActionValidationResult(
                        request,
                        0,
                        actionId,
                        "special_effect",
                        "respawn",
                        "validated",
                        success: true,
                        null,
                        null,
                        targetNetworkObjectId
                    );
                    RecordReplicationTrace(
                        "rpc_sent",
                        "client_rpc",
                        success: true,
                        request,
                        0,
                        actionId,
                        "respawn",
                        "respawn",
                        0uL,
                        targetNetworkObjectId
                    );
                    ApplyRespawnEffectClientRpc(targetNetworkObjectId, actionId);
                    RecordExecutionTrace(
                        "effect_dispatch",
                        success: true,
                        request,
                        0,
                        actionId,
                        null,
                        "special_effect",
                        "respawn",
                        "respawn",
                        0uL,
                        targetNetworkObjectId
                    );
                    NGLog.Info(
                        "DialogueFX",
                        NGLog.Format(
                            "Special effect applied",
                            ("mode", "respawn"),
                            ("target", targetNetworkObjectId)
                        )
                    );
                    return true;
                }
                default:
                    return false;
            }
        }

        private static bool LooksLikeModelRefusal(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string normalizedText = text.ToLowerInvariant();
            if (
                normalizedText.Contains("can't assist")
                || normalizedText.Contains("cannot assist")
            )
            {
                return true;
            }
            if (normalizedText.Contains("can't help") || normalizedText.Contains("cannot help"))
            {
                return true;
            }
            return normalizedText.Contains("i'm sorry")
                && (
                    normalizedText.Contains("can't")
                    || normalizedText.Contains("cannot")
                    || normalizedText.Contains("unable")
                );
        }
    }
}
