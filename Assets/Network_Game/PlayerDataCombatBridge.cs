using Network_Game.Combat;
using Network_Game.Core;
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
            CombatHealthV2.OnDamageApplied += HandleDamageApplied;
            CombatHealthV2.OnDied += HandleDeath;
        }

        private void OnDisable()
        {
            CombatHealthV2.OnDamageApplied -= HandleDamageApplied;
            CombatHealthV2.OnDied -= HandleDeath;
        }

        private void HandleDamageApplied(CombatHealthV2 health, CombatHealthV2.DamageEvent evt)
        {
            if (!NetworkManager.Singleton.IsServer) return;

            // Get player ID from owner
            string playerId = GetPlayerId(health);
            if (string.IsNullOrWhiteSpace(playerId)) return;

            // Update persistent data
            var data = PlayerDataManager.Instance?.GetPlayerData(playerId);
            if (data != null)
            {
                data.ModifyHealth(-evt.DamageAmount);
                
                if (evt.IsLethal)
                {
                    data.Deaths++;
                }

                if (m_LogDebug)
                {
                    Debug.Log($"[PlayerDataBridge] Updated {playerId} health: {data.CurrentHealth:F0}/{data.MaxHealth:F0}");
                }
            }
        }

        private void HandleDeath(CombatHealthV2 health, CombatHealthV2.LifeStateEvent evt)
        {
            if (!NetworkManager.Singleton.IsServer) return;

            string playerId = GetPlayerId(health);
            if (!string.IsNullOrWhiteSpace(playerId))
            {
                // Data already updated in HandleDamageApplied, just log here
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
