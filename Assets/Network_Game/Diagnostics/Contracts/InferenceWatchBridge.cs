namespace Network_Game.Diagnostics
{
    public interface IInferenceWatchBridge
    {
        void ReportInference(string prompt, string response, float elapsedMs);
    }

    public static class InferenceWatchBridgeRegistry
    {
        public static IInferenceWatchBridge Current { get; private set; }

        public static void Register(IInferenceWatchBridge bridge)
        {
            Current = bridge;
        }

        public static void Unregister(IInferenceWatchBridge bridge)
        {
            if (ReferenceEquals(Current, bridge))
            {
                Current = null;
            }
        }
    }
}
