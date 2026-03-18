using System;
using System.Collections.Generic;
using Network_Game.Auth;
using Network_Game.ThirdPersonController.InputSystem;
using Network_Game.UI.Dialogue;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Network_Game.UI
{
    /// <summary>
    /// Runtime HUD owner for the separate-panel UI setup used by this project.
    /// Panels stay authored in UI Builder; this component coordinates visibility,
    /// cursor routing, and shared HUD attachment points at runtime.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-520)]
    public sealed class ModernHudLayoutManager : MonoBehaviour
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

        private static readonly HudPanel[] PanelDocumentPriority =
        {
            HudPanel.Dialogue,
            HudPanel.Profile,
            HudPanel.Login,
            HudPanel.Combat,
            HudPanel.Feedback,
        };

        [Header("Panel References")]
        [Tooltip("Drag each panel GameObject here (Login, Profile, Dialogue, Combat, Feedback).")]
        [SerializeField]
        private GameObject[] m_Panels;

        [Header("Layout")]
        [SerializeField]
        private ModernHudLayoutProfile m_LayoutProfile;

        [Header("Default Visibility")]
        [SerializeField]
        private bool m_LoginVisible = true;

        [SerializeField]
        private bool m_ProfileVisible;

        [SerializeField]
        private bool m_DialogueVisible = true;

        [SerializeField]
        private bool m_CombatVisible;

        [SerializeField]
        private bool m_FeedbackVisible;

        private static ModernHudLayoutManager s_ActiveInstance;

        private readonly Dictionary<HudPanel, GameObject> m_PanelLookup = new();
        private readonly HashSet<int> m_UiCursorOwners = new();
        private readonly bool?[] m_RuntimePanelOverrides = new bool?[5];

        private bool m_IsUiCursorMode;
        private bool m_LastAuthenticatedState;
        private UIDocument m_PrimaryHudDocument;

        public static ModernHudLayoutManager Active
        {
            get
            {
                if (s_ActiveInstance == null)
                {
#if UNITY_2023_1_OR_NEWER
                    s_ActiveInstance = FindAnyObjectByType<ModernHudLayoutManager>();
#else
                    s_ActiveInstance = FindAnyObjectByType<ModernHudLayoutManager>();
#endif
                }

                return s_ActiveInstance;
            }
        }

        public ModernHudLayoutProfile LayoutProfile => m_LayoutProfile;
        public UIDocument HudDocument => ResolvePrimaryHudDocument();

        private void Reset()
        {
            RebuildPanelLookup();
            ApplyPanelVisibility();
        }

        private void Awake()
        {
            SetActiveInstance();
            RebuildPanelLookup();
            ApplyPanelVisibility();
            m_LastAuthenticatedState = IsAuthenticated();
        }

        private void OnEnable()
        {
            SetActiveInstance();
            LocalPlayerAuthService.OnPlayerLoggedIn += HandlePlayerLoggedIn;
            LocalPlayerAuthService.OnPlayerLoggedOut += HandlePlayerLoggedOut;
            RebuildPanelLookup();
            ApplyPanelVisibility();
        }

        private void OnDisable()
        {
            LocalPlayerAuthService.OnPlayerLoggedIn -= HandlePlayerLoggedIn;
            LocalPlayerAuthService.OnPlayerLoggedOut -= HandlePlayerLoggedOut;

            if (Application.isPlaying)
            {
                m_UiCursorOwners.Clear();
                ApplyUiCursorMode(false);
            }

            if (s_ActiveInstance == this)
            {
                s_ActiveInstance = null;
            }
        }

        // Subscribed events handle visibility now. No frame polling needed.
        private void Update()
        {
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            EditorApplication.delayCall -= HandleEditorValidate;
            EditorApplication.delayCall += HandleEditorValidate;
        }

        private void HandleEditorValidate()
        {
            if (this == null)
            {
                return;
            }

            RebuildPanelLookup();
            ApplyPanelVisibility();
        }
#endif

        public static bool TryAcquireUiCursor(UnityEngine.Object owner)
        {
            return Active != null && Active.SetUiCursorOwner(owner, true);
        }

        public static bool TryReleaseUiCursor(UnityEngine.Object owner)
        {
            return Active != null && Active.SetUiCursorOwner(owner, false);
        }

        public static bool SetPanelVisible(HudPanel panel, bool visible)
        {
            return Active != null && Active.SetPanelVisibleInternal(panel, visible);
        }

        public static bool SetFeedbackVisible(bool visible)
        {
            return SetPanelVisible(HudPanel.Feedback, visible);
        }

        public static VisualElement TryGetZone(HudZone zone)
        {
            return Active != null ? Active.ResolveZone(zone) : null;
        }

        public static bool TryApplyBottomBarLayout(VisualElement element)
        {
            return Active != null && Active.ApplyBottomBarLayoutToElement(element);
        }

        public void SetPanelVisible(string panelName, bool visible)
        {
            HudPanel? panel = TryMatchPanel(panelName);
            if (!panel.HasValue)
            {
                return;
            }

            SetPanelVisibleInternal(panel.Value, visible);
        }

        public void TogglePanel(string panelName)
        {
            HudPanel? panel = TryMatchPanel(panelName);
            if (!panel.HasValue)
            {
                return;
            }

            GameObject panelRoot = FindPanelRoot(panel.Value);
            bool currentlyVisible = panelRoot != null && panelRoot.activeSelf;
            SetPanelVisibleInternal(panel.Value, !currentlyVisible);
        }

        public GameObject GetPanel(string panelName)
        {
            HudPanel? panel = TryMatchPanel(panelName);
            return panel.HasValue ? FindPanelRoot(panel.Value) : null;
        }

        private void SetActiveInstance()
        {
            s_ActiveInstance = this;
        }

        private void HandlePlayerLoggedIn(LocalPlayerAuthService.LocalPlayerRecord _)
        {
            m_LastAuthenticatedState = true;
            ApplyPanelVisibility();
            RefreshDialogueControllers();
        }

        private void HandlePlayerLoggedOut()
        {
            m_LastAuthenticatedState = false;
            ApplyPanelVisibility();
        }

        private void RebuildPanelLookup()
        {
            m_PanelLookup.Clear();
            m_PrimaryHudDocument = null;

            if (m_Panels == null)
            {
                return;
            }

            for (int i = 0; i < m_Panels.Length; i++)
            {
                GameObject panelRoot = m_Panels[i];
                if (panelRoot == null)
                {
                    continue;
                }

                HudPanel? panel = TryMatchPanel(panelRoot.name);
                if (!panel.HasValue || m_PanelLookup.ContainsKey(panel.Value))
                {
                    continue;
                }

                m_PanelLookup.Add(panel.Value, panelRoot);
            }
        }

        private bool SetPanelVisibleInternal(HudPanel panel, bool visible)
        {
            m_RuntimePanelOverrides[(int)panel] = visible;
            ApplyPanelVisibility();
            return true;
        }

        private void ApplyPanelVisibility()
        {
            RebuildPanelLookupIfNeeded();

            foreach (KeyValuePair<HudPanel, GameObject> entry in m_PanelLookup)
            {
                if (entry.Value == null)
                {
                    continue;
                }

                entry.Value.SetActive(ResolvePanelVisible(entry.Key));
            }

            m_PrimaryHudDocument = null;
        }

        private bool ResolvePanelVisible(HudPanel panel)
        {
            if (!Application.isPlaying)
            {
                bool? editOverride = m_RuntimePanelOverrides[(int)panel];
                return editOverride ?? GetConfiguredDefaultVisibility(panel);
            }

            bool requireLoginOnly = Application.isPlaying && !IsAuthenticated();
            if (requireLoginOnly)
            {
                return panel == HudPanel.Login;
            }

            bool? overrideValue = m_RuntimePanelOverrides[(int)panel];
            if (overrideValue.HasValue)
            {
                return overrideValue.Value;
            }

            return panel switch
            {
                HudPanel.Login => false,
                HudPanel.Profile => GetConfiguredDefaultVisibility(HudPanel.Profile),
                HudPanel.Dialogue => GetConfiguredDefaultVisibility(HudPanel.Dialogue),
                HudPanel.Combat => GetConfiguredDefaultVisibility(HudPanel.Combat),
                HudPanel.Feedback => false,
                _ => m_LoginVisible,
            };
        }

        private bool GetConfiguredDefaultVisibility(HudPanel panel)
        {
            return panel switch
            {
                HudPanel.Login => m_LoginVisible,
                HudPanel.Profile => m_ProfileVisible,
                HudPanel.Dialogue => m_DialogueVisible,
                HudPanel.Combat => m_CombatVisible,
                HudPanel.Feedback => m_FeedbackVisible,
                _ => false,
            };
        }

        private static bool IsAuthenticated()
        {
            LocalPlayerAuthService authService = LocalPlayerAuthService.Instance;
            return authService != null && authService.HasCurrentPlayer;
        }

        private UIDocument ResolvePrimaryHudDocument()
        {
            if (IsUsableHudDocument(m_PrimaryHudDocument))
            {
                return m_PrimaryHudDocument;
            }

            RebuildPanelLookupIfNeeded();

            for (int i = 0; i < PanelDocumentPriority.Length; i++)
            {
                if (TryGetPanelDocument(PanelDocumentPriority[i], requireActivePanel: true, out UIDocument document))
                {
                    m_PrimaryHudDocument = document;
                    return document;
                }
            }

            for (int i = 0; i < PanelDocumentPriority.Length; i++)
            {
                if (TryGetPanelDocument(PanelDocumentPriority[i], requireActivePanel: false, out UIDocument document))
                {
                    m_PrimaryHudDocument = document;
                    return document;
                }
            }

#if UNITY_2023_1_OR_NEWER
            UIDocument[] documents = FindObjectsByType<UIDocument>(FindObjectsInactive.Exclude);
#else
            UIDocument[] documents = FindObjectsByType<UIDocument>(FindObjectsInactive.Exclude);
#endif
            for (int i = 0; i < documents.Length; i++)
            {
                if (!IsUsableHudDocument(documents[i]))
                {
                    continue;
                }

                m_PrimaryHudDocument = documents[i];
                return documents[i];
            }

            return null;
        }

        private bool TryGetPanelDocument(HudPanel panel, bool requireActivePanel, out UIDocument document)
        {
            document = null;
            GameObject panelRoot = FindPanelRoot(panel);
            if (panelRoot == null)
            {
                return false;
            }

            if (requireActivePanel && !panelRoot.activeInHierarchy)
            {
                return false;
            }

            UIDocument uidocument = panelRoot.GetComponent<UIDocument>();
            if (!IsUsableHudDocument(uidocument))
            {
                return false;
            }

            document = uidocument;
            return true;
        }

        private static bool IsUsableHudDocument(UIDocument document)
        {
            return document != null
                && document.isActiveAndEnabled
                && document.rootVisualElement != null
                && document.rootVisualElement.panel != null
                && document.rootVisualElement.resolvedStyle.display != DisplayStyle.None;
        }

        private VisualElement ResolveZone(HudZone zone)
        {
            string zoneName = ResolveZoneElementName(zone);
            if (string.IsNullOrWhiteSpace(zoneName))
            {
                return null;
            }

            RebuildPanelLookupIfNeeded();

            foreach (HudPanel panel in PanelDocumentPriority)
            {
                if (!TryGetPanelDocument(panel, requireActivePanel: true, out UIDocument document))
                {
                    continue;
                }

                VisualElement found = FindNamedElement(document.rootVisualElement, zoneName);
                if (found != null)
                {
                    return found;
                }
            }

#if UNITY_2023_1_OR_NEWER
            UIDocument[] documents = FindObjectsByType<UIDocument>(FindObjectsInactive.Exclude);
#else
            UIDocument[] documents = FindObjectsByType<UIDocument>(FindObjectsInactive.Exclude);
#endif
            for (int i = 0; i < documents.Length; i++)
            {
                if (!IsUsableHudDocument(documents[i]))
                {
                    continue;
                }

                VisualElement found = FindNamedElement(documents[i].rootVisualElement, zoneName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static string ResolveZoneElementName(HudZone zone)
        {
            return zone switch
            {
                HudZone.TopBar => "zone-top-bar",
                HudZone.TopLeft => "zone-top-left",
                HudZone.RightDock => "zone-right-dock",
                HudZone.BottomBar => "zone-bottom-bar",
                HudZone.ModalOverlay => "zone-modal-overlay",
                _ => null,
            };
        }

        private static VisualElement FindNamedElement(VisualElement root, string elementName)
        {
            if (root == null || root.panel == null)
            {
                return null;
            }

            if (string.Equals(root.name, elementName, StringComparison.Ordinal))
            {
                return root;
            }

            return root.Q(elementName);
        }

        private bool ApplyBottomBarLayoutToElement(VisualElement element)
        {
            if (element == null)
            {
                return false;
            }

            float outerMarginPx = 12f;
            float bottomBarHeightPx = 220f;
            if (m_LayoutProfile != null)
            {
                outerMarginPx = Mathf.Max(12f, Screen.width * m_LayoutProfile.OuterMarginPercent);
                bottomBarHeightPx = Mathf.Max(220f, Screen.height * m_LayoutProfile.BottomBarHeightPercent);
            }

            element.style.position = Position.Absolute;
            element.style.left = new Length(outerMarginPx, LengthUnit.Pixel);
            element.style.right = new Length(outerMarginPx, LengthUnit.Pixel);
            element.style.bottom = new Length(outerMarginPx, LengthUnit.Pixel);
            element.style.height = new Length(bottomBarHeightPx, LengthUnit.Pixel);
            element.style.width = StyleKeyword.Auto;
            return true;
        }

        private bool SetUiCursorOwner(UnityEngine.Object owner, bool wantsUiCursor)
        {
            if (!Application.isPlaying || owner == null)
            {
                return false;
            }

            bool changed = wantsUiCursor
                ? m_UiCursorOwners.Add(owner.GetHashCode())
                : m_UiCursorOwners.Remove(owner.GetHashCode());

            if (changed)
            {
                ApplyUiCursorMode(m_UiCursorOwners.Count > 0);
            }

            return true;
        }

        private void ApplyUiCursorMode(bool wantsUiCursor)
        {
            if (!Application.isPlaying || m_IsUiCursorMode == wantsUiCursor)
            {
                return;
            }

            m_IsUiCursorMode = wantsUiCursor;

            Cursor.lockState = wantsUiCursor ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = wantsUiCursor;

            StarterAssetsInputs inputs = ResolveLocalStarterInputs();
            bool allowGameplayLook = !wantsUiCursor;
            if (inputs != null)
            {
                inputs.cursorLocked = allowGameplayLook;
                inputs.cursorInputForLook = allowGameplayLook;
                inputs.SetCursorState(allowGameplayLook);
                inputs.inputBlocked = wantsUiCursor; // Block movement as well
                if (wantsUiCursor)
                {
                    inputs.move = Vector2.zero;
                    inputs.jump = false;
                    inputs.sprint = false;
                }
            }
        }

        private StarterAssetsInputs m_CachedInputs;
        private float m_NextInputResolveAt;
        private const float InputResolveInterval = 0.5f;

        private StarterAssetsInputs ResolveLocalStarterInputs()
        {
            if (m_CachedInputs != null) return m_CachedInputs;
            if (Time.unscaledTime < m_NextInputResolveAt) return null;

            m_NextInputResolveAt = Time.unscaledTime + InputResolveInterval;

            NetworkManager manager = NetworkManager.Singleton;
            if (manager != null && manager.LocalClient != null && manager.LocalClient.PlayerObject != null)
            {
                m_CachedInputs = manager.LocalClient.PlayerObject.GetComponent<StarterAssetsInputs>();
            }

            if (m_CachedInputs == null)
            {
#if UNITY_2023_1_OR_NEWER
                m_CachedInputs = FindAnyObjectByType<StarterAssetsInputs>();
#else
                m_CachedInputs = FindObjectOfType<StarterAssetsInputs>();
#endif
            }

            return m_CachedInputs;
        }

        private void RefreshDialogueControllers()
        {
#if UNITY_2023_1_OR_NEWER
            ModernDialogueController[] controllers = FindObjectsByType<ModernDialogueController>(FindObjectsInactive.Include);
#else
            ModernDialogueController[] controllers = FindObjectsOfType<ModernDialogueController>(true);
#endif
            for (int i = 0; i < controllers.Length; i++)
            {
                ModernDialogueController controller = controllers[i];
                if (controller == null || !controller.gameObject.scene.IsValid())
                {
                    continue;
                }

                controller.ForceRefreshBindings();
            }
        }

        private void RebuildPanelLookupIfNeeded()
        {
            if (m_PanelLookup.Count == 0 && m_Panels != null && m_Panels.Length > 0)
            {
                RebuildPanelLookup();
            }
        }

        private GameObject FindPanelRoot(HudPanel panel)
        {
            RebuildPanelLookupIfNeeded();
            return m_PanelLookup.TryGetValue(panel, out GameObject panelRoot) ? panelRoot : null;
        }

        private static HudPanel? TryMatchPanel(string panelName)
        {
            if (string.IsNullOrWhiteSpace(panelName))
            {
                return null;
            }

            if (panelName.IndexOf("login", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return HudPanel.Login;
            }

            if (panelName.IndexOf("profile", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return HudPanel.Profile;
            }

            if (panelName.IndexOf("dialog", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return HudPanel.Dialogue;
            }

            if (panelName.IndexOf("combat", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return HudPanel.Combat;
            }

            if (panelName.IndexOf("feedback", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return HudPanel.Feedback;
            }

            return null;
        }
    }
}
