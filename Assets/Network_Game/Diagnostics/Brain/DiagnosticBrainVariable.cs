using System;

namespace Network_Game.Diagnostics
{
    [Serializable]
    public struct DiagnosticBrainVariable
    {
        public string Key;
        public DiagnosticBrainVariableKind Kind;
        public DiagnosticBrainSeverity Severity;
        public string Phase;
        public string Value;
        public float Confidence;
        public string Source;
        public bool Pinned;
        public float CreatedAt;
        public float ExpiresAt;

        public bool IsExpired(float now)
        {
            return ExpiresAt > 0f && now >= ExpiresAt;
        }
    }
}
