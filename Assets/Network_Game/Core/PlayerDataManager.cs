using System;
using System.Collections.Generic;
using System.IO;
using Network_Game.Auth;
using Network_Game.Diagnostics;
using Unity.Netcode;
using UnityEngine;


namespace Network_Game.Core
{
    /// <summary>
    /// Central manager for persistent player game data.
    /// Handles save/load from disk and syncs with NetworkVariables for runtime.
    /// Server-authoritative: only server writes to disk.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public sealed class PlayerDataManager : NetworkBehaviour
    {
        public static PlayerDataManager Instance { get; private set; }
        
        [Header("Storage")]
        [SerializeField]
        [Tooltip("Subfolder in Application.persistentDataPath for saves")]
        private string m_SaveFolderName = "PlayerData";
        
        [SerializeField]
        [Tooltip("How often to auto-save dirty data (seconds)")]
        private float m_AutoSaveIntervalSeconds = 30f;
        
        
        
        [Header("Defaults")]
        [SerializeField]
        
        
        // Runtime storage: playerId -> session data
        private readonly Dictionary<string, PlayerGameData> m_SessionData = new();
        private readonly Dictionary<ulong, string> m_NetworkIdToPlayerId = new();
        
        private string m_SaveDirectoryPath;
        private float m_LastAutoSaveTime;
        private bool m_IsInitialized;
        
        // Events
        public static event Action<string, PlayerGameData> OnPlayerDataLoaded;
        public static event Action<string, PlayerGameData> OnPlayerDataSaved;
        public static event Action<string, int> OnPlayerLevelUp;
        public static event Action<string> OnPlayerDeath;
        
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
            
            InitializeStorage();
        }
        
        private void InitializeStorage()
        {
            m_SaveDirectoryPath = Path.Combine(Application.persistentDataPath, m_SaveFolderName);
            try
            {
                if (!Directory.Exists(m_SaveDirectoryPath))
                {
                    Directory.CreateDirectory(m_SaveDirectoryPath);
                    NGLog.Info("PlayerData", $"Created save directory: {m_SaveDirectoryPath}");
                }
                m_IsInitialized = true;
            }
            catch (Exception ex)
            {
                NGLog.Error("PlayerData", $"Failed to initialize storage: {ex.Message}");
            }
        }
        
        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                // Subscribe to player connection events
                NetworkManager.Singleton.OnConnectionEvent += HandleConnectionEvent;
                
