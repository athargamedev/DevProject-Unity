using System;

namespace Network_Game.Diagnostics
{
    [Flags]
    public enum AuthorityCapability
    {
        None = 0,
        SpawnPlayer = 1 << 0,
        ApproveConnection = 1 << 1,
        DriveInput = 1 << 2,
        MutateDialogueContext = 1 << 3,
        IssueDialogueRequest = 1 << 4,
        ExecuteSceneMutation = 1 << 5,
        BroadcastEffect = 1 << 6,
    }

    public enum AuthorityRole
    {
        Unknown = 0,
        Server = 1,
        Host = 2,
        ClientOwner = 3,
        ClientObserver = 4,
    }

    public enum MutableSurfaceKind
    {
        Unknown = 0,
        Transform = 1,
        Material = 2,
        Animation = 3,
        GameplayStat = 4,
        EffectSocket = 5,
    }
}
