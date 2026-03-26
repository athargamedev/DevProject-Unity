namespace Network_Game.Dialogue
{
    /// <summary>
    /// Platform-agnostic HTTP response carrier.
    /// Returned by <see cref="IWebHttpBackend"/> implementations for both
    /// <c>System.Net.Http.HttpClient</c> (standalone) and
    /// <c>UnityWebRequest</c> (WebGL).
    /// </summary>
    public struct WebHttpResult
    {
        /// <summary>HTTP status code (e.g. 200, 404, 500).</summary>
        public int StatusCode;

        /// <summary>Response body as a UTF-8 string.</summary>
        public string Body;

        /// <summary>True when StatusCode is in the 2xx range.</summary>
        public bool IsSuccess => StatusCode >= 200 && StatusCode < 300;
    }
}
