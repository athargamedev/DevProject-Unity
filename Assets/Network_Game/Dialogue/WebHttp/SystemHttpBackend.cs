#if !UNITY_WEBGL || UNITY_EDITOR
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Network_Game.Dialogue
{
    /// <summary>
    /// <see cref="IWebHttpBackend"/> backed by <c>System.Net.Http.HttpClient</c>.
    /// Used on all non-WebGL platforms (Windows, macOS, Linux, iOS, Android, Editor).
    /// Not compiled for WebGL because <c>HttpClient</c> is unsupported there.
    /// </summary>
    public sealed class SystemHttpBackend : IWebHttpBackend
    {
        // Shared across all instances — same pattern as the previous static s_Http fields.
        private static readonly HttpClient s_Http = new HttpClient
        {
            Timeout = System.Threading.Timeout.InfiniteTimeSpan,
        };

        public async Task<WebHttpResult> PostJsonAsync(
            string url,
            string json,
            IEnumerable<(string name, string value)> headers,
            CancellationToken ct
        )
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            ApplyHeaders(request, headers);

            using HttpResponseMessage response = await s_Http.SendAsync(request, ct).ConfigureAwait(false);
            string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return new WebHttpResult { StatusCode = (int)response.StatusCode, Body = body };
        }

        public async Task<WebHttpResult> GetAsync(
            string url,
            IEnumerable<(string name, string value)> headers,
            CancellationToken ct
        )
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            ApplyHeaders(request, headers);

            using HttpResponseMessage response = await s_Http.SendAsync(request, ct).ConfigureAwait(false);
            string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return new WebHttpResult { StatusCode = (int)response.StatusCode, Body = body };
        }

        private static void ApplyHeaders(
            HttpRequestMessage request,
            IEnumerable<(string name, string value)> headers
        )
        {
            if (headers == null)
                return;
            foreach (var (name, value) in headers)
            {
                if (name == "Authorization")
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ExtractToken(value));
                else
                    request.Headers.TryAddWithoutValidation(name, value);
            }
        }

        private static string ExtractToken(string bearerValue)
        {
            const string prefix = "Bearer ";
            return bearerValue != null && bearerValue.StartsWith(prefix)
                ? bearerValue.Substring(prefix.Length)
                : bearerValue ?? string.Empty;
        }
    }
}
#endif
