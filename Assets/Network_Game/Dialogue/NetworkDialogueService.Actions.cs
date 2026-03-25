using System;
using System.Collections;
using System.Collections.Generic;
using Network_Game.Diagnostics;
using Network_Game.Dialogue.Effects;
using Network_Game.Dialogue.Persistence;
using UnityEngine;

namespace Network_Game.Dialogue
{
    public partial class NetworkDialogueService
    {
        // Structured action dispatch and delayed action handling.
        private void TryApplyContextEffectsSafe(DialogueRequest request, string responseText)
        {
            try
            {
                ApplyContextEffects(request, responseText);
            }
            catch (Exception ex)
            {
                NGLog.Error("DialogueFX", ex.Message);
            }
        }

        private void TryApplyContextActionsSafe(
            DialogueRequest request,
            List<DialogueAction> actions,
            string speechText
        )
        {
            try
            {
                ApplyContextActions(request, actions, speechText);
            }
            catch (Exception ex)
            {
                NGLog.Error("DialogueFX", ex.Message);
            }
        }

        private void ApplyContextActions(
            DialogueRequest request,
            List<DialogueAction> actions,
            string speechText
        )
        {
            if (!m_EnableContextSceneEffects)
            {
                return;
            }

            bool hasStructuredActions = actions != null && actions.Count > 0;

            NGLog.Debug(
                "DialogueFX",
                NGLog.Format(
                    "ApplyContextActions",
                    ("hasStructured", hasStructuredActions),
                    ("actionCount", actions?.Count ?? 0)
                )
            );

            if (!hasStructuredActions)
            {
                if (
                    !string.IsNullOrWhiteSpace(speechText)
                    && (
                        DialogueAnimationDecisionPolicy.ContainsEffectTag(speechText)
                        || DialogueAnimationDecisionPolicy.ContainsAnimationTag(speechText)
                    )
                )
                {
                    ApplyContextEffects(request, speechText);
                }

                return;
            }

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

            GameObject speakerObject = ResolveSpawnedObject(normalizedRequest.SpeakerNetworkId);
            GameObject listenerObject = ResolveSpawnedObject(normalizedRequest.ListenerNetworkId);
            string effectContext = BuildEffectContextText(request.Prompt, speechText);
            ResolveEffectSpatialContext(
                effectContext,
                speakerObject,
                listenerObject,
                out Vector3 effectOrigin,
                out Vector3 effectForward,
                out _
            );

            NpcDialogueProfile profile = actor?.Profile;
            ParticleParameterExtractor.ParticleParameterIntent parameterIntent =
                profile != null && profile.EnableDynamicEffectParameters
                    ? ParticleParameterExtractor.Extract(effectContext)
                    : ParticleParameterExtractor.ParticleParameterIntent.Default;

            EnsureSceneEffectsController();

            AnimationCatalog animCatalog = null;

            foreach (DialogueAction action in actions)
            {
                if (
                    action != null
                    && string.Equals(action.Type, "PATCH", StringComparison.OrdinalIgnoreCase)
                )
                {
                    EnrichPatchFromSpeech(action, speechText);
                }
            }

            foreach (DialogueAction action in actions)
            {
                if (
                    action == null
                    || string.IsNullOrWhiteSpace(action.Type)
                    || string.IsNullOrWhiteSpace(action.Tag)
                )
                {
                    continue;
                }

                if (action.Delay > 0f)
                {
                    StartCoroutine(
                        DispatchActionAfterDelay(
                            action,
                            normalizedRequest,
                            actor,
                            animCatalog,
                            parameterIntent,
                            speechText,
                            effectOrigin,
                            effectForward,
                            speakerObject
                        )
                    );
                }
                else
                {
                    DispatchSingleAction(
                        action,
                        normalizedRequest,
                        actor,
                        ref animCatalog,
                        parameterIntent,
                        speechText,
                        effectOrigin,
                        effectForward,
                        speakerObject
                    );
                }
            }
        }

        private static bool PatchHasFields(DialogueAction action) =>
            action.HealthDelta.HasValue
            || action.PositionOffset != null
            || action.Scale.HasValue
            || !string.IsNullOrWhiteSpace(action.PatchColor)
            || action.Emission.HasValue
            || action.Visible.HasValue;