                // Load data for already-connected players
                foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
                {
                    TryLoadOrCreatePlayerData(client.ClientId);
                }
            }
        }
        
        public override void OnNetworkDespawn()
        {
            if (IsServer && NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnConnectionEvent -= HandleConnectionEvent;
            }
            
            // Save all dirty data on shutdown
            SaveAllDirtyData();
        }
        
        private void Update()
        {
            if (!IsServer) return;
            
            // Auto-save dirty data
            if (Time.time - m_LastAutoSaveTime >= m_AutoSaveIntervalSeconds)
            {
                SaveAllDirtyData();
                m_LastAutoSaveTime = Time.time;
            }
        }
        
        private void OnApplicationQuit()
        {
            SaveAllDirtyData();
        }
        
        #endregion
        
        #region Connection Handling
        
        private void HandleConnectionEvent(NetworkManager manager, ConnectionEventData eventData)
        {
            if (!IsServer) return;
            
            switch (eventData.EventType)
            {
                case ConnectionEvent.ClientConnected:
                    TryLoadOrCreatePlayerData(eventData.ClientId);
                    break;
                    
                case ConnectionEvent.ClientDisconnected:
                    // Save this player's data immediately
                    if (m_NetworkIdToPlayerId.TryGetValue(eventData.ClientId, out string playerId))
                    {
                        SavePlayerData(playerId);
                        m_NetworkIdToPlayerId.Remove(eventData.ClientId);
                    }
                    break;
            }
        }
        
        private async void TryLoadOrCreatePlayerData(ulong clientId)
        {
            // Get player identity from auth service
            string playerId = GetPlayerIdForClient(clientId);
            string playerName = GetPlayerNameForClient(clientId);
            
            if (string.IsNullOrWhiteSpace(playerId))
            {
                NGLog.Warn("PlayerData", $"No player ID for client {clientId}");
                return;
            }
            
            m_NetworkIdToPlayerId[clientId] = playerId;
            
            PlayerGameData data = null;
            
            // Try load from Supabase first (cloud-first on authenticated sessions)
            if (ShouldUseSupabaseIdentityForClient(clientId))
            {
                var supabaseKey = SupabaseAuthService.Instance.CurrentPlayerKey;
                if (!string.IsNullOrEmpty(supabaseKey))
                {
                    data = await SupabasePlayerDataProvider.Instance?.LoadFromCloudAsync(supabaseKey);
                    if (data != null)
                    {
                        // Use the Supabase player key as our local ID for consistency
                        playerId = supabaseKey;
                        m_NetworkIdToPlayerId[clientId] = playerId;
                        NGLog.Info("PlayerData", $"Loaded cloud data for {playerId}");
                    }
                }
            }
            
            // Fall back to local disk
            if (data == null)
            {
                data = LoadFromDisk(playerId);
            }
            
            if (data == null)
            {
                // Create new
                data = PlayerGameData.CreateNew(playerId, playerName, 0);
                NGLog.Info("PlayerData", $"Created new data for {playerId}");
            }
            else
            {
                data.OnSessionStart();
                NGLog.Info("PlayerData", $"Loaded data for {playerId}: Lv.{data.Level} HP:{data.CurrentHealth}/{data.MaxHealth}");
            }
            
            m_SessionData[playerId] = data;
            OnPlayerDataLoaded?.Invoke(playerId, data);
            
            // Sync to client
            SyncDataToClient(clientId, data);
        }
        
        private string GetPlayerIdForClient(ulong clientId)
        {
            if (NetworkManager.Singleton != null
                && clientId == NetworkManager.Singleton.LocalClientId
                && LocalPlayerAuthService.Instance != null
                && LocalPlayerAuthService.Instance.HasCurrentPlayer)
            {
                string nameId = LocalPlayerAuthService.Instance.CurrentPlayer.NameId;
                if (!string.IsNullOrWhiteSpace(nameId))
                {
                    return nameId.Trim().ToLowerInvariant();
                }
            }

            return $"player_client_{clientId}";
        }
        
        private string GetPlayerNameForClient(ulong clientId)
        {
            if (NetworkManager.Singleton != null
                && clientId == NetworkManager.Singleton.LocalClientId
                && LocalPlayerAuthService.Instance != null
                && LocalPlayerAuthService.Instance.HasCurrentPlayer)
            {
                string nameId = LocalPlayerAuthService.Instance.CurrentPlayer.NameId;
                if (!string.IsNullOrWhiteSpace(nameId))
                {
                    return nameId.Trim();
                }
            }

            return $"Player Client {clientId}";
        }

        private static bool ShouldUseSupabaseIdentityForClient(ulong clientId)
        {
            if (SupabaseAuthService.Instance?.IsAuthenticated != true)
            {
                return false;
            }

            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsListening)
            {
                return false;
            }

            return clientId == manager.LocalClientId;
        }
        
        #endregion
        
        #region Public API
        
        /// <summary>
        /// Gets player data for the current session. Returns null if not loaded.
        /// </summary>
        public PlayerGameData GetPlayerData(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId)) return null;
            m_SessionData.TryGetValue(playerId, out var data);
            return data;
        }
        
        /// <summary>
        /// Gets player data by network client ID.
        /// </summary>
        public PlayerGameData GetPlayerData(ulong clientId)
        {
            if (!m_NetworkIdToPlayerId.TryGetValue(clientId, out string playerId))
                return null;
            return GetPlayerData(playerId);
        }
        
        /// <summary>
        /// Gets data for the local player (client-side).
        /// </summary>
        public PlayerGameData GetLocalPlayerData()
        {
            if (NetworkManager.Singleton == null) return null;
            return GetPlayerData(NetworkManager.Singleton.LocalClientId);
        }
        
        /// <summary>
        /// Modifies a player's health and syncs to network.
        /// Call from CombatHealth.ApplyDamage on server.
        /// </summary>
        public bool ModifyHealth(string playerId, float delta, ulong sourceId = 0, string damageType = "")
        {
            if (!IsServer) return false;
            
            var data = GetPlayerData(playerId);
            if (data == null) return false;
            
            bool changed = data.ModifyHealth(delta);
            if (!changed) return false;
            
            // Check for death
            if (data.CurrentHealth <= 0f && delta < 0f)
            {
                data.Deaths++;
                OnPlayerDeath?.Invoke(playerId);
                
                // Schedule respawn restore
                StartCoroutine(RespawnPlayerDelayed(playerId, 2f));
            }
            
            // Notify clients
            NotifyHealthChangedClientRpc(playerId, data.CurrentHealth, data.MaxHealth, delta, sourceId, damageType);
            
            return true;
        }
        
        /// <summary>
        /// Adds experience to a player.
        /// </summary>
        public int AddExperience(string playerId, int amount)
        {
            if (!IsServer) return 0;
            
            var data = GetPlayerData(playerId);
            if (data == null) return 0;
            
            int levels = data.AddExperience(amount);
            if (levels > 0)
            {
                OnPlayerLevelUp?.Invoke(playerId, data.Level);
                NotifyLevelUpClientRpc(playerId, data.Level, levels);
            }
            
            return levels;
        }
        
        /// <summary>
        /// Records that a player survived an effect.
        /// </summary>
        public void RecordEffectSurvived(string playerId, string effectTag)
        {
            var data = GetPlayerData(playerId);
            data?.RecordEffectSurvived(effectTag);
        }
        
        /// <summary>
        /// Unlocks an effect for a player.
        /// </summary>
        public bool UnlockEffect(string playerId, string effectTag)
        {
            var data = GetPlayerData(playerId);
            return data?.UnlockEffect(effectTag) ?? false;
        }
        
        /// <summary>
        /// Forces a save of all dirty player data.
        /// </summary>
        public void SaveAllDirtyData()
        {
            if (!IsServer) return;
            
            foreach (var kvp in m_SessionData)
            {
                if (kvp.Value.NeedsSave())
                {
                    SavePlayerData(kvp.Key);
                }
            }
        }
        
        /// <summary>
        /// Forces a save of specific player data.
        /// </summary>
        public void SavePlayerData(string playerId)
        {
            if (!IsServer) return;
            
            var data = GetPlayerData(playerId);
            if (data == null) return;
            
            if (SaveToDisk(data))
            {
                data.MarkSaved();
                OnPlayerDataSaved?.Invoke(playerId, data);
            }
        }
        
        #endregion
        
        #region Persistence
        
        private PlayerGameData LoadFromDisk(string playerId)
        {
            if (!m_IsInitialized) return null;
            
            string path = GetSaveFilePath(playerId);
            if (!File.Exists(path))
            {
                return null;
            }
            
            try
            {
                string json = File.ReadAllText(path);
                var data = JsonUtility.FromJson<PlayerGameData>(json);
                data?.MarkSaved(); // Fresh load = not dirty
                return data;
            }
            catch (Exception ex)
            {
                NGLog.Error("PlayerData", $"Failed to load {playerId}: {ex.Message}");
                return null;
            }
        }
        
        private bool SaveToDisk(PlayerGameData data)
        {
            if (!m_IsInitialized || data == null) return false;
            
            string path = GetSaveFilePath(data.PlayerId);
            string tempPath = path + ".tmp";
            
            try
            {
                string json = JsonUtility.ToJson(data, prettyPrint: true);
                File.WriteAllText(tempPath, json);
                
                // Atomic replace
                if (File.Exists(path))
                {
                    File.Replace(tempPath, path, path + ".bak");
                }
                else
                {
                    File.Move(tempPath, path);
                }
                
                return true;
            }
            catch (Exception ex)
            {
                NGLog.Error("PlayerData", $"Failed to save {data.PlayerId}: {ex.Message}");
                return false;
            }
        }
        
        private string GetSaveFilePath(string playerId)
        {
            string safeId = string.Join("_", playerId.Split(Path.GetInvalidFileNameChars()));
            return Path.Combine(m_SaveDirectoryPath, $"{safeId}.json");
        }
        
        #endregion
        
        #region Network Sync
        
        private void SyncDataToClient(ulong clientId, PlayerGameData data)
        {
            if (!IsServer) return;
            
            // Send essential data via ClientRpc
            SyncPlayerDataClientRpc(
                data.PlayerId,
                data.PlayerName,
                data.CurrentHealth,
                data.MaxHealth,
                data.Level,
                data.Experience,
                new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } }
            );
        }
        
        [ClientRpc]
        private void SyncPlayerDataClientRpc(
            string playerId,
            string playerName,
            float currentHealth,
            float maxHealth,
            int level,
            int experience,
            ClientRpcParams rpcParams = default)
        {
            if (!m_SessionData.TryGetValue(playerId, out PlayerGameData data) || data == null)
            {
                data = PlayerGameData.CreateNew(playerId, playerName);
                m_SessionData[playerId] = data;
            }

            data.PlayerName = playerName;
            data.Level = Mathf.Max(1, level);
            data.Experience = Mathf.Max(0, experience);
            data.SetHealthSnapshot(currentHealth, maxHealth);
            data.MarkSaved();

            NGLog.Debug("PlayerData", $"Synced: {playerName} HP:{currentHealth:F0}/{maxHealth:F0} Lv.{level}");
        }
        
        [ClientRpc]
        private void NotifyHealthChangedClientRpc(
            string playerId,
            float currentHealth,
            float maxHealth,
            float delta,
            ulong sourceId,
            string damageType)
        {
            if (!m_SessionData.TryGetValue(playerId, out PlayerGameData data) || data == null)
            {
                data = PlayerGameData.CreateNew(playerId, playerId);
                m_SessionData[playerId] = data;
            }

            data.SetHealthSnapshot(currentHealth, maxHealth);
            data.MarkSaved();
        }
        
        [ClientRpc]
        private void NotifyLevelUpClientRpc(string playerId, int newLevel, int levelsGained)
        {
            NGLog.Info("PlayerData", $"🎉 LEVEL UP! {playerId} is now level {newLevel} (+{levelsGained})");
            // Trigger UI celebration
        }
        
        private System.Collections.IEnumerator RespawnPlayerDelayed(string playerId, float delay)
        {
            yield return new WaitForSeconds(delay);
            
            var data = GetPlayerData(playerId);
            if (data == null) yield break;
            
            // Full heal on respawn
            data.RestoreHealth();
            NGLog.Info("PlayerData", $"Player {playerId} respawned with full health");
            
            // Trigger respawn position reset
            ResetPlayerPosition(playerId);
        }
        
        private void ResetPlayerPosition(string playerId)
        {
            // Find the client ID for this player
            ulong clientId = 0;
            foreach (var kvp in m_NetworkIdToPlayerId)
            {
                if (kvp.Value == playerId)
                {
                    clientId = kvp.Key;
                    break;
                }
            }
            
            if (clientId == 0)
            {
                NGLog.Warn("PlayerData", $"Could not find client ID for player {playerId} during respawn position reset");
                return;
            }
            
            // Get the player's NetworkObject
            NetworkObject playerNetworkObject = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(clientId);
            if (playerNetworkObject == null)
            {
                NGLog.Warn("PlayerData", $"Could not find NetworkObject for player {playerId} (client {clientId}) during respawn");
                return;
            }
            
            // Get the spawn point from SceneSpawnManager
            var sceneSpawnManager = FindAnyObjectByType<SceneSpawnManager>();
            if (sceneSpawnManager == null || sceneSpawnManager.PlayerSpawnPoint == null)
            {
                NGLog.Warn("PlayerData", $"Could not find valid spawn point for player {playerId} respawn");
                return;
            }
            
            // Move player to spawn point
            Transform spawnPoint = sceneSpawnManager.PlayerSpawnPoint;
            GameObject playerObject = playerNetworkObject.gameObject;
            
            // Disable character controller temporarily to allow position change
            CharacterController characterController = playerObject.GetComponent<CharacterController>();
            bool controllerWasEnabled = characterController != null && characterController.enabled;
            if (controllerWasEnabled)
                characterController.enabled = false;
            
            // Set position and rotation
            playerObject.transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);
            
            // Re-enable character controller
            if (controllerWasEnabled)
                characterController.enabled = true;
            
            NGLog.Info("PlayerData", $"Reset player {playerId} position to spawn point: {spawnPoint.position}");
        }
        
        #endregion
        
        #region Debug
        
        [ContextMenu("Force Save All")]
        private void DebugForceSaveAll()
        {
            SaveAllDirtyData();
            NGLog.Info("PlayerData", "Force saved all player data");
        }
        
        [ContextMenu("Print Session Data")]
        private void DebugPrintSessionData()
        {
            foreach (var kvp in m_SessionData)
            {
                Debug.Log($"[PlayerData] {kvp.Value}");
            }
        }
        
        #endregion
    }
}
