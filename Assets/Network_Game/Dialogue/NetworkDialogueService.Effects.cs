using System.Collections.Generic;
using Network_Game.Diagnostics;
using Network_Game.Dialogue.Effects;
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
    }
}
