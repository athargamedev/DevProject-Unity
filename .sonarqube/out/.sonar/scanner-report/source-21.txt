using System;
using System.Collections.Generic;
using Network_Game.Diagnostics;
using UnityEngine;

namespace Network_Game.Behavior
{
    /// <summary>
    /// Minimal runtime milestone tracker used by scene bootstrap and UI readiness checks.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SceneWorkflowDiagnostics : MonoBehaviour, ISceneWorkflowStateBridge
    {
        private static SceneWorkflowDiagnostics s_Instance;

        private readonly HashSet<string> m_CompletedMilestones = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase
        );

        [SerializeField]
        private string m_ActiveBootId;

        public string ActiveBootId => m_ActiveBootId ?? string.Empty;

        public bool StartupCompleted => IsMilestoneComplete("scene_bootstrap_ready");

        private void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                Destroy(this);
                return;
            }

            s_Instance = this;
            if (string.IsNullOrWhiteSpace(m_ActiveBootId))
            {
                m_ActiveBootId = Guid.NewGuid().ToString("N");
            }

            SceneWorkflowStateBridgeRegistry.Register(this);
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(s_Instance, this))
            {
                s_Instance = null;
            }

            SceneWorkflowStateBridgeRegistry.Unregister(this);
        }

        public bool IsMilestoneComplete(string milestone)
        {
            return !string.IsNullOrWhiteSpace(milestone)
                && m_CompletedMilestones.Contains(milestone.Trim());
        }

        public void MarkMilestone(string milestone)
        {
            if (string.IsNullOrWhiteSpace(milestone))
            {
                return;
            }

            m_CompletedMilestones.Add(milestone.Trim());
        }

        public static void ReportMilestone(
            string milestone,
            Component source = null,
            params (string key, object value)[] data
        )
        {
            SceneWorkflowDiagnostics tracker = s_Instance;
            if (tracker == null)
            {
                tracker = source != null
                    ? source.GetComponent<SceneWorkflowDiagnostics>()
                    : FindAnyObjectByType<SceneWorkflowDiagnostics>();
            }

            tracker?.MarkMilestone(milestone);
        }
    }
}
