using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Network_Game.Diagnostics;
using Network_Game.Diagnostics.Contracts;
using Network_Game.Dialogue.Persistence;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Network_Game.Dialogue
{
    public partial class NetworkDialogueService
    {
        // Persistent transcript recall and semantic memory injection for prompts.
        private async Task<string> AppendPersistentMemoryContextAsync(
            DialogueRequest request,
            string prompt,
            bool includePersistedTranscriptFallback
        )
        {
            string memoryContext = await BuildPersistentMemoryPromptContextAsync(
                request,
                includePersistedTranscriptFallback
            );
            if (string.IsNullOrWhiteSpace(memoryContext))
            {
                return prompt;
            }

            return string.IsNullOrWhiteSpace(prompt) ? memoryContext : $"{prompt}\n\n{memoryContext}";
        }

        private async Task<string> BuildPersistentMemoryPromptContextAsync(
            DialogueRequest request,
            bool includePersistedTranscriptFallback
        )
        {
            if (!m_EnablePersistentMemoryRecall || !IsServer)
            {
                return string.Empty;
            }

            DialoguePersistenceGateway gateway = ResolveDialoguePersistenceGateway();
            if (gateway == null)
            {
                return string.Empty;
            }

            if (
                !TryResolvePersistentMemoryParticipants(
                    request,
                    out string playerKey,
                    out string npcKey
                )
            )
            {
                return string.Empty;
            }

            int messageLimit = includePersistedTranscriptFallback
                ? Mathf.Clamp(m_PersistentMemoryMaxRecentMessages, 1, 8)
                : 0;
            int memoryLimit = Mathf.Clamp(m_PersistentMemoryMaxSummaries, 1, 8);
            float timeoutSeconds = Mathf.Clamp(m_PersistentMemoryFetchTimeoutSeconds, 0.1f, 5f);

            try
            {
                using CancellationTokenSource cts = new CancellationTokenSource(
                    TimeSpan.FromSeconds(timeoutSeconds)
                );
                JToken context = await gateway.GetRecentDialogueContextAsync(
                    playerKey,
                    npcKey,
                    messageLimit,
                    memoryLimit,
                    cts.Token
                );
                JToken semanticMatches = await TryFetchSemanticMemoryMatchesAsync(
                    request,
                    gateway,
                    playerKey,
                    npcKey,
                    cts.Token
                );
                JToken knowledgeMatches = await TryFetchNpcKnowledgeContextAsync(
                    request,
                    gateway,
                    npcKey,
                    cts.Token
                );
                string built = FormatPersistentMemoryPromptContext(
                    context,
                    semanticMatches,
                    knowledgeMatches,
                    includePersistedTranscriptFallback
                );
                if (m_LogDebug && !string.IsNullOrWhiteSpace(built))
                {
                    NGLog.Debug(
                        DialogueCategory,
                        NGLog.Format(
                            "Persistent memory context resolved",
                            ("playerKey", playerKey),
                            ("npcKey", npcKey),
                            ("chars", built.Length),
                            ("includeTranscript", includePersistedTranscriptFallback)
                        )
                    );
                }

                return built;
            }
            catch (OperationCanceledException)
            {
                if (m_LogDebug)
                {
                    NGLog.Warn(
                        DialogueCategory,
                        NGLog.Format(
                            "Persistent memory lookup timed out",
                            ("playerKey", playerKey),
                            ("npcKey", npcKey),
                            ("timeoutSec", timeoutSeconds)
                        )
                    );
                }
            }
            catch (Exception ex)
            {
                if (m_LogDebug)
                {
                    NGLog.Warn(
                        DialogueCategory,
                        NGLog.Format(
                            "Persistent memory lookup failed",
                            ("playerKey", playerKey),
                            ("npcKey", npcKey),
                            ("error", ex.Message ?? string.Empty)
                        )
                    );
                }
            }

            return string.Empty;
        }

        private string FormatPersistentMemoryPromptContext(
            JToken root,
            JToken semanticMatchesRoot,
            JToken knowledgeMatchesRoot,
            bool includePersistedTranscriptFallback
        )
        {
            StringBuilder builder = new StringBuilder(512);
            int appendedSections = 0;
            JArray semanticMatches = semanticMatchesRoot?["matches"] as JArray;
            bool hasSemanticMatches = semanticMatches != null && semanticMatches.Count > 0;

            if (
                includePersistedTranscriptFallback
                && root?["recent_messages"] is JArray recentMessages
                && recentMessages.Count > 0
            )
            {
                builder.AppendLine("[Persisted Recent Dialogue]");
                builder.AppendLine(
                    "Use this only if it helps continuity. Do not restate it unless relevant."
                );
                foreach (JToken message in recentMessages)
                {
                    string role = ReadJsonString(message, "speaker_role", "unknown");
                    string content = TrimPromptSegment(ReadJsonString(message, "content"), 180);
                    if (string.IsNullOrWhiteSpace(content))
                    {
                        continue;
                    }

                    builder.Append("- ").Append(role).Append(": ").AppendLine(content);
                }

                appendedSections++;
            }

            if (hasSemanticMatches)
            {
                if (builder.Length > 0)
                {
                    builder.AppendLine();
                }

                builder.AppendLine("[Relevant Long-Term Memories]");
                builder.AppendLine(
                    "Use only the memories that directly help this reply. Do not quote them verbatim."
                );
                foreach (JToken memory in semanticMatches)
                {
                    string summary = TrimPromptSegment(ReadJsonString(memory, "summary"), 220);
                    if (string.IsNullOrWhiteSpace(summary))
                    {
                        summary = TrimPromptSegment(ReadJsonString(memory, "memory_text"), 220);
                    }

                    if (string.IsNullOrWhiteSpace(summary))
                    {
                        continue;
                    }

                    string scope = ReadJsonString(memory, "memory_scope", "memory");
                    builder.Append("- ").Append(scope).Append(": ").AppendLine(summary);
                }

                appendedSections++;
            }

            if (
                !hasSemanticMatches
                && root?["recent_memories"] is JArray recentMemories
                && recentMemories.Count > 0
            )
            {
                if (builder.Length > 0)
                {
                    builder.AppendLine();
                }

                builder.AppendLine("[Persistent Memory Summaries]");
                builder.AppendLine(
                    "Treat these as durable prior knowledge about this player relationship."
                );
                foreach (JToken memory in recentMemories)
                {
                    string summary = TrimPromptSegment(ReadJsonString(memory, "summary"), 220);
                    if (string.IsNullOrWhiteSpace(summary))
                    {
                        summary = TrimPromptSegment(ReadJsonString(memory, "memory_text"), 220);
                    }

                    if (string.IsNullOrWhiteSpace(summary))
                    {
                        continue;
                    }

                    string scope = ReadJsonString(memory, "memory_scope", "memory");
                    builder.Append("- ").Append(scope).Append(": ").AppendLine(summary);
                }

                appendedSections++;
            }

            if (knowledgeMatchesRoot is JArray knowledgeMatches && knowledgeMatches.Count > 0)
            {
                if (builder.Length > 0)
                {
                    builder.AppendLine();
                }

                builder.AppendLine("[Code Context]");
                builder.AppendLine(
                    "These are relevant excerpts from the project codebase. Use them to answer architecture questions or ask the player quiz questions about the code."
                );
                foreach (JToken entry in knowledgeMatches)
                {
                    string title = ReadJsonString(entry, "title", "unknown");
                    string content = TrimPromptSegment(ReadJsonString(entry, "content"), 280);
                    if (string.IsNullOrWhiteSpace(content))
                    {
                        continue;
                    }

                    builder.Append("- ").Append(title).Append(": ").AppendLine(content);
                }

                appendedSections++;
            }

            return appendedSections > 0 ? builder.ToString().Trim() : string.Empty;
        }

        private async Task<JToken> TryFetchSemanticMemoryMatchesAsync(
            DialogueRequest request,
            DialoguePersistenceGateway gateway,
            string playerKey,
            string npcKey,
            CancellationToken cancellationToken
        )
        {
            if (!m_EnablePersistentSemanticRecall || gateway == null)
            {
                return null;
            }

            string query = request.Prompt?.Trim();
            if (string.IsNullOrWhiteSpace(query))
            {
                return null;
            }

            DialogueMemoryWorker worker = ResolveDialogueMemoryWorker();
            if (worker == null || !worker.TryCreateEmbeddingClient(out DialogueEmbeddingClient client))
            {
                return null;
            }

            try
            {
                float[] embedding = await client.CreateEmbeddingAsync(query, cancellationToken);
                if (embedding == null || embedding.Length == 0)
                {
                    return null;
                }

                if (!worker.IsExpectedEmbeddingLength(embedding))
                {
                    if (m_LogDebug)
                    {
                        NGLog.Warn(
                            DialogueCategory,
                            NGLog.Format(
                                "Persistent semantic recall skipped due to embedding dimension mismatch",
                                ("playerKey", playerKey),
                                ("npcKey", npcKey),
                                ("length", embedding.Length)
                            )
                        );
                    }

                    return null;
                }

                return await gateway.MatchDialogueMemoriesAsync(
                    playerKey,
                    npcKey,
                    embedding,
                    Mathf.Clamp(m_PersistentSemanticRecallMaxMatches, 1, 8),
                    Mathf.Clamp(m_PersistentSemanticRecallThreshold, 0.1f, 1f),
                    cancellationToken
                );
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (m_LogDebug)
                {
                    NGLog.Warn(
                        DialogueCategory,
                        NGLog.Format(
                            "Persistent semantic recall failed",
                            ("playerKey", playerKey),
                            ("npcKey", npcKey),
                            ("error", ex.Message ?? string.Empty)
                        )
                    );
                }

                return null;
            }
        }

        private async Task<JToken> TryFetchNpcKnowledgeContextAsync(
            DialogueRequest request,
            DialoguePersistenceGateway gateway,
            string npcKey,
            CancellationToken cancellationToken
        )
        {
            if (!m_EnablePersistentSemanticRecall || gateway == null)
            {
                return null;
            }

            string query = request.Prompt?.Trim();
            if (string.IsNullOrWhiteSpace(query))
            {
                return null;
            }

            DialogueMemoryWorker worker = ResolveDialogueMemoryWorker();
            if (worker == null || !worker.TryCreateEmbeddingClient(out DialogueEmbeddingClient client))
            {
                return null;
            }

            try
            {
                float[] embedding = await client.CreateEmbeddingAsync(query, cancellationToken);
                if (embedding == null || embedding.Length == 0)
                {
                    return null;
                }

                if (!worker.IsExpectedEmbeddingLength(embedding))
                {
                    return null;
                }

                return await gateway.SearchNpcKnowledgeAsync(
                    npcKey,
                    embedding,
                    Mathf.Clamp(m_PersistentSemanticRecallMaxMatches, 1, 5),
                    Mathf.Clamp(m_PersistentSemanticRecallThreshold, 0.1f, 1f),
                    cancellationToken
                );
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (m_LogDebug)
                {
                    NGLog.Warn(
                        DialogueCategory,
                        NGLog.Format(
                            "NPC knowledge recall failed",
                            ("npcKey", npcKey),
                            ("error", ex.Message ?? string.Empty)
                        )
                    );
                }

                return null;
            }
        }

        private DialoguePersistenceGateway ResolveDialoguePersistenceGateway()
        {
            if (m_DialoguePersistenceGateway != null)
            {
                return m_DialoguePersistenceGateway;
            }

            m_DialoguePersistenceGateway = FindAnyObjectByType<DialoguePersistenceGateway>(
                FindObjectsInactive.Exclude
            );
            return m_DialoguePersistenceGateway;
        }

        private DialogueMemoryWorker ResolveDialogueMemoryWorker()
        {
            if (m_DialogueMemoryWorker != null)
            {
                return m_DialogueMemoryWorker;
            }

            m_DialogueMemoryWorker = FindAnyObjectByType<DialogueMemoryWorker>(
                FindObjectsInactive.Exclude
            );
            return m_DialogueMemoryWorker;
        }

        private bool TryResolvePersistentMemoryParticipants(
            DialogueRequest request,
            out string playerKey,
            out string npcKey
        )
        {
            playerKey = string.Empty;
            npcKey = string.Empty;

            NpcDialogueActor actor = ResolveDialogueActorForRequest(request, out _, out _, out _);
            if (actor == null)
            {
                return false;
            }

            npcKey = !string.IsNullOrWhiteSpace(actor.ProfileId)
                ? actor.ProfileId.Trim()
                : actor.gameObject.name ?? string.Empty;
            if (string.IsNullOrWhiteSpace(npcKey))
            {
                return false;
            }

            PlayerIdentityBinding identity = ResolvePlayerIdentityForRequest(request);
            if (identity != null && !string.IsNullOrWhiteSpace(identity.NameId))
            {
                playerKey = identity.NameId.Trim();
                return true;
            }

            if (
                ProviderRegistry.PlayerRuntimeState.TryGetPlayerRuntimeState(
                    request.RequestingClientId,
                    out var fallbackData
                )
                && !string.IsNullOrWhiteSpace(fallbackData.PlayerKey)
            )
            {
                playerKey = fallbackData.PlayerKey.Trim();
                return true;
            }

            if (request.RequestingClientId != 0)
            {
                playerKey = $"player_{request.RequestingClientId}";
                return true;
            }

            return false;
        }

        private static string ReadJsonString(
            JToken element,
            string propertyName,
            string fallback = ""
        )
        {
            JToken value = element?[propertyName];
            if (value == null || value.Type == JTokenType.Null)
            {
                return fallback;
            }

            if (value.Type == JTokenType.String)
            {
                return value.Value<string>() ?? fallback;
            }

            return value.Type == JTokenType.Object || value.Type == JTokenType.Array
                ? fallback
                : value.ToString();
        }
    }
}
