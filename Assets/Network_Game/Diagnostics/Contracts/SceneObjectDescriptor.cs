using System;
using UnityEngine;

namespace Network_Game.Diagnostics
{
    [Serializable]
    public struct SceneObjectDescriptor
    {
        public string ObjectId;
        public string SemanticId;
        public string DisplayName;
        public string Role;
        public string[] Aliases;

        public bool IsNetworkObject;
        public ulong NetworkObjectId;
        public ulong OwnerClientId;
        public bool IsSpawned;

        public Vector3 Position;
        public Vector3 EulerAngles;
        public Vector3 BoundsSize;
        public float DistanceFromProbe;

        public string RendererSummary;
        public string MaterialSummary;
        public string MeshSummary;
        public string AnimationSummary;
        public string GameplaySummary;

        public MutableSurfaceDescriptor[] MutableSurfaces;
    }
}
