using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Network_Game.Diagnostics;
using System.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Network_Game.Dialogue
{
    /// <summary>
    /// HTTP client for OpenAI-compatible chat completion APIs (LM Studio, Ollama, vLLM,
    /// OpenRouter, hosted Qwen, etc.).  Platform-agnostic: uses <see cref="IWebHttpBackend"/>
    /// so the same code compiles and runs on Windows/macOS/Linux AND WebGL.
    ///
    /// Parameter mapping from DialogueBackendConfig fields to OpenAI-compat JSON:
    ///   temperature        → temperature
    ///   numPredict         → max_tokens       (-1 → omit, let server decide)
    ///   topP               → top_p
    ///   frequencyPenalty   → frequency_penalty
    ///   presencePenalty    → presence_penalty
    ///   seed               → seed             (0 → omit)
    ///   topK               → top_k            (LM Studio / llama.cpp extension)
    ///   repeatPenalty      → repeat_penalty   (LM Studio / llama.cpp extension)
    ///   minP               → min_p            (LM Studio / llama.cpp extension)
    ///   StopSequences      → stop             (chat-template guard, e.g. ["</s>","[INST]"])
    ///   ThinkingBudgetTokens → thinking.budget_tokens  (Qwen3 extended thinking, 0 = disabled)
    /// </summary>
    public class OpenAIChatClient : IDialogueInferenceClient, IDisposable
    {
        private readonly IWebHttpBackend m_Http;
        private bool m_ForceAutoModelRouting;
        private string m_LastActiveModelId = string.Empty;

        public OpenAIChatClient() : this(null) { }

        /// <param name="http">
        /// Optional backend override (e.g. for unit tests).
        /// Null → <see cref="WebHttpBackendFactory.Create"/> picks the platform default.
        /// </param>
        internal OpenAIChatClient(IWebHttpBackend http)
        {
            m_Http = http ?? WebHttpBackendFactory.Create();
        }

        // ── Connection ──────────────────────────────────────────────────────────
        public string Host { get; set; } = "localhost";
        public int Port { get; set; } = 7002;
        public string ApiKey { get; set; } = "";

        /// <summary>
        /// Model identifier sent in the request body. Leave empty for LM Studio
        /// single-model setups ("auto" is used as fallback to satisfy the spec).
        /// </summary>
        public string Model { get; set; } = "";

        // ── Standard OpenAI sampling parameters ────────────────────────────────
        public float Temperature { get; set; } = 0.2f;

        /// <summary>Max tokens to generate. -1 means omit the field (server default).</summary>
        public int MaxTokens { get; set; } = -1;

        public float TopP { get; set; } = 0.9f;
        public float FrequencyPenalty { get; set; } = 0f;
        public float PresencePenalty { get; set; } = 0f;

        /// <summary>Seed for reproducible generation. 0 means omit (random).</summary>
        public int Seed { get; set; } = 0;

        // ── LM Studio / llama.cpp extended parameters ──────────────────────────
        public int TopK { get; set; } = 40;
        public float RepeatPenalty { get; set; } = 1.1f;
        public float MinP { get; set; } = 0.05f;
        public float TypicalP { get; set; } = 1f;
        public int RepeatLastN { get; set; } = 64;
        public int Mirostat { get; set; } = 0;
        public float MirostatTau { get; set; } = 5f;
        public float MirostatEta { get; set; } = 0.1f;
        public int NProbs { get; set; } = 0;
        public bool IgnoreEos { get; set; } = false;
        public bool CachePrompt { get; set; } = true;
        public string Grammar { get; set; } = null;

        /// <summary>
        /// Stop sequences injected into every request.
        /// Null or empty = omit the field entirely.
        /// </summary>
        public string[] StopSequences { get; set; } = null;

        /// <summary>
        /// Qwen3 extended thinking budget in tokens (0 = disabled).
        /// When > 0 the request includes <c>{"thinking":{"type":"enabled","budget_tokens":N}}</c>
        /// which causes Qwen3 to reason before producing the JSON output.
        /// Improves effect/animation decision quality at the cost of latency.
        /// </summary>
        public int ThinkingBudgetTokens { get; set; } = 0;

        private string BaseUrl => $"http://{Host}:{Port}";
        public string BackendName => "openai_compatible_remote";
        public bool ManagesHistoryInternally => false;
        public string EndpointLabel => BaseUrl;
        public string LastActiveModelId => m_LastActiveModelId ?? string.Empty;
        public string EffectiveModelName => ResolveModelForRequest();

        public void ApplyConfig(DialogueInferenceRuntimeConfig config)
        {
            if (config == null)
                return;

            Host                = string.IsNullOrWhiteSpace(config.Host) ? "127.0.0.1" : config.Host.Trim();
            Port                = config.Port;
            ApiKey              = config.ApiKey ?? string.Empty;
            Model               = config.Model ?? string.Empty;
            Temperature         = config.Temperature;
            MaxTokens           = config.MaxTokens;
            TopP                = config.TopP;
            FrequencyPenalty    = config.FrequencyPenalty;
            PresencePenalty     = config.PresencePenalty;
            Seed                = config.Seed;
            TopK                = config.TopK;
            RepeatPenalty       = config.RepeatPenalty;
            MinP                = config.MinP;
            TypicalP            = config.TypicalP;
            RepeatLastN         = config.RepeatLastN;
            Mirostat            = config.Mirostat;
            MirostatTau         = config.MirostatTau;
            MirostatEta         = config.MirostatEta;
            NProbs              = config.NProbs;
            IgnoreEos           = config.IgnoreEos;
            CachePrompt         = config.CachePrompt;
            Grammar             = config.Grammar;
            StopSequences       = config.StopSequences;
            ThinkingBudgetTokens = config.ThinkingBudgetTokens;
        }

        // ── Chat completion ─────────────────────────────────────────────────────

        public async Task<string> ChatAsync(
            string systemPrompt,
            IReadOnlyList<DialogueInferenceMessage> history,
            string userPrompt,
            bool addToHistory = true,
            CancellationToken ct = default
        )
        {
            return await ChatInternalAsync(systemPrompt, history, userPrompt, null, addToHistory, ct)
                .ConfigureAwait(false);
        }

        public async Task<string> ChatWithOptionsAsync(
            string systemPrompt,
            IReadOnlyList<DialogueInferenceMessage> history,
            string userPrompt,
            DialogueInferenceRequestOptions requestOptions,
            bool addToHistory = true,
            CancellationToken ct = default
        )
        {
            return await ChatInternalAsync(systemPrompt, history, userPrompt, requestOptions, addToHistory, ct)
                .ConfigureAwait(false);
        }

        private async Task<string> ChatInternalAsync(
            string systemPrompt,
            IReadOnlyList<DialogueInferenceMessage> history,
            string userPrompt,
            DialogueInferenceRequestOptions requestOptions,
            bool addToHistory,
            CancellationToken ct
        )
        {
            var stopwatch = Stopwatch.StartNew();
            bool preferJsonResponse = requestOptions != null && requestOptions.PreferJsonResponse;
            int historyCount = history != null ? history.Count : 0;
            int messageCapacity = historyCount + 1;
            if (!string.IsNullOrWhiteSpace(systemPrompt)) messageCapacity++;
            if (preferJsonResponse) messageCapacity++;

            var messages = new List<MessageDto>(messageCapacity);
            int effectiveMaxTokens =
                requestOptions != null && requestOptions.MaxTokensOverride > 0
                ? requestOptions.MaxTokensOverride
                : MaxTokens;

            if (!string.IsNullOrWhiteSpace(systemPrompt))
                messages.Add(new MessageDto { role = "system", content = systemPrompt });

            if (preferJsonResponse)
            {
                messages.Add(new MessageDto
                {
                    role = "system",
                    content = BuildStructuredResponseInstruction(requestOptions),
                });
            }

            if (history != null)
                foreach (var msg in history)
                    messages.Add(new MessageDto { role = msg.Role, content = msg.Content });

            messages.Add(new MessageDto { role = "user", content = userPrompt });

            JObject requestBody = BuildRequestBody(messages, requestOptions, effectiveMaxTokens);
            string url = $"{BaseUrl}/v1/chat/completions";
            string json = requestBody.ToString(Formatting.None);

            LogInfo(
                $"Sending OpenAI request | url={url} | model={requestBody["model"] ?? "auto"}"
                + $" | temp={Temperature} | topK={TopK} | topP={TopP}"
                + $" | thinking={(ThinkingBudgetTokens > 0 ? ThinkingBudgetTokens.ToString() : "off")}"
                + $" | maxTokens={effectiveMaxTokens} | structured={(preferJsonResponse ? "json" : "off")}"
            );

            string responseBody = string.Empty;
            WebHttpResult result;
            try
            {
                result = await SendChatRequestAsync(url, json, ct).ConfigureAwait(false);
                responseBody = result.Body;
            }
            catch (OperationCanceledException)
            {
                LogWarn("OpenAI request cancelled.");
                return string.Empty;
            }
            catch (Exception ex)
            {
                LogWarn($"OpenAI HTTP request failed | error={ex.Message}");
                return string.Empty;
            }

            if (!result.IsSuccess && ShouldRetryWithAutoModel(result.StatusCode, responseBody, requestBody))
            {
                requestBody["model"] = "auto";
                m_ForceAutoModelRouting = true;
                string retryJson = requestBody.ToString(Formatting.None);
                LogWarn($"Configured model routing failed; retrying with auto | status={result.StatusCode}");
                try
                {
                    WebHttpResult retry = await SendChatRequestAsync(url, retryJson, ct).ConfigureAwait(false);
                    responseBody = retry.Body;
                    if (!retry.IsSuccess)
                    {
                        LogError($"OpenAI HTTP error on retry | status={retry.StatusCode} | body={Truncate(responseBody, 300)}");
                        return string.Empty;
                    }
                }
                catch (OperationCanceledException) { LogWarn("OpenAI retry cancelled."); return string.Empty; }
                catch (Exception ex) { LogWarn($"OpenAI retry failed | error={ex.Message}"); return string.Empty; }
            }
            else if (!result.IsSuccess)
            {
                LogError($"OpenAI HTTP error | status={result.StatusCode} | body={Truncate(responseBody, 300)}");
                return string.Empty;
            }

            try
            {
                var obj = JObject.Parse(responseBody);
                string content = obj?["choices"]?[0]?["message"]?["content"]?.ToString();

                if (string.IsNullOrEmpty(content))
                    LogWarn($"OpenAI empty content | body={Truncate(responseBody, 300)}");

                return content ?? string.Empty;
            }
            catch (JsonException ex)
            {
                LogError($"OpenAI JSON parse error | error={ex.Message} | body={Truncate(responseBody, 300)}");
                return string.Empty;
            }
        }

        // ── Connection probe ────────────────────────────────────────────────────

        public async Task<bool> CheckConnectionAsync(CancellationToken ct = default)
        {
            string url = $"{BaseUrl}/v1/models";
            CancellationTokenSource timeoutCts = null;
            CancellationToken effectiveToken = ct;
            if (!effectiveToken.CanBeCanceled)
            {
                timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                effectiveToken = timeoutCts.Token;
            }

            try
            {
                var headers = BuildAuthHeaders();
                WebHttpResult result = await m_Http.GetAsync(url, headers, effectiveToken).ConfigureAwait(false);

                if (!result.IsSuccess)
                {
                    LogWarn($"OpenAI connection check non-success | url={url} | status={result.StatusCode} | body={Truncate(result.Body, 300)}");
                    return false;
                }

                try
                {
                    var obj = JObject.Parse(result.Body);
                    var data = obj["data"] as JArray;
                    if (data != null && data.Count > 0)
                    {
                        string firstId = data[0]?["id"]?.ToString() ?? "(unknown)";
                        m_LastActiveModelId = firstId;
                        bool configuredAvailable = IsConfiguredModelAvailable(data);
                        if (!string.IsNullOrWhiteSpace(Model) && !configuredAvailable)
                        {
                            m_ForceAutoModelRouting = true;
                            LogWarn($"Configured model not found; forcing auto routing | configured={Model} | active={firstId}");
                        }
                        else
                        {
                            m_ForceAutoModelRouting = string.IsNullOrWhiteSpace(Model);
                        }
                        LogInfo($"LM Studio connected | active_model={firstId} | configured_model={(string.IsNullOrWhiteSpace(Model) ? "(auto)" : Model)} | total_loaded={data.Count}");
                    }
                }
                catch { /* diagnostics only */ }

                return true;
            }
            catch (OperationCanceledException)
            {
                LogWarn($"OpenAI connection check timed out | url={url}");
                return false;
            }
            catch (Exception ex)
            {
                LogWarn($"OpenAI connection check failed | url={url} | error={ex.Message}");
                return false;
            }
            finally
            {
                timeoutCts?.Dispose();
            }
        }

        public void Dispose() { }

        // ── Helpers ─────────────────────────────────────────────────────────────

        private JObject BuildRequestBody(
            List<MessageDto> messages,
            DialogueInferenceRequestOptions requestOptions,
            int effectiveMaxTokens
        )
        {
            bool preferJsonResponse = requestOptions != null && requestOptions.PreferJsonResponse;

            var body = new JObject
            {
                ["messages"]       = JArray.FromObject(messages),
                ["model"]          = ResolveModelForRequest(),
                ["temperature"]    = Temperature,
                ["top_p"]          = TopP,
                ["frequency_penalty"] = FrequencyPenalty,
                ["presence_penalty"]  = PresencePenalty,
                ["stream"]         = false,
                // LM Studio / llama.cpp extensions
                ["top_k"]          = TopK,
                ["repeat_penalty"] = RepeatPenalty,
                ["min_p"]          = MinP,
                ["typical_p"]      = TypicalP,
                ["repeat_last_n"]  = RepeatLastN,
                ["mirostat"]       = Mirostat,
                ["mirostat_tau"]   = MirostatTau,
                ["mirostat_eta"]   = MirostatEta,
                ["ignore_eos"]     = IgnoreEos,
                ["n_probs"]        = NProbs,
                ["cache_prompt"]   = CachePrompt,
            };

            if (effectiveMaxTokens > 0)
                body["max_tokens"] = effectiveMaxTokens;

            if (Seed > 0)
                body["seed"] = Seed;

            if (StopSequences != null && StopSequences.Length > 0)
                body["stop"] = JArray.FromObject(StopSequences);

            if (!preferJsonResponse && !string.IsNullOrWhiteSpace(Grammar))
                body["grammar"] = Grammar;

            if (preferJsonResponse)
                body["response_format"] = BuildStructuredResponseFormat();

            // Qwen3 extended thinking — let the model reason before emitting JSON
            int thinkingBudget = requestOptions?.ThinkingBudgetOverride >= 0
                ? requestOptions.ThinkingBudgetOverride
                : ThinkingBudgetTokens;
            if (thinkingBudget > 0)
            {
                body["thinking"] = new JObject
                {
                    ["type"]         = "enabled",
                    ["budget_tokens"] = thinkingBudget,
                };
            }

            return body;
        }

        private IEnumerable<(string, string)> BuildAuthHeaders()
        {
            if (!string.IsNullOrEmpty(ApiKey))
                yield return ("Authorization", $"Bearer {ApiKey}");
        }

        private async Task<WebHttpResult> SendChatRequestAsync(string url, string json, CancellationToken ct)
        {
            var headers = BuildAuthHeaders();
            try
            {
                return await m_Http.PostJsonAsync(url, json, headers, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException ex) when (ct.IsCancellationRequested)
            {
                LogWarn("OpenAI request cancelled by caller.");
                throw new OperationCanceledException("OpenAI request cancelled.", ex, ct);
            }
            catch (Exception ex)
            {
                LogError($"OpenAI HTTP error | url={url} | error={ex.Message}");
                throw;
            }
        }

        private string ResolveModelForRequest()
        {
            if (m_ForceAutoModelRouting) return "auto";
            string configured = string.IsNullOrWhiteSpace(Model) ? string.Empty : Model.Trim();
            if (!string.IsNullOrWhiteSpace(configured)) return configured;
            return string.IsNullOrWhiteSpace(m_LastActiveModelId) ? "auto" : m_LastActiveModelId;
        }

        private bool ShouldRetryWithAutoModel(int statusCode, string responseBody, JObject requestBody)
        {
            string currentModel = requestBody?["model"]?.ToString();
            if (string.Equals(currentModel, "auto", StringComparison.OrdinalIgnoreCase))
                return false;

            if (statusCode != 400 && statusCode != 404 && statusCode != 422)
                return false;

            string body = responseBody ?? string.Empty;
            return body.IndexOf("model", StringComparison.OrdinalIgnoreCase) >= 0
                && (body.IndexOf("not found", StringComparison.OrdinalIgnoreCase) >= 0
                    || body.IndexOf("unknown", StringComparison.OrdinalIgnoreCase) >= 0
                    || body.IndexOf("invalid", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private bool IsConfiguredModelAvailable(JArray models)
        {
            if (string.IsNullOrWhiteSpace(Model) || models == null || models.Count == 0)
                return false;
            string configured = Model.Trim();
            for (int i = 0; i < models.Count; i++)
            {
                string loaded = models[i]?["id"]?.ToString();
                if (string.Equals(loaded, configured, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= max) return s;
            return s.Substring(0, max) + "...";
        }

        private static JObject BuildStructuredResponseFormat()
        {
            return new JObject
            {
                ["type"] = "json_schema",
                ["json_schema"] = new JObject
                {
                    ["name"]   = "npc_response",
                    ["strict"] = true,
                    ["schema"] = new JObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject
                        {
                            ["speech"]  = new JObject { ["type"] = "string" },
                            ["actions"] = new JObject
                            {
                                ["type"]  = "array",
                                ["items"] = new JObject
                                {
                                    ["type"] = "object",
                                    ["properties"] = new JObject
                                    {
                                        ["type"]   = new JObject { ["type"] = "string" },
                                        ["tag"]    = new JObject { ["type"] = "string" },
                                        ["target"] = new JObject { ["type"] = "string" },
                                        ["delay"]  = new JObject { ["type"] = "number" },
                                    },
                                    ["required"] = new JArray("type", "tag"),
                                    ["additionalProperties"] = false,
                                },
                            },
                        },
                        ["required"] = new JArray("speech"),
                        ["additionalProperties"] = false,
                    },
                },
            };
        }

        private static string BuildStructuredResponseInstruction(DialogueInferenceRequestOptions options)
        {
            if (options != null && !string.IsNullOrWhiteSpace(options.StructuredResponseInstruction))
                return options.StructuredResponseInstruction.Trim();

            return "Respond with a valid JSON object only. "
                + "Use the key \"speech\" for the user-facing reply. "
                + "Optionally include an \"actions\" array where each entry has \"type\" (\"EFFECT\", \"ANIM\", or \"PATCH\"), "
                + "\"tag\" (the exact tag name), \"target\" (\"Self\" or a player/object name), and \"delay\" (seconds, default 0). "
                + "No analysis. No extra keys.";
        }

        // ── Response parsing ────────────────────────────────────────────────────

        /// <summary>
        /// Parse a raw LLM response string into a <see cref="DialogueActionResponse"/>.
        /// Handles the unified <c>{"speech":…,"actions":[…]}</c> format, the legacy
        /// <c>{"responseText":…}</c> probe format, and plain-text fallback.
        /// Also supports <c>"SEQUENCE"</c> action items (flattened with relative delays).
        /// </summary>
        internal static bool TryExtractActionResponse(string content, out DialogueActionResponse response)
        {
            response = null;
            if (string.IsNullOrWhiteSpace(content))
            {
                response = new DialogueActionResponse { Speech = string.Empty };
                return true;
            }

            try
            {
                var obj = JObject.Parse(content);

                string speech = obj["speech"]?.ToString();
                if (!string.IsNullOrWhiteSpace(speech))
                {
                    response = new DialogueActionResponse
                    {
                        Speech  = speech,
                        Actions = ParseActionArray(obj["actions"] as JArray, 0f),
                    };
                    return true;
                }

                string legacyText = obj["responseText"]?.ToString();
                if (!string.IsNullOrWhiteSpace(legacyText))
                {
                    response = new DialogueActionResponse { Speech = legacyText };
                    return true;
                }
            }
            catch (JsonException) { }

            response = new DialogueActionResponse { Speech = content };
            return true;
        }

        /// <summary>
        /// Parse an actions JArray into a flat list of <see cref="DialogueAction"/>s.
        /// SEQUENCE items are recursively expanded, with the sequence's delay added to
        /// each inner action's delay.
        /// </summary>
        private static List<DialogueAction> ParseActionArray(JArray arr, float baseDelay)
        {
            if (arr == null || arr.Count == 0)
                return null;

            var list = new List<DialogueAction>(arr.Count);
            foreach (JToken token in arr)
            {
                if (!(token is JObject item))
                    continue;

                string type = item["type"]?.ToString();
                if (string.IsNullOrWhiteSpace(type))
                    continue;

                float itemDelay = baseDelay + (item["delay"] != null ? (float)item["delay"] : 0f);

                // SEQUENCE — flatten inner actions with offset delays
                if (string.Equals(type, "SEQUENCE", StringComparison.OrdinalIgnoreCase))
                {
                    List<DialogueAction> inner = ParseActionArray(item["actions"] as JArray, itemDelay);
                    if (inner != null) list.AddRange(inner);
                    continue;
                }

                string tag = item["tag"]?.ToString();
                if (string.IsNullOrWhiteSpace(tag))
                    continue;

                list.Add(new DialogueAction
                {
                    Type           = type,
                    Tag            = tag,
                    Target         = item["target"]?.ToString() ?? "Self",
                    Delay          = itemDelay,
                    HealthDelta    = ParseOptionalFloat(item["health"]),
                    PositionOffset = ParseOptionalVector3(item["offset"]),
                    Scale          = ParseOptionalFloat(item["scale"]),
                    PatchColor     = item["color"]?.ToString(),
                    Emission       = ParseOptionalFloat(item["emission"]),
                    Visible        = ParseOptionalBool(item["visible"]),
                    Intensity      = ParseOptionalFloat(item["intensity"]),
                    Duration       = ParseOptionalFloat(item["duration"]),
                    Speed          = ParseOptionalFloat(item["speed"]),
                    Radius         = ParseOptionalFloat(item["radius"]),
                    Damage         = ParseOptionalFloat(item["damage"]),
                    EffectColor    = item["effect_color"]?.ToString() ?? item["effectColor"]?.ToString(),
                    Emotion        = item["emotion"]?.ToString(),
                });
            }

            return list.Count > 0 ? list : null;
        }

        private static float? ParseOptionalFloat(JToken token)
        {
            if (token == null) return null;
            try { return (float)token; }
            catch { return null; }
        }

        private static bool? ParseOptionalBool(JToken token)
        {
            if (token == null) return null;
            if (token.Type == JTokenType.Boolean) return (bool)token;
            if (token.Type == JTokenType.String)
            {
                string s = token.ToString().Trim().ToLowerInvariant();
                if (s == "true") return true;
                if (s == "false") return false;
            }
            return null;
        }

        private static float[] ParseOptionalVector3(JToken token)
        {
            if (!(token is JArray arr) || arr.Count < 3) return null;
            try { return new[] { (float)arr[0], (float)arr[1], (float)arr[2] }; }
            catch { return null; }
        }

        private static void LogInfo(string msg)  => NGLog.Info("OpenAI", msg);
        private static void LogWarn(string msg)  => NGLog.Warn("OpenAI", msg);
        private static void LogError(string msg) => NGLog.Error("OpenAI", msg);

        [Serializable]
        private class MessageDto
        {
            public string role;
            public string content;
        }
    }
}
