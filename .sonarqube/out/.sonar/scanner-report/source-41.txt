using System.Collections;
using Network_Game.Diagnostics;
using UnityEngine;

namespace Network_Game.Combat
{
    /// <summary>
    /// Applies visual hit reaction effects (glow, flash, particles) when the owner takes damage.
    /// Subscribes to CombatHealthV2 damage events.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CombatHealthV2))]
    public sealed class CombatHitReactionEffect : MonoBehaviour
    {
        [Header("Glow Effect")]
        [SerializeField] 
        [Tooltip("SkinnedMeshRenderer to apply glow to. Auto-finds in children if null.")]
        private SkinnedMeshRenderer m_TargetRenderer;

        [SerializeField]
        [Tooltip("Emission color when hit (HDR recommended for bloom)")]
        private Color m_HitGlowColor = new Color(2f, 0.3f, 0.1f, 1f);

        [SerializeField]
        [Min(0f)]
        [Tooltip("How long the glow lasts in seconds")]
        private float m_GlowDurationSeconds = 0.3f;

        [SerializeField]
        [Tooltip("Glow intensity curve over time")]
        private AnimationCurve m_GlowCurve;

        [Header("Optional Particle Effect")]
        [SerializeField]
        [Tooltip("Optional hit effect to spawn at damage location")]
        private GameObject m_HitEffectPrefab;

        [SerializeField]
        [Tooltip("If true, uses the damage source position")]
        private bool m_SpawnAtSource = true;

        [SerializeField]
        private Vector3 m_EffectOffset = new Vector3(0f, 1f, 0f);

        [Header("Settings")]
        [SerializeField]
        private bool m_LocalPlayerOnly = false;

        [SerializeField]
        private bool m_LogDebug = false;

        private CombatHealthV2 m_Health;
        private MaterialPropertyBlock m_PropertyBlock;
        private static readonly int s_EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private Coroutine m_ActiveGlowCoroutine;
        private Color m_OriginalEmission;

        private void Awake()
        {
            m_Health = GetComponent<CombatHealthV2>();
            if (m_Health == null)
            {
                NGLog.Warn("CombatHitReaction", "No CombatHealthV2 found on this GameObject!");
                enabled = false;
                return;
            }

            m_PropertyBlock = new MaterialPropertyBlock();
            
            if (m_TargetRenderer == null)
            {
                m_TargetRenderer = GetComponentInChildren<SkinnedMeshRenderer>(true);
            }

            if (m_GlowCurve == null || m_GlowCurve.length == 0)
            {
                m_GlowCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
            }

            CacheOriginalEmission();
        }

        private void CacheOriginalEmission()
        {
            if (m_TargetRenderer == null) return;
            
            m_TargetRenderer.GetPropertyBlock(m_PropertyBlock);
            m_OriginalEmission = m_PropertyBlock.HasColor(s_EmissionColorId) 
                ? m_PropertyBlock.GetColor(s_EmissionColorId) 
                : Color.black;
        }

        private void OnEnable()
        {
            if (m_Health != null)
            {
                CombatHealthV2.OnDamageApplied += HandleDamageApplied;
            }
        }

        private void OnDisable()
        {
            CombatHealthV2.OnDamageApplied -= HandleDamageApplied;
            EndGlow();
        }

        private void HandleDamageApplied(CombatHealthV2 health, CombatHealthV2.DamageEvent evt)
        {
            // Only react to damage on our own health component
            if (health != m_Health) return;

            if (m_LocalPlayerOnly)
            {
                var netObj = health.CachedNetworkObject;
                if (netObj != null && !netObj.IsOwner) return;
            }

            if (m_LogDebug)
            {
                NGLog.Debug("CombatHitReaction", $"Hit reaction: {evt.DamageType} damage from {evt.SourceNetworkObjectId}");
            }

            TriggerGlow(GetDamageColor(evt.DamageType));

            if (m_HitEffectPrefab != null)
            {
                SpawnEffect(evt.SourceNetworkObjectId);
            }
        }

        private void TriggerGlow(Color color)
        {
            if (m_TargetRenderer == null) return;
            if (m_ActiveGlowCoroutine != null) StopCoroutine(m_ActiveGlowCoroutine);
            m_ActiveGlowCoroutine = StartCoroutine(GlowRoutine(color));
        }

        private System.Collections.IEnumerator GlowRoutine(Color targetColor)
        {
            float elapsed = 0f;
            while (elapsed < m_GlowDurationSeconds)
            {
                float t = elapsed / m_GlowDurationSeconds;
                float intensity = m_GlowCurve.Evaluate(t);
                SetEmission(Color.Lerp(m_OriginalEmission, targetColor * intensity, intensity));
                elapsed += Time.deltaTime;
                yield return null;
            }
            SetEmission(m_OriginalEmission);
            m_ActiveGlowCoroutine = null;
        }

        private void SetEmission(Color color)
        {
            if (m_TargetRenderer == null) return;
            m_TargetRenderer.GetPropertyBlock(m_PropertyBlock);
            m_PropertyBlock.SetColor(s_EmissionColorId, color);
            m_TargetRenderer.SetPropertyBlock(m_PropertyBlock);
        }

        private void EndGlow()
        {
            if (m_ActiveGlowCoroutine != null)
            {
                StopCoroutine(m_ActiveGlowCoroutine);
                m_ActiveGlowCoroutine = null;
            }
            SetEmission(m_OriginalEmission);
        }

        private Color GetDamageColor(string damageType)
        {
            if (string.IsNullOrWhiteSpace(damageType)) return m_HitGlowColor;
            
            return damageType.ToLowerInvariant() switch
            {
                "fire" => new Color(3f, 0.5f, 0f, 1f),
                "ice" or "frost" => new Color(0.2f, 0.8f, 3f, 1f),
                "lightning" or "electric" or "thunder" => new Color(3f, 3f, 0.5f, 1f),
                "poison" or "toxic" => new Color(0.3f, 3f, 0.2f, 1f),
                _ => m_HitGlowColor
            };
        }

        private void SpawnEffect(ulong sourceId)
        {
            Vector3 pos = transform.position + m_EffectOffset;
            
            if (m_SpawnAtSource && sourceId != 0)
            {
                var nm = Unity.Netcode.NetworkManager.Singleton;
                if (nm?.SpawnManager?.SpawnedObjects.TryGetValue(sourceId, out var obj) == true && obj != null)
                {
                    pos = obj.transform.position + m_EffectOffset;
                }
            }

            GameObject fx = Instantiate(m_HitEffectPrefab, pos, Quaternion.identity);
            var ps = fx.GetComponentInChildren<ParticleSystem>();
            Destroy(fx, ps != null ? ps.main.duration + 1f : 3f);
        }
    }
}
