using Unity.Netcode;
using UnityEngine;

namespace Network_Game.Behavior
{
    /// <summary>
    /// Ensures every spawned player GameObject has a unique, client-aware name on all instances.
    /// Add to the player prefab root alongside NetworkObject.
    ///
    /// Remote players (non-owned) are immediately renamed on spawn so they are distinguishable
    /// in the hierarchy even when they join after the local PlayerBootstrap has already run.
    /// The local player's name is set by PlayerBootstrap (which includes the auth NameId).
    ///
    /// Remote instance → "Player (Client {OwnerClientId})"
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerObjectIdentity : NetworkBehaviour
    {
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            // Local player naming is handled by PlayerBootstrap with auth-aware naming.
            // Only rename remote player objects here to avoid conflicts.
            if (!IsOwner)
            {
                gameObject.name = $"Player (Client {OwnerClientId})";
            }
        }

        // Re-apply name if ownership transfers (e.g. host migration).
        public override void OnGainedOwnership()
        {
            base.OnGainedOwnership();
            // Now we own it — let PlayerBootstrap rename with auth name on next RefreshRuntimePlayerNames.
            // Give it a temporary recognisable name in the meantime.
            gameObject.name = "Player (Ownership Transfer)";
        }

        public override void OnLostOwnership()
        {
            base.OnLostOwnership();
            gameObject.name = $"Player (Client {OwnerClientId})";
        }
    }
}
