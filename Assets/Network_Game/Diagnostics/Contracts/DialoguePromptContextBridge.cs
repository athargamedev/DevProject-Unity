namespace Network_Game.Diagnostics
{
    public interface IDialoguePromptContextBridge
    {
        bool IsServer { get; }
        bool SetPlayerPromptContext(ulong playerNetworkId, string nameId, string customizationJson);
        bool ClearPlayerPromptContext(ulong playerNetworkId);
        bool RequestSetPlayerPromptContextFromClient(string nameId, string customizationJson);
        bool RequestClearPlayerPromptContextFromClient();
    }

    public static class DialoguePromptContextBridgeRegistry
    {
        public static IDialoguePromptContextBridge Current { get; private set; }

        public static void Register(IDialoguePromptContextBridge bridge)
        {
            Current = bridge;
        }

        public static void Unregister(IDialoguePromptContextBridge bridge)
        {
            if (ReferenceEquals(Current, bridge))
            {
                Current = null;
            }
        }
    }
}
