using System.Threading.Tasks;
using Network_Game.Diagnostics;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Network_Game.Auth
{
    /// <summary>
    /// Simple UI for Supabase authentication (register/login).
    /// Attach to a Canvas with input fields for email/password.
    /// </summary>
    public class AuthUIManager : MonoBehaviour
    {
        private const string Category = "AuthUI";
        
        [Header("UI References")]
        [SerializeField] private GameObject m_AuthPanel;
        [SerializeField] private TMP_InputField m_EmailInput;
        [SerializeField] private TMP_InputField m_PasswordInput;
        [SerializeField] private TMP_InputField m_PlayerNameInput;
        [SerializeField] private Button m_LoginButton;
        [SerializeField] private Button m_RegisterButton;
        [SerializeField] private Button m_GuestButton;
        [SerializeField] private TextMeshProUGUI m_StatusText;
        [SerializeField] private GameObject m_LoadingIndicator;
        
        [Header("Settings")]
        [SerializeField] private bool m_ShowGuestOption = true;
        [SerializeField] private string m_GuestPrefix = "Guest_";
        
        private bool m_IsProcessing;
        
        #region Lifecycle
        
        private void Awake()
        {
            if (m_AuthPanel == null)
            {
                NGLog.Error(Category, "AuthPanel not assigned!");
                enabled = false;
                return;
            }
            
            // Wire up buttons
            if (m_LoginButton != null)
                m_LoginButton.onClick.AddListener(OnLoginClicked);
            
            if (m_RegisterButton != null)
                m_RegisterButton.onClick.AddListener(OnRegisterClicked);
            
            if (m_GuestButton != null)
            {
                m_GuestButton.onClick.AddListener(OnGuestClicked);
                m_GuestButton.gameObject.SetActive(m_ShowGuestOption);
            }
            
            // Subscribe to auth events
            SupabaseAuthService.OnAuthStateChanged += HandleAuthStateChanged;
            SupabaseAuthService.OnAuthError += HandleAuthError;
        }
        
        private void Start()
        {
            // Show panel if not authenticated
            if (SupabaseAuthService.Instance == null || !SupabaseAuthService.Instance.IsAuthenticated)
            {
                ShowAuthPanel();
            }
            else
            {
                HideAuthPanel();
            }
        }
        
        private void OnDestroy()
        {
            if (m_LoginButton != null)
                m_LoginButton.onClick.RemoveListener(OnLoginClicked);
            
            if (m_RegisterButton != null)
                m_RegisterButton.onClick.RemoveListener(OnRegisterClicked);
            
            if (m_GuestButton != null)
                m_GuestButton.onClick.RemoveListener(OnGuestClicked);
            
            SupabaseAuthService.OnAuthStateChanged -= HandleAuthStateChanged;
            SupabaseAuthService.OnAuthError -= HandleAuthError;
        }
        
        #endregion
        
        #region Button Handlers
        
        private void OnLoginClicked()
        {
            if (m_IsProcessing) return;
            
            string email = m_EmailInput?.text?.Trim();
            string password = m_PasswordInput?.text;
            
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                SetStatus("Please enter email and password", isError: true);
                return;
            }
            
            _ = ProcessLoginAsync(email, password);
        }
        
        private void OnRegisterClicked()
        {
            if (m_IsProcessing) return;
            
            string email = m_EmailInput?.text?.Trim();
            string password = m_PasswordInput?.text;
            string playerName = m_PlayerNameInput?.text?.Trim();
            
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                SetStatus("Please enter email and password", isError: true);
                return;
            }
            
            if (password.Length < 6)
            {
                SetStatus("Password must be at least 6 characters", isError: true);
                return;
            }
            
            _ = ProcessRegisterAsync(email, password, playerName);
        }
        
        private void OnGuestClicked()
        {
            if (m_IsProcessing) return;
            
            // Generate guest credentials
            string guestId = System.Guid.NewGuid().ToString("N")[..8];
            string email = $"{m_GuestPrefix}{guestId}@localhost";
            string password = System.Guid.NewGuid().ToString("N");
            string playerName = $"Guest {guestId}";
            
            _ = ProcessRegisterAsync(email, password, playerName);
        }
        
        #endregion
        
        #region Async Operations
        
        private async Task ProcessLoginAsync(string email, string password)
        {
            SetProcessing(true);
            SetStatus("Logging in...");
            
            bool success = await SupabaseAuthService.Instance.LoginAsync(email, password);
            
            if (!success && !SupabaseAuthService.Instance.IsAuthenticated)
            {
                // Login failed, stay on auth panel
                SetStatus("Login failed. Check credentials.", isError: true);
            }
            
            SetProcessing(false);
        }
        
        private async Task ProcessRegisterAsync(string email, string password, string playerName)
        {
            SetProcessing(true);
            SetStatus("Creating account...");
            
            bool success = await SupabaseAuthService.Instance.RegisterAsync(email, password, playerName);
            
            if (success)
            {
                SetStatus("Account created! Logging in...");
            }
            else
            {
                SetStatus("Registration failed. Try again.", isError: true);
            }
            
            SetProcessing(false);
        }
        
        #endregion
        
        #region Event Handlers
        
        private void HandleAuthStateChanged(SupabaseAuthService.AuthSession session)
        {
            if (session.IsAuthenticated)
            {
                SetStatus($"Welcome! Player: {session.PlayerKey}");
                HideAuthPanel();
            }
            else
            {
                ShowAuthPanel();
            }
        }
        
        private void HandleAuthError(string error)
        {
            SetStatus(error, isError: true);
            SetProcessing(false);
        }
        
        #endregion
        
        #region UI Helpers
        
        private void ShowAuthPanel()
        {
            if (m_AuthPanel != null)
                m_AuthPanel.SetActive(true);
        }
        
        private void HideAuthPanel()
        {
            if (m_AuthPanel != null)
                m_AuthPanel.SetActive(false);
        }
        
        private void SetProcessing(bool processing)
        {
            m_IsProcessing = processing;
            
            if (m_LoadingIndicator != null)
                m_LoadingIndicator.SetActive(processing);
            
            if (m_LoginButton != null)
                m_LoginButton.interactable = !processing;
            
            if (m_RegisterButton != null)
                m_RegisterButton.interactable = !processing;
            
            if (m_GuestButton != null)
                m_GuestButton.interactable = !processing;
        }
        
        private void SetStatus(string message, bool isError = false)
        {
            if (m_StatusText == null) return;
            
            m_StatusText.text = message;
            m_StatusText.color = isError ? Color.red : Color.white;
            
            NGLog.Info(Category, message);
        }
        
        #endregion
    }
}
