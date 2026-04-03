using System;

namespace Network_Game.Dialogue
{
    /// <summary>
    /// Lightweight identifier for entities in damage/collision tracking.
    /// </summary>
    public readonly struct DialogueEntityId : IEquatable<DialogueEntityId>
    {
        public readonly string Value;

        public DialogueEntityId(string value)
        {
            Value = value ?? string.Empty;
        }

        public bool Equals(DialogueEntityId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is DialogueEntityId other && Equals(other);
        public override int GetHashCode() => Value?.GetHashCode() ?? 0;
        public override string ToString() => Value ?? "null";

        public static bool operator ==(DialogueEntityId left, DialogueEntityId right) => left.Equals(right);
        public static bool operator !=(DialogueEntityId left, DialogueEntityId right) => !left.Equals(right);
    }

    /// <summary>
    /// Extension methods for getting entity IDs from various types.
    /// </summary>
    public static class DialogueEntityIdExtensions
    {
        public static DialogueEntityId GetEntityId(this Combat.CombatHealthV2 health)
        {
            if (health == null) return new DialogueEntityId("null");
            
            // Use NetworkObjectId if available
            var netObj = health.GetComponent<Unity.Netcode.NetworkObject>();
            if (netObj != null)
            {
                return new DialogueEntityId($"net_{netObj.NetworkObjectId}");
            }
            
            return new DialogueEntityId(health.gameObject.name);
        }

        public static DialogueEntityId GetEntityId(this UnityEngine.GameObject gameObject)
        {
            if (gameObject == null) return new DialogueEntityId("null");
            
            var netObj = gameObject.GetComponent<Unity.Netcode.NetworkObject>();
            if (netObj != null)
            {
                return new DialogueEntityId($"net_{netObj.NetworkObjectId}");
            }
            
            return new DialogueEntityId(gameObject.name);
        }
    }
}
