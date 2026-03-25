using System;

namespace Network_Game.Diagnostics
{
    public enum AuthorityRole
    {
        Unknown = 0,
        Offline = 1,
        Client = 2,
        Server = 3,
        Host = 4,
    }

    public enum MutableSurfaceKind
    {
        Unknown = 0,
        Material = 1,
        Animation = 2,
        Transform = 3,
        Gameplay = 4,
    }
}
