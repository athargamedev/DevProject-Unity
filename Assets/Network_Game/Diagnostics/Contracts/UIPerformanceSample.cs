using System;

namespace Network_Game.Diagnostics
{
    [Serializable]
    public struct UIPerformanceSample
    {
        public string RunId;
        public string BootId;
        public string UiId;
        public string UiKind;
        public string SceneName;
        public string SampleName;
        public int Frame;
        public float RealtimeSinceStartup;

        public float DurationMs;
        public int TranscriptLineCount;
        public int TranscriptCharacterCount;
        public int VisibleElementCount;
        public int TextElementCount;
        public int LayoutPassCount;
        public string Notes;
    }
}
