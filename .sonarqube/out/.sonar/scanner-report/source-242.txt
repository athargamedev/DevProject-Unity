namespace Network_Game.Dialogue
{
    /// <summary>
    /// Selects the correct <see cref="IWebHttpBackend"/> for the current platform
    /// at compile time. WebGL uses <see cref="UnityWebRequestBackend"/>;
    /// all other platforms use <see cref="SystemHttpBackend"/>.
    /// </summary>
    public static class WebHttpBackendFactory
    {
        /// <summary>Create the appropriate HTTP backend for this build target.</summary>
        public static IWebHttpBackend Create()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return new UnityWebRequestBackend();
#else
            return new SystemHttpBackend();
#endif
        }
    }
}
