#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Network_Game.Diagnostics;
using UnityEngine;

namespace Network_Game.Dialogue
{
    /// <summary>
    /// <see cref="IDialogueInferenceClient"/> that routes requests through the
    /// Supabase <c>llm-proxy</c> Edge Function instead of directly to the LLM.
    ///
    /// <para>Use this on WebGL builds where the LLM backend is not reachable
    /// from the browser and the API key must not be shipped in the client bundle.
    /// The client authenticates with the project's public anon key, which is safe
    /// to embed in a WebGL build.</para>
    ///
    /// <para>Architecture:
    /// WebGL game → <c>POST /functions/v1/llm-proxy</c> (anon key)
    ///           → Supabase Edge Function → LLM backend (API key in Supabase secret)
    /// </para>
    /// </summary>
    public sealed class EdgeFunctionInferenceClient : IDialogueInferenceClient
    {
        private const string Category = "EdgeFunctionClient";
        private const string ConnectionProbeModel = "any";

        // ── IDialogueInferenceClient ─────────────────────────────────────────

        public string BackendName => "SupabaseEdgeFunction";

        /// <summary>
        /// The Edge Function manages only this single request; history is tracked
        /// by <see cref="OpenAIChatClient"/> (the history list is passed in each call).
        /// </summary>
        public bool ManagesHistoryInternally => false;

        // ── State ────────────────────────────────────────────────────────────

        private readonly IWebHttpBackend m_Http;
        private readonly List<DialogueInferenceMessage> m_History = new();

        private string m_EdgeFunctionUrl = string.Empty;
        private string m_AnonKey         = string.Empty;
        private string m_Model           = string.Empty;
        private float  m_Temperature     = 0.3f;
        private int    m_MaxTokens       = 2048;
        private float  m_TopP            = 0.9f;
        private string[]? m_StopSequences;
        private int    m_ThinkingBudgetTokens = 0;

        // ── Constructor ──────────────────────────────────────────────────────

        public EdgeFunctionInferenceClient() : this(null) { }

        internal EdgeFunctionInferenceClient(IWebHttpBackend? http)
        {
            m_Http = http ?? WebHttpBackendFactory.Create();
        }

        // ── IDialogueInferenceClient.ApplyConfig ─────────────────────────────

        public void ApplyConfig(DialogueInferenceRuntimeConfig config)
        {
            if (config == null) return;

            // The "host" field in the config is re-interpreted as the full Edge
            // Function URL when this client is active.
            m_EdgeFunctionUrl = BuildEdgeFunctionUrl(config.Host, config.Port);
            m_AnonKey         = config.ApiKey  ?? string.Empty;
            m_Model           = config.Model   ?? string.Empty;
            m_Temperature     = config.Temperature;
            m_MaxTokens       = config.MaxTokens;
            m_TopP            = config.TopP;
            m_StopSequences   = config.StopSequences;
            m_ThinkingBudgetTokens = config.ThinkingBudgetTokens;
        }

        // ── IDialogueInferenceClient.CheckConnectionAsync ────────────────────

        /// <summary>
        /// Sends a minimal probe request to the Edge Function and returns true
        /// if it responds with a non-5xx status.
        /// </summary>
        public async Task<bool> CheckConnectionAsync(CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(m_EdgeFunctionUrl))
            {
                NGLog.Warn(Category, "Edge Function URL is not configured.");
                return false;
            }

            // Send the smallest valid payload to verify the function is reachable.
            var probe = new JObject
            {
                ["messages"] = new JArray
                {
                    new JObject { ["role"] = "user", ["content"] = "ping" },
                },
                ["max_tokens"] = 1,
            };

            try
            {
                WebHttpResult result = await m_Http.PostJsonAsync(
                    m_EdgeFunctionUrl,
                    probe.ToString(Newtonsoft.Json.Formatting.None),
                    BuildHeaders(),
                    ct
                ).ConfigureAwait(false);

                // 2xx or 4xx (e.g. 400 for bad model) both mean the function is up.
                return result.StatusCode < 500;
            }
            catch
            {
                return false;
            }
        }

        // ── IDialogueInferenceClient.ChatAsync ───────────────────────────────

