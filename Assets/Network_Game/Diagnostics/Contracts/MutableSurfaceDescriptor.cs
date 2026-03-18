using System;

namespace Network_Game.Diagnostics
{
    [Serializable]
    public struct MutableSurfaceDescriptor
    {
        public MutableSurfaceKind Kind;
        public string SurfaceId;
        public string DisplayName;
        public string[] AllowedProperties;
        public string[] AllowedOperations;
        public string RequiredAuthority;
        public bool Replicated;
    }
}
