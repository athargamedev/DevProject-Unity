using Network_Game.Combat;
using Network_Game.Core;
using Network_Game.Auth;
using Unity.Netcode;
using UnityEngine;

namespace Network_Game
{
    /// <summary>
    /// Bridges CombatHealthV2 events to PlayerDataManager for persistent health storage.
    /// Placed in main Network_Game assembly to avoid cyclic dependencies.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-50)]
    public sealed class PlayerDataCombatBridge : MonoBehaviour
    {
        [SerializeField]
        private bool m_LogDebug = false;

        private void OnEnable()
        {
            // Subscribe to CombatHealthV2 events
            CombatHealthV2.OnHealthChanged += HandleHealthChanged;
            CombatHealthV2.OnDied += HandleDeath;
        }

        private void OnDisable()
        {
            CombatHealthV2.OnHealthChanged -= HandleHealthChanged;
            CombatHealthV2.OnDied -= HandleDeath;
        }

        private void HandleHealthChanged(CombatHealthV2 health, CombatHealthV2.HealthChangedEvent evt)
        {
            if (!NetworkManager.Singleton.IsServer) return;

            // Get player ID from owner
            string playerId = GetPlayerId(health);
            if (string.IsNullOrWhiteSpace(playerId)) return;

            // Update persistent data
            var data = PlayerDataManager.Instance?.GetPlayerData(playerId);
            if (data != null)
            {
                data.SetHealthSnapshot(evt.CurrentHealth, evt.MaxHealth);

                if (m_LogDebug)
                {
                    Debug.Log($"[PlayerDataBridge] Updated {playerId} health: {data.CurrentHealth:F0}/{data.MaxHealth:F0}");
                }

                if (
                    SupabasePlayerDataProvider.Instance?.IsCloudSyncEnabled == true
                    && string.Equals(
                        playerId,
                        SupabaseAuthService.Instance?.CurrentPlayerKey,
                        System.StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    _ = SupabasePlayerDataProvider.Instance.SaveToCloudAsync(playerId, data);
                }
            }
        }

        private void HandleDeath(CombatHealthV2 health, CombatHealthV2.LifeStateEvent evt)
        {
            if (!NetworkManager.Singleton.IsServer) return;

            string playerId = GetPlayerId(health);
            if (!string.IsNullOrWhiteSpace(playerId))
            {
                var data = PlayerDataManager.Instance?.GetPlayerData(playerId);
                if (data != null)
                {
                    data.Deaths++;
                }

                // Health snapshot is already mirrored in HandleHealthChanged.
                if (m_LogDebug)
                {
                    Debug.Log($"[PlayerDataBridge] Player {playerId} died");
                }
            }
        }

        private string GetPlayerId(CombatHealthV2 health)
        {
            // Try to get owner client ID
            if (health.CachedNetworkObject != null)
            {
                return PlayerDataManager.Instance?.GetPlayerData(health.CachedNetworkObject.OwnerClientId)?.PlayerId;
            }
            return null;
        }
    }
}
