namespace Network_Game.Diagnostics
{
    public interface ISceneWorkflowStateBridge
    {
        string ActiveBootId { get; }
        bool StartupCompleted { get; }
        bool IsMilestoneComplete(string milestone);
    }

    public static class SceneWorkflowStateBridgeRegistry
    {
        public static ISceneWorkflowStateBridge Current { get; private set; }

        public static void Register(ISceneWorkflowStateBridge bridge)
        {
            if (bridge != null)
            {
                Current = bridge;
            }
        }

        public static void Unregister(ISceneWorkflowStateBridge bridge)
        {
            if (ReferenceEquals(Current, bridge))
            {
                Current = null;
            }
        }
    }
}
