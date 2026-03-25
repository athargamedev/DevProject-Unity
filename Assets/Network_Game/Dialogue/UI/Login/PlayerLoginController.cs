using Network_Game.Auth;
using Network_Game.UI;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;

namespace Network_Game.UI.Login
{
    [RequireComponent(typeof(UIDocument))]
    public class PlayerLoginController : MonoBehaviour
    {
        private VisualElement m_Root;
        private TextField m_NameInput;
        private TextField m_BioInput;
        private Button m_LoginButton;
        private Label m_StatusLabel;
        private bool m_LoginVisible;

        public bool IsVisible => m_LoginVisible;

        private void OnEnable()
        {
            LocalPlayerAuthService authService = LocalPlayerAuthService.EnsureInstance();
            m_Root = GetComponent<UIDocument>().rootVisualElement;

            m_NameInput = m_Root.Q<TextField>("name-input");
            m_BioInput = m_Root.Q<TextField>("bio-input");
            m_LoginButton = m_Root.Q<Button>("login-button");
            m_StatusLabel = m_Root.Q<Label>("status-label");

            if (m_LoginButton != null)
                m_LoginButton.clicked += OnLoginClicked;

            LocalPlayerAuthService.OnPlayerLoggedIn += HandleLoginSuccess;

            if (authService != null && m_NameInput != null)
            {
                m_NameInput.value = authService.LastLoginNameId;
            }

            bool hasCurrentPlayer = authService != null && authService.HasCurrentPlayer;
            SetLoginVisible(!hasCurrentPlayer);
        }

        private void ApplyUiCursorAndLookState()
        {
            ModernHudLayoutManager.TryAcquireUiCursor(this);
        }

        private void RestoreGameplayCursorAndLookState()
        {
            ModernHudLayoutManager.TryReleaseUiCursor(this);
        }

        private void OnDisable()
        {
            if (m_LoginButton != null)
                m_LoginButton.clicked -= OnLoginClicked;
            LocalPlayerAuthService.OnPlayerLoggedIn -= HandleLoginSuccess;
            RestoreGameplayCursorAndLookState();
        }

        private void OnLoginClicked()
        {
            LocalPlayerAuthService authService = LocalPlayerAuthService.EnsureInstance();
            if (authService == null)
            {
                if (m_StatusLabel != null)
                {
                    m_StatusLabel.text = "LOGIN SERVICE UNAVAILABLE";
                    m_StatusLabel.style.color = new StyleColor(Color.red);
                }
                return;
            }

            string nameId = m_NameInput != null ? m_NameInput.value?.Trim() : string.Empty;
            if (string.IsNullOrEmpty(nameId))
            {
                if (m_StatusLabel != null)
                {
                    m_StatusLabel.text = "ERROR: NAME_ID CANNOT BE EMPTY";
                    m_StatusLabel.style.color = new StyleColor(Color.red);
                }
                return;
            }

            if (m_StatusLabel != null)
            {
                m_StatusLabel.text = "LOGGING IN...";
                m_StatusLabel.style.color = new StyleColor(new Color(1f, 0.84f, 0.54f));
            }

            if (authService.Login(nameId))
            {
                AttachCurrentLocalPlayer(authService);

                string bioText = m_BioInput != null ? m_BioInput.value?.Trim() : string.Empty;
                if (!string.IsNullOrEmpty(bioText))
                {
                    if (!bioText.StartsWith("{"))
                    {
                        bioText = "{\"bio\": \"" + bioText.Replace("\"", "\\\"") + "\"}";
                    }
                    authService.SetCustomizationJson(bioText);
                }
            }
            else
            {
                if (m_StatusLabel != null)
                {
                    m_StatusLabel.text = "LOGIN FAILED";
                    m_StatusLabel.style.color = new StyleColor(Color.red);
                }
            }
        }

        private void HandleLoginSuccess(LocalPlayerAuthService.LocalPlayerRecord record)
        {
            m_StatusLabel.text = $"LOGGED IN AS {record.NameId.ToUpper()}";
            m_StatusLabel.style.color = new StyleColor(new Color(0.72f, 0.96f, 0.76f));

            m_Root.schedule.Execute(() =>
            {
                SetLoginVisible(false);
                RestoreGameplayCursorAndLookState();
            }).StartingIn(1500);
        }

        public void Show()
        {
            SetLoginVisible(true);
        }

        private void SetLoginVisible(bool visible)
        {
            bool visibilityChanged = m_LoginVisible != visible;
            m_LoginVisible = visible;

            if (!ModernHudLayoutManager.SetPanelVisible(ModernHudLayoutManager.HudPanel.Login, visible))
            {
                if (m_Root != null)
                {
                    m_Root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
                }
            }

            if (visible && visibilityChanged)
            {
                ApplyUiCursorAndLookState();
                FocusNameInput();
            }
        }

        private void FocusNameInput()
        {
            if (m_NameInput == null) return;

            m_NameInput.schedule.Execute(() =>
            {
                if (m_NameInput == null) return;
                m_NameInput.Focus();
            });
        }

        private static void AttachCurrentLocalPlayer(LocalPlayerAuthService authService)
        {
            if (authService == null) return;

            GameObject localPlayer = null;
            NetworkManager manager = NetworkManager.Singleton;
            if (manager != null && manager.LocalClient != null && manager.LocalClient.PlayerObject != null)
            {
                localPlayer = manager.LocalClient.PlayerObject.gameObject;
            }

            authService.AttachLocalPlayer(localPlayer);
        }
    }
}
