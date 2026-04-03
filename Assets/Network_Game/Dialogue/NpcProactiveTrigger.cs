using System.Collections.Generic;
using Network_Game.Combat;
using Network_Game.Diagnostics;
using Unity.Netcode;
using UnityEngine;

namespace Network_Game.Dialogue
{
    /// <summary>
    /// Server-authoritative component that makes an NPC react to nearby gameplay
    /// events (player enter/exit proximity, health critical, player death) by
    /// automatically enqueuing a dialogue request.
    ///
    /// <para>Attach to any NPC that has a <see cref="NetworkObject"/> and an
    /// optional <see cref="NpcDialogueActor"/>. All trigger logic runs only on the
    /// server; clients are silent.</para>
    ///
    /// <para>Requires a <see cref="SphereCollider"/> (trigger) to detect proximity.
    /// The collider is auto-created at runtime if none is present.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NpcProactiveTrigger : NetworkBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────────

        [Header("Proximity")]
        [SerializeField]
        [Tooltip("Radius in world units within which players trigger NPC reactions.")]
        [Min(0.5f)]
        private float m_TriggerRadius = 8f;

        [SerializeField]
        [Tooltip("Layer mask for player detection. Leave as default to hit all layers.")]
        private LayerMask m_PlayerLayerMask = ~0;

        [Header("Trigger Types")]
        [SerializeField]
        [Tooltip("React when a player enters the trigger radius for the first time.")]
        private bool m_OnPlayerEnter = true;

        [SerializeField]
        [Tooltip("React when a player leaves the trigger radius.")]
        private bool m_OnPlayerExit = false;

        [SerializeField]
        [Tooltip("React when a nearby player's health drops below the critical threshold.")]
        private bool m_OnPlayerHealthCritical = true;

        [SerializeField]
        [Range(0.05f, 0.4f)]
        [Tooltip("Normalised health threshold (0–1) that counts as critical.")]
        private float m_CriticalHealthThreshold = 0.25f;

        [SerializeField]
        [Tooltip("React when a nearby player dies.")]
        private bool m_OnPlayerDeath = true;

        [Header("Prompts")]
        [SerializeField]
        [Tooltip("Prompt injected when a player enters the NPC's radius.")]
        [TextArea(2, 4)]
        private string m_EnterPrompt =
            "[A player just walked up to you. React in character — greet, warn, or acknowledge them.]";

        [SerializeField]
        [Tooltip("Prompt injected when a player leaves the NPC's radius.")]
        [TextArea(2, 4)]
        private string m_ExitPrompt =
            "[The player just walked away. React briefly in character.]";

        [SerializeField]
        [Tooltip("Prompt injected when a nearby player's health becomes critical.")]
        [TextArea(2, 4)]
        private string m_CriticalHealthPrompt =
            "[A nearby player's health is critically low. React in character — concern, taunt, or urgency.]";

        [SerializeField]
        [Tooltip("Prompt injected when a nearby player dies.")]
        [TextArea(2, 4)]
        private string m_DeathPrompt =
            "[A player near you just died. React in character — grief, triumph, or indifference.]";

        [Header("Cooldowns")]
        [SerializeField]
        [Min(0f)]
        [Tooltip("Minimum seconds between any two proactive dialogue reactions.")]
        private float m_GlobalCooldownSeconds = 12f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Minimum seconds before the same player can trigger the same event again.")]
        private float m_PerPlayerCooldownSeconds = 30f;

        [Header("Broadcast")]
        [SerializeField]
        [Tooltip("Show the NPC's proactive speech as a world-space broadcast (visible to all).")]
        private bool m_BroadcastReaction = true;

        [SerializeField]
        [Min(1f)]
        private float m_BroadcastDurationSeconds = 5f;

        [SerializeField]
        private bool m_LogDebug;

        // ── Runtime ──────────────────────────────────────────────────────────

        private NetworkObject m_NetworkObject;
        private NpcDialogueActor m_Actor;

        private float m_LastReactionAt = float.NegativeInfinity;
        private readonly Dictionary<ulong, float> m_LastReactionAtByClient =
            new Dictionary<ulong, float>();
        private readonly Dictionary<ulong, bool> m_WasHealthCriticalByClient =
            new Dictionary<ulong, bool>();

        // Tracks which players are currently inside the trigger radius.
        private readonly HashSet<ulong> m_PlayersInRange = new HashSet<ulong>();

        // ── Unity ────────────────────────────────────────────────────────────

        private void Awake()
        {
            m_NetworkObject = GetComponent<NetworkObject>();
            m_Actor         = GetComponent<NpcDialogueActor>();
            EnsureSphereCollider();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (!IsServer) return;

            CombatHealthV2.OnHealthChanged += HandleHealthChanged;
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            CombatHealthV2.OnHealthChanged -= HandleHealthChanged;
        }

        // ── Collider proximity ───────────────────────────────────────────────

        private void OnTriggerEnter(Collider other)
        {
            if (!IsServer) return;

            if (!TryResolvePlayerClientId(other.gameObject, out ulong clientId, out ulong netObjId))
                return;

            m_PlayersInRange.Add(clientId);

            if (m_OnPlayerEnter && !string.IsNullOrWhiteSpace(m_EnterPrompt))
                TryEnqueueReaction(m_EnterPrompt, netObjId, clientId, "player_enter");
        }

