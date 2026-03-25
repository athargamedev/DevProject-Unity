using System;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Network_Game.Dialogue.Persistence
{
    internal sealed class SupabaseRpcClient
    {
        private static readonly HttpClient s_Http = new HttpClient();
        private const int MaxLoggedBodyChars = 512;
        private static readonly JsonSerializerSettings s_JsonSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.None,
            NullValueHandling = NullValueHandling.Include,
        };

        private readonly string m_BaseUrl;
        private readonly string m_ServiceKey;

        public SupabaseRpcClient(string baseUrl, string serviceKey)
        {
            m_BaseUrl = (baseUrl ?? string.Empty).TrimEnd('/');
            m_ServiceKey = serviceKey ?? string.Empty;
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
            {
                throw new InvalidOperationException("Supabase RPC client is not configured.");
            }

            string requestUrl = $"{m_BaseUrl}/rest/v1/rpc/{functionName}";
            string json = JsonConvert.SerializeObject(payload, s_JsonSettings);

            using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", m_ServiceKey);
            request.Headers.Add("apikey", m_ServiceKey);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            HttpResponseMessage response;
            try
            {
                response = await s_Http.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken
                );
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException(
                    $"Supabase RPC {functionName} timed out while sending request to {requestUrl}."
                );
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Supabase RPC {functionName} transport failed for {requestUrl}: {FlattenExceptionMessages(ex)}",
                    ex
                );
            }

            using (response)
            {
                string body = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    string reason = string.IsNullOrWhiteSpace(response.ReasonPhrase)
                        ? "unknown"
                        : response.ReasonPhrase;
                    throw new InvalidOperationException(
                        $"Supabase RPC {functionName} failed ({(int)response.StatusCode} {reason}) at {requestUrl}. Response: {TrimForLog(body)}"
                    );
                }

                return string.IsNullOrWhiteSpace(body) ? new JObject() : JToken.Parse(body);
            }
        }

        private static string FlattenExceptionMessages(Exception ex)
        {
            if (ex == null)
            {
                return "unknown";
            }

            var builder = new StringBuilder(128);
            Exception current = ex;
            while (current != null)
            {
                if (builder.Length > 0)
                {
                    builder.Append(" --> ");
                }

                builder.Append(current.GetType().Name);
                if (!string.IsNullOrWhiteSpace(current.Message))
                {
                    builder.Append(": ");
                    builder.Append(current.Message);
                }

                current = current.InnerException;
            }

            return builder.ToString();
        }

        private static string TrimForLog(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "<empty>";
            }

            string normalized = Regex.Replace(value, "\\s+", " ").Trim();
            if (normalized.Length <= MaxLoggedBodyChars)
            {
                return normalized;
            }

            return normalized.Substring(0, MaxLoggedBodyChars) + "...";
        }

        public static string ReadString(JToken token, string propertyName)
        {
            JToken value = token?[propertyName];
            return value?.Type switch
            {
                JTokenType.String => value.Value<string>() ?? string.Empty,
                JTokenType.Null => string.Empty,
                null => string.Empty,
                _ => value.ToString(Formatting.None),
            };
        }

        public static int ReadInt(JToken token, string propertyName, int fallback = 0)
        {
            JToken value = token?[propertyName];
            if (value == null || value.Type == JTokenType.Null)
            {
                return fallback;
            }

            if (value.Type == JTokenType.Integer && int.TryParse(value.ToString(), out int direct))
            {
                return direct;
            }

            if (value.Type == JTokenType.String && int.TryParse(value.Value<string>(), out int parsed))
            {
                return parsed;
            }

            return fallback;
        }
    }

    internal sealed class DialogueEmbeddingClient
    {
        private static readonly HttpClient s_Http = new HttpClient
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };

        private readonly string m_EndpointUrl;
        private readonly string m_ApiKey;
        private readonly string m_Model;

        public DialogueEmbeddingClient(string endpointUrl, string apiKey, string model)
        {
            m_EndpointUrl = endpointUrl ?? string.Empty;
            m_ApiKey = apiKey ?? string.Empty;
            m_Model = string.IsNullOrWhiteSpace(model) ? string.Empty : model.Trim();
        }

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(m_EndpointUrl) && !string.IsNullOrWhiteSpace(m_Model);

        public async Task<float[]> CreateEmbeddingAsync(string input, CancellationToken cancellationToken)
        {
            if (!IsConfigured)
            {
                throw new InvalidOperationException("Embedding client is not configured.");
            }

            if (string.IsNullOrWhiteSpace(input))
            {
                return null;
            }

            JObject payload = new JObject
            {
                ["model"] = m_Model,
                ["input"] = input,
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, m_EndpointUrl);
            if (!string.IsNullOrWhiteSpace(m_ApiKey))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", m_ApiKey);
            }
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Content = new StringContent(
                payload.ToString(Formatting.None),
                Encoding.UTF8,
                "application/json"
            );

            using HttpResponseMessage response = await s_Http.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken
            );

            string body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Embedding request failed ({(int)response.StatusCode}): {body}"
                );
            }

            JObject parsed = JObject.Parse(body);
            if (!(parsed["data"] is JArray data) || data.Count == 0)
            {
                return null;
            }

            if (!(data[0]?["embedding"] is JArray embeddingArray) || embeddingArray.Count == 0)
            {
                return null;
            }

            var embedding = new float[embeddingArray.Count];
            for (int i = 0; i < embeddingArray.Count; i++)
            {
                JToken value = embeddingArray[i];
                if (value == null)
                {
                    embedding[i] = 0f;
                    continue;
                }

                embedding[i] = float.TryParse(
                    value.ToString(),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out float parsedValue
                )
                    ? parsedValue
                    : 0f;
            }

            return embedding;
        }
    }
}
