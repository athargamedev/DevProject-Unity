using System;
using System.Collections;
using System.Collections.Generic;
using Network_Game.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Network_Game.Dialogue.Effects
{
    /// <summary>
    /// Handles surface material overrides (freeze effects) and floor dissolve.
    /// Separated from DialogueSceneEffectsController to follow Single Responsibility Principle.
    /// </summary>
    [DisallowMultipleComponent]
    public class SurfaceMaterialEffectController : MonoBehaviour
    {
        public static SurfaceMaterialEffectController Instance { get; private set; }
        public static event Action<SurfaceEffectInfo> OnSurfaceEffectApplied;
        public static event Action<FloorDissolveInfo> OnFloorDissolveApplied;

        public struct SurfaceEffectInfo
        {
            public string EffectTag;
            public string SurfaceId;
            public float DurationSeconds;
            public Vector3 Position;
        }

        public struct FloorDissolveInfo
        {
            public int TargetCount;
            public float DurationSeconds;
            public Vector3 Position;
        }

        [Header("Floor Dissolve")]
        [SerializeField]
        [Min(0.05f)]
        private float m_FloorDissolveFadeOutSeconds = 1.4f;

        [SerializeField]
        [Min(0.05f)]
        private float m_FloorDissolveFadeInSeconds = 1.2f;

        [SerializeField]
        private AnimationCurve m_FloorDissolveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Settings")]
        [SerializeField]
        private bool m_LogDebug;

        private const float kMinDissolveFadeOutSeconds = 1.2f;
        private const float kMinDissolveFadeInSeconds = 1.0f;
        private const float kMinDissolveHoldSeconds = 0.2f;
        private static readonly string[] s_FloorNameHints =
        {
            "floor",
            "ground",
            "terrain",
            "stairs",
            "stair",
        };

        private readonly Dictionary<string, Coroutine> m_ActiveSurfaceMaterialRoutines =
            new Dictionary<string, Coroutine>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, SurfaceMaterialOverrideState> m_ActiveSurfaceMaterialStates =
            new Dictionary<string, SurfaceMaterialOverrideState>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<GameObject, Coroutine> m_ActiveObjectHideRoutines =
            new Dictionary<GameObject, Coroutine>();
        private readonly Dictionary<GameObject, RendererFadeState[]> m_ActiveObjectHideStates =
            new Dictionary<GameObject, RendererFadeState[]>();

        // Surface cache — built once on Awake, avoiding per-RPC FindObjectsByType scans.
        private Dictionary<string, EffectSurface> m_SurfaceById;

        private sealed class SurfaceMaterialOverrideState
        {
            public string Key;
            public string SurfaceId;
            public string RendererPath;
            public Renderer Renderer;
            public Material[] OriginalMaterials;
            public int MaterialSlotIndex;
            public float AppliedAtRealtime;
        }

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
            BuildSurfaceCache();
        }

        private void OnDestroy()
        {
            // Restore all active surface material overrides
            var activeSurfaceKeys = new List<string>(m_ActiveSurfaceMaterialStates.Keys);
            for (int i = 0; i < activeSurfaceKeys.Count; i++)
            {
                StopActiveSurfaceMaterialOverride(activeSurfaceKeys[i], restoreOriginalMaterial: true);
            }

            // Restore all active floor dissolves
            var activeObjectHideTargets = new List<GameObject>(m_ActiveObjectHideStates.Keys);
            for (int i = 0; i < activeObjectHideTargets.Count; i++)
            {
                StopActiveObjectHide(activeObjectHideTargets[i], restoreVisible: true);
            }

            if (Instance == this)
                Instance = null;
        }

        #region Surface Material Override

        /// <summary>
        /// Apply surface material override effect at the nearest eligible surface.
        /// </summary>
        public void ApplySurfaceMaterialEffect(
            string effectTag,
            Vector3 referencePosition,
            float durationSeconds,
            EffectDefinition definition,
            ulong sourceNetworkObjectId = 0,
            ulong targetNetworkObjectId = 0,
            string actionId = ""
        )
        {
            if (definition == null || !definition.enableSurfaceMaterialOverride)
            {
                NGLog.Warn("SurfaceFX", $"Surface material effect skipped: {effectTag} has override disabled");
                return;
            }

            if (
                !TryResolveNearestSurfaceOverrideTarget(
                    referencePosition,
                    out EffectSurface surface,
                    out Renderer renderer,
                    out int materialSlotIndex,
                    out string rendererHierarchyPath
                )
            )
            {
                NGLog.Warn("SurfaceFX", $"Surface material effect skipped: no nearby eligible surface for {effectTag}");
                return;
            }

            ApplySurfaceMaterialEffect(
                effectTag,
                surface != null ? surface.SurfaceId : string.Empty,
                rendererHierarchyPath,
                materialSlotIndex,
                durationSeconds,
                definition,
                sourceNetworkObjectId,
                targetNetworkObjectId,
                actionId
            );
        }

        /// <summary>
        /// Apply surface material override effect to a specific surface.
        /// </summary>
        public void ApplySurfaceMaterialEffect(
            string effectTag,
            string surfaceId,
            string rendererHierarchyPath,
            int materialSlotIndex,
            float durationSeconds,
            EffectDefinition definition,
            ulong sourceNetworkObjectId = 0,
            ulong targetNetworkObjectId = 0,
            string actionId = ""
        )
        {
            if (definition == null || !definition.enableSurfaceMaterialOverride)
            {
                NGLog.Warn("SurfaceFX", $"Surface material effect skipped: {effectTag} has override disabled");
                return;
            }

            Material overrideMaterial = definition.surfaceOverrideMaterial;
            if (overrideMaterial == null)
            {
                NGLog.Warn("SurfaceFX", $"Surface material effect skipped: {effectTag} has no material");
                return;
            }

            if (
                !TryResolveFloorSurfaceOverrideTarget(
                    surfaceId,
                    rendererHierarchyPath,
                    materialSlotIndex,
                    out EffectSurface surface,
                    out Renderer renderer,
                    out int resolvedMaterialSlot,
                    out string overrideKey,
                    out string reason
                )
            )
            {
                NGLog.Warn("SurfaceFX", $"Surface material effect skipped: {reason}");
                return;
            }

            if (surface != null && surface.SurfaceType == EffectSurfaceType.Wall)
            {
                NGLog.Warn("SurfaceFX", $"Surface material effect skipped: wall surfaces not supported ({surface.SurfaceId})");
                return;
            }

            if (surface != null && !surface.AllowFreeze)
            {
                NGLog.Warn("SurfaceFX", $"Surface material effect skipped: surface disallows override ({surface.SurfaceId})");
                return;
            }

            Material[] workingMaterials;
            try
            {
                workingMaterials = renderer.materials;
            }
            catch (Exception ex)
            {
                NGLog.Warn("SurfaceFX", $"Surface material effect skipped: cannot access materials on {renderer.name} - {ex.Message}");
                return;
            }

            if (workingMaterials == null || workingMaterials.Length == 0)
            {
                NGLog.Warn("SurfaceFX", $"Surface material effect skipped: renderer has no materials ({renderer.name})");
                return;
            }

            resolvedMaterialSlot = Mathf.Clamp(resolvedMaterialSlot, 0, workingMaterials.Length - 1);
            StopActiveSurfaceMaterialOverride(overrideKey, restoreOriginalMaterial: true);

            Material[] originalMaterials = (Material[])workingMaterials.Clone();
            workingMaterials[resolvedMaterialSlot] = overrideMaterial;

            try
            {
                renderer.materials = workingMaterials;
            }
            catch (Exception ex)
            {
                NGLog.Warn("SurfaceFX", $"Surface material effect skipped: failed to apply material - {ex.Message}");
                return;
            }

            float clampedDuration = Mathf.Clamp(
                durationSeconds > 0f ? durationSeconds : definition.surfaceOverrideDuration,
                0.25f,
                60f
            );

            m_ActiveSurfaceMaterialStates[overrideKey] = new SurfaceMaterialOverrideState
            {
                Key = overrideKey,
                SurfaceId = surface != null ? surface.SurfaceId : string.Empty,
                RendererPath = BuildHierarchyPath(renderer != null ? renderer.transform : null),
                Renderer = renderer,
                OriginalMaterials = originalMaterials,
                MaterialSlotIndex = resolvedMaterialSlot,
                AppliedAtRealtime = Time.realtimeSinceStartup,
            };

            m_ActiveSurfaceMaterialRoutines[overrideKey] = StartCoroutine(
                RestoreSurfaceMaterialOverrideAfter(overrideKey, clampedDuration)
            );

            NGLog.Info(
                "SurfaceFX",
                $"Surface material effect applied: {effectTag} on {(surface != null ? surface.SurfaceId : "unknown")} " +
                $"renderer={renderer.name}, slot={resolvedMaterialSlot}, duration={clampedDuration:F2}"
            );

            OnSurfaceEffectApplied?.Invoke(new SurfaceEffectInfo
            {
                EffectTag = effectTag,
                SurfaceId = surface != null ? surface.SurfaceId : string.Empty,
                DurationSeconds = clampedDuration,
                Position = renderer.bounds.center,
            });
        }

        private IEnumerator RestoreSurfaceMaterialOverrideAfter(string key, float durationSeconds)
        {
            yield return new WaitForSeconds(Mathf.Max(0.01f, durationSeconds));

            m_ActiveSurfaceMaterialRoutines.Remove(key);

            if (m_ActiveSurfaceMaterialStates.TryGetValue(key, out SurfaceMaterialOverrideState state))
            {
                TryRestoreSurfaceMaterialOverride(state);
                m_ActiveSurfaceMaterialStates.Remove(key);
            }
        }

        private void StopActiveSurfaceMaterialOverride(string key, bool restoreOriginalMaterial)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            if (m_ActiveSurfaceMaterialRoutines.TryGetValue(key, out Coroutine routine))
            {
                if (routine != null)
                {
                    StopCoroutine(routine);
                }
                m_ActiveSurfaceMaterialRoutines.Remove(key);
            }

            if (m_ActiveSurfaceMaterialStates.TryGetValue(key, out SurfaceMaterialOverrideState state))
            {
                if (restoreOriginalMaterial)
                {
                    TryRestoreSurfaceMaterialOverride(state);
                }
                m_ActiveSurfaceMaterialStates.Remove(key);
            }
        }

        private void TryRestoreSurfaceMaterialOverride(SurfaceMaterialOverrideState state)
        {
            if (state == null || state.Renderer == null || state.OriginalMaterials == null)
            {
                return;
            }

            try
            {
                state.Renderer.materials = state.OriginalMaterials;
                if (m_LogDebug)
                {
                    NGLog.Debug("SurfaceFX", $"Surface material restored: {state.SurfaceId} slot={state.MaterialSlotIndex}");
                }
            }
            catch (Exception ex)
            {
                NGLog.Warn("SurfaceFX", $"Failed to restore surface material: {state.SurfaceId} - {ex.Message}");
            }
        }

        #endregion

        #region Floor Dissolve

        /// <summary>
        /// Apply dissolve-style temporary invisibility to semantic floor/terrain objects.
        /// </summary>
        public void ApplyFloorDissolveEffect(float durationSeconds = 8f, string actionId = "")
        {
            List<GameObject> floorTargets = CollectFloorTargets();
            if (floorTargets.Count == 0)
            {
                NGLog.Warn("SurfaceFX", "Floor dissolve skipped: no floor targets found.");
                return;
            }

            float clampedDuration = Mathf.Clamp(durationSeconds, 0.6f, 30f);
            int appliedCount = 0;
            Vector3 firstPosition = Vector3.zero;

            for (int i = 0; i < floorTargets.Count; i++)
            {
                GameObject target = floorTargets[i];
                if (target == null)
                {
                    continue;
                }

                Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
                if (renderers == null || renderers.Length == 0)
                {
                    continue;
                }

                StopActiveObjectHide(target, restoreVisible: true);

                RendererFadeState[] fadeStates = BuildFadeStates(renderers);
                m_ActiveObjectHideStates[target] = fadeStates;

                Coroutine routine = StartCoroutine(
                    RunFloorDissolveSequence(target, target.name, clampedDuration, fadeStates)
                );
                m_ActiveObjectHideRoutines[target] = routine;

                if (appliedCount == 0)
                {
                    firstPosition = target.transform.position;
                }
                appliedCount++;
            }

            if (appliedCount <= 0)
            {
                NGLog.Warn("SurfaceFX", "Floor dissolve skipped: targets had no renderers.");
                return;
            }

            NGLog.Info("SurfaceFX", $"Floor dissolve effect started on {appliedCount} object(s) for {clampedDuration:0.00}s");

            OnFloorDissolveApplied?.Invoke(new FloorDissolveInfo
            {
                TargetCount = appliedCount,
                DurationSeconds = clampedDuration,
                Position = firstPosition,
            });
        }

        private IEnumerator RunFloorDissolveSequence(
            GameObject targetObject,
            string targetName,
            float durationSeconds,
            RendererFadeState[] fadeStates
        )
        {
            ResolveDissolveTimings(durationSeconds, out float fadeOut, out float hold, out float fadeIn);

            if (fadeOut > 0f)
            {
                float elapsed = 0f;
                while (elapsed < fadeOut)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / fadeOut);
                    float curved = m_FloorDissolveCurve != null ? m_FloorDissolveCurve.Evaluate(t) : t;
                    ApplyFadeAlpha(fadeStates, 1f - curved);
                    yield return null;
                }
            }

            SetRenderersEnabled(fadeStates, false);
            if (hold > 0f)
            {
                yield return new WaitForSecondsRealtime(hold);
            }

            SetRenderersEnabled(fadeStates, true);
            ApplyFadeAlpha(fadeStates, 0f);
            if (fadeIn > 0f)
            {
                float elapsed = 0f;
                while (elapsed < fadeIn)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / fadeIn);
                    float curved = m_FloorDissolveCurve != null ? m_FloorDissolveCurve.Evaluate(t) : t;
                    ApplyFadeAlpha(fadeStates, curved);
                    yield return null;
                }
            }

            RestoreFadeStates(fadeStates, forceVisible: true);
            m_ActiveObjectHideRoutines.Remove(targetObject);
            m_ActiveObjectHideStates.Remove(targetObject);

            NGLog.Info("SurfaceFX", $"Floor dissolve effect ended - visibility restored for {targetName}");
        }

        private void StopActiveObjectHide(GameObject targetObject, bool restoreVisible)
        {
            if (targetObject != null && m_ActiveObjectHideRoutines.TryGetValue(targetObject, out Coroutine routine))
            {
                if (routine != null)
                {
                    StopCoroutine(routine);
                }
                m_ActiveObjectHideRoutines.Remove(targetObject);
            }

            if (targetObject != null && m_ActiveObjectHideStates.TryGetValue(targetObject, out RendererFadeState[] states))
            {
                if (restoreVisible)
                {
                    RestoreFadeStates(states, forceVisible: true);
                }
                m_ActiveObjectHideStates.Remove(targetObject);
            }
        }

        private void ResolveDissolveTimings(
            float durationSeconds,
            out float fadeOut,
            out float hold,
            out float fadeIn
        )
        {
            float duration = Mathf.Max(0.6f, durationSeconds);
            float desiredFadeOut = Mathf.Max(m_FloorDissolveFadeOutSeconds, kMinDissolveFadeOutSeconds);
            float desiredFadeIn = Mathf.Max(m_FloorDissolveFadeInSeconds, kMinDissolveFadeInSeconds);
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

        #endregion

        #region Surface Resolution

        private void BuildSurfaceCache()
        {
            m_SurfaceById = new Dictionary<string, EffectSurface>(StringComparer.OrdinalIgnoreCase);
#if UNITY_2023_1_OR_NEWER
            EffectSurface[] surfaces = FindObjectsByType<EffectSurface>(FindObjectsInactive.Include);
#else
            EffectSurface[] surfaces = FindObjectsOfType<EffectSurface>(true);
#endif
            if (surfaces == null)
                return;
            for (int i = 0; i < surfaces.Length; i++)
            {
                EffectSurface s = surfaces[i];
                if (s != null && !string.IsNullOrWhiteSpace(s.SurfaceId))
                    m_SurfaceById.TryAdd(s.SurfaceId, s);
            }
        }

        private bool TryResolveNearestSurfaceOverrideTarget(
            Vector3 referencePosition,
            out EffectSurface surface,
            out Renderer renderer,
            out int materialSlotIndex,
            out string rendererHierarchyPath
        )
        {
            surface = null;
            renderer = null;
            materialSlotIndex = 0;
            rendererHierarchyPath = string.Empty;

            if (m_SurfaceById == null || m_SurfaceById.Count == 0)
            {
                BuildSurfaceCache();
            }

            float bestDistanceSq = float.PositiveInfinity;
            foreach (var kvp in m_SurfaceById)
            {
                EffectSurface candidate = kvp.Value;
                if (
                    candidate == null
                    || !candidate.AllowFreeze
                    || candidate.SurfaceType == EffectSurfaceType.Wall
                    || candidate.SurfaceType == EffectSurfaceType.Ceiling
                )
                {
                    continue;
                }

                Renderer candidateRenderer = candidate.GetPrimaryRenderer();
                if (candidateRenderer == null)
                {
                    continue;
                }

                Vector3 closestPoint = candidateRenderer.bounds.ClosestPoint(referencePosition);
                float distanceSq = (closestPoint - referencePosition).sqrMagnitude;
                if (distanceSq >= bestDistanceSq)
                {
                    continue;
                }

                bestDistanceSq = distanceSq;
                surface = candidate;
                renderer = candidateRenderer;
                materialSlotIndex = candidate.ResolveMaterialSlot(candidateRenderer);
                rendererHierarchyPath = BuildHierarchyPath(candidateRenderer.transform);
            }

            return surface != null && renderer != null;
        }

        private bool TryResolveFloorSurfaceOverrideTarget(
            string surfaceId,
            string rendererHierarchyPath,
            int requestedMaterialSlot,
            out EffectSurface surface,
            out Renderer renderer,
            out int resolvedMaterialSlot,
            out string overrideKey,
            out string reason
        )
        {
            surface = null;
            renderer = null;
            resolvedMaterialSlot = Mathf.Max(0, requestedMaterialSlot);
            overrideKey = string.Empty;
            reason = string.Empty;

            if (!string.IsNullOrWhiteSpace(surfaceId))
            {
                surface = FindEffectSurfaceById(surfaceId);
                if (surface != null)
                {
                    renderer = surface.GetPrimaryRenderer();
                    if (renderer == null)
                    {
                        reason = "Matched surface has no renderer.";
                        return false;
                    }

                    resolvedMaterialSlot = surface.ResolveMaterialSlot(renderer, requestedMaterialSlot);
                    overrideKey = "surface:" + surface.SurfaceId;
                    return true;
                }
            }

            string normalizedRendererPath = string.IsNullOrWhiteSpace(rendererHierarchyPath)
                ? string.Empty
                : rendererHierarchyPath.Trim();
            if (!string.IsNullOrWhiteSpace(normalizedRendererPath))
            {
                renderer = FindRendererByHierarchyPath(normalizedRendererPath);
                if (renderer != null)
                {
                    surface = renderer.GetComponentInParent<EffectSurface>();
                    if (surface != null)
                    {
                        resolvedMaterialSlot = surface.ResolveMaterialSlot(renderer, requestedMaterialSlot);
                        overrideKey = "surface:" + surface.SurfaceId;
                    }
                    else
                    {
                        Material[] shared = renderer.sharedMaterials;
                        int maxSlot = shared != null ? Mathf.Max(0, shared.Length - 1) : 0;
                        resolvedMaterialSlot = Mathf.Clamp(requestedMaterialSlot, 0, maxSlot);
                        overrideKey = "renderer:" + BuildHierarchyPath(renderer.transform);
                    }

                    return true;
                }
            }

            reason = string.IsNullOrWhiteSpace(surfaceId)
                ? "No surface id or renderer path supplied."
                : "Surface id was not found and renderer path fallback failed.";
            return false;
        }

        private EffectSurface FindEffectSurfaceById(string surfaceId)
        {
            if (string.IsNullOrWhiteSpace(surfaceId))
                return null;

            if (m_SurfaceById == null)
                BuildSurfaceCache();

            return m_SurfaceById.TryGetValue(surfaceId.Trim(), out var surface) ? surface : null;
        }

        private static Renderer FindRendererByHierarchyPath(string hierarchyPath)
        {
            if (string.IsNullOrWhiteSpace(hierarchyPath))
            {
                return null;
            }

            GameObject target = GameObject.Find(hierarchyPath);
            if (target != null)
            {
                Renderer directRenderer = target.GetComponent<Renderer>();
                if (directRenderer != null)
                {
                    return directRenderer;
                }

                return target.GetComponentInChildren<Renderer>(includeInactive: true);
            }

#if UNITY_2023_1_OR_NEWER
            Renderer[] renderers = FindObjectsByType<Renderer>(FindObjectsInactive.Include);
#else
            Renderer[] renderers = FindObjectsOfType<Renderer>(true);
#endif
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer candidate = renderers[i];
                if (candidate == null)
                {
                    continue;
                }

                if (string.Equals(BuildHierarchyPath(candidate.transform), hierarchyPath, StringComparison.Ordinal))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static string BuildHierarchyPath(Transform target)
        {
            if (target == null)
            {
                return string.Empty;
            }

            string path = target.name;
            Transform current = target.parent;
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }

        #endregion

        #region Floor Target Collection

        private List<GameObject> CollectFloorTargets()
        {
            var targets = new List<GameObject>();
            var seen = new HashSet<GameObject>();

            if (
                DialogueSceneTargetRegistry.GetTargetsForRoles(
                    targets,
                    DialogueSemanticRole.Floor,
                    DialogueSemanticRole.Terrain
                ) > 0
            )
            {
                return targets;
            }

#if UNITY_2023_1_OR_NEWER
            DialogueSemanticTag[] semanticTags = FindObjectsByType<DialogueSemanticTag>(
                findObjectsInactive: FindObjectsInactive.Exclude
            );
#else
            DialogueSemanticTag[] semanticTags = FindObjectsOfType<DialogueSemanticTag>();
#endif
            for (int i = 0; i < semanticTags.Length; i++)
            {
                DialogueSemanticTag tag = semanticTags[i];
                if (tag == null || tag.gameObject == null)
                {
                    continue;
                }

                if (tag.Role != DialogueSemanticRole.Floor && tag.Role != DialogueSemanticRole.Terrain)
                {
                    continue;
                }

                TryAddFloorTarget(tag.gameObject, seen, targets);
            }

            if (targets.Count > 0)
            {
                return targets;
            }

#if UNITY_2023_1_OR_NEWER
            Transform[] transforms = FindObjectsByType<Transform>(findObjectsInactive: FindObjectsInactive.Exclude);
#else
            Transform[] transforms = FindObjectsOfType<Transform>();
#endif
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform transform = transforms[i];
                if (transform == null || transform.gameObject == null)
                {
                    continue;
                }

                string objectName = transform.gameObject.name ?? string.Empty;
                if (!IsLikelyFloorName(objectName))
                {
                    continue;
                }

                TryAddFloorTarget(transform.gameObject, seen, targets);
            }

            return targets;
        }

        private static bool IsLikelyFloorName(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return false;
            }

            string lower = objectName.ToLowerInvariant();
            for (int i = 0; i < s_FloorNameHints.Length; i++)
            {
                if (lower.Contains(s_FloorNameHints[i], StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static void TryAddFloorTarget(GameObject candidate, HashSet<GameObject> seen, List<GameObject> targets)
        {
            if (candidate == null)
            {
                return;
            }

            if (!seen.Add(candidate))
            {
                return;
            }

            Renderer[] renderers = candidate.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                return;
            }

            targets.Add(candidate);
        }

        #endregion

        #region Fade State Helpers

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
                catch (Exception ex)
                {
                    NGLog.Warn("SurfaceFX", $"Dissolve skipped renderer {renderer.name}: {ex.Message}");
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
                material.SetFloat("_Surface", 1f);
            }
            if (state.HasMode)
            {
                material.SetFloat("_Mode", 2f);
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

        #endregion
    }
}
