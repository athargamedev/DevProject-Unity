using System;
using System.Collections;
using System.Collections.Generic;
using Network_Game.Diagnostics;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;

namespace Network_Game.Combat
{
    /// <summary>
    /// Handles player dissolve (invisibility) and respawn (visibility restore) effects.
    /// Separated from DialogueSceneEffectsController to follow Single Responsibility Principle.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerDissolveController : MonoBehaviour
    {
        public static PlayerDissolveController Instance { get; private set; }
        public static event Action<DissolveEffectInfo> OnDissolveApplied;
        public static event Action<ulong> OnRespawnApplied;

        public struct DissolveEffectInfo
        {
            public ulong TargetNetworkObjectId;
            public float DurationSeconds;
            public float AppliedAtRealtime;
        }

        [Header("Dissolve Timing")]
        [SerializeField]
        [Min(0.05f)]
        private float m_DissolveFadeOutSeconds = 1.4f;

        [SerializeField]
        [Min(0.05f)]
        private float m_DissolveFadeInSeconds = 1.2f;

        [SerializeField]
        private AnimationCurve m_DissolveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Settings")]
        [SerializeField]
        private bool m_LogDebug;

        private const float kMinDissolveFadeOutSeconds = 1.2f;
        private const float kMinDissolveFadeInSeconds = 1.0f;
        private const float kMinDissolveHoldSeconds = 0.2f;

        private readonly Dictionary<ulong, Coroutine> m_ActiveDissolveRoutines = new Dictionary<ulong, Coroutine>();
        private readonly Dictionary<ulong, RendererFadeState[]> m_ActiveDissolveStates = new Dictionary<ulong, RendererFadeState[]>();

        private sealed class RendererFadeState
        {
            public Renderer Renderer;
            public bool WasEnabled;
            public MaterialFadeState[] Materials;
        }

        private sealed class MaterialFadeState
        {
            public Material Material;
            public string ColorPropertyName;
            public Color OriginalColor;
            public int OriginalRenderQueue;
            public bool HasSurface;
            public float Surface;
            public bool HasMode;
            public float Mode;
            public bool HasSrcBlend;
            public float SrcBlend;
            public bool HasDstBlend;
            public float DstBlend;
            public bool HasZWrite;
            public float ZWrite;
            public bool KeywordAlphaTest;
            public bool KeywordAlphaBlend;
            public bool KeywordAlphaPremultiply;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            // Restore visibility for all active dissolves
            var activeIds = new List<ulong>(m_ActiveDissolveStates.Keys);
            for (int i = 0; i < activeIds.Count; i++)
            {
                StopActiveDissolve(activeIds[i], restoreVisible: true);
            }

            if (Instance == this)
                Instance = null;
        }

        /// <summary>
        /// Apply dissolve effect to make player invisible.
        /// </summary>
        public void ApplyDissolveEffect(ulong targetNetworkObjectId, float durationSeconds = 5f, string actionId = "")
        {
            var targetObj = GetNetworkObject(targetNetworkObjectId);
            if (targetObj == null)
            {
                NGLog.Warn("PlayerDissolve", $"Dissolve effect: target {targetNetworkObjectId} not found");
                return;
            }

            GameObject target = targetObj.gameObject;

            // Cancel any existing dissolve session for this target and restore baseline first.
            StopActiveDissolve(targetNetworkObjectId, restoreVisible: true);

            Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                NGLog.Warn("PlayerDissolve", $"Dissolve effect: no renderers found on {target.name}");
                return;
            }

            RendererFadeState[] fadeStates = BuildFadeStates(renderers);
            m_ActiveDissolveStates[targetNetworkObjectId] = fadeStates;

            float clampedDuration = Mathf.Clamp(durationSeconds, 0.6f, 30f);
            Coroutine routine = StartCoroutine(
                RunDissolveSequence(targetNetworkObjectId, target.name, clampedDuration, fadeStates)
            );
            m_ActiveDissolveRoutines[targetNetworkObjectId] = routine;

            NGLog.Info("PlayerDissolve", $"Dissolve effect started on {target.name} for {clampedDuration:0.00}s");

            OnDissolveApplied?.Invoke(new DissolveEffectInfo
            {
                TargetNetworkObjectId = targetNetworkObjectId,
                DurationSeconds = clampedDuration,
                AppliedAtRealtime = Time.realtimeSinceStartup,
            });
        }

        /// <summary>
        /// Apply respawn effect to restore player visibility.
        /// </summary>
        public void ApplyRespawnEffect(ulong targetNetworkObjectId, string actionId = "")
        {
            var targetObj = GetNetworkObject(targetNetworkObjectId);
            if (targetObj == null)
            {
                NGLog.Warn("PlayerDissolve", $"Respawn effect: target {targetNetworkObjectId} not found");
                return;
            }

            GameObject target = targetObj.gameObject;

            StopActiveDissolve(targetNetworkObjectId, restoreVisible: true);

            // Ensure visibility even if no dissolve session is active.
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in renderers)
            {
                renderer.enabled = true;
            }