        private static readonly string[] s_InvisibleWords =
        {
            "invisible",
            "hidden",
            "hide",
            "vanish",
            "disappear",
            "cloak",
            "transparency",
            "transparent",
        };

        private static readonly string[] s_VisibleWords =
        {
            "visible",
            "appear",
            "reveal",
            "uncloak",
            "reappear",
        };

        private static readonly string[] s_GlowWords =
        {
            "glow",
            "glowing",
            "radiant",
            "bright",
            "shine",
            "shining",
            "luminous",
            "aura",
        };

        private static readonly string[] s_BigWords =
        {
            "huge",
            "giant",
            "massive",
            "enormous",
            "grow",
            "bigger",
            "larger",
            "expand",
        };

        private static readonly string[] s_SmallWords =
        {
            "tiny",
            "mini",
            "small",
            "shrink",
            "smaller",
            "diminish",
        };

        private static readonly string[] s_ColorWords =
        {
            "red",
            "blue",
            "green",
            "yellow",
            "white",
            "black",
            "cyan",
            "magenta",
            "gray",
            "grey",
            "crimson",
            "scarlet",
            "azure",
            "teal",
            "gold",
            "orange",
            "purple",
            "pink",
            "fire",
            "flame",
            "ice",
            "frost",
            "storm",
            "lightning",
            "electric",
            "shadow",
            "dark",
            "holy",
            "light",
            "divine",
            "arcane",
            "magic",
            "blood",
            "poison",
            "water",
        };

        private static void EnrichPatchFromSpeech(DialogueAction action, string speech)
        {
            if (PatchHasFields(action) || string.IsNullOrWhiteSpace(speech))
            {
                return;
            }

            string lower = speech.ToLowerInvariant();

            if (!action.Visible.HasValue)
            {
                if (ContainsAnyWord(lower, s_InvisibleWords))
                {
                    action.Visible = false;
                }
                else if (ContainsAnyWord(lower, s_VisibleWords))
                {
                    action.Visible = true;
                }
            }

            if (string.IsNullOrWhiteSpace(action.PatchColor))
            {
                foreach (string colorWord in s_ColorWords)
                {
                    if (lower.Contains(colorWord, StringComparison.Ordinal))
                    {
                        action.PatchColor = colorWord;
                        break;
                    }
                }
            }

            if (!action.Emission.HasValue && ContainsAnyWord(lower, s_GlowWords))
            {
                action.Emission = 2f;
            }

            if (!action.Scale.HasValue)
            {
                if (ContainsAnyWord(lower, s_BigWords))
                {
                    action.Scale = 2.5f;
                }
                else if (ContainsAnyWord(lower, s_SmallWords))
                {
                    action.Scale = 0.3f;
                }
            }

            if (PatchHasFields(action))
            {
                NGLog.Info(
                    "DialogueFX",
                    $"[PatchInfer] Enriched PATCH from speech | tag={action.Tag} color={action.PatchColor ?? "â€“"} visible={action.Visible?.ToString() ?? "â€“"} emission={action.Emission?.ToString() ?? "â€“"} scale={action.Scale?.ToString() ?? "â€“"}"
                );
            }
            else
            {
                NGLog.Warn(
                    "DialogueFX",
                    $"[PatchInfer] PATCH no-op: no fields from LLM or speech. tag={action.Tag} speech='{speech}'"
                );
            }
        }

