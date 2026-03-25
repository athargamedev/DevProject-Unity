using System;
using Network_Game.Diagnostics;
using Network_Game.Dialogue.Effects;
using Unity.Netcode;
using UnityEngine;

namespace Network_Game.Dialogue
{
    public partial class NetworkDialogueService
    {
        private bool TryDispatchStructuredSpecialEffect(
            DialogueAction action,
            DialogueRequest request,
            ParticleParameterExtractor.ParticleParameterIntent parameterIntent,
            string speechText
        )
        {
            if (action == null || string.IsNullOrWhiteSpace(action.Type))
            {
                return false;
            }

            bool isEffectAction = string.Equals(
                action.Type,
                "EFFECT",
                StringComparison.OrdinalIgnoreCase
            );
            bool isPatchWithoutFields = string.Equals(
                    action.Type,
                    "PATCH",
                    StringComparison.OrdinalIgnoreCase
                )
                && !PatchHasFields(action);
            if (!isEffectAction && !isPatchWithoutFields)
            {
                return false;
            }

            PlayerSpecialEffectMode specialMode = ResolveStructuredActionSpecialEffectMode(
                action,
                speechText
            );
            if (specialMode == PlayerSpecialEffectMode.None)
            {
                return false;
            }

            string targetHint = ResolveStructuredActionTargetHint(action);
            ulong targetNetworkObjectId = ResolveStructuredActionTargetNetworkObjectId(
                action,
                request
            );

            switch (specialMode)
            {
                case PlayerSpecialEffectMode.Dissolve:
                {
                    if (targetNetworkObjectId == 0)
                    {
                        NGLog.Warn(
                            "DialogueFX",
                            NGLog.Format(
                                "Structured special effect skipped (invalid dissolve target)",
                                ("tag", action.Tag ?? string.Empty),
                                ("target", targetHint ?? string.Empty)
                            )
                        );
                        return true;
                    }

                    float durationSeconds = ResolveSpecialEffectDurationSeconds(
                        parameterIntent,
                        5f
                    );
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
                        actionKind: "special_effect",
                        actionName: "dissolve",
                        decision: "validated",
                        success: true,
                        requestedTargetHint: targetHint,
                        resolvedTargetNetworkObjectId: targetNetworkObjectId,
                        requestedDuration: durationSeconds,
                        appliedDuration: durationSeconds
                    );
                    RecordReplicationTrace(
                        stage: "rpc_sent",
                        networkPath: "client_rpc",
                        success: true,
                        request,
                        0,
                        actionId: actionId,
                        effectType: "dissolve",
                        effectName: "dissolve",
                        targetNetworkObjectId: targetNetworkObjectId,
                        detail: durationSeconds.ToString("F2")
                    );
                    ApplyDissolveEffectClientRpc(targetNetworkObjectId, durationSeconds, actionId);
                    RecordExecutionTrace(
                        stage: "effect_dispatch",
                        success: true,
                        request,
                        0,
                        actionId: actionId,
                        stageDetail: "structured_special_effect",
                        effectType: "dissolve",
                        effectName: "dissolve",
                        targetNetworkObjectId: targetNetworkObjectId,
                        responsePreview: speechText
                    );
                    NGLog.Info(
                        "DialogueFX",
                        NGLog.Format(
                            "Structured action normalized to special effect",
                            ("fromType", action.Type ?? string.Empty),
                            ("mode", "dissolve"),
                            ("targetHint", targetHint ?? string.Empty),
                            ("target", targetNetworkObjectId),
                            ("duration", durationSeconds.ToString("F2"))
                        )
                    );
                    return true;
                }
                case PlayerSpecialEffectMode.FloorDissolve:
                {
                    float durationSeconds = ResolveSpecialEffectDurationSeconds(
                        parameterIntent,
                        8f
                    );
                    string actionId = BuildActionId(
                        request,
                        0,
                        "special_effect",
                        "floor_dissolve",
                        0UL
                    );
                    RecordActionValidationResult(
                        request,
                        0,
                        actionId,
                        actionKind: "special_effect",
                        actionName: "floor_dissolve",
                        decision: "validated",
                        success: true,
                        requestedTargetHint: targetHint,
                        requestedDuration: durationSeconds,
                        appliedDuration: durationSeconds,
                        resolvedSpatialType: "Area"
                    );
                    RecordReplicationTrace(
                        stage: "rpc_sent",
                        networkPath: "client_rpc",
                        success: true,
                        request,
                        0,
                        actionId: actionId,
                        effectType: "dissolve",
                        effectName: "floor_dissolve",
                        detail: durationSeconds.ToString("F2")
                    );
                    ApplyFloorDissolveEffectClientRpc(durationSeconds, actionId);
                    RecordExecutionTrace(
                        stage: "effect_dispatch",
                        success: true,
                        request,
                        0,
                        actionId: actionId,
                        stageDetail: "structured_special_effect",
                        effectType: "dissolve",
                        effectName: "floor_dissolve",
                        responsePreview: speechText
                    );
                    NGLog.Info(
                        "DialogueFX",
                        NGLog.Format(
                            "Structured action normalized to special effect",
                            ("fromType", action.Type ?? string.Empty),
                            ("mode", "floor_dissolve"),
                            ("targetHint", targetHint ?? string.Empty),
                            ("duration", durationSeconds.ToString("F2"))
                        )
                    );
                    return true;
                }
                case PlayerSpecialEffectMode.Respawn:
                {
                    if (targetNetworkObjectId == 0)
                    {
                        NGLog.Warn(
                            "DialogueFX",
                            NGLog.Format(
                                "Structured special effect skipped (invalid respawn target)",
                                ("tag", action.Tag ?? string.Empty),
                                ("target", targetHint ?? string.Empty)
                            )
                        );
                        return true;
                    }

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
                        actionKind: "special_effect",
                        actionName: "respawn",
                        decision: "validated",
                        success: true,
                        requestedTargetHint: targetHint,
                        resolvedTargetNetworkObjectId: targetNetworkObjectId
                    );
                    RecordReplicationTrace(
                        stage: "rpc_sent",
                        networkPath: "client_rpc",
                        success: true,
                        request,
                        0,
                        actionId: actionId,
                        effectType: "respawn",
                        effectName: "respawn",
                        targetNetworkObjectId: targetNetworkObjectId
                    );
                    ApplyRespawnEffectClientRpc(targetNetworkObjectId, actionId);
                    RecordExecutionTrace(
                        stage: "effect_dispatch",
                        success: true,
                        request,
                        0,
                        actionId: actionId,
                        stageDetail: "structured_special_effect",
                        effectType: "respawn",
                        effectName: "respawn",
                        targetNetworkObjectId: targetNetworkObjectId,
                        responsePreview: speechText
                    );
                    NGLog.Info(
                        "DialogueFX",
                        NGLog.Format(
                            "Structured action normalized to special effect",
                            ("fromType", action.Type ?? string.Empty),
                            ("mode", "respawn"),
                            ("targetHint", targetHint ?? string.Empty),
                            ("target", targetNetworkObjectId)
                        )
                    );
                    return true;
                }
            }

