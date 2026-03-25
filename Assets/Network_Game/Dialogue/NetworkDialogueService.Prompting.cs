using System;
using System.Threading.Tasks;
using Network_Game.Combat;
using Network_Game.Diagnostics;
using UnityEngine;

namespace Network_Game.Dialogue
{
    public partial class NetworkDialogueService
    {
        private async Task<string> BuildSystemPromptForRequestAsync(
            DialogueRequest request,
            bool includePersistedTranscriptFallback
        )
        {
            string basePrompt = string.IsNullOrWhiteSpace(m_DefaultSystemPromptOverride)
                ? m_DefaultSystemPrompt
                : m_DefaultSystemPromptOverride;
            if (basePrompt == null)
            {
                basePrompt = string.Empty;
            }

            if (!m_EnablePersonaRouting)
            {
                string memoryOnlyPrompt = await AppendPersistentMemoryContextAsync(
                    request,
                    basePrompt,
                    includePersistedTranscriptFallback
                );
                return ApplyRemoteSystemPromptBudget(memoryOnlyPrompt);
            }

            string prompt = basePrompt;
            NpcDialogueActor actor = ResolveDialogueActorForRequest(
                request,
                out _,
                out ulong resolvedListenerNetworkId,
                out _
            );
            GameObject listenerObject = ResolveSpawnedObject(resolvedListenerNetworkId);
            if (actor != null && actor.Profile != null)
            {
                PlayerIdentityBinding listenerIdentity = ResolvePlayerIdentityForRequest(request);
                string listenerNameId = listenerIdentity?.NameId;
                if (string.IsNullOrWhiteSpace(listenerNameId) && listenerObject != null)
                {
                    listenerNameId = listenerObject.name;
                }

                if (listenerObject != null)
                {
                    CombatHealthV2 health = listenerObject.GetComponentInChildren<CombatHealthV2>();
                    actor.UpdateTargetContext(listenerObject, health);
                }

                prompt = actor.BuildSystemPrompt(
                    basePrompt,
                    request.Prompt,
                    listenerObject,
                    listenerNameId
                );
            }

            string playerContextPrompt = BuildPlayerContextPrompt(request, listenerObject);
            if (!string.IsNullOrWhiteSpace(playerContextPrompt))
            {
                prompt = string.IsNullOrWhiteSpace(prompt)
                    ? playerContextPrompt
                    : $"{prompt}\n\n{playerContextPrompt}";
            }

            prompt = await AppendPersistentMemoryContextAsync(
                request,
                prompt,
                includePersistedTranscriptFallback
            );
            return ApplyRemoteSystemPromptBudget(prompt);
        }

        private float GetEffectiveRequestTimeoutSeconds(string systemPrompt, string userPrompt)
        {
            float minRemote = Mathf.Max(120f, m_RemoteMinRequestTimeoutSeconds);
            float configured = Mathf.Max(m_RequestTimeoutSeconds, minRemote);
            int promptChars = (systemPrompt?.Length ?? 0) + (userPrompt?.Length ?? 0);
            float heuristic = 75f + (promptChars * 0.06f);
            float resolved = Mathf.Max(configured, heuristic);
            return Mathf.Clamp(resolved, minRemote, 420f);
        }

        private string ApplyRemoteSystemPromptBudget(string prompt)
        {
            if (!UseOpenAIRemote || string.IsNullOrWhiteSpace(prompt))
            {
                return prompt;
            }

            int hardCap = Mathf.Clamp(m_RemoteSystemPromptHardCapChars, 512, 16000);
            int budget = Mathf.Clamp(m_RemoteSystemPromptCharBudget, 512, hardCap);
            if (prompt.Length <= budget)
            {
                return prompt;
            }

            string[] controlSectionMarkers =
            {
                "[Response format]",
                "EFFECT FORMAT",
                "EFFECT TAGS",
                "ANIMATION TAG",
                "ANIMATIONS",
                "When using powers",
                "include an effect tag",
                "include effect tags",
                "use [anim:]",
                "use [anim:",
                "use an animation tag",
                "[effect:",
                "[anim:",
                "effect tag at the END",
            };

            int controlSectionStart = -1;
            foreach (string marker in controlSectionMarkers)
            {
                int index = prompt.LastIndexOf(marker, System.StringComparison.OrdinalIgnoreCase);
                if (index > controlSectionStart)
                {
                    controlSectionStart = index;
                }
            }

            if (controlSectionStart > 0)
            {
                int controlSectionLength = prompt.Length - controlSectionStart;
                const string trimMarker = "\n[...trimmed for latency...]\n";
                int trimMarkerLength = trimMarker.Length;
                int maxTailBudget = Mathf.Max(128, budget - trimMarkerLength - 64);
                int preservedTailLength = Mathf.Min(controlSectionLength, maxTailBudget);
                int preservedTailStart = prompt.Length - preservedTailLength;
                if (preservedTailStart > controlSectionStart)
                {
                    preservedTailStart = Mathf.Max(controlSectionStart, preservedTailStart);
                }

                int headLength = budget - preservedTailLength - trimMarkerLength;
                string tail = prompt.Substring(preservedTailStart).TrimStart();
                string trimmed = headLength >= 64
                    ? prompt.Substring(0, headLength).TrimEnd() + trimMarker + tail
                    : trimMarker + tail;

                if (m_LogDebug)
                {
                    NGLog.Warn(
                        DialogueCategory,
                        NGLog.Format(
                            "Trimmed remote system prompt (preserved structured-tag section)",
                            ("fromChars", prompt.Length),
                            ("toChars", trimmed.Length),
                            ("budget", budget),
                            ("controlSectionStart", controlSectionStart),
                            ("preservedTailLength", preservedTailLength)
                        )
                    );
                }

                return trimmed;
            }

            int head = Mathf.Clamp(Mathf.FloorToInt(budget * 0.58f), 256, budget - 256);
            int tailLength = budget - head - 24;
            if (tailLength < 128)
            {
                tailLength = 128;
                head = budget - tailLength - 24;
            }

            string fallbackTrimmed =
                prompt.Substring(0, head).TrimEnd()
                + "\n[...trimmed for latency...]\n"
                + prompt.Substring(prompt.Length - tailLength).TrimStart();

            if (m_LogDebug)
            {
                NGLog.Warn(
                    DialogueCategory,
                    NGLog.Format(
                        "Trimmed remote system prompt",
                        ("fromChars", prompt.Length),
                        ("toChars", fallbackTrimmed.Length),
                        ("budget", budget)
                    )
                );
            }

            return fallbackTrimmed;
        }
    }
}
