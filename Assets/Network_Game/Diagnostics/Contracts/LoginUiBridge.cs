namespace Network_Game.Diagnostics
{
    public interface ILoginUiBridge
    {
        bool IsVisible { get; }
        void Show();
    }

    public static class LoginUiBridgeRegistry
    {
        public static ILoginUiBridge Current { get; private set; }

        public static void Register(ILoginUiBridge bridge)
        {
            Current = bridge;
        }

        public static void Unregister(ILoginUiBridge bridge)
        {
            if (ReferenceEquals(Current, bridge))
            {
                Current = null;
            }
        }
    }
}