        public async Task<string> ChatAsync(
            string systemPrompt,
            IReadOnlyList<DialogueInferenceMessage> history,
            string userPrompt,
            bool addToHistory = true,
            CancellationToken ct = default
        )
        {
            if (string.IsNullOrWhiteSpace(m_EdgeFunctionUrl))
            {
                throw new InvalidOperationException(
                    "Edge Function URL is not configured. Set WebGlEdgeFunctionUrl in DialogueBackendConfig."
                );
            }

            // Build the messages array
            var messages = new JArray();

            if (!string.IsNullOrWhiteSpace(systemPrompt))
            {
                messages.Add(new JObject { ["role"] = "system", ["content"] = systemPrompt });
            }

            foreach (var msg in history)
            {
                messages.Add(new JObject { ["role"] = msg.Role, ["content"] = msg.Content });
            }

            if (!string.IsNullOrWhiteSpace(userPrompt))
            {
                messages.Add(new JObject { ["role"] = "user", ["content"] = userPrompt });
            }

            var body = new JObject { ["messages"] = messages };

            if (!string.IsNullOrWhiteSpace(m_Model))
                body["model"] = m_Model;

            body["temperature"] = m_Temperature;

            if (m_MaxTokens > 0)
                body["max_tokens"] = m_MaxTokens;

            body["top_p"] = m_TopP;
            body["stream"] = false;

            if (m_StopSequences != null && m_StopSequences.Length > 0)
                body["stop"] = new JArray(m_StopSequences);

            if (m_ThinkingBudgetTokens > 0)
            {
                body["thinking"] = new JObject
                {
                    ["type"]          = "enabled",
                    ["budget_tokens"] = m_ThinkingBudgetTokens,
                };
            }

            WebHttpResult result = await m_Http.PostJsonAsync(
                m_EdgeFunctionUrl,
                body.ToString(Newtonsoft.Json.Formatting.None),
                BuildHeaders(),
                ct
            ).ConfigureAwait(false);

            if (!result.IsSuccess)
            {
                throw new InvalidOperationException(
                    $"Edge Function llm-proxy returned {result.StatusCode}: {TrimBody(result.Body)}"
                );
            }

            string reply = ExtractReplyContent(result.Body);

            if (addToHistory)
            {
                m_History.Add(new DialogueInferenceMessage("user",      userPrompt ?? string.Empty));
                m_History.Add(new DialogueInferenceMessage("assistant", reply));
            }

            return reply;
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private IEnumerable<(string name, string value)> BuildHeaders()
        {
            yield return ("Content-Type",  "application/json");
            yield return ("Accept",        "application/json");

            if (!string.IsNullOrWhiteSpace(m_AnonKey))
            {
                yield return ("Authorization", $"Bearer {m_AnonKey}");
                yield return ("apikey",        m_AnonKey);
            }
        }

        private static string BuildEdgeFunctionUrl(string host, int port)
        {
            if (string.IsNullOrWhiteSpace(host))
                return string.Empty;

            string trimmed = host.TrimEnd('/');

            // If the host already looks like a full Edge Function URL, use it as-is.
            if (trimmed.Contains("/functions/v1/"))
                return trimmed;

            // If it already ends with /v1 treat it as the base functions path.
            if (trimmed.EndsWith("/v1", StringComparison.Ordinal))
                return trimmed + "/llm-proxy";

            // Construct the URL from host+port.
            bool isHttps  = trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
            bool isHttp   = trimmed.StartsWith("http://",  StringComparison.OrdinalIgnoreCase);
            bool hasScheme = isHttps || isHttp;

            if (!hasScheme)
                trimmed = (port == 443 ? "https://" : "http://") + trimmed;

            // Standard Supabase port for local dev is 54321.
            bool isLocalDev = port == 54321 || port == 54322;
            if (isLocalDev)
                return $"{trimmed}:{port}/functions/v1/llm-proxy";

            // For hosted Supabase (port 443/80), the Edge Function path is standard.
            if (port == 443)
                return $"{trimmed}/functions/v1/llm-proxy";

            return $"{trimmed}:{port}/functions/v1/llm-proxy";
        }

        private static string ExtractReplyContent(string responseBody)
        {
            if (string.IsNullOrWhiteSpace(responseBody))
                return string.Empty;

            try
            {
                JObject root = JObject.Parse(responseBody);

                // Standard OpenAI response: choices[0].message.content
                JArray? choices = root["choices"] as JArray;
                if (choices != null && choices.Count > 0)
                {
                    string? content = choices[0]?["message"]?["content"]?.Value<string>();
                    if (!string.IsNullOrWhiteSpace(content))
                        return content!;
                }

                // Fallback: plain error message
                string? error = root["error"]?.Value<string>()
                    ?? root["error"]?["message"]?.Value<string>();
                if (!string.IsNullOrWhiteSpace(error))
                    throw new InvalidOperationException($"LLM proxy error: {error}");
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                NGLog.Warn(Category, $"Failed to parse Edge Function response: {ex.Message}");
            }

            return responseBody;
        }

        private static string TrimBody(string body)
        {
            if (string.IsNullOrWhiteSpace(body)) return "<empty>";
            return body.Length > 256 ? body.Substring(0, 256) + "..." : body;
        }
    }
}
