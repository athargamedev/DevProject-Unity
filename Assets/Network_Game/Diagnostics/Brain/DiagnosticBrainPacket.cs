using System;

namespace Network_Game.Diagnostics
{
    [Serializable]
    public struct DiagnosticBrainPacket
    {
        public string RunId;
        public string BootId;
        public string Objective;
        public string SceneName;
        public string CurrentPhase;

        public AuthoritySnapshot Authority;
        public AuthoritativeSceneSnapshot SceneSnapshot;
        public DialogueInferenceEnvelope LatestEnvelope;

        public DiagnosticBrainVariable[] TopPriorities;
        public DiagnosticBrainVariable[] ActiveFacts;
        public DiagnosticBrainVariable[] ActiveSuppressions;

        public string Summary;
    }
}
