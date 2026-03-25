using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Network_Game.Diagnostics;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Network_Game.Auth
{
    /// <summary>
    /// Supabase Auth integration for player registration and login.
    /// Handles JWT tokens, session management, and player profile linking.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-250)]
    public sealed class SupabaseAuthService : MonoBehaviour
    {
        private const string Category = "SupabaseAuth";
        
        [Header("Supabase Configuration")]
        [SerializeField] private string m_SupabaseUrl = "http://127.0.0.1:54321";
        [SerializeField] private string m_AnonKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZS1kZW1vIiwicm9sZSI6ImFub24iLCJleHAiOjE5ODM4MTI5OTZ9.CRXP1A7WOeoJeXxjNni43kdQwgnWNReilDMblYTn_I0";
        
        [Header("Session")]
        [SerializeField] private bool m_PersistSession = true;
        
        // Session state
        private string m_AccessToken;
        private string m_RefreshToken;
        private string m_UserId;
        private string m_PlayerKey;
        private DateTime m_ExpiresAt;
        
        // Events
        public static event Action<AuthSession> OnAuthStateChanged;
        public static event Action<string> OnAuthError;
        
        public static SupabaseAuthService Instance { get; private set; }
        public bool IsAuthenticated => !string.IsNullOrEmpty(m_AccessToken) && DateTime.UtcNow < m_ExpiresAt;
        public string CurrentUserId => m_UserId;
        public string CurrentPlayerKey => m_PlayerKey;
        public string AccessToken => m_AccessToken;

        public static SupabaseAuthService EnsureInstance()
        {
            if (Instance != null)
            {
                return Instance;
            }

            Instance = FindAnyObjectByType<SupabaseAuthService>();
            if (Instance != null)
            {
                return Instance;
            }

            var go = new GameObject("SupabaseAuthService");
            Instance = go.AddComponent<SupabaseAuthService>();
            return Instance;
        }
        
        [Serializable]
        public class AuthSession
        {
            public string AccessToken;
            public string RefreshToken;
            public string UserId;
            public string PlayerKey;
            public DateTime ExpiresAt;
            public bool IsAuthenticated;
        }
        
        [Serializable]
        private class AuthRequest
        {
            public string email;
            public string password;
        }
        
        [Serializable]
        private class AuthResponse
        {
            public string access_token;
            public string refresh_token;
            public int expires_in;
            public UserData user;
        }
        
        [Serializable]
        private class UserData
        {
            public string id;
            public string email;
            public UserMetadata user_metadata;
        }
        
        [Serializable]
        private class UserMetadata
        {
            public string player_key;
            public string player_handle;
        }
        
        #region Lifecycle
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            LoadPersistedSession();
        }
        
        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
        
        #endregion
        
        #region Public API
        
        /// <summary>
        /// Register a new player with email/password.
        /// </summary>
        public async Task<bool> RegisterAsync(string email, string password, string playerHandle)
        {
            try
            {
                var url = $"{m_SupabaseUrl}/auth/v1/signup";
                var payload = new AuthRequest { email = email, password = password };
                var json = JsonConvert.SerializeObject(payload);
                
                using var request = new UnityWebRequest(url, "POST");
                request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("apikey", m_AnonKey);
                
                var op = request.SendWebRequest();
                while (!op.isDone) await Task.Yield();
                
                if (request.result != UnityWebRequest.Result.Success)
                {
                    NGLog.Error(Category, $"Registration failed: {request.error} - {request.downloadHandler.text}");
                    OnAuthError?.Invoke($"Registration failed: {ParseError(request.downloadHandler.text)}");
                    return false;
                }
                
                var response = JsonConvert.DeserializeObject<AuthResponse>(request.downloadHandler.text);
                await HandleAuthResponse(response, playerHandle);
                
                NGLog.Info(Category, $"Player registered: {email} -> {m_PlayerKey}");
                return true;
            }
            catch (Exception ex)
            {
                NGLog.Error(Category, $"Registration exception: {ex.Message}");
                OnAuthError?.Invoke($"Registration error: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Login existing player with email/password.
        /// </summary>
        public async Task<bool> LoginAsync(string email, string password)
        {
            try
            {
                var url = $"{m_SupabaseUrl}/auth/v1/token?grant_type=password";
                var payload = new AuthRequest { email = email, password = password };
                var json = JsonConvert.SerializeObject(payload);
                
                using var request = new UnityWebRequest(url, "POST");
                request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("apikey", m_AnonKey);
                
                var op = request.SendWebRequest();
                while (!op.isDone) await Task.Yield();
                
                if (request.result != UnityWebRequest.Result.Success)
                {
                    NGLog.Error(Category, $"Login failed: {request.error} - {request.downloadHandler.text}");
                    OnAuthError?.Invoke($"Login failed: {ParseError(request.downloadHandler.text)}");
                    return false;
                }
                
                var response = JsonConvert.DeserializeObject<AuthResponse>(request.downloadHandler.text);
                await HandleAuthResponse(response, null);
                
                NGLog.Info(Category, $"Player logged in: {email} -> {m_PlayerKey}");
                return true;
            }
            catch (Exception ex)
            {
                NGLog.Error(Category, $"Login exception: {ex.Message}");
                OnAuthError?.Invoke($"Login error: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Logout current player.
        /// </summary>
        public async Task LogoutAsync()
        {
            if (!string.IsNullOrEmpty(m_AccessToken))
            {
                try
                {
                    var url = $"{m_SupabaseUrl}/auth/v1/logout";
                    using var request = UnityWebRequest.PostWwwForm(url, "");
                    request.SetRequestHeader("Authorization", $"Bearer {m_AccessToken}");
                    request.SetRequestHeader("apikey", m_AnonKey);
                    
                    var op = request.SendWebRequest();
                    while (!op.isDone) await Task.Yield();
                }
                catch (Exception ex)
                {
                    NGLog.Warn(Category, $"Logout request failed (ignoring): {ex.Message}");
                }
            }
            
            ClearSession();
            NGLog.Info(Category, "Player logged out");
        }
        
        /// <summary>
        /// Refresh the access token if expired.
        /// </summary>
        public async Task<bool> RefreshSessionAsync()
        {
            if (string.IsNullOrEmpty(m_RefreshToken))
            {
                return false;
            }
            
            try
            {
                var url = $"{m_SupabaseUrl}/auth/v1/token?grant_type=refresh_token";
                var payload = new { refresh_token = m_RefreshToken };
                var json = JsonConvert.SerializeObject(payload);
                
                using var request = new UnityWebRequest(url, "POST");
                request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("apikey", m_AnonKey);
                
                var op = request.SendWebRequest();
                while (!op.isDone) await Task.Yield();
                
                if (request.result != UnityWebRequest.Result.Success)
                {
                    NGLog.Error(Category, $"Token refresh failed: {request.error}");
                    ClearSession();
                    return false;
                }
                
                var response = JsonConvert.DeserializeObject<AuthResponse>(request.downloadHandler.text);
                await HandleAuthResponse(response, null);
                
                NGLog.Info(Category, "Session refreshed");
                return true;
            }
            catch (Exception ex)
            {
                NGLog.Error(Category, $"Refresh exception: {ex.Message}");
                ClearSession();
                return false;
            }
        }
        
        #endregion
        
        #region Private Methods
        
        private async Task HandleAuthResponse(AuthResponse response, string playerHandle)
        {
            m_AccessToken = response.access_token;
            m_RefreshToken = response.refresh_token;
            m_UserId = response.user?.id;
            m_ExpiresAt = DateTime.UtcNow.AddSeconds(response.expires_in - 60); // 60s buffer
            
            // Get or create player profile
            if (!string.IsNullOrEmpty(response.user?.user_metadata?.player_key))
            {
                m_PlayerKey = response.user.user_metadata.player_key;
            }
            else
            {
                // Create player profile via RPC
                m_PlayerKey = await CreatePlayerProfileAsync(playerHandle ?? m_UserId.Substring(0, 8));
            }
            
            PersistSession();
            
            OnAuthStateChanged?.Invoke(new AuthSession
            {
                AccessToken = m_AccessToken,
                RefreshToken = m_RefreshToken,
                UserId = m_UserId,
                PlayerKey = m_PlayerKey,
                ExpiresAt = m_ExpiresAt,
                IsAuthenticated = true
            });
        }
        
        private async Task<string> CreatePlayerProfileAsync(string playerHandle)
        {
            try
            {
                var playerKey = $"player_{m_UserId.Substring(0, 8)}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
                
                var url = $"{m_SupabaseUrl}/rest/v1/rpc/create_player_profile";
                var payload = new
                {
                    p_player_key = playerKey,
                    p_player_handle = playerHandle,
                    p_auth_user_id = m_UserId
                };
                var json = JsonConvert.SerializeObject(payload);
                
                using var request = new UnityWebRequest(url, "POST");
                request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("apikey", m_AnonKey);
                request.SetRequestHeader("Authorization", $"Bearer {m_AccessToken}");
                
                var op = request.SendWebRequest();
                while (!op.isDone) await Task.Yield();
                
                if (request.result != UnityWebRequest.Result.Success)
                {
                    NGLog.Error(Category, $"Failed to create player profile: {request.error}");
                    // Fallback to a generated key
                    return playerKey;
                }
                
                NGLog.Info(Category, $"Created player profile: {playerKey}");
                return playerKey;
            }
            catch (Exception ex)
            {
                NGLog.Error(Category, $"Create profile exception: {ex.Message}");
                return $"player_{m_UserId.Substring(0, 8)}";
            }
        }
        
        private void PersistSession()
        {
            if (!m_PersistSession) return;
            
            PlayerPrefs.SetString("Supabase_AccessToken", m_AccessToken ?? "");
            PlayerPrefs.SetString("Supabase_RefreshToken", m_RefreshToken ?? "");
            PlayerPrefs.SetString("Supabase_UserId", m_UserId ?? "");
            PlayerPrefs.SetString("Supabase_PlayerKey", m_PlayerKey ?? "");
            PlayerPrefs.SetString("Supabase_ExpiresAt", m_ExpiresAt.ToString("O"));
            PlayerPrefs.Save();
        }
        
        private void LoadPersistedSession()
        {
            if (!m_PersistSession) return;
            
            m_AccessToken = PlayerPrefs.GetString("Supabase_AccessToken", "");
            m_RefreshToken = PlayerPrefs.GetString("Supabase_RefreshToken", "");
            m_UserId = PlayerPrefs.GetString("Supabase_UserId", "");
            m_PlayerKey = PlayerPrefs.GetString("Supabase_PlayerKey", "");
            
            if (DateTime.TryParse(PlayerPrefs.GetString("Supabase_ExpiresAt", ""), out var expiresAt))
            {
                m_ExpiresAt = expiresAt;
            }
            
            if (IsAuthenticated)
            {
                NGLog.Info(Category, $"Restored session for player: {m_PlayerKey}");
                OnAuthStateChanged?.Invoke(new AuthSession
                {
                    AccessToken = m_AccessToken,
                    RefreshToken = m_RefreshToken,
                    UserId = m_UserId,
                    PlayerKey = m_PlayerKey,
                    ExpiresAt = m_ExpiresAt,
                    IsAuthenticated = true
                });
            }
        }
        
        private void ClearSession()
        {
            m_AccessToken = null;
            m_RefreshToken = null;
            m_UserId = null;
            m_PlayerKey = null;
            m_ExpiresAt = DateTime.MinValue;
            
            if (m_PersistSession)
            {
                PlayerPrefs.DeleteKey("Supabase_AccessToken");
                PlayerPrefs.DeleteKey("Supabase_RefreshToken");
                PlayerPrefs.DeleteKey("Supabase_UserId");
                PlayerPrefs.DeleteKey("Supabase_PlayerKey");
                PlayerPrefs.DeleteKey("Supabase_ExpiresAt");
                PlayerPrefs.Save();
            }
            
            OnAuthStateChanged?.Invoke(new AuthSession
            {
                IsAuthenticated = false
            });
        }
        
        private string ParseError(string json)
        {
            try
            {
                var obj = JObject.Parse(json);
                return obj["msg"]?.Value<string>() ?? obj["error_description"]?.Value<string>() ?? json;
            }
            catch
            {
                return json;
            }
        }
        
        #endregion
    }
}