        private void OnTriggerExit(Collider other)
        {
            if (!IsServer) return;

            if (!TryResolvePlayerClientId(other.gameObject, out ulong clientId, out ulong netObjId))
                return;

            m_PlayersInRange.Remove(clientId);
            m_WasHealthCriticalByClient.Remove(clientId);

            if (m_OnPlayerExit && !string.IsNullOrWhiteSpace(m_ExitPrompt))
                TryEnqueueReaction(m_ExitPrompt, netObjId, clientId, "player_exit");
        }

        // ── Health event ─────────────────────────────────────────────────────

        private void HandleHealthChanged(
            CombatHealthV2 health,
            CombatHealthV2.HealthChangedEvent changeEvent
        )
        {
            if (!IsServer || health == null) return;

            NetworkObject netObj = health.GetComponentInParent<NetworkObject>();
            if (netObj == null || netObj == m_NetworkObject) return;

            ulong clientId = netObj.OwnerClientId;

            // Only react to players currently inside the trigger radius.
            if (!m_PlayersInRange.Contains(clientId)) return;

            // Death
            if (m_OnPlayerDeath && changeEvent.CurrentHealth <= 0f)
            {
                m_WasHealthCriticalByClient.Remove(clientId);
                if (!string.IsNullOrWhiteSpace(m_DeathPrompt))
                    TryEnqueueReaction(m_DeathPrompt, netObj.NetworkObjectId, clientId, "player_death");
                return;
            }

            // Critical health (one-shot per dip below threshold)
            if (m_OnPlayerHealthCritical && changeEvent.MaxHealth > 0f)
            {
                bool nowCritical = changeEvent.CurrentHealth / changeEvent.MaxHealth < m_CriticalHealthThreshold;
                m_WasHealthCriticalByClient.TryGetValue(clientId, out bool wasCritical);

                if (nowCritical && !wasCritical)
                {
                    m_WasHealthCriticalByClient[clientId] = true;
                    if (!string.IsNullOrWhiteSpace(m_CriticalHealthPrompt))
                        TryEnqueueReaction(m_CriticalHealthPrompt, netObj.NetworkObjectId, clientId, "player_critical");
                }
                else if (!nowCritical)
                {
                    m_WasHealthCriticalByClient[clientId] = false;
                }
            }
        }

        // ── Core enqueue ─────────────────────────────────────────────────────

        private bool TryEnqueueReaction(
            string prompt,
            ulong listenerNetworkId,
            ulong requestingClientId,
            string triggerTag
        )
        {
            NetworkDialogueService service = NetworkDialogueService.Instance;
            if (service == null || m_NetworkObject == null)
                return false;

            float now = Time.realtimeSinceStartup;

            // Global cooldown
            if (now - m_LastReactionAt < m_GlobalCooldownSeconds)
            {
                if (m_LogDebug)
                    NGLog.Debug("NpcProactiveTrigger", $"Global cooldown active — skipping {triggerTag}.");
                return false;
            }

            // Per-player cooldown
            if (
                m_LastReactionAtByClient.TryGetValue(requestingClientId, out float lastClientAt)
                && now - lastClientAt < m_PerPlayerCooldownSeconds
            )
            {
                if (m_LogDebug)
                    NGLog.Debug("NpcProactiveTrigger", $"Per-player cooldown active for client {requestingClientId} — skipping {triggerTag}.");
                return false;
            }

            var request = new NetworkDialogueService.DialogueRequest
            {
                Prompt             = prompt,
                SpeakerNetworkId   = m_NetworkObject.NetworkObjectId,
                ListenerNetworkId  = listenerNetworkId,
                RequestingClientId = requestingClientId,
                IsUserInitiated    = false,
                Broadcast          = m_BroadcastReaction,
                BroadcastDuration  = m_BroadcastDurationSeconds,
                NotifyClient       = true,
                BlockRepeatedPrompt = true,
                MinRepeatDelaySeconds = m_GlobalCooldownSeconds,
            };

            int requestId = service.EnqueueRequest(request);
            if (requestId < 0)
            {
                if (m_LogDebug)
                    NGLog.Debug("NpcProactiveTrigger", $"EnqueueRequest rejected for trigger {triggerTag}.");
                return false;
            }

            m_LastReactionAt = now;
            m_LastReactionAtByClient[requestingClientId] = now;

            if (m_LogDebug)
            {
                NGLog.Debug(
                    "NpcProactiveTrigger",
                    NGLog.Format(
                        "Proactive dialogue enqueued",
                        ("trigger", triggerTag),
                        ("requestId", requestId),
                        ("client", requestingClientId)
                    )
                );
            }

            return true;
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static bool TryResolvePlayerClientId(
            GameObject go,
            out ulong clientId,
            out ulong networkObjectId
        )
        {
            clientId = 0;
            networkObjectId = 0;

            NetworkObject netObj = go.GetComponentInParent<NetworkObject>();
            if (netObj == null) return false;

            // Exclude NPCs from triggering proactive reactions to themselves.
            if (go.GetComponentInParent<NpcDialogueActor>() != null) return false;

            clientId        = netObj.OwnerClientId;
            networkObjectId = netObj.NetworkObjectId;
            return true;
        }

        private void EnsureSphereCollider()
        {
            // Check for any existing trigger collider; add one if absent.
            Collider[] colliders = GetComponents<Collider>();
            foreach (Collider c in colliders)
            {
                if (c.isTrigger) return;
            }

            SphereCollider sphere = gameObject.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius    = m_TriggerRadius;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 0.2f, 0.25f);
            Gizmos.DrawSphere(transform.position, m_TriggerRadius);
            Gizmos.color = new Color(0.2f, 0.8f, 0.2f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, m_TriggerRadius);
        }
#endif
    }
}
