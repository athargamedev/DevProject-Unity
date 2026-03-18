using System;
using UnityEngine;

namespace Network_Game.Diagnostics
{
    [DisallowMultipleComponent]
    public sealed class DialogueExecutionTraceStore : MonoBehaviour
    {
        private static DialogueExecutionTraceStore s_Instance;

        [SerializeField]
        [Min(8)]
        private int m_Capacity = 128;

        private DialogueExecutionTrace[] m_Buffer = Array.Empty<DialogueExecutionTrace>();
        private int m_Count;
        private int m_NextIndex;

        public static DialogueExecutionTraceStore Instance => s_Instance;

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
                m_Buffer = new DialogueExecutionTrace[m_Capacity];
            }
        }

        private void OnDestroy()
        {
            if (s_Instance == this)
            {
                s_Instance = null;
            }
        }

        public void Record(DialogueExecutionTrace trace)
        {
            if (m_Buffer.Length != m_Capacity)
            {
                m_Buffer = new DialogueExecutionTrace[m_Capacity];
                m_Count = 0;
                m_NextIndex = 0;
            }

            m_Buffer[m_NextIndex] = trace;
            m_NextIndex = (m_NextIndex + 1) % m_Buffer.Length;
            m_Count = Mathf.Min(m_Count + 1, m_Buffer.Length);
        }

        public bool TryGetLatest(out DialogueExecutionTrace trace)
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
    }
}
