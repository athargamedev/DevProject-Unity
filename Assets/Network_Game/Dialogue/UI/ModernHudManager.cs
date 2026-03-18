using UnityEngine;
using UnityEngine.UIElements;

namespace Network_Game.UI
{
    /// <summary>
    /// Compatibility shim for older callers. The runtime HUD owner is
    /// <see cref="ModernHudLayoutManager"/> in the current project.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-520)]
    public sealed class ModernHudManager : MonoBehaviour
    {
        public enum HudPanel
        {
            Login,
            Profile,
            Dialogue,
            Combat,
            Feedback,
        }

        public enum HudZone
        {
            TopBar,
            TopLeft,
            RightDock,
            BottomBar,
            ModalOverlay,
        }

        private static ModernHudManager s_ActiveInstance;

        public static ModernHudManager Active
        {
            get
            {
                if (s_ActiveInstance == null)
                {
#if UNITY_2023_1_OR_NEWER
                    s_ActiveInstance = FindAnyObjectByType<ModernHudManager>();
#else
                    s_ActiveInstance = FindAnyObjectByType<ModernHudManager>();
#endif
                }

                return s_ActiveInstance;
            }
        }

        public UIDocument HudDocument => ModernHudLayoutManager.Active != null
            ? ModernHudLayoutManager.Active.HudDocument
            : null;

        public ModernHudLayoutProfile LayoutProfile => ModernHudLayoutManager.Active != null
            ? ModernHudLayoutManager.Active.LayoutProfile
            : null;

        private void OnEnable()
        {
            s_ActiveInstance = this;
        }

        private void OnDisable()
        {
            if (s_ActiveInstance == this)
            {
                s_ActiveInstance = null;
            }
        }

        public static bool TryAcquireUiCursor(UnityEngine.Object owner)
        {
            return ModernHudLayoutManager.TryAcquireUiCursor(owner);
        }

        public static bool TryReleaseUiCursor(UnityEngine.Object owner)
        {
            return ModernHudLayoutManager.TryReleaseUiCursor(owner);
        }

        public static bool SetPanelVisible(HudPanel panel, bool visible)
        {
            return ModernHudLayoutManager.SetPanelVisible(MapPanel(panel), visible);
        }

        public static VisualElement TryGetZone(HudZone zone)
        {
            return ModernHudLayoutManager.TryGetZone(MapZone(zone));
        }

        public static bool TryApplyBottomBarLayout(VisualElement element)
        {
            return ModernHudLayoutManager.TryApplyBottomBarLayout(element);
        }

        public static bool SetFeedbackVisible(bool visible)
        {
            return ModernHudLayoutManager.SetFeedbackVisible(visible);
        }

        private static ModernHudLayoutManager.HudPanel MapPanel(HudPanel panel)
        {
            return panel switch
            {
                HudPanel.Login => ModernHudLayoutManager.HudPanel.Login,
                HudPanel.Profile => ModernHudLayoutManager.HudPanel.Profile,
                HudPanel.Dialogue => ModernHudLayoutManager.HudPanel.Dialogue,
                HudPanel.Combat => ModernHudLayoutManager.HudPanel.Combat,
                HudPanel.Feedback => ModernHudLayoutManager.HudPanel.Feedback,
                _ => ModernHudLayoutManager.HudPanel.Dialogue,
            };
        }

        private static ModernHudLayoutManager.HudZone MapZone(HudZone zone)
        {
            return zone switch
            {
                HudZone.TopBar => ModernHudLayoutManager.HudZone.TopBar,
                HudZone.TopLeft => ModernHudLayoutManager.HudZone.TopLeft,
                HudZone.RightDock => ModernHudLayoutManager.HudZone.RightDock,
                HudZone.BottomBar => ModernHudLayoutManager.HudZone.BottomBar,
                HudZone.ModalOverlay => ModernHudLayoutManager.HudZone.ModalOverlay,
                _ => ModernHudLayoutManager.HudZone.BottomBar,
            };
        }
    }
}
