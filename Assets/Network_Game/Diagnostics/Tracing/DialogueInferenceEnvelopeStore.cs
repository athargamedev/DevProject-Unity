using System;
using UnityEngine;

namespace Network_Game.Diagnostics
{
    [DisallowMultipleComponent]
    public sealed class DialogueInferenceEnvelopeStore : MonoBehaviour
    {
        private static DialogueInferenceEnvelopeStore s_Instance;

        [SerializeField]
        [Min(8)]
        private int m_Capacity = 64;

        private DialogueInferenceEnvelope[] m_Buffer = Array.Empty<DialogueInferenceEnvelope>();
        private int m_Count;
        private int m_NextIndex;

        public static DialogueInferenceEnvelopeStore Instance => s_Instance;

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
                m_Buffer = new DialogueInferenceEnvelope[m_Capacity];
            }
        }

        private void OnDestroy()
        {
            if (s_Instance == this)
            {
                s_Instance = null;
            }
        }

        public void Record(DialogueInferenceEnvelope envelope)
        {
            if (m_Buffer.Length != m_Capacity)
            {
                m_Buffer = new DialogueInferenceEnvelope[m_Capacity];
                m_Count = 0;
                m_NextIndex = 0;
            }

            m_Buffer[m_NextIndex] = envelope;
            m_NextIndex = (m_NextIndex + 1) % m_Buffer.Length;
            m_Count = Mathf.Min(m_Count + 1, m_Buffer.Length);
        }

        public bool TryGetLatest(out DialogueInferenceEnvelope envelope)
        {
            if (m_Count <= 0 || m_Buffer.Length == 0)
            {
                envelope = default;
                return false;
            }

            int index = m_NextIndex - 1;
            if (index < 0)
            {
                index = m_Buffer.Length - 1;
            }

            envelope = m_Buffer[index];
            return !string.IsNullOrWhiteSpace(envelope.EnvelopeId);
        }

        public bool TryGetByRequestId(int requestId, out DialogueInferenceEnvelope envelope)
        {
            for (int i = 0; i < m_Count; i++)
            {
                int index = m_NextIndex - 1 - i;
                if (index < 0)
                {
                    index += m_Buffer.Length;
                }

                if (m_Buffer[index].RequestId != requestId)
                {
                    continue;
                }

                envelope = m_Buffer[index];
                return true;
            }

            envelope = default;
            return false;
        }

        public DialogueInferenceEnvelope[] GetRecent(int maxCount = 10)
        {
            if (m_Count <= 0 || maxCount <= 0)
            {
                return Array.Empty<DialogueInferenceEnvelope>();
            }

            int take = Mathf.Min(maxCount, m_Count);
            var result = new DialogueInferenceEnvelope[take];
            for (int i = 0; i < take; i++)
            {
                int index = m_NextIndex - 1 - i;
                if (index < 0)
                {
                    index += m_Buffer.Length;
                }

                result[i] = m_Buffer[index];
            }

            return result;
        }
    }
}
