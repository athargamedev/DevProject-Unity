using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Network_Game.Dialogue.Persistence
{
    /// <summary>
    /// Supabase REST RPC client — platform-agnostic via <see cref="IWebHttpBackend"/>.
    /// On standalone (server) uses <see cref="SystemHttpBackend"/>;
    /// on WebGL uses <see cref="UnityWebRequestBackend"/>.
    ///
    /// All authoritative write methods are guarded upstream by
    /// <c>EnsureAuthoritativeServerAccess()</c>, so the service-role key
    /// is only ever transmitted from the standalone NGO server, never from
    /// a WebGL client build.
    /// </summary>
    internal sealed class SupabaseRpcClient
    {
        private const int MaxLoggedBodyChars = 512;
        private static readonly JsonSerializerSettings s_JsonSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.None,
            NullValueHandling = NullValueHandling.Include,
        };

        private readonly IWebHttpBackend m_Http;
        private readonly string m_BaseUrl;
        private readonly string m_ServiceKey;

        public SupabaseRpcClient(string baseUrl, string serviceKey)
            : this(baseUrl, serviceKey, null) { }

        internal SupabaseRpcClient(string baseUrl, string serviceKey, IWebHttpBackend http)
        {
            m_BaseUrl    = (baseUrl ?? string.Empty).TrimEnd('/');
            m_ServiceKey = serviceKey ?? string.Empty;
            m_Http       = http ?? WebHttpBackendFactory.Create();
        }

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(m_BaseUrl) && !string.IsNullOrWhiteSpace(m_ServiceKey);

        public async Task<JToken> InvokeRpcAsync<TPayload>(
            string functionName,
            TPayload payload,
            CancellationToken cancellationToken
        )
        {
            if (!IsConfigured)
                throw new InvalidOperationException("Supabase RPC client is not configured.");

            string requestUrl = $"{m_BaseUrl}/rest/v1/rpc/{functionName}";
            string json = JsonConvert.SerializeObject(payload, s_JsonSettings);

            var headers = new[]
            {
                ("Authorization", $"Bearer {m_ServiceKey}"),
                ("apikey",        m_ServiceKey),
                ("Accept",        "application/json"),
            };

            WebHttpResult result;
            try
            {
                result = await m_Http.PostJsonAsync(requestUrl, json, headers, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException(
                    $"Supabase RPC {functionName} timed out while sending to {requestUrl}.");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Supabase RPC {functionName} transport failed for {requestUrl}: {FlattenExceptionMessages(ex)}", ex);
            }

            if (!result.IsSuccess)
            {
                throw new InvalidOperationException(
                    $"Supabase RPC {functionName} failed ({result.StatusCode}) at {requestUrl}. Response: {TrimForLog(result.Body)}");
            }

            return string.IsNullOrWhiteSpace(result.Body)
                ? new JObject()
                : JToken.Parse(result.Body);
        }

        private static string FlattenExceptionMessages(Exception ex)
        {
            if (ex == null) return "unknown";
            var sb = new StringBuilder(128);
            Exception current = ex;
            while (current != null)
            {
                if (sb.Length > 0) sb.Append(" --> ");
                sb.Append(current.GetType().Name);
                if (!string.IsNullOrWhiteSpace(current.Message))
                {
                    sb.Append(": ");
                    sb.Append(current.Message);
                }
                current = current.InnerException;
            }
            return sb.ToString();
        }

        private static string TrimForLog(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "<empty>";
            string normalized = Regex.Replace(value, "\\s+", " ").Trim();
            return normalized.Length <= MaxLoggedBodyChars
                ? normalized
                : normalized.Substring(0, MaxLoggedBodyChars) + "...";
        }

        public static string ReadString(JToken token, string propertyName)
        {
            JToken value = token?[propertyName];
            return value?.Type switch
            {
                JTokenType.String => value.Value<string>() ?? string.Empty,
                JTokenType.Null   => string.Empty,
                null              => string.Empty,
                _                 => value.ToString(Formatting.None),
            };
        }

        public static int ReadInt(JToken token, string propertyName, int fallback = 0)
        {
            JToken value = token?[propertyName];
            if (value == null || value.Type == JTokenType.Null)
                return fallback;
            if (value.Type == JTokenType.Integer && int.TryParse(value.ToString(), out int direct))
                return direct;
            if (value.Type == JTokenType.String && int.TryParse(value.Value<string>(), out int parsed))
                return parsed;
            return fallback;
        }
    }

    // ── Embedding client ─────────────────────────────────────────────────────────

    internal sealed class DialogueEmbeddingClient
    {
        private readonly IWebHttpBackend m_Http;
        private readonly string m_EndpointUrl;
        private readonly string m_ApiKey;
        private readonly string m_Model;

        public DialogueEmbeddingClient(string endpointUrl, string apiKey, string model)
            : this(endpointUrl, apiKey, model, null) { }

        internal DialogueEmbeddingClient(string endpointUrl, string apiKey, string model, IWebHttpBackend http)
        {
            m_EndpointUrl = endpointUrl ?? string.Empty;
            m_ApiKey      = apiKey ?? string.Empty;
            m_Model       = string.IsNullOrWhiteSpace(model) ? string.Empty : model.Trim();
            m_Http        = http ?? WebHttpBackendFactory.Create();
        }

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(m_EndpointUrl) && !string.IsNullOrWhiteSpace(m_Model);

        public async Task<float[]> CreateEmbeddingAsync(string input, CancellationToken cancellationToken)
        {
            if (!IsConfigured)
                throw new InvalidOperationException("Embedding client is not configured.");

            if (string.IsNullOrWhiteSpace(input))
                return null;

            JObject payload = new JObject { ["model"] = m_Model, ["input"] = input };

            var headers = new List<(string, string)> { ("Accept", "application/json") };
            if (!string.IsNullOrWhiteSpace(m_ApiKey))
                headers.Insert(0, ("Authorization", $"Bearer {m_ApiKey}"));

            WebHttpResult result = await m_Http.PostJsonAsync(
                m_EndpointUrl,
                payload.ToString(Formatting.None),
                headers,
                cancellationToken
            ).ConfigureAwait(false);

            if (!result.IsSuccess)
                throw new InvalidOperationException($"Embedding request failed ({result.StatusCode}): {result.Body}");

            JObject parsed = JObject.Parse(result.Body);
            if (!(parsed["data"] is JArray data) || data.Count == 0)
                return null;

            if (!(data[0]?["embedding"] is JArray embeddingArray) || embeddingArray.Count == 0)
                return null;

            var embedding = new float[embeddingArray.Count];
            for (int i = 0; i < embeddingArray.Count; i++)
            {
                JToken value = embeddingArray[i];
                embedding[i] = value != null && float.TryParse(
                    value.ToString(),
                    NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out float v) ? v : 0f;
            }
            return embedding;
        }
    }
}
