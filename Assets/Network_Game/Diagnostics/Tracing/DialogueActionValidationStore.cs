using System;
using UnityEngine;

namespace Network_Game.Diagnostics
{
    [DisallowMultipleComponent]
    public sealed class DialogueActionValidationStore : MonoBehaviour
    {
        private static DialogueActionValidationStore s_Instance;

        [SerializeField]
        [Min(8)]
        private int m_Capacity = 128;

        private DialogueActionValidationResult[] m_Buffer = Array.Empty<DialogueActionValidationResult>();
        private int m_Count;
        private int m_NextIndex;

        public static DialogueActionValidationStore Instance => s_Instance;

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
                m_Buffer = new DialogueActionValidationResult[m_Capacity];
            }
        }

        private void OnDestroy()
        {
            if (s_Instance == this)
            {
                s_Instance = null;
            }
        }

        public void Record(DialogueActionValidationResult result)
        {
            if (m_Buffer.Length != m_Capacity)
            {
                m_Buffer = new DialogueActionValidationResult[m_Capacity];
                m_Count = 0;
                m_NextIndex = 0;
            }

            m_Buffer[m_NextIndex] = result;
            m_NextIndex = (m_NextIndex + 1) % m_Buffer.Length;
            m_Count = Mathf.Min(m_Count + 1, m_Buffer.Length);
        }

        public bool TryGetLatest(out DialogueActionValidationResult result)
        {
            if (m_Count <= 0 || m_Buffer.Length == 0)
            {
                result = default;
                return false;
            }

            int index = m_NextIndex - 1;
            if (index < 0)
            {
                index = m_Buffer.Length - 1;
            }

            result = m_Buffer[index];
            return !string.IsNullOrWhiteSpace(result.ResultId);
        }

        public DialogueActionValidationResult[] GetRecent()
        {
            if (m_Count <= 0 || m_Buffer.Length == 0)
            {
                return Array.Empty<DialogueActionValidationResult>();
            }

            var result = new DialogueActionValidationResult[m_Count];
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
