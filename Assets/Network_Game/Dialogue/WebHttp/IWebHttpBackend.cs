using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Network_Game.Dialogue
{
    /// <summary>
    /// Minimal HTTP abstraction that lets the dialogue HTTP clients
    /// (<see cref="OpenAIChatClient"/>, <see cref="Persistence.SupabaseRpcClient"/>)
    /// stay platform-agnostic. Concrete implementations are selected at compile
    /// time via <see cref="WebHttpBackendFactory"/>.
    /// </summary>
    public interface IWebHttpBackend
    {
        /// <summary>POST <paramref name="json"/> to <paramref name="url"/>.</summary>
        Task<WebHttpResult> PostJsonAsync(
            string url,
            string json,
            IEnumerable<(string name, string value)> headers,
            CancellationToken ct
        );

        /// <summary>GET <paramref name="url"/> with optional <paramref name="headers"/>.</summary>
        Task<WebHttpResult> GetAsync(
            string url,
            IEnumerable<(string name, string value)> headers,
            CancellationToken ct
        );
    }
}
