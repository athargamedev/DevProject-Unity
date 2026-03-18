using System.Collections.Generic;
using UnityEngine;

namespace Network_Game.Diagnostics
{
    [DisallowMultipleComponent]
    public sealed class UiDiagnosticsStore : MonoBehaviour
    {
        private static UiDiagnosticsStore s_Instance;
        private readonly Dictionary<string, UIBehaviorSnapshot> m_BehaviorByUiId =
            new Dictionary<string, UIBehaviorSnapshot>(System.StringComparer.Ordinal);
        private readonly Dictionary<string, UIPerformanceSample> m_PerformanceByUiId =
            new Dictionary<string, UIPerformanceSample>(System.StringComparer.Ordinal);

        public static UiDiagnosticsStore Instance => s_Instance;

        private void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                Destroy(this);
                return;
            }

            s_Instance = this;
        }

        private void OnDestroy()
        {
            if (s_Instance == this)
            {
                s_Instance = null;
            }
        }

        public void RecordBehaviorSnapshot(UIBehaviorSnapshot snapshot)
        {
            if (string.IsNullOrWhiteSpace(snapshot.UiId))
            {
                return;
            }

            m_BehaviorByUiId[snapshot.UiId] = snapshot;
        }

        public void RecordPerformanceSample(UIPerformanceSample sample)
        {
            if (string.IsNullOrWhiteSpace(sample.UiId))
            {
                return;
            }

            m_PerformanceByUiId[sample.UiId] = sample;
        }

        public bool TryGetLatestBehaviorSnapshot(string uiId, out UIBehaviorSnapshot snapshot)
        {
            if (!string.IsNullOrWhiteSpace(uiId))
            {
                return m_BehaviorByUiId.TryGetValue(uiId, out snapshot);
            }

            bool found = false;
            snapshot = default;
            foreach (KeyValuePair<string, UIBehaviorSnapshot> pair in m_BehaviorByUiId)
            {
                if (!found || pair.Value.RealtimeSinceStartup > snapshot.RealtimeSinceStartup)
                {
                    snapshot = pair.Value;
                    found = true;
                }
            }

            return found;
        }

        public bool TryGetLatestPerformanceSample(string uiId, out UIPerformanceSample sample)
        {
            if (!string.IsNullOrWhiteSpace(uiId))
            {
                return m_PerformanceByUiId.TryGetValue(uiId, out sample);
            }

            bool found = false;
            sample = default;
            foreach (KeyValuePair<string, UIPerformanceSample> pair in m_PerformanceByUiId)
            {
                if (!found || pair.Value.RealtimeSinceStartup > sample.RealtimeSinceStartup)
                {
                    sample = pair.Value;
                    found = true;
                }
            }

            return found;
        }
    }
}
