using System;
using UnityEngine;

namespace Network_Game.Diagnostics
{
    [DisallowMultipleComponent]
    public sealed class DialogueReplicationTraceStore : MonoBehaviour
    {
        private static DialogueReplicationTraceStore s_Instance;

        [SerializeField]
        [Min(8)]
        private int m_Capacity = 128;

        private DialogueReplicationTrace[] m_Buffer = Array.Empty<DialogueReplicationTrace>();
        private int m_Count;
        private int m_NextIndex;

        public static DialogueReplicationTraceStore Instance => s_Instance;

        private void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                Destroy(this);
                return;
            }

            s_Instance = this;
            if (m_Buffer.Length != m_Capacity)
            {
                m_Buffer = new DialogueReplicationTrace[m_Capacity];
            }
        }

        private void OnDestroy()
        {
            if (s_Instance == this)
            {
                s_Instance = null;
            }
        }

        public void Record(DialogueReplicationTrace trace)
        {
            if (m_Buffer.Length != m_Capacity)
            {
                m_Buffer = new DialogueReplicationTrace[m_Capacity];
                m_Count = 0;
                m_NextIndex = 0;
            }

            m_Buffer[m_NextIndex] = trace;
            m_NextIndex = (m_NextIndex + 1) % m_Buffer.Length;
            m_Count = Mathf.Min(m_Count + 1, m_Buffer.Length);
        }

        public bool TryGetLatest(out DialogueReplicationTrace trace)
        {
            if (m_Count <= 0 || m_Buffer.Length == 0)
            {
                trace = default;
                return false;
            }

            int index = m_NextIndex - 1;
            if (index < 0)
            {
                index = m_Buffer.Length - 1;
            }

            trace = m_Buffer[index];
            return !string.IsNullOrWhiteSpace(trace.TraceId);
        }

        public DialogueReplicationTrace[] GetRecent()
        {
            if (m_Count <= 0 || m_Buffer.Length == 0)
            {
                return Array.Empty<DialogueReplicationTrace>();
            }

            var result = new DialogueReplicationTrace[m_Count];
            int start = m_Count == m_Buffer.Length ? m_NextIndex : 0;
            for (int i = 0; i < m_Count; i++)
            {
                int index = (start + i) % m_Buffer.Length;
                result[i] = m_Buffer[index];
            }

            return result;
        }
    }
}
