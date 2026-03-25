using System;
using Network_Game.Diagnostics;
using UnityEngine;

namespace Network_Game.Dialogue
{
    public partial class NetworkDialogueService
    {
        // Shared metadata, prompt-template ids, and small utility helpers.
        private void RecordExecutionTrace(
            string stage,
            bool success,
            DialogueRequest request,
            int requestId,
            string actionId = null,
            string flowId = null,
            string stageDetail = null,
            string effectType = null,
            string effectName = null,
            ulong sourceNetworkObjectId = 0UL,
            ulong targetNetworkObjectId = 0UL,
            string error = null,
            string responsePreview = null
        )
        {
            // Diagnostics removed
        }

        private void RecordActionValidationResult(
            DialogueRequest request,
            int requestId,
            string actionId,
            string actionKind,
            string actionName,
            string decision,
            bool success,
            string reason = null,
            string requestedTargetHint = null,
            ulong resolvedTargetNetworkObjectId = 0UL,
            string requestedPlacementHint = null,
            string resolvedSpatialType = null,
            string spatialReason = null,
            float requestedScale = 0f,
            float appliedScale = 0f,
            float requestedDuration = 0f,
            float appliedDuration = 0f,
            float requestedDamageRadius = 0f,
            float appliedDamageRadius = 0f,
            float requestedDamageAmount = 0f,
            float appliedDamageAmount = 0f
        )
        {
            // Diagnostics removed
        }

        private void RecordLocalEffectReceipt(
            string effectType,
            string effectName,
            string actionId = null,
            ulong sourceNetworkObjectId = 0UL,
            ulong targetNetworkObjectId = 0UL,
            string responsePreview = null
        )
        {
            // Diagnostics removed
        }

        private void RecordReplicationTrace(
            string stage,
            string networkPath,
            bool success,
            DialogueRequest request,
            int requestId,
            string actionId = null,
            string effectType = null,
            string effectName = null,
            ulong sourceNetworkObjectId = 0UL,
            ulong targetNetworkObjectId = 0UL,
            string detail = null,
            string error = null
        )
        {
            // Diagnostics removed
        }

        private static string ResolveExecutionTraceBootId()
        {
            return string.Empty;
        }

        private static string BuildExecutionTraceResponsePreview(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string normalized = value.Replace('\n', ' ').Replace('\r', ' ').Trim();
            return normalized.Length <= 120 ? normalized : normalized.Substring(0, 120);
        }

