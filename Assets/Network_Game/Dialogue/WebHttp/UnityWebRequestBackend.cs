using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.Networking;

namespace Network_Game.Dialogue
{
    /// <summary>
    /// <see cref="IWebHttpBackend"/> backed by <c>UnityWebRequest</c>.
    /// Works on all platforms including WebGL, where <c>System.Net.Http.HttpClient</c>
    /// is not supported. Runs fully on the Unity main thread via
    /// <c>TaskCompletionSource</c> + <c>AsyncOperation.completed</c>.
    /// </summary>
    public sealed class UnityWebRequestBackend : IWebHttpBackend
    {
        public async Task<WebHttpResult> PostJsonAsync(
            string url,
            string json,
            IEnumerable<(string name, string value)> headers,
            CancellationToken ct
        )
        {
            byte[] body = Encoding.UTF8.GetBytes(json ?? string.Empty);
            using var uwr = new UnityWebRequest(url, "POST");
            uwr.uploadHandler = new UploadHandlerRaw(body);
            uwr.downloadHandler = new DownloadHandlerBuffer();
            uwr.SetRequestHeader("Content-Type", "application/json");
            ApplyHeaders(uwr, headers);

            await SendAsync(uwr, ct);

            return new WebHttpResult
            {
                StatusCode = (int)uwr.responseCode,
                Body = uwr.downloadHandler?.text ?? string.Empty,
            };
        }

        public async Task<WebHttpResult> GetAsync(
            string url,
            IEnumerable<(string name, string value)> headers,
            CancellationToken ct
        )
        {
            using var uwr = UnityWebRequest.Get(url);
            ApplyHeaders(uwr, headers);

            await SendAsync(uwr, ct);

            return new WebHttpResult
            {
                StatusCode = (int)uwr.responseCode,
                Body = uwr.downloadHandler?.text ?? string.Empty,
            };
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private static void ApplyHeaders(
            UnityWebRequest uwr,
            IEnumerable<(string name, string value)> headers
        )
        {
            if (headers == null)
                return;
            foreach (var (name, value) in headers)
                uwr.SetRequestHeader(name, value ?? string.Empty);
        }

        /// <summary>
        /// Awaitable wrapper for <see cref="UnityWebRequest.SendWebRequest"/>.
        /// Uses <c>AsyncOperation.completed</c> so it never blocks a thread.
        /// Cancellation aborts the in-flight request.
        /// </summary>
        private static Task SendAsync(UnityWebRequest uwr, CancellationToken ct)
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var op = uwr.SendWebRequest();
            op.completed += _ => tcs.TrySetResult(true);

            if (ct.CanBeCanceled)
            {
                ct.Register(() =>
                {
                    uwr.Abort();
                    tcs.TrySetCanceled();
                });
            }

            return tcs.Task;
        }
    }
}