            var mainRenderer = target.GetComponent<Renderer>();
            if (mainRenderer != null)
            {
                mainRenderer.enabled = true;
            }

            NGLog.Info("PlayerDissolve", $"Respawn effect applied to {target.name}");
            OnRespawnApplied?.Invoke(targetNetworkObjectId);
        }

        private IEnumerator RunDissolveSequence(
            ulong targetNetworkObjectId,
            string targetName,
            float durationSeconds,
            RendererFadeState[] fadeStates
        )
        {
            ResolveDissolveTimings(durationSeconds, out float fadeOut, out float hold, out float fadeIn);

            // Fade out
            if (fadeOut > 0f)
            {
                float elapsed = 0f;
                while (elapsed < fadeOut)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / fadeOut);
                    float curved = m_DissolveCurve != null ? m_DissolveCurve.Evaluate(t) : t;
                    ApplyFadeAlpha(fadeStates, 1f - curved);
                    yield return null;
                }
            }

            SetRenderersEnabled(fadeStates, false);
            yield return new WaitForSeconds(hold);

            // Fade in
            SetRenderersEnabled(fadeStates, true);
            ApplyFadeAlpha(fadeStates, 0f);
            if (fadeIn > 0f)
            {
                float elapsed = 0f;
                while (elapsed < fadeIn)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / fadeIn);
                    float curved = m_DissolveCurve != null ? m_DissolveCurve.Evaluate(t) : t;
                    ApplyFadeAlpha(fadeStates, curved);
                    yield return null;
                }
            }

            RestoreFadeStates(fadeStates, forceVisible: true);
            m_ActiveDissolveRoutines.Remove(targetNetworkObjectId);
            m_ActiveDissolveStates.Remove(targetNetworkObjectId);

            NGLog.Info("PlayerDissolve", $"Dissolve effect ended - visibility restored for {targetName}");
        }

        private void ResolveDissolveTimings(
            float durationSeconds,
            out float fadeOut,
            out float hold,
            out float fadeIn
        )
        {
            float duration = Mathf.Max(0.6f, durationSeconds);
            float desiredFadeOut = Mathf.Max(m_DissolveFadeOutSeconds, kMinDissolveFadeOutSeconds);
            float desiredFadeIn = Mathf.Max(m_DissolveFadeInSeconds, kMinDissolveFadeInSeconds);
            float desiredHold = kMinDissolveHoldSeconds;

            float required = desiredFadeOut + desiredFadeIn + desiredHold;
            if (required <= duration)
            {
                fadeOut = desiredFadeOut;
                fadeIn = desiredFadeIn;
                hold = duration - fadeOut - fadeIn;
                return;
            }

            float remainingForFades = Mathf.Max(0.1f, duration - desiredHold);
            float fadeScale = remainingForFades / Mathf.Max(0.1f, desiredFadeOut + desiredFadeIn);
            fadeScale = Mathf.Clamp(fadeScale, 0.05f, 1f);

            fadeOut = Mathf.Max(0.05f, desiredFadeOut * fadeScale);
            fadeIn = Mathf.Max(0.05f, desiredFadeIn * fadeScale);
            hold = Mathf.Max(0.05f, duration - fadeOut - fadeIn);
        }

        private void StopActiveDissolve(ulong targetNetworkObjectId, bool restoreVisible)
        {
            if (m_ActiveDissolveRoutines.TryGetValue(targetNetworkObjectId, out Coroutine routine))
            {
                if (routine != null)
                {
                    StopCoroutine(routine);
                }
                m_ActiveDissolveRoutines.Remove(targetNetworkObjectId);
            }

            if (m_ActiveDissolveStates.TryGetValue(targetNetworkObjectId, out RendererFadeState[] states))
            {
                if (restoreVisible)
                {
                    RestoreFadeStates(states, forceVisible: true);
                }
                m_ActiveDissolveStates.Remove(targetNetworkObjectId);
            }
        }

        private RendererFadeState[] BuildFadeStates(Renderer[] renderers)
        {
            var states = new RendererFadeState[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }
                try
                {
                    Material[] materials = renderer.materials;
                    var materialStates = new MaterialFadeState[materials.Length];
                    for (int j = 0; j < materials.Length; j++)
                    {
                        Material mat = materials[j];
                        if (mat == null)
                        {
                            continue;
                        }

                        MaterialFadeState matState = CaptureMaterialFadeState(mat);
                        materialStates[j] = matState;
                    }

                    states[i] = new RendererFadeState
                    {
                        Renderer = renderer,
                        WasEnabled = renderer.enabled,
                        Materials = materialStates,
                    };
                }
                catch (System.Exception ex)
                {
                    string rendererName = renderer != null ? renderer.name : "<null>";
                    string rendererType = renderer != null ? renderer.GetType().Name : "<null>";
                    NGLog.Warn(
                        "PlayerDissolve",
                        NGLog.Format(
                            "Dissolve skipped renderer due to setup error",
                            ("renderer", rendererName),
                            ("rendererType", rendererType),
                            ("error", ex.Message ?? string.Empty)
                        )
                    );
                    NGLog.Error("PlayerDissolve", ex.ToString(), renderer);
                }
            }

            return states;
        }

        private static MaterialFadeState CaptureMaterialFadeState(Material material)
        {
            var state = new MaterialFadeState
            {
                Material = material,
                OriginalRenderQueue = material.renderQueue,
                KeywordAlphaTest = material.IsKeywordEnabled("_ALPHATEST_ON"),
                KeywordAlphaBlend = material.IsKeywordEnabled("_ALPHABLEND_ON"),
                KeywordAlphaPremultiply = material.IsKeywordEnabled("_ALPHAPREMULTIPLY_ON"),
            };

            if (material.HasProperty("_BaseColor"))
            {
                state.ColorPropertyName = "_BaseColor";
                state.OriginalColor = material.GetColor("_BaseColor");
            }
            else if (material.HasProperty("_Color"))
            {
                state.ColorPropertyName = "_Color";
                state.OriginalColor = material.GetColor("_Color");
            }

            if (material.HasProperty("_Surface"))
            {
                state.HasSurface = true;
                state.Surface = material.GetFloat("_Surface");
            }
            if (material.HasProperty("_Mode"))
            {
                state.HasMode = true;
                state.Mode = material.GetFloat("_Mode");
            }
            if (material.HasProperty("_SrcBlend"))
            {
                state.HasSrcBlend = true;
                state.SrcBlend = material.GetFloat("_SrcBlend");
            }
            if (material.HasProperty("_DstBlend"))
            {
                state.HasDstBlend = true;
                state.DstBlend = material.GetFloat("_DstBlend");
            }
            if (material.HasProperty("_ZWrite"))
            {
                state.HasZWrite = true;
                state.ZWrite = material.GetFloat("_ZWrite");
            }

            PrepareMaterialForFade(state);
            return state;
        }

        private static void PrepareMaterialForFade(MaterialFadeState state)
        {
            Material material = state.Material;
            if (material == null || string.IsNullOrEmpty(state.ColorPropertyName))
            {
                return;
            }

            if (state.HasSurface)
            {
                material.SetFloat("_Surface", 1f); // URP Transparent
            }

            if (state.HasMode)
            {
                material.SetFloat("_Mode", 2f); // Standard Fade
            }

            if (state.HasSrcBlend)
            {
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            }

            if (state.HasDstBlend)
            {
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            }

            if (state.HasZWrite)
            {
                material.SetFloat("_ZWrite", 0f);
            }

            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = (int)RenderQueue.Transparent;
        }

        private static void ApplyFadeAlpha(RendererFadeState[] states, float alpha01)
        {
            float clamped = Mathf.Clamp01(alpha01);
            if (states == null)
            {
                return;
            }

            for (int i = 0; i < states.Length; i++)
            {
                RendererFadeState rendererState = states[i];
                if (rendererState?.Materials == null)
                {
                    continue;
                }

                for (int j = 0; j < rendererState.Materials.Length; j++)
                {
                    MaterialFadeState matState = rendererState.Materials[j];
                    if (matState?.Material == null || string.IsNullOrEmpty(matState.ColorPropertyName))
                    {
                        continue;
                    }

                    Color color = matState.OriginalColor;
                    color.a = Mathf.Clamp01(matState.OriginalColor.a * clamped);
                    matState.Material.SetColor(matState.ColorPropertyName, color);
                }
            }
        }

        private static void SetRenderersEnabled(RendererFadeState[] states, bool enabled)
        {
            if (states == null)
            {
                return;
            }

            for (int i = 0; i < states.Length; i++)
            {
                RendererFadeState state = states[i];
                if (state?.Renderer == null)
                {
                    continue;
                }

                state.Renderer.enabled = enabled;
            }
        }

        private static void RestoreFadeStates(RendererFadeState[] states, bool forceVisible)
        {
            if (states == null)
            {
                return;
            }

            for (int i = 0; i < states.Length; i++)
            {
                RendererFadeState rendererState = states[i];
                if (rendererState == null)
                {
                    continue;
                }

                if (rendererState.Renderer != null)
                {
                    rendererState.Renderer.enabled = forceVisible || rendererState.WasEnabled;
                }

                if (rendererState.Materials == null)
                {
                    continue;
                }

                for (int j = 0; j < rendererState.Materials.Length; j++)
                {
                    MaterialFadeState matState = rendererState.Materials[j];
                    if (matState?.Material == null)
                    {
                        continue;
                    }

                    if (!string.IsNullOrEmpty(matState.ColorPropertyName))
                    {
                        matState.Material.SetColor(matState.ColorPropertyName, matState.OriginalColor);
                    }

                    if (matState.HasSurface)
                    {
                        matState.Material.SetFloat("_Surface", matState.Surface);
                    }
                    if (matState.HasMode)
                    {
                        matState.Material.SetFloat("_Mode", matState.Mode);
                    }
                    if (matState.HasSrcBlend)
                    {
                        matState.Material.SetFloat("_SrcBlend", matState.SrcBlend);
                    }
                    if (matState.HasDstBlend)
                    {
                        matState.Material.SetFloat("_DstBlend", matState.DstBlend);
                    }
                    if (matState.HasZWrite)
                    {
                        matState.Material.SetFloat("_ZWrite", matState.ZWrite);
                    }

                    if (matState.KeywordAlphaTest)
                    {
                        matState.Material.EnableKeyword("_ALPHATEST_ON");
                    }
                    else
                    {
                        matState.Material.DisableKeyword("_ALPHATEST_ON");
                    }

                    if (matState.KeywordAlphaBlend)
                    {
                        matState.Material.EnableKeyword("_ALPHABLEND_ON");
                    }
                    else
                    {
                        matState.Material.DisableKeyword("_ALPHABLEND_ON");
                    }

                    if (matState.KeywordAlphaPremultiply)
                    {
                        matState.Material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                    }
                    else
                    {
                        matState.Material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    }

                    matState.Material.renderQueue = matState.OriginalRenderQueue;
                }
            }
        }

        private static NetworkObject GetNetworkObject(ulong networkObjectId)
        {
            if (NetworkManager.Singleton == null)
                return null;

            var networkManager = NetworkManager.Singleton;

            if (networkManager.SpawnManager != null)
            {
                var spawnedObjects = networkManager.SpawnManager.SpawnedObjects;
                if (spawnedObjects.TryGetValue(networkObjectId, out var obj))
                {
                    return obj;
                }
            }
            return null;
        }

        #region Test Methods

        /// <summary>
        /// TEST: Apply dissolve effect to local player to make invisible
        /// </summary>
        [ContextMenu("Test Dissolve Effect (5s)")]
        public void TestDissolveEffect()
        {
            if (NetworkManager.Singleton == null)
            {
                NGLog.Warn("PlayerDissolve", "Test dissolve skipped: no NetworkManager found.", this);
                return;
            }

            var spawnManager = NetworkManager.Singleton.SpawnManager;
            if (spawnManager == null)
            {
                NGLog.Warn("PlayerDissolve", "Test dissolve skipped: no SpawnManager found.", this);
                return;
            }

            foreach (var kvp in spawnManager.SpawnedObjects)
            {
                var networkObject = kvp.Value;
                if (networkObject != null && networkObject.IsLocalPlayer)
                {
                    ApplyDissolveEffect(networkObject.NetworkObjectId, 5f);
                    NGLog.Info(
                        "PlayerDissolve",
                        NGLog.Format(
                            "Dissolve applied to local player",
                            ("targetNetworkObjectId", networkObject.NetworkObjectId),
                            ("mode", "test")
                        ),
                        this
                    );
                    return;
                }
            }

            NGLog.Warn("PlayerDissolve", "Test dissolve skipped: local player not found.", this);
        }

        /// <summary>
        /// TEST: Restore visibility to local player
        /// </summary>
        [ContextMenu("Test Respawn Effect")]
        public void TestRespawnEffect()
        {
            if (NetworkManager.Singleton == null)
            {
                NGLog.Warn("PlayerDissolve", "Test respawn skipped: no NetworkManager found.", this);
                return;
            }

            var spawnManager = NetworkManager.Singleton.SpawnManager;
            if (spawnManager == null)
            {
                NGLog.Warn("PlayerDissolve", "Test respawn skipped: no SpawnManager found.", this);
                return;
            }

            foreach (var kvp in spawnManager.SpawnedObjects)
            {
                var networkObject = kvp.Value;
                if (networkObject != null && networkObject.IsLocalPlayer)
                {
                    ApplyRespawnEffect(networkObject.NetworkObjectId);
                    NGLog.Info(
                        "PlayerDissolve",
                        NGLog.Format(
                            "Respawn applied to local player",
                            ("targetNetworkObjectId", networkObject.NetworkObjectId),
                            ("mode", "test")
                        ),
                        this
                    );
                    return;
                }
            }

            NGLog.Warn("PlayerDissolve", "Test respawn skipped: local player not found.", this);
        }

        #endregion
    }
}