        private static string ResolveEnvelopeModelName(OpenAIChatClient openAiClient)
        {
            if (openAiClient == null)
            {
                return string.Empty;
            }

            string effective = openAiClient.EffectiveModelName;
            if (
                string.Equals(effective, "auto", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(openAiClient.LastActiveModelId)
            )
            {
                return openAiClient.LastActiveModelId;
            }

            return effective ?? string.Empty;
        }

        private string BuildSystemPromptId()
        {
            return string.IsNullOrWhiteSpace(m_DefaultSystemPromptOverride)
                ? "network_dialogue_service.default_system_prompt"
                : "network_dialogue_service.override_system_prompt";
        }

        private string BuildPromptTemplateId(
            DialogueRequest request,
            string promptText,
            DialogueInferenceRequestOptions requestOptions
        )
        {
            if (requestOptions != null && requestOptions.PreferJsonResponse)
            {
                if (
                    IsGameplayProbeRequest(request, promptText)
                    && IsEffectValidationProbePrompt(promptText)
                )
                {
                    return "dialogue.effect_probe_json";
                }

                if (
                    IsGameplayProbeRequest(request, promptText)
                    && IsAnimationValidationProbePrompt(promptText)
                )
                {
                    return "dialogue.animation_probe_json";
                }

                if (request.IsUserInitiated)
                {
                    return "dialogue.user_json";
                }

                return "dialogue.structured_json";
            }

            return "dialogue.freeform";
        }

        private static int ComputeSceneSnapshotCharCount(AuthoritativeSceneSnapshot snapshot)
        {
            int total = string.IsNullOrWhiteSpace(snapshot.SemanticSummary)
                ? 0
                : snapshot.SemanticSummary.Length;
            if (snapshot.Objects == null)
            {
                return total;
            }

            for (int i = 0; i < snapshot.Objects.Length; i++)
            {
                SceneObjectDescriptor descriptor = snapshot.Objects[i];
                total += descriptor.DisplayName != null ? descriptor.DisplayName.Length : 0;
                total += descriptor.Role != null ? descriptor.Role.Length : 0;
                total += descriptor.SemanticId != null ? descriptor.SemanticId.Length : 0;
                total += descriptor.GameplaySummary != null ? descriptor.GameplaySummary.Length : 0;
            }

            return total;
        }

        private static int EstimatePromptTokens(
            string systemPrompt,
            string promptText,
            AuthoritativeSceneSnapshot snapshot
        )
        {
            int chars =
                (systemPrompt != null ? systemPrompt.Length : 0)
                + (promptText != null ? promptText.Length : 0)
                + ComputeSceneSnapshotCharCount(snapshot);
            return Mathf.Max(1, Mathf.CeilToInt(chars / 4f));
        }

        private static string BuildPromptPreview(string promptText)
        {
            if (string.IsNullOrWhiteSpace(promptText))
            {
                return string.Empty;
            }

            string normalized = promptText.Replace('\r', ' ').Replace('\n', ' ').Trim();
            const int maxLength = 220;
            if (normalized.Length <= maxLength)
            {
                return normalized;
            }

            return normalized.Substring(0, maxLength).TrimEnd() + "...";
        }

        private DialogueInferenceRequestOptions BuildInferenceRequestOptions(
            DialogueRequest request,
            string promptText
        )
        {
            bool isGameplayProbe = IsGameplayProbeRequest(request, promptText);

            if (isGameplayProbe && IsEffectValidationProbePrompt(promptText))
            {
                return new DialogueInferenceRequestOptions
                {
                    MaxTokensOverride = EffectProbeMaxResponseTokens,
                    PreferJsonResponse = true,
                    StructuredResponseInstruction =
                        "For this request, respond with a valid JSON object only. "
                        + "Use the key \"speech\" for your reply. "
                        + "The speech value must contain one short in-character sentence followed by exactly one [EFFECT: ...] tag. "
                        + "No analysis. No extra keys.",
                };
            }

            if (isGameplayProbe && IsAnimationValidationProbePrompt(promptText))
            {
                return new DialogueInferenceRequestOptions
                {
                    MaxTokensOverride = AnimationProbeMaxResponseTokens,
                    PreferJsonResponse = true,
                    StructuredResponseInstruction =
                        "For this request, respond with a valid JSON object only. "
                        + "Use the key \"speech\" for your reply. "
                        + "The speech value must contain one short in-character sentence followed by exactly one [ANIM: ...] tag. "
                        + "Use the exact animation tag format requested in the prompt. "
                        + "No analysis. No extra keys.",
                };
            }

            if (request.IsUserInitiated)
            {
                return new DialogueInferenceRequestOptions
                {
                    MaxTokensOverride = m_RemoteDialogueResponseMaxTokens,
                    PreferJsonResponse = true,
                    StructuredResponseInstruction =
                        "Respond with a valid JSON object only. "
                        + "Put your spoken reply in \"speech\". "
                        + "Use the optional \"actions\" array for animations and effects: "
                        + "each entry needs \"type\" (\"ANIM\", \"EFFECT\", or \"PATCH\"), \"tag\", optional \"target\", and optional \"delay\" (seconds). "
                        + "PATCH is only for property edits and must include property fields like visible, color, emission, scale, health, or offset. "
                        + "Do not use PATCH for dissolve, floor_dissolve, or respawn; those must be EFFECT actions with the exact tag names. "
                        + "ANIM actions must target Self. EFFECT actions can target Self, a player name, or a scene object. "
                        + "You may combine multiple actions and stagger them with delay. "
                        + "No extra keys.",
                };
            }

            return null;
        }

        private static bool IsEffectValidationProbePrompt(string promptText)
        {
            if (string.IsNullOrWhiteSpace(promptText))
            {
                return false;
            }

            return promptText.IndexOf("Effect validation step.", StringComparison.OrdinalIgnoreCase)
                    >= 0
                || (
                    promptText.IndexOf("[EFFECT:", StringComparison.OrdinalIgnoreCase) >= 0
                    && promptText.IndexOf(
                        "append exactly one tag",
                        StringComparison.OrdinalIgnoreCase
                    ) >= 0
                );
        }

        private static bool IsAnimationValidationProbePrompt(string promptText)
        {
            if (string.IsNullOrWhiteSpace(promptText))
            {
                return false;
            }

            return promptText.IndexOf(
                    "Animation validation step.",
                    StringComparison.OrdinalIgnoreCase
                ) >= 0
                || (
                    promptText.IndexOf("[ANIM:", StringComparison.OrdinalIgnoreCase) >= 0
                    && promptText.IndexOf(
                        "append exactly one tag",
                        StringComparison.OrdinalIgnoreCase
                    ) >= 0
                );
        }
    }
}
