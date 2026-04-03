using System.Threading.Tasks;
using Network_Game.Auth;
using Network_Game.Core;
using Network_Game.Diagnostics;
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
        private TextField m_EmailInput;
        private TextField m_PasswordInput;
        private Button m_LoginButton;
        private Button m_CloudLoginButton;
        private Button m_RegisterButton;
        private Label m_StatusLabel;
        private bool m_LoginVisible;
        private bool m_IsProcessing;

        public bool IsVisible => m_LoginVisible;

        private void OnEnable()
        {
            LocalPlayerAuthService authService = LocalPlayerAuthService.EnsureInstance();
            SupabaseAuthService.EnsureInstance();
            SupabasePlayerDataProvider.EnsureInstance();
            m_Root = GetComponent<UIDocument>().rootVisualElement;

            m_NameInput = m_Root.Q<TextField>("name-input");
            m_BioInput = m_Root.Q<TextField>("bio-input");
            m_EmailInput = m_Root.Q<TextField>("email-input");
            m_PasswordInput = m_Root.Q<TextField>("password-input");
            m_LoginButton = m_Root.Q<Button>("local-login-button");
            m_CloudLoginButton = m_Root.Q<Button>("cloud-login-button");
            m_RegisterButton = m_Root.Q<Button>("register-button");
            m_StatusLabel = m_Root.Q<Label>("status-auth-label");

            if (m_LoginButton != null)
                m_LoginButton.clicked += OnLoginClicked;

            if (m_CloudLoginButton != null)
                m_CloudLoginButton.clicked += OnCloudLoginClicked;

            if (m_RegisterButton != null)
                m_RegisterButton.clicked += OnRegisterClicked;

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
            if (m_CloudLoginButton != null)
                m_CloudLoginButton.clicked -= OnCloudLoginClicked;
            if (m_RegisterButton != null)
                m_RegisterButton.clicked -= OnRegisterClicked;
            LocalPlayerAuthService.OnPlayerLoggedIn -= HandleLoginSuccess;
            RestoreGameplayCursorAndLookState();
        }

        private void OnLoginClicked()
        {
            if (m_IsProcessing)
            {
                return;
            }

            string email = m_EmailInput != null ? m_EmailInput.value?.Trim() : string.Empty;
            string password = m_PasswordInput != null ? m_PasswordInput.value ?? string.Empty : string.Empty;
            if (!string.IsNullOrWhiteSpace(email) || !string.IsNullOrWhiteSpace(password))
            {
                SetStatus("EMAIL/PASSWORD DETECTED. USE CLOUD LOGIN OR REGISTER CLOUD.", Color.yellow);
                return;
            }

            LocalPlayerAuthService authService = LocalPlayerAuthService.EnsureInstance();
            if (authService == null)
            {
                SetStatus("LOGIN SERVICE UNAVAILABLE", Color.red);
                return;
            }

            string nameId = m_NameInput != null ? m_NameInput.value?.Trim() : string.Empty;
            if (string.IsNullOrEmpty(nameId))
            {
                SetStatus("ERROR: NAME_ID CANNOT BE EMPTY", Color.red);
                return;
            }

            SetStatus("LOGGING IN...", new Color(1f, 0.84f, 0.54f));

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
                SetStatus("LOGIN FAILED", Color.red);
            }
        }

        private void OnCloudLoginClicked()
        {
            if (m_IsProcessing)
            {
                return;
            }

            NGLog.Info("AuthUI", "Cloud login button clicked.");
            _ = ProcessCloudLoginAsync();
        }

        private void OnRegisterClicked()
        {
            if (m_IsProcessing)
            {
                return;
            }

            NGLog.Info("AuthUI", "Cloud register button clicked.");
            _ = ProcessCloudRegisterAsync();
        }

        private void HandleLoginSuccess(LocalPlayerAuthService.LocalPlayerRecord record)
        {
            SetStatus($"LOGGED IN AS {record.NameId.ToUpper()}", new Color(0.72f, 0.96f, 0.76f));

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

            if (!visible)
            {
                RestoreGameplayCursorAndLookState();
            }
        }

        private async Task ProcessCloudLoginAsync()
        {
            string email = m_EmailInput != null ? m_EmailInput.value?.Trim() : string.Empty;
            string password = m_PasswordInput != null ? m_PasswordInput.value ?? string.Empty : string.Empty;

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                SetStatus("ERROR: EMAIL AND PASSWORD REQUIRED", Color.red);
                return;
            }

            SupabaseAuthService supabaseAuth = SupabaseAuthService.EnsureInstance();
            if (supabaseAuth == null)
            {
                SetStatus("CLOUD AUTH SERVICE UNAVAILABLE", Color.red);
                return;
            }

            SetProcessing(true);
            SetStatus("SIGNING INTO CLOUD...", new Color(0.52f, 0.86f, 1f));

            bool success = await supabaseAuth.LoginAsync(email, password);
            if (success && await BindSupabaseIdentityToLocalAuthAsync())
            {
                SetStatus($"CLOUD LOGIN READY: {supabaseAuth.CurrentPlayerKey}", new Color(0.72f, 0.96f, 0.76f));
            }
            else
            {
                SetStatus("CLOUD LOGIN FAILED", Color.red);
            }

            SetProcessing(false);
        }

        private async Task ProcessCloudRegisterAsync()
        {
            string email = m_EmailInput != null ? m_EmailInput.value?.Trim() : string.Empty;
            string password = m_PasswordInput != null ? m_PasswordInput.value ?? string.Empty : string.Empty;
            string playerHandle = m_NameInput != null ? m_NameInput.value?.Trim() : string.Empty;

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                SetStatus("ERROR: EMAIL AND PASSWORD REQUIRED", Color.red);
                return;
            }

            if (password.Length < 6)
            {
                SetStatus("ERROR: PASSWORD MUST BE 6+ CHARS", Color.red);
                return;
            }

            SupabaseAuthService supabaseAuth = SupabaseAuthService.EnsureInstance();
            if (supabaseAuth == null)
            {
                SetStatus("CLOUD AUTH SERVICE UNAVAILABLE", Color.red);
                return;
            }

            SetProcessing(true);
            SetStatus("REGISTERING CLOUD PLAYER...", new Color(0.52f, 0.86f, 1f));

            bool success = await supabaseAuth.RegisterAsync(email, password, playerHandle);
            if (success && await BindSupabaseIdentityToLocalAuthAsync())
            {
                SetStatus($"REGISTERED: {supabaseAuth.CurrentPlayerKey}", new Color(0.72f, 0.96f, 0.76f));
            }
            else
            {
                SetStatus("CLOUD REGISTRATION FAILED", Color.red);
            }

            SetProcessing(false);
        }

        private async Task<bool> BindSupabaseIdentityToLocalAuthAsync()
        {
            SupabaseAuthService supabaseAuth = SupabaseAuthService.Instance;
            LocalPlayerAuthService localAuth = LocalPlayerAuthService.EnsureInstance();
            if (
                supabaseAuth == null
                || !supabaseAuth.IsAuthenticated
                || string.IsNullOrWhiteSpace(supabaseAuth.CurrentPlayerKey)
                || localAuth == null
            )
            {
                return false;
            }

            if (!localAuth.Login(supabaseAuth.CurrentPlayerKey))
            {
                return false;
            }

            AttachCurrentLocalPlayer(localAuth);

            string bioText = m_BioInput != null ? m_BioInput.value?.Trim() : string.Empty;
            if (!string.IsNullOrEmpty(bioText))
            {
                if (!bioText.StartsWith("{"))
                {
                    bioText = "{\"bio\": \"" + bioText.Replace("\"", "\\\"") + "\"}";
                }

                localAuth.SetCustomizationJson(bioText);
            }

            await Task.Yield();
            return true;
        }

        private void SetProcessing(bool processing)
        {
            m_IsProcessing = processing;

            if (m_LoginButton != null)
            {
                m_LoginButton.SetEnabled(!processing);
            }

            if (m_CloudLoginButton != null)
            {
                m_CloudLoginButton.SetEnabled(!processing);
            }

            if (m_RegisterButton != null)
            {
                m_RegisterButton.SetEnabled(!processing);
            }
        }

        private void SetStatus(string text, Color color)
        {
            if (m_StatusLabel == null)
            {
                NGLog.Info("AuthUI", text);
                return;
            }

            m_StatusLabel.text = text;
            m_StatusLabel.style.color = new StyleColor(color);
            NGLog.Info("AuthUI", text);
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