            return false;
        }

        private PlayerSpecialEffectMode ResolveStructuredActionSpecialEffectMode(
            DialogueAction action,
            string speechText
        )
        {
            if (action == null)
            {
                return PlayerSpecialEffectMode.None;
            }

            string targetHint = ResolveStructuredActionTargetHint(action);
            string semanticText =
                string.Join(
                    " ",
                    action.Type ?? string.Empty,
                    action.Tag ?? string.Empty,
                    targetHint ?? string.Empty,
                    speechText ?? string.Empty
                ).ToLowerInvariant();

            if (
                semanticText.Contains("floor_dissolve", StringComparison.Ordinal)
                || (LooksLikeFloorTargetHint(targetHint)
                    && (
                        semanticText.Contains("dissolve", StringComparison.Ordinal)
                        || semanticText.Contains("vanish", StringComparison.Ordinal)
                    ))
            )
            {
                return PlayerSpecialEffectMode.FloorDissolve;
            }

            if (
                semanticText.Contains("dissolve", StringComparison.Ordinal)
                || semanticText.Contains("vanish", StringComparison.Ordinal)
            )
            {
                return PlayerSpecialEffectMode.Dissolve;
            }

            if (
                semanticText.Contains("respawn", StringComparison.Ordinal)
                || semanticText.Contains("revive", StringComparison.Ordinal)
            )
            {
                return PlayerSpecialEffectMode.Respawn;
            }

            return PlayerSpecialEffectMode.None;
        }

        private static string ResolveStructuredActionTargetHint(DialogueAction action)
        {
            if (action == null)
            {
                return string.Empty;
            }

            if (
                string.Equals(action.Type, "PATCH", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(action.Tag)
            )
            {
                return action.Tag;
            }

            return !string.IsNullOrWhiteSpace(action.Target)
                ? action.Target
                : action.Tag ?? string.Empty;
        }

        private ulong ResolveStructuredActionTargetNetworkObjectId(
            DialogueAction action,
            DialogueRequest request
        )
        {
            string targetHint = ResolveStructuredActionTargetHint(action);
            ulong preferredListenerTargetNetworkObjectId =
                ResolvePreferredListenerTargetNetworkObjectId(request);
            if (string.IsNullOrWhiteSpace(targetHint))
            {
                return preferredListenerTargetNetworkObjectId != 0
                    ? preferredListenerTargetNetworkObjectId
                    : request.SpeakerNetworkId;
            }

            string lower = targetHint.Trim().ToLowerInvariant();
            if (LooksLikeFloorTargetHint(lower))
            {
                return 0UL;
            }

            if (LooksLikeExplicitPlayerTargetToken(lower))
            {
                return TryResolveExplicitPlayerTargetNetworkObjectId(
                    lower,
                    request,
                    out ulong explicitPlayerTargetNetworkObjectId
                )
                    ? explicitPlayerTargetNetworkObjectId
                    : 0UL;
            }

            if (lower is "self" or "npc" or "speaker" or "caster")
            {
                return request.SpeakerNetworkId != 0
                    ? request.SpeakerNetworkId
                    : request.ListenerNetworkId;
            }

            if (
                lower is "listener" or "player"
                || IsPlayerTargetToken(lower)
                || IsPlayerHeadAlias(lower)
                || IsPlayerFeetAlias(lower)
            )
            {
                return preferredListenerTargetNetworkObjectId != 0
                    ? preferredListenerTargetNetworkObjectId
                    : request.SpeakerNetworkId;
            }

            GameObject targetObject = GameObject.Find(targetHint);
            NetworkObject networkObject = targetObject != null
                ? targetObject.GetComponent<NetworkObject>()
                : null;
            if (networkObject != null)
            {
                return networkObject.NetworkObjectId;
            }

            return preferredListenerTargetNetworkObjectId != 0
                ? preferredListenerTargetNetworkObjectId
                : request.SpeakerNetworkId;
        }
    }
}
