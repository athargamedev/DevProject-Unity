using System;
using UnityEngine;

namespace Network_Game.Diagnostics
{
    [Serializable]
    public struct AuthoritativeSceneSnapshot
    {
        public string SnapshotId;
        public string SnapshotHash;
        public string SceneName;
        public int Frame;
        public float RealtimeSinceStartup;

        public ulong ProbeListenerNetworkObjectId;
        public string ProbeListenerName;
        public Vector3 ProbeOrigin;
        public float MaxDistance;

        public string SemanticSummary;
        public SceneObjectDescriptor[] Objects;
    }
}
