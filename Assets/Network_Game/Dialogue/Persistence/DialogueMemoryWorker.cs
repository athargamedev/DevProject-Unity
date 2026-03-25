using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Network_Game.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.Netcode;
using UnityEngine;

namespace Network_Game.Dialogue.Persistence
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-420)]
    public sealed class DialogueMemoryWorker : MonoBehaviour
    {
        private const string Category = "DialogueMemoryWorker";
        private const int DefaultEmbeddingDimensions = 768;

        [Header("Worker")]
        [SerializeField] private bool m_EnableWorker = true;
        [SerializeField][Min(0.25f)] private float m_PollIntervalSeconds = 2f;
        [SerializeField] private bool m_RequeueStaleJobs = true;
        [SerializeField][Min(5)] private int m_StaleJobAfterSeconds = 90;
        [SerializeField][Min(1f)] private float m_StaleJobRequeueIntervalSeconds = 10f;
        [SerializeField][Min(4)] private int m_MaxTranscriptMessages = 12;
        [SerializeField][Min(64)] private int m_MaxTranscriptChars = 2400;
        [SerializeField][Min(64)] private int m_MaxSummaryChars = 280;
        [SerializeField][Min(0.5f)] private float m_InferenceTimeoutSeconds = 25f;
        [SerializeField] private bool m_LogDebug;

        [Header("Embeddings")]
        [SerializeField] private bool m_EnableEmbeddings = true;
        [SerializeField] private string m_EmbeddingEndpointUrl = string.Empty;
        [SerializeField] private string m_EmbeddingApiKey = string.Empty;
        [SerializeField] private string m_EmbeddingApiKeyEnvironmentVariable = "LMSTUDIO_API_KEY";
        [SerializeField] private string m_EmbeddingModel = "nomic-embed-text-v1.5";
        [SerializeField][Min(64)] private int m_ExpectedEmbeddingDimensions = DefaultEmbeddingDimensions;
        [SerializeField][Min(0.5f)] private float m_EmbeddingTimeoutSeconds = 15f;

        private CancellationTokenSource m_LoopCts;
        private Task m_LoopTask;
        private float m_LastStaleJobSweepAt = float.NegativeInfinity;

        private void OnEnable()
        {
            StartLoopIfNeeded();
        }

        private void Start()
        {
            StartLoopIfNeeded();
        }

        private void OnDisable()
        {
            StopLoop();
        }

        private void OnDestroy()
        {
            StopLoop();
        }

        private void StartLoopIfNeeded()
        {
            if (m_LoopTask != null || !m_EnableWorker)
            {
                return;
            }

            m_LoopCts = new CancellationTokenSource();
            m_LoopTask = RunLoopAsync(m_LoopCts.Token);
        }

        private void StopLoop()
        {
            if (m_LoopCts == null)
            {
                return;
            }

            m_LoopCts.Cancel();
            m_LoopCts.Dispose();
            m_LoopCts = null;
            m_LoopTask = null;
        }

        private async Task RunLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (!CanRun())
                {
                    await DelayAsync(1f, cancellationToken);
                    continue;
                }

                DialoguePersistenceGateway gateway = ResolveGateway();
                if (gateway == null)
                {
                    await DelayAsync(1f, cancellationToken);
                    continue;
                }

                try
                {
                    await TryRequeueStaleJobsAsync(gateway, cancellationToken);

                    string workerId = $"{Application.productName}-memory-worker";
                    JToken job = await gateway.ClaimNextMemoryJobAsync(workerId, cancellationToken);
                    if (job == null || job.Type == JTokenType.Null || job.Type == JTokenType.None)
                    {
                        await DelayAsync(m_PollIntervalSeconds, cancellationToken);
                        continue;
                    }

                    await ProcessJobAsync(gateway, job, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    NGLog.Warn(
                        Category,
                        $"Memory worker loop failed ({ex.GetType().Name}): {ex.Message}",
                        this
                    );
                    await DelayAsync(m_PollIntervalSeconds, cancellationToken);
                }
            }
        }

        private async Task TryRequeueStaleJobsAsync(
            DialoguePersistenceGateway gateway,
            CancellationToken cancellationToken
        )
        {
            if (!m_RequeueStaleJobs || gateway == null)
            {
                return;
            }

            float now = Time.realtimeSinceStartup;
            if (
                !float.IsNegativeInfinity(m_LastStaleJobSweepAt)
                && now - m_LastStaleJobSweepAt
                    < Mathf.Max(1f, m_StaleJobRequeueIntervalSeconds)
            )
            {
                return;
            }

            m_LastStaleJobSweepAt = now;
            try
            {
                await gateway.RequeueStaleMemoryJobsAsync(
                    Mathf.Max(5, m_StaleJobAfterSeconds),
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
                        Category,
                        $"Stale memory-job requeue failed ({ex.GetType().Name}): {ex.Message}",
                        this
                    );
                }
            }
        }

        private async Task ProcessJobAsync(
            DialoguePersistenceGateway gateway,
            JToken job,
            CancellationToken cancellationToken
        )
        {
            Guid jobId = TryReadGuid(job, "job_id");
            Guid sessionId = TryReadGuid(job, "session_id");
            string jobType = SupabaseRpcClient.ReadString(job, "job_type");
            string playerKey = SupabaseRpcClient.ReadString(job, "player_key");
            string npcKey = SupabaseRpcClient.ReadString(job, "npc_key");

            if (jobId == Guid.Empty)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(jobType))
            {
                await gateway.UpdateMemoryJobStatusAsync(
                    jobId,
                    "failed",
                    "memory job type is missing",
                    cancellationToken: cancellationToken
                );
                return;
            }

            if (jobType != "summarize_turns" && jobType != "summarize_session")
            {
                await gateway.UpdateMemoryJobStatusAsync(
                    jobId,
                    "failed",
                    $"memory job type '{jobType}' is not supported by the Unity worker",
                    cancellationToken: cancellationToken
                );
                return;
            }

            if (sessionId == Guid.Empty)
            {
                await gateway.UpdateMemoryJobStatusAsync(
                    jobId,
                    "failed",
                    "memory job is missing session_id",
                    cancellationToken: cancellationToken
                );
                return;
            }

            JToken transcript = await gateway.GetDialogueSessionTranscriptAsync(
                sessionId,
                Mathf.Clamp(m_MaxTranscriptMessages, 4, 32),
                cancellationToken
            );
            string transcriptBlock = BuildTranscriptBlock(transcript);
            if (string.IsNullOrWhiteSpace(transcriptBlock))
            {
                await gateway.UpdateMemoryJobStatusAsync(
                    jobId,
                    "failed",
                    "no transcript content was available for summarization",
                    cancellationToken: cancellationToken
                );
                return;
            }

            JObject memory = await SummarizeTranscriptAsync(
                playerKey,
                npcKey,
                sessionId,
                transcriptBlock,
                cancellationToken
            );
            if (memory == null)
            {
                await gateway.UpdateMemoryJobStatusAsync(
                    jobId,
                    "failed",
                    "memory worker could not parse a valid summary response",
                    cancellationToken: cancellationToken
                );
                return;
            }

            string memoryScope = NormalizeMemoryScope(memory.Value<string>("memory_scope"));
            string summary = TrimToBudget(memory.Value<string>("summary"), m_MaxSummaryChars);
            string memoryText = TrimToBudget(
                memory.Value<string>("memory_text"),
                Mathf.Max(m_MaxSummaryChars + 80, 420)
            );
            int importance = Mathf.Clamp(memory.Value<int?>("importance") ?? 5, 1, 10);

            if (string.IsNullOrWhiteSpace(summary))
            {
                summary = TrimToBudget(memoryText, m_MaxSummaryChars);
            }

            if (string.IsNullOrWhiteSpace(memoryText))
            {
                memoryText = summary;
            }

            if (string.IsNullOrWhiteSpace(summary) || string.IsNullOrWhiteSpace(memoryText))
            {
                await gateway.UpdateMemoryJobStatusAsync(
                    jobId,
                    "failed",
                    "memory worker produced an empty summary",
                    cancellationToken: cancellationToken
                );
                return;
            }

            float[] embedding = await TryGenerateEmbeddingAsync(memoryText, cancellationToken);
            JToken memoryInsert = await gateway.UpsertDialogueMemoryAsync(
                playerKey,
                npcKey,
                sessionId,
                memoryScope,
                summary,
                memoryText,
                importance,
                embedding,
                metadata: BuildMemoryMetadata(jobId, jobType),
                cancellationToken: cancellationToken
            );

            await gateway.UpdateMemoryJobStatusAsync(
                jobId,
                "completed",
                payloadPatch: new JObject
                {
                    ["memory_id"] = SupabaseRpcClient.ReadString(memoryInsert, "memory_id"),
                    ["memory_scope"] = memoryScope,
                    ["importance"] = importance,
                },
                cancellationToken: cancellationToken
            );

            if (m_LogDebug)
            {
                NGLog.Info(
                    Category,
                    NGLog.Format(
                        "Memory job completed",
                        ("jobId", jobId),
                        ("jobType", jobType),
                        ("playerKey", playerKey),
                        ("npcKey", npcKey),
                        ("memoryScope", memoryScope),
                        ("importance", importance)
                    ),
                    this
                );
            }
        }

        private async Task<JObject> SummarizeTranscriptAsync(
            string playerKey,
            string npcKey,
            Guid sessionId,
            string transcriptBlock,
            CancellationToken cancellationToken
        )
        {
            NetworkDialogueService service = ResolveDialogueService();
            if (service == null)
            {
                return null;
            }

            using OpenAIChatClient client = service.CreateConfiguredOpenAIChatClient();
            using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken
            );
            timeoutCts.CancelAfter(
                TimeSpan.FromSeconds(Mathf.Clamp(m_InferenceTimeoutSeconds, 0.5f, 60f))
            );

            string systemPrompt =
                "You are a narrative memory compressor for a multiplayer RPG. "
                + "Extract one durable memory that an NPC should remember about its relationship with the player. "
                + "Focus on stable facts, promises, strong emotional beats, threats, alliances, injuries, gifts, and identity clues. "
                + "Do not include temporary filler chatter. "
                + "Return JSON only with keys: memory_scope, summary, memory_text, importance. "
                + "memory_scope must be episodic, semantic, or profile. "
                + "importance must be an integer from 1 to 10. "
                + "summary must be concise. memory_text may be slightly longer.";

            string userPrompt =
                $"player_key: {playerKey}\n"
                + $"npc_key: {npcKey}\n"
                + $"session_id: {sessionId}\n"
                + "Create a single durable memory from this transcript.\n\n"
                + transcriptBlock;

            string raw = await client.ChatAsync(
                systemPrompt,
                null,
                userPrompt,
                addToHistory: false,
                ct: timeoutCts.Token
            );

            return TryParseJsonObject(raw, out JObject parsed) ? parsed : null;
        }

        internal bool TryCreateEmbeddingClient(out DialogueEmbeddingClient client)
        {
            client = null;
            if (!m_EnableEmbeddings)
            {
                return false;
            }

            string endpointUrl = ResolveEmbeddingEndpointUrl();
            string model = ResolveEmbeddingModel();
            if (string.IsNullOrWhiteSpace(endpointUrl) || string.IsNullOrWhiteSpace(model))
            {
                return false;
            }

            string apiKey = ResolveEmbeddingApiKey();
            if (!IsLoopbackEndpoint(endpointUrl) && string.IsNullOrWhiteSpace(apiKey))
            {
                return false;
            }

            client = new DialogueEmbeddingClient(endpointUrl, apiKey, model);
            return client.IsConfigured;
        }

        internal bool IsExpectedEmbeddingLength(float[] embedding)
        {
            if (embedding == null)
            {
                return false;
            }

            return embedding.Length == Mathf.Max(64, m_ExpectedEmbeddingDimensions);
        }

        private async Task<float[]> TryGenerateEmbeddingAsync(
            string input,
            CancellationToken cancellationToken
        )
        {
            if (!TryCreateEmbeddingClient(out DialogueEmbeddingClient client))
            {
                return null;
            }

            using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken
            );
            timeoutCts.CancelAfter(
                TimeSpan.FromSeconds(Mathf.Clamp(m_EmbeddingTimeoutSeconds, 0.5f, 60f))
            );

            try
            {
                float[] embedding = await client.CreateEmbeddingAsync(input, timeoutCts.Token);
                if (embedding == null)
                {
                    return null;
                }

                if (IsExpectedEmbeddingLength(embedding))
                {
                    return embedding;
                }

                NGLog.Warn(
                    Category,
                    $"Embedding length {embedding.Length} does not match expected dimension {Mathf.Max(64, m_ExpectedEmbeddingDimensions)}. Semantic storage will be skipped.",
                    this
                );
                return null;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                if (m_LogDebug)
                {
                    NGLog.Warn(Category, $"Embedding generation failed: {ex.Message}", this);
                }

                return null;
            }
        }

        private string BuildTranscriptBlock(JToken transcript)
        {
            if (!(transcript?["messages"] is JArray messages) || messages.Count == 0)
            {
                return string.Empty;
            }

            var builder = new StringBuilder(Mathf.Max(256, m_MaxTranscriptChars));
            builder.AppendLine("[Transcript]");

            for (int i = 0; i < messages.Count; i++)
            {
                JToken message = messages[i];
                string role = SupabaseRpcClient.ReadString(message, "speaker_role");
                string content = SupabaseRpcClient.ReadString(message, "content");
                if (string.IsNullOrWhiteSpace(content))
                {
                    continue;
                }

                content = TrimToBudget(content, 220);
                builder.Append("- ")
                    .Append(string.IsNullOrWhiteSpace(role) ? "unknown" : role)
                    .Append(": ")
                    .AppendLine(content);

                if (builder.Length >= m_MaxTranscriptChars)
                {
                    break;
                }
            }

            return builder.ToString().Trim();
        }

        private static bool TryParseJsonObject(string raw, out JObject parsed)
        {
            parsed = null;
            if (string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }

            string candidate = raw.Trim();
            int fenceStart = candidate.IndexOf('{');
            int fenceEnd = candidate.LastIndexOf('}');
            if (fenceStart >= 0 && fenceEnd > fenceStart)
            {
                candidate = candidate.Substring(fenceStart, fenceEnd - fenceStart + 1);
            }

            try
            {
                parsed = JObject.Parse(candidate);
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private static string NormalizeMemoryScope(string scope)
        {
            if (string.IsNullOrWhiteSpace(scope))
            {
                return "episodic";
            }

            scope = scope.Trim().ToLowerInvariant();
            return scope switch
            {
                "semantic" => "semantic",
                "profile" => "profile",
                _ => "episodic",
            };
        }

        private static string TrimToBudget(string value, int maxChars)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string trimmed = value.Trim();
            return trimmed.Length <= maxChars ? trimmed : trimmed.Substring(0, maxChars).TrimEnd() + "...";
        }

        private static Guid TryReadGuid(JToken token, string propertyName)
        {
            string raw = SupabaseRpcClient.ReadString(token, propertyName);
            return Guid.TryParse(raw, out Guid parsed) ? parsed : Guid.Empty;
        }

        private static JToken BuildMemoryMetadata(Guid jobId, string jobType)
        {
            return new JObject
            {
                ["source"] = nameof(DialogueMemoryWorker),
                ["job_id"] = jobId.ToString(),
                ["job_type"] = jobType ?? string.Empty,
            };
        }

        private static async Task DelayAsync(float seconds, CancellationToken cancellationToken)
        {
            int delayMs = Mathf.Max(50, Mathf.RoundToInt(seconds * 1000f));
            await Task.Delay(delayMs, cancellationToken);
        }

        private bool CanRun()
        {
            NetworkManager manager = NetworkManager.Singleton;
            return m_EnableWorker && manager != null && manager.IsServer;
        }

        private DialoguePersistenceGateway ResolveGateway()
        {
            return FindAnyObjectByType<DialoguePersistenceGateway>(FindObjectsInactive.Exclude);
        }

        private NetworkDialogueService ResolveDialogueService()
        {
            return NetworkDialogueService.Instance
                ?? FindAnyObjectByType<NetworkDialogueService>(FindObjectsInactive.Exclude);
        }

        private string ResolveEmbeddingApiKey()
        {
            if (!string.IsNullOrWhiteSpace(m_EmbeddingApiKey))
            {
                return m_EmbeddingApiKey.Trim();
            }

            if (!string.IsNullOrWhiteSpace(m_EmbeddingApiKeyEnvironmentVariable))
            {
                string configured = Environment.GetEnvironmentVariable(
                    m_EmbeddingApiKeyEnvironmentVariable.Trim()
                );
                if (!string.IsNullOrWhiteSpace(configured))
                {
                    return configured.Trim();
                }
            }

            string localConfigured = Environment.GetEnvironmentVariable("LMSTUDIO_API_KEY");
            if (!string.IsNullOrWhiteSpace(localConfigured))
            {
                return localConfigured.Trim();
            }

            string openAiConfigured = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            if (!string.IsNullOrWhiteSpace(openAiConfigured))
            {
                return openAiConfigured.Trim();
            }

            return string.Empty;
        }

        private string ResolveEmbeddingEndpointUrl()
        {
            if (!string.IsNullOrWhiteSpace(m_EmbeddingEndpointUrl))
            {
                return m_EmbeddingEndpointUrl.Trim();
            }

            NetworkDialogueService service = ResolveDialogueService();
            if (service == null)
            {
                return string.Empty;
            }

            DialogueInferenceRuntimeConfig config = service.CreateInferenceRuntimeConfigSnapshot();
            if (config == null || string.IsNullOrWhiteSpace(config.Host) || config.Port <= 0)
            {
                return string.Empty;
            }

            return $"http://{config.Host}:{config.Port}/v1/embeddings";
        }

        private string ResolveEmbeddingModel()
        {
            return string.IsNullOrWhiteSpace(m_EmbeddingModel) ? string.Empty : m_EmbeddingModel.Trim();
        }

        private static bool IsLoopbackEndpoint(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
            {
                return false;
            }

            return string.Equals(uri.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase);
        }
    }
}
