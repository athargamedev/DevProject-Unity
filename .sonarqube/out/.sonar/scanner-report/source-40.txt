using System;
using System.Collections;
using System.Collections.Generic;
using Network_Game.Diagnostics;
using Unity.Netcode;
using UnityEngine;

namespace Network_Game.Combat
{
    /// <summary>
    /// Consolidated health system with persistent player data integration.
    /// Combines real-time NetworkVariable sync with disk persistence via PlayerDataManager.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CombatHealthV2 : NetworkBehaviour
    {
        #region Events
        
        public struct DamageEvent
        {
            public CombatHealthV2 Target;
            public ulong SourceNetworkObjectId;
            public float DamageAmount;
            public float PreviousHealth;
            public float CurrentHealth;
            public string DamageType;
            public bool IsLethal;
        }

        public struct HealthChangedEvent
        {
            public CombatHealthV2 Target;
            public float PreviousHealth;
            public float CurrentHealth;
            public float MaxHealth;
        }

        public struct LifeStateEvent
        {
            public CombatHealthV2 Target;
            public float CurrentHealth;
            public float MaxHealth;
        }

        public static event Action<CombatHealthV2, DamageEvent> OnDamageApplied;
        public static event Action<CombatHealthV2, HealthChangedEvent> OnHealthChanged;
        public static event Action<CombatHealthV2, LifeStateEvent> OnDied;
        public static event Action<CombatHealthV2, LifeStateEvent> OnRespawned;
        
        // Event for external systems (like PlayerDataManager) to provide initial health values
        public static event Func<ulong, float> OnRequestInitialMaxHealth;
        public static event Func<ulong, float> OnRequestInitialHealth;
        
        #endregion

        #region Serialized Fields

        [Header("Base Stats")]
        [SerializeField]
        [Min(1f)]
        private float m_BaseMaxHealth = 100f;

        [SerializeField]
        private bool m_AutoRespawn = true;

        [SerializeField]
        [Min(0f)]
        private float m_RespawnDelaySeconds = 2f;

        [SerializeField]
        private bool m_LogDebug;

        [Header("Animation")]
        [SerializeField]
        private bool m_EnableAnimationTriggers = true;

        [SerializeField]
        private Animator m_Animator;

        [SerializeField]
        private string m_DamageTrigger = "OnHit";

        [SerializeField]
        private string m_DeathTrigger = "Death";

        [SerializeField]
        private string m_RespawnTrigger = "Respawn";

        [SerializeField]
        [Min(0f)]
        private float m_MinDamageTriggerInterval = 0.08f;

        [SerializeField]
        [Min(0.01f)]
        private float m_AnimationPulseDuration = 0.12f;

        #endregion

        #region Runtime State

        // NetworkVariable for real-time sync (server authoritative)
        private readonly NetworkVariable<float> m_NetworkHealth = new(
            100f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        private readonly NetworkVariable<float> m_NetworkMaxHealth = new(
            100f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        private readonly NetworkVariable<bool> m_NetworkIsDead = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        // Local state
        private NetworkObject m_CachedNetworkObject;
        private bool m_IsInitialized;
        private float m_LastDamageTriggerTime = float.MinValue;
        private Coroutine m_RespawnCoroutine;

        // Animation tracking
        private readonly Dictionary<string, AnimatorControllerParameterType> m_AnimatorParams = new();
        private readonly Dictionary<string, Coroutine> m_ActivePulses = new();
        private readonly HashSet<string> m_MissingParamWarnings = new();

        #endregion

        #region Public Properties

        public float MaxHealth => m_NetworkMaxHealth.Value;
        public float CurrentHealth => m_NetworkHealth.Value;
        public float NormalizedHealth => MaxHealth > 0f ? CurrentHealth / MaxHealth : 0f;
        public bool IsDead => m_NetworkIsDead.Value || CurrentHealth <= 0f;
        public bool IsAlive => !IsDead;
        public NetworkObject CachedNetworkObject => m_CachedNetworkObject;
        public float BaseMaxHealth => m_BaseMaxHealth;

        #endregion

        #region Lifecycle

        private void Awake()
        {
            m_CachedNetworkObject = GetComponent<NetworkObject>();
            ResolveAnimator();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            // Subscribe to value changes
            m_NetworkHealth.OnValueChanged += HandleHealthChanged;
            m_NetworkMaxHealth.OnValueChanged += HandleMaxHealthChanged;
            m_NetworkIsDead.OnValueChanged += HandleDeathStateChanged;

            if (IsServer)
            {
                InitializeHealthValues();
            }
            else
            {
                // Client: just trigger initial state events
                HandleHealthChanged(MaxHealth, CurrentHealth);
            }
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();

            m_NetworkHealth.OnValueChanged -= HandleHealthChanged;
            m_NetworkMaxHealth.OnValueChanged -= HandleMaxHealthChanged;
            m_NetworkIsDead.OnValueChanged -= HandleDeathStateChanged;

            CancelRespawn();
            StopAllPulses();
        }

        #endregion

        #region Initialization

        private void InitializeHealthValues()
        {
            if (m_IsInitialized) return;

            // Allow external systems (PlayerDataManager) to provide initial values
            float? initialMaxHealth = OnRequestInitialMaxHealth?.Invoke(OwnerClientId);
            float? initialHealth = OnRequestInitialHealth?.Invoke(OwnerClientId);

            if (initialMaxHealth.HasValue)
            {
                m_NetworkMaxHealth.Value = initialMaxHealth.Value;
            }
            else
            {
                m_NetworkMaxHealth.Value = m_BaseMaxHealth;
            }

            if (initialHealth.HasValue)
            {
                m_NetworkHealth.Value = initialHealth.Value;
            }
            else
            {
                m_NetworkHealth.Value = m_NetworkMaxHealth.Value;
            }

            if (m_LogDebug)
            {
                NGLog.Info("CombatHealth", 
                    $"Initialized {gameObject.name}: HP {CurrentHealth:F0}/{MaxHealth:F0}");
            }

            m_IsInitialized = true;
        }

        #endregion

        #region Public API

        /// <summary>
        /// Applies damage to this entity. Server-authoritative.
        /// </summary>
        public void ApplyDamage(float amount, ulong sourceId = 0, string damageType = "")
        {
            if (!IsServer || amount <= 0f || IsDead) return;

            float previousHealth = CurrentHealth;
            float newHealth = Mathf.Max(0f, previousHealth - amount);
            bool isLethal = newHealth <= 0f && previousHealth > 0f;

            // Update NetworkVariable (triggers sync)
            m_NetworkHealth.Value = newHealth;

            // Emit event for external systems (e.g., PlayerDataManager) to persist
            EmitDamageEvent(amount, sourceId, damageType, previousHealth, newHealth, isLethal);

            if (m_LogDebug)
            {
                NGLog.Debug("CombatHealth", 
                    $"💥 {gameObject.name} took {amount:F1} {damageType} damage from {sourceId}. " +
                    $"HP: {previousHealth:F0} → {newHealth:F0}");
            }

            // Handle death
            if (isLethal)
            {
                HandleDeath();
            }
        }

        /// <summary>
        /// Heals this entity. Server-authoritative.
        /// </summary>
        public void Heal(float amount, string source = "")
        {
            if (!IsServer || amount <= 0f) return;

            float previousHealth = CurrentHealth;
            float newHealth = Mathf.Min(previousHealth + amount, MaxHealth);

            if (Mathf.Approximately(previousHealth, newHealth)) return;

            m_NetworkHealth.Value = newHealth;

            EmitHealthChangedEvent(previousHealth, newHealth);

            if (m_LogDebug)
            {
                NGLog.Debug("CombatHealth", 
                    $"❤️ {gameObject.name} healed {amount:F1} ({source}). HP: {previousHealth:F0} → {newHealth:F0}");
            }
        }

        /// <summary>
        /// Sets max health (e.g., on level up). Server-authoritative.
        /// </summary>
        public void SetMaxHealth(float newMax, bool healToFull = false)
        {
            if (!IsServer || newMax < 1f) return;

            float previousMax = MaxHealth;
            m_NetworkMaxHealth.Value = newMax;

            if (healToFull)
            {
                m_NetworkHealth.Value = newMax;
            }
            else
            {
                // Clamp current to new max
                m_NetworkHealth.Value = Mathf.Min(CurrentHealth, newMax);
            }

            EmitMaxHealthChangedEvent(previousMax, newMax);

            NGLog.Info("CombatHealth", 
                $"{gameObject.name} max health changed: {previousMax:F0} → {newMax:F0}");
        }

        /// <summary>
        /// Full heal and respawn. Server-authoritative.
        /// </summary>
        public void Respawn()
        {
            if (!IsServer) return;

            CancelRespawn();
            
            m_NetworkIsDead.Value = false;
            m_NetworkHealth.Value = MaxHealth;

            EmitRespawnEvent();

            NGLog.Info("CombatHealth", $"{gameObject.name} respawned with full health");
        }

        #endregion

        #region Internal Handlers

        private void HandleHealthChanged(float previous, float current)
        {
            EmitHealthChangedEvent(previous, current);

            // Animation triggers
            if (m_EnableAnimationTriggers && previous > current)
            {
                TriggerHitAnimation();
            }
        }

        private void HandleMaxHealthChanged(float previous, float current)
        {
            EmitMaxHealthChangedEvent(previous, current);
        }

        private void HandleDeathStateChanged(bool previous, bool current)
        {
            if (!previous && current)
            {
                // Just died
                TriggerDeathAnimation();
            }
            else if (previous && !current)
            {
                // Just respawned
                TriggerRespawnAnimation();
            }
        }

        private void HandleDeath()
        {
            m_NetworkIsDead.Value = true;
            
            EmitDeathEvent();
            TriggerDeathAnimation();

            if (m_AutoRespawn)
            {
                m_RespawnCoroutine = StartCoroutine(RespawnAfterDelay());
            }
        }

        private IEnumerator RespawnAfterDelay()
        {
            yield return new WaitForSeconds(m_RespawnDelaySeconds);
            Respawn();
        }

        private void CancelRespawn()
        {
            if (m_RespawnCoroutine != null)
            {
                StopCoroutine(m_RespawnCoroutine);
                m_RespawnCoroutine = null;
            }
        }

        #endregion

        #region Animation

        private void ResolveAnimator()
        {
            if (m_Animator != null) return;
            m_Animator = GetComponentInChildren<Animator>(true);
            CacheAnimatorParameters();
        }

        private void CacheAnimatorParameters()
        {
            m_AnimatorParams.Clear();
            if (m_Animator == null || m_Animator.parameters == null) return;

            foreach (var param in m_Animator.parameters)
            {
                m_AnimatorParams[param.name] = param.type;
            }
        }

        private void TriggerHitAnimation()
        {
            if (m_Animator == null) return;

            float now = Time.time;
            if (now - m_LastDamageTriggerTime < m_MinDamageTriggerInterval) return;
            m_LastDamageTriggerTime = now;

            TrySetAnimatorParam(m_DamageTrigger, "OnHit", "Hit", "HitReaction");
        }

        private void TriggerDeathAnimation()
        {
            TrySetAnimatorParam(m_DeathTrigger, "Death", "Die", "Dead");
        }

        private void TriggerRespawnAnimation()
        {
            TrySetAnimatorParam(m_RespawnTrigger, "Respawn", "Revive", "Spawn");
        }

        private void TrySetAnimatorParam(string primary, params string[] aliases)
        {
            if (TrySetParamInternal(primary, suppressWarning: true)) return;

            foreach (var alias in aliases)
            {
                if (TrySetParamInternal(alias, suppressWarning: true)) return;
            }

            // Final attempt with warning
            TrySetParamInternal(primary, suppressWarning: false);
        }

        private bool TrySetParamInternal(string name, bool suppressWarning)
        {
            if (m_Animator == null || string.IsNullOrWhiteSpace(name)) return false;

            if (!m_AnimatorParams.TryGetValue(name, out var type))
            {
                if (!suppressWarning && !m_MissingParamWarnings.Contains(name))
                {
                    m_MissingParamWarnings.Add(name);
                    NGLog.Warn("CombatHealth", 
                        $"Animator parameter '{name}' not found on {m_Animator.name}");
                }
                return false;
            }

            if (type == AnimatorControllerParameterType.Trigger)
            {
                m_Animator.SetTrigger(name);
                return true;
            }

            if (type == AnimatorControllerParameterType.Bool)
            {
                if (m_ActivePulses.TryGetValue(name, out var existing) && existing != null)
                {
                    StopCoroutine(existing);
                }
                m_ActivePulses[name] = StartCoroutine(PulseBoolParam(name));
                return true;
            }

            return false;
        }

        private IEnumerator PulseBoolParam(string name)
        {
            m_Animator.SetBool(name, true);
            yield return new WaitForSeconds(m_AnimationPulseDuration);
            if (m_Animator != null) m_Animator.SetBool(name, false);
            m_ActivePulses.Remove(name);
        }

        private void StopAllPulses()
        {
            foreach (var coroutine in m_ActivePulses.Values)
            {
                if (coroutine != null) StopCoroutine(coroutine);
            }
            m_ActivePulses.Clear();
        }

        #endregion

        #region Events

        private void EmitDamageEvent(float amount, ulong sourceId, string type, float prev, float curr, bool lethal)
        {
            var evt = new DamageEvent
            {
                Target = this,
                SourceNetworkObjectId = sourceId,
                DamageAmount = amount,
                PreviousHealth = prev,
                CurrentHealth = curr,
                DamageType = type ?? "",
                IsLethal = lethal
            };
            OnDamageApplied?.Invoke(this, evt);
        }

        private void EmitHealthChangedEvent(float prev, float curr)
        {
            OnHealthChanged?.Invoke(this, new HealthChangedEvent
            {
                Target = this,
                PreviousHealth = prev,
                CurrentHealth = curr,
                MaxHealth = MaxHealth
            });
        }

        private void EmitMaxHealthChangedEvent(float prev, float curr)
        {
            OnHealthChanged?.Invoke(this, new HealthChangedEvent
            {
                Target = this,
                PreviousHealth = CurrentHealth,
                CurrentHealth = CurrentHealth,
                MaxHealth = curr
            });
        }

        private void EmitDeathEvent()
        {
            OnDied?.Invoke(this, new LifeStateEvent
            {
                Target = this,
                CurrentHealth = 0f,
                MaxHealth = MaxHealth
            });
        }

        private void EmitRespawnEvent()
        {
            OnRespawned?.Invoke(this, new LifeStateEvent
            {
                Target = this,
                CurrentHealth = CurrentHealth,
                MaxHealth = MaxHealth
            });
        }

        #endregion

        #region Debug

        [ContextMenu("Take 20 Damage")]
        private void DebugTakeDamage()
        {
            if (!IsServer) return;
            ApplyDamage(20f, 0, "debug");
        }

        [ContextMenu("Heal 20")]
        private void DebugHeal()
        {
            if (!IsServer) return;
            Heal(20f, "debug");
        }

        [ContextMenu("Print State")]
        private void DebugPrintState()
        {
            Debug.Log($"[CombatHealth] {gameObject.name}: {CurrentHealth:F0}/{MaxHealth:F0} ({NormalizedHealth:P0}) IsDead={IsDead}");
        }

        #endregion
    }
}