        private static bool ContainsAnyWord(string text, string[] words)
        {
            for (int i = 0; i < words.Length; i++)
            {
                if (text.Contains(words[i], StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private void DispatchSingleAction(
            DialogueAction action,
            DialogueRequest request,
            NpcDialogueActor actor,
            ref AnimationCatalog animCatalog,
            ParticleParameterExtractor.ParticleParameterIntent parameterIntent,
            string speechText,
            Vector3 effectOrigin,
            Vector3 effectForward,
            GameObject speakerObject
        )
        {
            NGLog.Info(
                "DialogueFX",
                NGLog.Format(
                    "DispatchSingleAction",
                    ("type", action.Type ?? string.Empty),
                    ("tag", action.Tag ?? string.Empty),
                    ("target", action.Target ?? string.Empty),
                    ("delay", action.Delay),
                    ("scale", action.Scale?.ToString() ?? "null"),
                    ("duration", action.Duration?.ToString() ?? "null"),
                    ("color", action.EffectColor ?? "null"),
                    ("health", action.HealthDelta?.ToString() ?? "null"),
                    ("visible", action.Visible?.ToString() ?? "null")
                )
            );

            if (string.Equals(action.Type, "PATCH", StringComparison.OrdinalIgnoreCase))
            {
                NGLog.Warn(
                    "DialogueFX",
                    $"âš ï¸ LLM sent PATCH action instead of EFFECT! tag={action.Tag}, health={action.HealthDelta}, color={action.PatchColor}, scale={action.Scale}, visible={action.Visible}"
                );
            }

            if (TryDispatchStructuredSpecialEffect(action, request, parameterIntent, speechText))
            {
                return;
            }

            if (string.Equals(action.Type, "EFFECT", StringComparison.OrdinalIgnoreCase))
            {
                if (m_SceneEffectsController == null)
                {
                    NGLog.Warn(
                        "DialogueFX",
                        NGLog.Format(
                            "EFFECT skipped â€” SceneEffectsController missing",
                            ("tag", action.Tag ?? string.Empty)
                        )
                    );
                    return;
                }

                string cleanTag = action.Tag ?? string.Empty;
                int pipeIndex = cleanTag.IndexOf('|');
                if (pipeIndex > 0)
                {
                    cleanTag = cleanTag.Substring(0, pipeIndex).Trim();
                }

                EffectDefinition definition = null;
                if (actor != null)
                {
                    if (!actor.TryGetEffect(cleanTag, out definition))
                    {
                        if (
                            actor.TryFuzzyGetEffect(
                                cleanTag,
                                out definition,
                                out string fuzzyMatch
                            )
                        )
                        {
                            NGLog.Warn(
                                "DialogueFX",
                                $"[ActionDispatch] Fuzzy-matched EFFECT '{cleanTag}' â†’ '{fuzzyMatch}'."
                            );
                        }
                    }
                }

                if (definition == null)
                {
                    NGLog.Warn(
                        "DialogueFX",
                        $"[ActionDispatch] Unknown EFFECT tag '{cleanTag}' â€” not found in NPC profile effects."
                    );
                    return;
                }

                var intent = new EffectIntent
                {
                    rawTagName = cleanTag,
                    target = action.Target ?? string.Empty,
                    scale = action.Scale ?? 0f,
                    intensity = action.Intensity ?? 1f,
                    duration = action.Duration ?? 0f,
                    speed = action.Speed ?? 0f,
                    radius = action.Radius ?? 0f,
                    damage = action.Damage ?? 0f,
                    emotion = action.Emotion ?? string.Empty,
                    color = !string.IsNullOrWhiteSpace(action.EffectColor)
                        ? EffectParser.ParseColor(action.EffectColor)
                        : Color.white,
                    definition = definition,
                };

                GameObject targetObject = ResolvePreferredListenerTargetObject(request);
                string targetName = targetObject?.name ?? "unknown";

                var appropriateness = actor?.CheckEffectAppropriateness(definition);
                if (
                    appropriateness.HasValue
                    && appropriateness.Value.IsCautionary
                    && m_LogDebug
                )
                {
                    NGLog.Debug(
                        "DialogueFX",
                        $"Effect '{definition.effectTag}' caution: {appropriateness.Value.Reason}"
                    );
                }

                ApplyEffectParserIntents(
                    new List<EffectIntent>(1) { intent },
                    request,
                    effectOrigin,
                    effectForward
                );

                actor?.RecordEffectUsage(
                    definition,
                    intent.scale,
                    intent.duration,
                    targetName,
                    intent.emotion
                );

                EffectDecisionTelemetry.LogEffectDecision(
                    actor?.name ?? "unknown",
                    definition,
                    intent,
                    actor?.GetTargetContext(),
                    actor?.GetDecisionContext(),
                    true,
                    null
                );
            }
            else if (string.Equals(action.Type, "ANIM", StringComparison.OrdinalIgnoreCase))
            {
                NpcDialogueAnimationController animationController =
                    speakerObject?.GetComponent<NpcDialogueAnimationController>();
                if (animationController == null)
                {
                    return;
                }

                string syntheticTag = $"[ANIM: {action.Tag}]";
                if (
                    DialogueAnimationDecisionPolicy.TryParseFirstAnimationTag(
                        syntheticTag,
                        out var animationIntent
                    )
                )
                {
                    if (animationIntent.IsCatalogTag)
                    {
                        if (animCatalog == null)
                        {
                            animCatalog = AnimationCatalog.Instance ?? AnimationCatalog.Load();
                        }

                        if (
                            animCatalog != null
                            && animCatalog.TryGet(
                                animationIntent.RawTag,
                                out AnimationDefinition definition
                            )
                        )
                        {
                            animationController.TryPlayCatalogActionWithBroadcast(
                                definition,
                                out _
                            );
                        }
                        else if (m_LogDebug)
                        {
                            NGLog.Debug(
                                "DialogueFX",
                                $"[ActionDispatch] Unknown ANIM catalog tag '{action.Tag}'."
                            );
                        }
                    }
                    else
                    {
                        animationController.TryPlayActionWithBroadcast(
                            animationIntent.Action,
                            out _
                        );
                    }
                }
            }
            else if (string.Equals(action.Type, "PATCH", StringComparison.OrdinalIgnoreCase))
            {
                if (m_SceneEffectsController == null)
                {
                    return;
                }

                string targetName = !string.IsNullOrWhiteSpace(action.Tag)
                    ? action.Tag
                    : action.Target;
                GameObject patchTarget = null;
                if (!string.IsNullOrWhiteSpace(targetName))
                {
                    string lower = targetName.Trim().ToLowerInvariant();
                    if (lower == "self" || lower == "npc" || lower == "speaker")
                    {
                        patchTarget = speakerObject;
                    }
                    else if (lower == "listener" || lower == "player")
                    {
                        patchTarget = ResolvePreferredListenerTargetObject(request);
                    }
                    else
                    {
                        patchTarget = GameObject.Find(targetName);
                    }
                }

                if (patchTarget == null)
                {
                    patchTarget = ResolvePreferredListenerTargetObject(request);
                }

                if (patchTarget == null)
                {
                    patchTarget = speakerObject;
                }

                if (patchTarget != null)
                {
                    m_SceneEffectsController.ApplyPropertyPatches(action, patchTarget);
                }
                else if (m_LogDebug)
                {
                    NGLog.Debug(
                        "DialogueFX",
                        $"[ActionDispatch] PATCH: could not resolve target '{targetName}'."
                    );
                }
            }
            else if (string.Equals(action.Type, "SCORE", StringComparison.OrdinalIgnoreCase))
            {
                int xpDelta = action.HealthDelta.HasValue ? Mathf.RoundToInt(action.HealthDelta.Value) : 0;
                if (xpDelta == 0)
                {
                    return;
                }

                DialoguePersistenceGateway gateway = ResolveDialoguePersistenceGateway();
                if (
                    gateway != null
                    && TryResolvePersistentMemoryParticipants(
                        request,
                        out string playerKey,
                        out _
                    )
                )
                {
                    string reason = !string.IsNullOrWhiteSpace(action.Tag) ? action.Tag : "quiz_answer";
                    _ = gateway.AwardPlayerXpAsync(playerKey, xpDelta, reason);
                    if (m_LogDebug)
                    {
                        NGLog.Debug(
                            DialogueCategory,
                            NGLog.Format(
                                "SCORE awarded",
                                ("playerKey", playerKey),
                                ("xp", xpDelta),
                                ("reason", reason)
                            )
                        );
                    }
                }
            }
        }

        private IEnumerator DispatchActionAfterDelay(
            DialogueAction action,
            DialogueRequest request,
            NpcDialogueActor actor,
            AnimationCatalog animCatalog,
            ParticleParameterExtractor.ParticleParameterIntent parameterIntent,
            string speechText,
            Vector3 effectOrigin,
            Vector3 effectForward,
            GameObject speakerObject
        )
        {
            yield return new WaitForSeconds(Mathf.Max(0f, action.Delay));

            if (
                speakerObject == null
                && !string.Equals(action.Type, "EFFECT", StringComparison.OrdinalIgnoreCase)
            )
            {
                yield break;
            }

            DispatchSingleAction(
                action,
                request,
                actor,
                ref animCatalog,
                parameterIntent,
                speechText,
                effectOrigin,
                effectForward,
                speakerObject
            );
        }
    }
}
