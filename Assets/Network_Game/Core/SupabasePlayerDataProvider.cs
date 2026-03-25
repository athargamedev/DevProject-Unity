using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Network_Game.Auth;
using Network_Game.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace Network_Game.Core
{
    /// <summary>
    /// Syncs player game data with Supabase cloud storage.
    /// Works alongside PlayerDataManager for hybrid local/cloud persistence.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SupabasePlayerDataProvider : MonoBehaviour
    {
        private const string Category = "SupabaseData";
        
        [Header("Supabase")]
        [SerializeField] private string m_SupabaseUrl = "http://127.0.0.1:54321";
        [SerializeField] private string m_AnonKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZS1kZW1vIiwicm9sZSI6ImFub24iLCJleHAiOjE5ODM4MTI5OTZ9.CRXP1A7WOeoJeXxjNni43kdQwgnWNReilDMblYTn_I0";
        
        [Header("Sync")]
        [SerializeField] private bool m_EnableCloudSync = true;
        [SerializeField] private float m_SyncIntervalSeconds = 60f;
        
        private float m_LastSyncTime;
        private readonly HashSet<string> m_DirtyPlayers = new();
        
        public static SupabasePlayerDataProvider Instance { get; private set; }
        public bool IsCloudSyncEnabled => m_EnableCloudSync && SupabaseAuthService.Instance?.IsAuthenticated == true;

        public static SupabasePlayerDataProvider EnsureInstance()
        {
            if (Instance != null)
            {
                return Instance;
            }

            Instance = FindAnyObjectByType<SupabasePlayerDataProvider>();
            if (Instance != null)
            {
                return Instance;
            }

            var go = new GameObject("SupabasePlayerDataProvider");
            Instance = go.AddComponent<SupabasePlayerDataProvider>();
            return Instance;
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
        }
        
        private void OnEnable()
        {
            PlayerDataManager.OnPlayerDataSaved += HandlePlayerDataSaved;
            SupabaseAuthService.OnAuthStateChanged += HandleAuthStateChanged;
        }
        
        private void OnDisable()
        {
            PlayerDataManager.OnPlayerDataSaved -= HandlePlayerDataSaved;
            SupabaseAuthService.OnAuthStateChanged -= HandleAuthStateChanged;
        }
        
        private void Update()
        {
            if (!IsCloudSyncEnabled) return;
            
            if (Time.time - m_LastSyncTime >= m_SyncIntervalSeconds)
            {
                m_LastSyncTime = Time.time;
                _ = SyncAllDirtyPlayersAsync();
            }
        }
        
        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
        
        #endregion
        
        #region Public API
        
        /// <summary>
        /// Load player data from Supabase (cloud-first on login).
        /// </summary>
        public async Task<PlayerGameData> LoadFromCloudAsync(string playerKey)
        {
            if (!IsCloudSyncEnabled || string.IsNullOrEmpty(playerKey))
            {
                return null;
            }
            
            try
            {
                var token = SupabaseAuthService.Instance.AccessToken;
                var url = $"{m_SupabaseUrl}/rest/v1/rpc/get_player_game_data";
                var payload = new { p_player_key = playerKey };
                var json = JsonConvert.SerializeObject(payload);
                
                using var request = new UnityWebRequest(url, "POST");
                request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("apikey", m_AnonKey);
                request.SetRequestHeader("Authorization", $"Bearer {token}");
                
                var op = request.SendWebRequest();
                while (!op.isDone) await Task.Yield();
                
                if (request.result != UnityWebRequest.Result.Success)
                {
                    NGLog.Warn(Category, $"Failed to load cloud data: {request.error}");
                    return null;
                }
                
                var response = JToken.Parse(request.downloadHandler.text);
                if (response is JArray arr && arr.Count > 0)
                {
                    var data = ParseGameData(arr[0]);
                    NGLog.Info(Category, $"Loaded cloud data for {playerKey}: Lv.{data.Level} HP:{data.CurrentHealth}/{data.MaxHealth}");
                    return data;
                }
                
                return null;
            }
            catch (Exception ex)
            {
                NGLog.Error(Category, $"Load from cloud exception: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Save player data to Supabase immediately.
        /// </summary>
        public async Task<bool> SaveToCloudAsync(string playerKey, PlayerGameData data)
        {
            if (
                !IsCloudSyncEnabled
                || string.IsNullOrEmpty(playerKey)
                || data == null
                || !CanSyncPlayerKey(playerKey)
            )
            {
                return false;
            }
            
            try
            {
                var token = SupabaseAuthService.Instance.AccessToken;
                var url = $"{m_SupabaseUrl}/rest/v1/rpc/upsert_player_game_data";
                var payload = new
                {
                    p_player_key = playerKey,
                    p_max_health = data.MaxHealth,
                    p_current_health = data.CurrentHealth,
                    p_level = data.Level,
                    p_experience = data.Experience,
                    p_enemies_defeated = data.EnemiesDefeated,
                    p_deaths = data.Deaths,
                    p_dialogue_interactions = data.DialogueInteractions,
                    p_effects_survived = data.EffectsSurvived,
                    p_unlocked_effects = JArray.FromObject(data.UnlockedEffects ?? System.Array.Empty<string>()),
                    p_completed_quests = JArray.FromObject(data.CompletedQuests ?? System.Array.Empty<string>()),
                    p_preferred_damage_color = data.PreferredDamageColor,
                    p_total_sessions = data.TotalSessions,
                    p_total_play_time_seconds = data.TotalPlayTimeSeconds,
                    p_profile_slot = data.ProfileSlot
                };
                var json = JsonConvert.SerializeObject(payload);
                
                using var request = new UnityWebRequest(url, "POST");
                request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("apikey", m_AnonKey);
                request.SetRequestHeader("Authorization", $"Bearer {token}");
                
                var op = request.SendWebRequest();
                while (!op.isDone) await Task.Yield();
                
                if (request.result != UnityWebRequest.Result.Success)
                {
                    NGLog.Warn(Category, $"Failed to save cloud data: {request.error}");
                    return false;
                }
                
                NGLog.Debug(Category, $"Synced {playerKey} to cloud");
                return true;
            }
            catch (Exception ex)
            {
                NGLog.Error(Category, $"Save to cloud exception: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Mark a player for cloud sync.
        /// </summary>
        public void MarkDirty(string playerKey)
        {
            if (!string.IsNullOrEmpty(playerKey))
            {
                m_DirtyPlayers.Add(playerKey);
            }
        }
        
        #endregion
        
        #region Event Handlers
        
        private void HandlePlayerDataSaved(string playerKey, PlayerGameData data)
        {
            if (IsCloudSyncEnabled && CanSyncPlayerKey(playerKey))
            {
                MarkDirty(playerKey);
                // Immediate sync for important saves
                _ = SaveToCloudAsync(playerKey, data);
            }
        }
        
        private void HandleAuthStateChanged(SupabaseAuthService.AuthSession session)
        {
            if (session.IsAuthenticated)
            {
                NGLog.Info(Category, "Auth state changed: authenticated, will sync to cloud");
                // On login, we might want to pull cloud data first
            }
            else
            {
                NGLog.Info(Category, "Auth state changed: logged out, disabling cloud sync");
                m_DirtyPlayers.Clear();
            }
        }
        
        #endregion
        
        #region Private Methods
        
        private async Task SyncAllDirtyPlayersAsync()
        {
            if (m_DirtyPlayers.Count == 0) return;
            
            var copy = new List<string>(m_DirtyPlayers);
            m_DirtyPlayers.Clear();
            
            foreach (var playerKey in copy)
            {
                if (!CanSyncPlayerKey(playerKey))
                {
                    continue;
                }

                var data = PlayerDataManager.Instance?.GetPlayerData(playerKey);
                if (data != null)
                {
                    await SaveToCloudAsync(playerKey, data);
                }
            }
        }

        private static bool CanSyncPlayerKey(string playerKey)
        {
            if (string.IsNullOrWhiteSpace(playerKey))
            {
                return false;
            }

            string authenticatedKey = SupabaseAuthService.Instance?.CurrentPlayerKey;
            return !string.IsNullOrWhiteSpace(authenticatedKey)
                && string.Equals(playerKey, authenticatedKey, StringComparison.OrdinalIgnoreCase);
        }
        
        private PlayerGameData ParseGameData(JToken token)
        {
            return new PlayerGameData
            {
                PlayerId = token["player_key"]?.Value<string>(),
                MaxHealth = (float)(token["max_health"]?.Value<double>() ?? 100),
                CurrentHealth = (float)(token["current_health"]?.Value<double>() ?? 100),
                Level = token["level"]?.Value<int>() ?? 1,
                Experience = token["experience"]?.Value<int>() ?? 0,
                EnemiesDefeated = token["enemies_defeated"]?.Value<int>() ?? 0,
                Deaths = token["deaths"]?.Value<int>() ?? 0,
                DialogueInteractions = token["dialogue_interactions"]?.Value<int>() ?? 0,
                EffectsSurvived = token["effects_survived"]?.Value<int>() ?? 0,
                UnlockedEffects = token["unlocked_effects"]?.ToObject<string[]>() ?? System.Array.Empty<string>(),
                CompletedQuests = token["completed_quests"]?.ToObject<string[]>() ?? System.Array.Empty<string>(),
                PreferredDamageColor = token["preferred_damage_color"]?.Value<string>() ?? "default",
                TotalSessions = token["total_sessions"]?.Value<int>() ?? 1,
                TotalPlayTimeSeconds = (float)(token["total_play_time_seconds"]?.Value<double>() ?? 0),
                ProfileSlot = token["profile_slot"]?.Value<int>() ?? 0
            };
        }
        
        #endregion
    }
}
