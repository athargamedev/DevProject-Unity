using System;
using UnityEngine;

namespace Network_Game.Core
{
    /// <summary>
    /// Persistent player game data. Serialized to disk between sessions.
    /// </summary>
    [Serializable]
    public sealed class PlayerGameData
    {
        // Identity
        public string PlayerId;           // Unique persistent ID
        public string PlayerName;
        public int ProfileSlot;           // For multiple save slots
        
        // Timestamps
        public string CreatedAt;          // ISO 8601
        public string LastPlayedAt;       // ISO 8601
        public int TotalSessions;
        public float TotalPlayTimeSeconds;
        
        // Core Stats (Persistent)
        public float MaxHealth = 100f;
        public float CurrentHealth = 100f;
        public int Level = 1;
        public int Experience = 0;
        
        // Progression
        public int EnemiesDefeated;
        public int Deaths;
        public int DialogueInteractions;
        public int EffectsSurvived;
        
        // Unlocks & Customization
        public string[] UnlockedEffects = Array.Empty<string>();
        public string[] CompletedQuests = Array.Empty<string>();
        public string PreferredDamageColor = "default"; // For hit reaction glow
        
        // Session deltas (not saved, applied on load)
        [NonSerialized] public bool IsDirty;
        
        /// <summary>
        /// Creates default data for a new player.
        /// </summary>
        public static PlayerGameData CreateNew(string playerId, string playerName, int slot = 0)
        {
            return new PlayerGameData
            {
                PlayerId = playerId,
                PlayerName = playerName ?? "Player",
                ProfileSlot = slot,
                CreatedAt = DateTime.UtcNow.ToString("O"),
                LastPlayedAt = DateTime.UtcNow.ToString("O"),
                TotalSessions = 1,
                TotalPlayTimeSeconds = 0f,
                MaxHealth = 100f,
                CurrentHealth = 100f,
                Level = 1,
                Experience = 0,
                IsDirty = false
            };
        }
        
        /// <summary>
        /// Call when starting a new play session.
        /// </summary>
        public void OnSessionStart()
        {
            TotalSessions++;
            LastPlayedAt = DateTime.UtcNow.ToString("O");
            IsDirty = true;
        }
        
        /// <summary>
        /// Updates play time. Call periodically during gameplay.
        /// </summary>
        public void AddPlayTime(float deltaSeconds)
        {
            TotalPlayTimeSeconds += deltaSeconds;
            IsDirty = true;
        }
        
        /// <summary>
        /// Modifies health and marks dirty if changed.
        /// </summary>
        public bool ModifyHealth(float delta)
        {
            float previous = CurrentHealth;
            CurrentHealth = Mathf.Clamp(CurrentHealth + delta, 0f, MaxHealth);
            
            if (!Mathf.Approximately(previous, CurrentHealth))
            {
                IsDirty = true;
                return true;
            }
            return false;
        }
        
        /// <summary>
        /// Full heal. Call on respawn.
        /// </summary>
        public void RestoreHealth()
        {
            if (!Mathf.Approximately(CurrentHealth, MaxHealth))
            {
                CurrentHealth = MaxHealth;
                IsDirty = true;
            }
        }
        
        /// <summary>
        /// Adds experience and handles level-ups.
        /// </summary>
        public int AddExperience(int amount)
        {
            if (amount <= 0) return 0;
            
            Experience += amount;
            IsDirty = true;
            
            // Simple level formula: level * 100 XP needed per level
            int levelsGained = 0;
            while (Experience >= Level * 100)
            {
                Experience -= Level * 100;
                Level++;
                levelsGained++;
                
                // Health increase on level up
                MaxHealth += 10f;
                CurrentHealth = MaxHealth; // Full heal on level up
            }
            
            return levelsGained;
        }
        
        /// <summary>
        /// Records an effect survived (for stats/achievements).
        /// </summary>
        public void RecordEffectSurvived(string effectTag)
        {
            EffectsSurvived++;
            IsDirty = true;
        }
        
        /// <summary>
        /// Unlocks an effect for the player.
        /// </summary>
        public bool UnlockEffect(string effectTag)
        {
            if (string.IsNullOrWhiteSpace(effectTag)) return false;
            
            // Check if already unlocked
            foreach (var unlocked in UnlockedEffects)
            {
                if (unlocked.Equals(effectTag, StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            
            // Add to unlocked
            var list = new System.Collections.Generic.List<string>(UnlockedEffects);
            list.Add(effectTag);
            UnlockedEffects = list.ToArray();
            IsDirty = true;
            return true;
        }
        
        /// <summary>
        /// Returns true if this data needs to be saved to disk.
        /// </summary>
        public bool NeedsSave()
        {
            return IsDirty;
        }
        
        /// <summary>
        /// Marks as saved (clears dirty flag).
        /// </summary>
        public void MarkSaved()
        {
            IsDirty = false;
        }
        
        /// <summary>
        /// Creates a deep copy for session use.
        /// </summary>
        public PlayerGameData Clone()
        {
            return new PlayerGameData
            {
                PlayerId = PlayerId,
                PlayerName = PlayerName,
                ProfileSlot = ProfileSlot,
                CreatedAt = CreatedAt,
                LastPlayedAt = LastPlayedAt,
                TotalSessions = TotalSessions,
                TotalPlayTimeSeconds = TotalPlayTimeSeconds,
                MaxHealth = MaxHealth,
                CurrentHealth = CurrentHealth,
                Level = Level,
                Experience = Experience,
                EnemiesDefeated = EnemiesDefeated,
                Deaths = Deaths,
                DialogueInteractions = DialogueInteractions,
                EffectsSurvived = EffectsSurvived,
                UnlockedEffects = UnlockedEffects?.Length > 0 ? (string[])UnlockedEffects.Clone() : Array.Empty<string>(),
                CompletedQuests = CompletedQuests?.Length > 0 ? (string[])CompletedQuests.Clone() : Array.Empty<string>(),
                PreferredDamageColor = PreferredDamageColor,
                IsDirty = false
            };
        }
        
        public override string ToString()
        {
            return $"[{PlayerId}] {PlayerName} Lv.{Level} HP:{CurrentHealth:F0}/{MaxHealth:F0} XP:{Experience} Sessions:{TotalSessions}";
        }
    }
}
