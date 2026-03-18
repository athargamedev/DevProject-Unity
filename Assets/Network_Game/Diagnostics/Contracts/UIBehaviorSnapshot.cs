using System;

namespace Network_Game.Diagnostics
{
    [Serializable]
    public struct UIBehaviorSnapshot
    {
        public string RunId;
        public string BootId;
        public string UiId;
        public string UiKind;
        public string SceneName;
        public int Frame;
        public float RealtimeSinceStartup;

        public bool IsVisible;
        public bool ConversationReady;
        public bool GameplayInputSuppressed;
        public bool HasAuthenticatedPlayer;
        public bool SendEnabled;
        public string SendBlockedReason;
        public bool InputFocused;

        public bool HasPendingRequest;
        public int PendingRequestId;
        public ulong SelectedNpcNetworkObjectId;
        public string SelectedNpcName;
        public bool NpcInRange;

        public int TranscriptLineCount;
        public int TranscriptCharacterCount;
        public bool AutoScrollEnabled;

        public string Summary;

        public void RefreshSummary()
        {
            string uiKind = string.IsNullOrWhiteSpace(UiKind) ? "ui" : UiKind;
            string target = string.IsNullOrWhiteSpace(SelectedNpcName) ? "none" : SelectedNpcName;
            string blocker = SendEnabled
                ? string.Empty
                : string.IsNullOrWhiteSpace(SendBlockedReason)
                    ? "blocked"
                    : SendBlockedReason;
            Summary = string.Format(
                "{0} visible={1} ready={2} target={3} inRange={4} pending={5} send={6}{7} transcript={8}",
                uiKind,
                IsVisible,
                ConversationReady,
                target,
                NpcInRange,
                HasPendingRequest,
                SendEnabled,
                string.IsNullOrWhiteSpace(blocker) ? string.Empty : " blocker=" + blocker,
                TranscriptLineCount
            );
        }
    }
}
