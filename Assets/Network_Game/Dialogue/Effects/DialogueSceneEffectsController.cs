using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Network_Game.Combat;
using Network_Game.Diagnostics;

using Network_Game.Dialogue.Effects;

using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;

namespace Network_Game.Dialogue
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-460)]
    public class DialogueSceneEffectsController : MonoBehaviour
    {
        public static DialogueSceneEffectsController Instance { get; private set; }
        public static event Action<AppliedEffectInfo> OnEffectApplied;

        public struct AppliedEffectInfo
        {
            public string ActionId;
            public string EffectType;
            public string EffectName;
            public ulong SourceNetworkObjectId;
            public ulong TargetNetworkObjectId;
            public Vector3 Position;
            public float Scale;
            public float DurationSeconds;
            public bool AttachToTarget;
            public bool FitToTargetMesh;
            public float AppliedAtRealtime;
            public float FeedbackDelaySeconds;
        }

        private static readonly string[] s_BuiltInEffectTags =
        {
            // Built-in effects removed - use EffectCatalog ScriptableObject instead
        };

        /// <summary>
        /// Get the runtime-available effect tags for the current scene.
        /// </summary>
        public static string[] GetAvailableEffects()
        {
            var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < s_BuiltInEffectTags.Length; i++)
            {
                tags.Add(s_BuiltInEffectTags[i]);
            }

            CollectSceneEffectTags(tags);
            if (tags.Count == 0)
            {
                return Array.Empty<string>();
            }

            var result = new string[tags.Count];
            tags.CopyTo(result);
            Array.Sort(result, StringComparer.OrdinalIgnoreCase);
            return result;
        }

        [SerializeField]
        private bool m_LogDebug;

        // Unified effect lookup: effectTag (and prefab name as alias) → EffectDefinition.
        // Built from all NpcDialogueActor profiles in the scene.
        private Dictionary<string, EffectDefinition> m_EffectByTag;
        private bool m_EffectDefinitionLookupBuilt;

        private struct EffectPreflightState
        {
            public Vector3 Position;
            public Vector3 Forward;
            public float Scale;
            public float DurationSeconds;
            public float DamageRadius;
            public bool AttachToTarget;
            public bool FitToTargetMesh;
            public Transform TargetTransform;
            public string Reason;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                return;
            }

            Instance = this;
            EnsureEffectDefinitionLookup(forceRefresh: true);
            DialogueSceneTargetRegistry.EnsureAvailable();
        }

        /// <summary>
        /// Applies NPC operator property patches to a target GameObject.
        /// Called by the dialogue dispatcher when action.Type == "PATCH".
        /// </summary>
        public void ApplyPropertyPatches(DialogueAction action, GameObject target)
        {
            if (action == null || target == null)
            {
                NGLog.Warn("DialogueFX", NGLog.Format("ApplyPropertyPatches: null argument", ("action", action == null ? "null" : "ok"), ("target", target == null ? "null" : target.name)));
                return;
            }

            bool didAnything = false;
            var log = new System.Text.StringBuilder("PATCH applied");
            log.Append($" target={target.name}");

            Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);

            // ── Health ───────────────────────────────────────────────────────────
            if (action.HealthDelta.HasValue && action.HealthDelta.Value != 0f)
            {
                var health = target.GetComponentInChildren<CombatHealthV2>();
                if (health != null)
                {
                    float delta = action.HealthDelta.Value;
                    if (delta < 0f)
                        health.ApplyDamage(-delta, 0ul, "patch");
                    log.Append($" health={delta:F1}");
                    didAnything = true;
                }
                else
                {
                    log.Append(" health=SKIP(no CombatHealthV2)");
                }
            }

            // ── Position offset ──────────────────────────────────────────────────
            if (action.PositionOffset != null && action.PositionOffset.Length >= 3)
            {
                var offset = new Vector3(action.PositionOffset[0], action.PositionOffset[1], action.PositionOffset[2]);
                target.transform.position += offset;
                log.Append($" offset={offset}");
                didAnything = true;
            }

            // ── Scale ────────────────────────────────────────────────────────────
            if (action.Scale.HasValue && action.Scale.Value > 0f)
            {
                target.transform.localScale = Vector3.one * action.Scale.Value;
                log.Append($" scale={action.Scale.Value:F2}");
                didAnything = true;
            }

            // ── Material color (URP-safe: sets _BaseColor and _Color) ────────────
            if (!string.IsNullOrWhiteSpace(action.PatchColor))
            {
                Color c = Effects.EffectParser.ParseColor(action.PatchColor);
                if (renderers.Length > 0)
                {
                    foreach (Renderer rend in renderers)
                    {
                        if (rend.material.HasProperty("_BaseColor"))
                            rend.material.SetColor("_BaseColor", c);
                        else if (rend.material.HasProperty("_Color"))
                            rend.material.SetColor("_Color", c);
                    }
                    log.Append($" color={action.PatchColor}");
                    didAnything = true;
                }
                else
                {
                    log.Append(" color=SKIP(no renderer)");
                }
            }

            // ── Emission ─────────────────────────────────────────────────────────
            if (action.Emission.HasValue)
            {
                if (renderers.Length > 0)
                {
                    Color emissionColor = Color.white * Mathf.Max(0f, action.Emission.Value);
                    foreach (Renderer rend in renderers)
                    {
                        if (rend.material.HasProperty("_EmissionColor"))
                        {
                            rend.material.SetColor("_EmissionColor", emissionColor);
                            if (action.Emission.Value > 0f)
                                rend.material.EnableKeyword("_EMISSION");
                            else
                                rend.material.DisableKeyword("_EMISSION");
                        }
                    }
                    log.Append($" emission={action.Emission.Value:F2}");
                    didAnything = true;
                }
                else
                {
                    log.Append(" emission=SKIP(no renderer)");
                }
            }

            // ── Visibility ───────────────────────────────────────────────────────
            if (action.Visible.HasValue)
            {
                foreach (Renderer rend in renderers)
                    rend.enabled = action.Visible.Value;
                log.Append($" visible={action.Visible.Value}");
                didAnything = true;
            }

            if (didAnything)
                NGLog.Info("DialogueFX", log.ToString());
        }

        public void ApplyPrefabPower(
            string prefabName,
            Vector3 position,
            Vector3 forward,
            float scale,
            float durationSeconds,
            Color color,
            bool useColorOverride,
            bool enableGameplayDamage = false,
            bool enableHoming = false,
            float projectileSpeed = 10f,
            float homingTurnRateDegrees = 240f,
            float damageAmount = 0f,
            float damageRadius = 1f,
            bool affectPlayerOnly = false,
            string damageType = "effect",
            ulong sourceNetworkObjectId = 0,
            ulong targetNetworkObjectId = 0,
            bool attachToTarget = false,
            bool fitToTargetMesh = false,
            float serverSpawnTimeSeconds = -1f,
            uint effectSeed = 0,
            string actionId = ""
        )
        {
            EffectDefinition definition = ResolveEffectDefinition(prefabName);
            if (definition?.effectPrefab == null)
            {
                NGLog.Warn(
                    "DialogueFX",
                    NGLog.Format(
                        "Prefab power skipped: profile effect definition missing",
                        ("tag", prefabName ?? string.Empty)
                    )
                );
                return;
            }
            GameObject prefab = definition.effectPrefab;

            Vector3 spawnForward =
                forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
            Quaternion rotation = Quaternion.LookRotation(spawnForward, Vector3.up);

            GameObject instance = Instantiate(prefab, position, rotation);
            if (instance == null)
            {
                NGLog.Warn(
                    "DialogueFX",
                    NGLog.Format(
                        "Prefab instantiate returned null",
                        ("prefab", prefabName ?? string.Empty)
                    )
                );
                return;
            }

            instance.name = $"DialoguePrefabPower_{prefabName}";
            instance.SetActive(false);

            float clampedScale = Mathf.Max(0.1f, scale);
            float tunedDurationSeconds = Mathf.Max(0.5f, durationSeconds);
            Transform targetTransform = ResolveNetworkTargetTransform(targetNetworkObjectId);

            EffectPreflightState preflight = ValidatePrefabPowerPreflight(
                prefabName,
                definition,
                position,
                spawnForward,
                clampedScale,
                tunedDurationSeconds,
                damageRadius,
                targetTransform,
                attachToTarget,
                fitToTargetMesh
            );

            position = preflight.Position;
            spawnForward = preflight.Forward;
            clampedScale = preflight.Scale;
            tunedDurationSeconds = preflight.DurationSeconds;
            damageRadius = preflight.DamageRadius;
            targetTransform = preflight.TargetTransform;
            attachToTarget = preflight.AttachToTarget;
            fitToTargetMesh = preflight.FitToTargetMesh;
            rotation = Quaternion.LookRotation(spawnForward, Vector3.up);

            if (
                attachToTarget
                && fitToTargetMesh
                && targetTransform != null
                && TryResolveTargetBounds(targetTransform, out Bounds targetBounds)
            )
            {
                clampedScale = Mathf.Clamp(
                    clampedScale * ComputeMeshFitScale(targetBounds),
                    0.1f,
                    60f
                );
                position = targetBounds.center;
            }
            else if (attachToTarget && targetTransform != null)
            {
                position = targetTransform.position;
            }
            else if (targetTransform != null && position.y - targetTransform.position.y > 5f)
            {
                // Non-attached effects should not inherit stale elevated probe positions.
                Vector3 anchorForward = Vector3.ProjectOnPlane(targetTransform.forward, Vector3.up);
                if (anchorForward.sqrMagnitude < 0.0001f)
                {
                    anchorForward = Vector3.ProjectOnPlane(spawnForward, Vector3.up);
                }

                if (anchorForward.sqrMagnitude < 0.0001f)
                {
                    anchorForward = Vector3.forward;
                }

                position = targetTransform.position + anchorForward.normalized * 2f;
            }

            instance.transform.SetPositionAndRotation(position, rotation);
            instance.transform.localScale = prefab.transform.localScale * clampedScale;

            if (attachToTarget && targetTransform != null)
            {
                instance.transform.SetParent(targetTransform, true);
            }

            // When scale is significantly larger than default, also boost emission rate
            // and shape radius so the particle density matches the visual coverage.
            float emissionScaleFactor = clampedScale > 1.5f ? Mathf.Sqrt(clampedScale) : 1f;
            bool enableProjectileBehavior = enableGameplayDamage || enableHoming;
            bool restrictDamageToTarget =
                targetNetworkObjectId != 0UL
                && (attachToTarget || fitToTargetMesh || enableHoming || affectPlayerOnly);
            float catchUpSeconds = ResolveEffectCatchUpSeconds(serverSpawnTimeSeconds);
            uint baseSeed =
                effectSeed == 0U ? (uint)UnityEngine.Random.Range(1, int.MaxValue) : effectSeed;

            float duration = tunedDurationSeconds;
            ParticleSystem[] systems = instance.GetComponentsInChildren<ParticleSystem>(true);
            float maxLifetime = duration;

            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem ps = systems[i];
                if (ps == null)
                {
                    continue;
                }

                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

                ParticleSystem.MainModule main = ps.main;
                main.loop = enableProjectileBehavior;
                main.playOnAwake = false;
                main.duration = duration;

                if (useColorOverride)
                {
                    main.startColor = color;
                }

                ps.useAutoRandomSeed = false;
                ps.randomSeed = baseSeed + (uint)(i * 9973);

                // Scale emission to maintain particle density at larger scales
                if (emissionScaleFactor > 1f)
                {
                    ParticleSystem.EmissionModule emission = ps.emission;
                    emission.rateOverTime = ScaleCurve(emission.rateOverTime, emissionScaleFactor);
                    emission.rateOverDistance = ScaleCurve(
                        emission.rateOverDistance,
                        emissionScaleFactor
                    );
                    main.maxParticles = Mathf.Max(
                        main.maxParticles,
                        Mathf.RoundToInt(main.maxParticles * emissionScaleFactor)
                    );
                }

                float systemLifetime =
                    duration + ResolveMaxStartLifetimeSeconds(main.startLifetime);
                if (systemLifetime > maxLifetime)
                {
                    maxLifetime = systemLifetime;
                }
            }

            instance.SetActive(true);
            NGLog.Info(
                "DialogueFX",
                NGLog.Format(
                    "Prefab power instance spawned",
                    ("prefab", prefabName ?? string.Empty),
                    ("name", instance.name),
                    ("source", sourceNetworkObjectId),
                    ("target", targetNetworkObjectId),
                    ("position", $"({position.x:F2}, {position.y:F2}, {position.z:F2})"),
                    ("scale", clampedScale),
                    ("duration", duration)
                )
            );
            
            // DEBUG: Draw a visible marker at spawn position
            Debug.DrawLine(position, position + Vector3.up * 5f, Color.yellow, 5f);
            Debug.DrawLine(position, position + Vector3.right * 2f, Color.red, 5f);
            Debug.DrawLine(position, position + Vector3.forward * 2f, Color.blue, 5f);


            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem ps = systems[i];
                if (ps == null)
                {
                    continue;
                }

                ps.Clear(true);
                ps.Play(true);
                if (catchUpSeconds > 0.001f)
                {
                    float simulateSeconds = Mathf.Clamp(catchUpSeconds, 0f, maxLifetime);
                    ps.Simulate(simulateSeconds, true, false, true);
                }
            }

            if (enableProjectileBehavior)
            {
                DialogueEffectProjectile projectile =
                    instance.GetComponent<DialogueEffectProjectile>();
                if (projectile == null)
                {
                    projectile = instance.AddComponent<DialogueEffectProjectile>();
                }

                projectile.Configure(
                    sourceNetworkObjectId,
                    targetNetworkObjectId,
                    targetTransform,
                    enableHoming,
                    projectileSpeed,
                    homingTurnRateDegrees,
                    damageAmount,
                    damageRadius,
                    duration,
                    affectPlayerOnly,
                    restrictDamageToTarget,
                    damageType,
                    m_LogDebug
                );
            }
            else
            {
                StartCoroutine(DestroyParticleSystemAfter(instance, maxLifetime + 1f));
            }

            if (m_LogDebug)
            {
                NGLog.Info(
                    "DialogueFX",
                    NGLog.Format(
                        "Applied prefab power",
                        ("prefab", prefabName),
                        ("scale", clampedScale),
                        ("duration", duration),
                        ("systems", systems.Length),
                        ("colorOverride", useColorOverride),
                        ("gameplay", enableGameplayDamage),
                        ("homing", enableHoming),
                        ("source", sourceNetworkObjectId),
                        ("target", targetNetworkObjectId),
                        ("strictTarget", restrictDamageToTarget),
                        ("seed", baseSeed),
                        ("catchUp", catchUpSeconds.ToString("F3")),
                        ("damage", damageAmount),
                        ("attached", attachToTarget),
                        ("fitToMesh", fitToTargetMesh)
                    )
                );
            }

            float feedbackDelaySeconds = Mathf.Clamp(duration * 0.25f, 0.35f, 1.1f);
            EmitEffectApplied(
                new AppliedEffectInfo
                {
                    ActionId = actionId ?? string.Empty,
                    EffectType = "prefab_power",
                    EffectName = prefabName ?? string.Empty,
                    SourceNetworkObjectId = sourceNetworkObjectId,
                    TargetNetworkObjectId = targetNetworkObjectId,
                    Position = position,
                    Scale = clampedScale,
                    DurationSeconds = duration,
                    AttachToTarget = attachToTarget,
                    FitToTargetMesh = fitToTargetMesh,
                    AppliedAtRealtime = Time.realtimeSinceStartup,
                    FeedbackDelaySeconds = feedbackDelaySeconds,
                }
            );
            NGLog.Info(
                "DialogueFX",
                NGLog.Format(
                    "Prefab power feedback emitted",
                    ("prefab", prefabName ?? string.Empty),
                    ("feedbackDelay", feedbackDelaySeconds.ToString("F2"))
                )
            );
        }

        private static bool TryResolveTargetBounds(Transform targetTransform, out Bounds bounds)
        {
            bounds = default;
            if (targetTransform == null)
            {
                return false;
            }

            Renderer[] renderers = targetTransform.GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds;
        }

        private static float ComputeMeshFitScale(Bounds bounds)
        {
            float height = Mathf.Max(0.1f, bounds.size.y);
            float width = Mathf.Max(0.1f, Mathf.Max(bounds.size.x, bounds.size.z));
            float fit = Mathf.Max(height * 0.45f, width * 0.8f);
            return Mathf.Clamp(fit, 0.5f, 4f);
        }

        private static float ResolveEffectCatchUpSeconds(float serverSpawnTimeSeconds)
        {
            if (serverSpawnTimeSeconds <= 0f)
            {
                return 0f;
            }

            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager == null || !networkManager.IsListening)
            {
                return 0f;
            }

            float localServerTime = networkManager.ServerTime.TimeAsFloat;
            return Mathf.Clamp(localServerTime - serverSpawnTimeSeconds, 0f, 2f);
        }

        private static Transform ResolveNetworkTargetTransform(ulong networkObjectId)
        {
            if (networkObjectId == 0)
            {
                return null;
            }

            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsListening || manager.SpawnManager == null)
            {
                return null;
            }

            if (
                manager.SpawnManager.SpawnedObjects.TryGetValue(
                    networkObjectId,
                    out NetworkObject networkObject
                )
                && networkObject != null
            )
            {
                return networkObject.transform;
            }

            return null;
        }

        private EffectPreflightState ValidatePrefabPowerPreflight(
            string prefabName,
            EffectDefinition definition,
            Vector3 position,
            Vector3 forward,
            float scale,
            float durationSeconds,
            float damageRadius,
            Transform targetTransform,
            bool attachToTarget,
            bool fitToTargetMesh
        )
        {
            EffectPreflightState state = new EffectPreflightState
            {
                Position = position,
                Forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward,
                Scale = Mathf.Clamp(scale, 0.1f, 60f),
                DurationSeconds = Mathf.Clamp(durationSeconds, 0.1f, 60f),
                DamageRadius = Mathf.Clamp(damageRadius, 0.1f, 60f),
                AttachToTarget = attachToTarget,
                FitToTargetMesh = fitToTargetMesh,
                TargetTransform = targetTransform,
                Reason = string.Empty,
            };

            List<string> reasons = null;
            if (definition != null)
            {
                state.Scale = Mathf.Clamp(state.Scale, definition.minScale, definition.maxScale);
                state.DurationSeconds = Mathf.Clamp(
                    state.DurationSeconds,
                    definition.minDuration,
                    definition.maxDuration
                );
                state.DamageRadius = Mathf.Clamp(
                    state.DamageRadius,
                    definition.minRadius,
                    definition.maxRadius
                );

                switch (definition.placementMode)
                {
                    case EffectPlacementMode.AttachMesh:
                        state.AttachToTarget = true;
                        state.FitToTargetMesh = definition.preferFitTargetMesh;
                        break;
                    case EffectPlacementMode.GroundAoe:
                        state.AttachToTarget = false;
                        state.FitToTargetMesh = false;
                        if (TryProjectToGround(state.Position, out Vector3 groundPos))
                        {
                            state.Position = groundPos;
                        }
                        else if (state.TargetTransform != null)
                        {
                            Vector3 targetGroundProbe = state.TargetTransform.position;
                            if (TryProjectToGround(targetGroundProbe, out groundPos))
                            {
                                state.Position = groundPos;
                            }
                        }
                        break;
                    case EffectPlacementMode.SkyVolume:
                        state.AttachToTarget = false;
                        state.FitToTargetMesh = false;
                        Vector3 skyAnchor =
                            state.TargetTransform != null
                            ? state.TargetTransform.position
                            : state.Position;
                        state.Position = skyAnchor + Vector3.up * Mathf.Max(6f, state.Scale * 2f);
                        break;
                    case EffectPlacementMode.Projectile:
                        state.AttachToTarget = false;
                        state.FitToTargetMesh = false;
                        if (state.TargetTransform != null)
                        {
                            Vector3 toTarget = state.TargetTransform.position - state.Position;
                            if (toTarget.sqrMagnitude > 0.0001f)
                            {
                                state.Forward = toTarget.normalized;
                            }
                        }
                        break;
                }

                switch (definition.targetType)
                {
                    case EffectTargetType.Floor:
                        state.AttachToTarget = false;
                        state.FitToTargetMesh = false;
                        Vector3 floorAnchor =
                            state.TargetTransform != null
                            ? state.TargetTransform.position
                            : state.Position;
                        if (TryProjectToGround(floorAnchor, out Vector3 floorPos))
                        {
                            state.Position = floorPos;
                        }
                        break;
                    case EffectTargetType.WorldPoint:
                        state.AttachToTarget = false;
                        state.FitToTargetMesh = false;
                        break;
                }
            }

            if (state.AttachToTarget && state.TargetTransform == null)
            {
                state.AttachToTarget = false;
                state.FitToTargetMesh = false;
                reasons ??= new List<string>(2);
                reasons.Add("missing_target_for_attach");
            }

            if (state.FitToTargetMesh)
            {
                if (
                    state.TargetTransform == null
                    || !TryResolveTargetBounds(state.TargetTransform, out _)
                )
                {
                    state.FitToTargetMesh = false;
                    reasons ??= new List<string>(2);
                    reasons.Add("mesh_fit_unavailable");
                }
            }

            if (!state.AttachToTarget && LooksLikeGroundEffect(prefabName))
            {
                if (TryProjectToGround(state.Position, out Vector3 projectedGround))
                {
                    state.Position = projectedGround;
                }
            }

            if (state.Forward.sqrMagnitude < 0.0001f)
            {
                state.Forward = Vector3.forward;
            }

            if (reasons != null && reasons.Count > 0)
            {
                state.Reason = string.Join(",", reasons);
                if (m_LogDebug)
                {
                    NGLog.Info(
                        "DialogueFX",
                        NGLog.Format(
                            "Prefab preflight fallback applied",
                            ("prefab", prefabName ?? string.Empty),
                            ("reason", state.Reason),
                            ("attach", state.AttachToTarget),
                            ("fitMesh", state.FitToTargetMesh)
                        )
                    );
                }
            }

            return state;
        }

        private static bool LooksLikeGroundEffect(string prefabName)
        {
            if (string.IsNullOrWhiteSpace(prefabName))
            {
                return false;
            }

            string lower = prefabName.ToLowerInvariant();
            return lower.Contains("ground")
                || lower.Contains("floor")
                || lower.Contains("freeze")
                || lower.Contains("frost")
                || lower.Contains("ice")
                || lower.Contains("snow")
                || lower.Contains("shockwave")
                || lower.Contains("blast")
                || lower.Contains("explosion");
        }

        private static bool TryProjectToGround(Vector3 source, out Vector3 groundedPosition)
        {
            Vector3 rayOrigin = source + Vector3.up * 6f;
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 30f))
            {
                groundedPosition = hit.point;
                return true;
            }

            groundedPosition = source;
            return false;
        }

        private void EnsureEffectDefinitionLookup(bool forceRefresh)
        {
            if (m_EffectDefinitionLookupBuilt && !forceRefresh)
                return;

            m_EffectDefinitionLookupBuilt = true;

            if (m_EffectByTag == null)
                m_EffectByTag = new Dictionary<string, EffectDefinition>(StringComparer.OrdinalIgnoreCase);
            else
                m_EffectByTag.Clear();

            int count = 0;
            foreach (EffectDefinition def in EnumerateSceneEffectDefinitions())
            {
                if (def == null || !def.enabled || def.effectPrefab == null)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(def.effectTag))
                    m_EffectByTag.TryAdd(def.effectTag.Trim(), def);
                // Also register by prefab name as an alias for the current wire contract.
                m_EffectByTag.TryAdd(def.effectPrefab.name, def);
                if (def.alternativeTags != null)
                    foreach (string alt in def.alternativeTags)
                        if (!string.IsNullOrWhiteSpace(alt))
                            m_EffectByTag.TryAdd(alt.Trim(), def);
                count++;
            }

            if (m_LogDebug)
                NGLog.Debug("DialogueFX", NGLog.Format("EffectDefinitionLookup built", ("count", count)));
        }

        /// <summary>
        /// Resolves the EffectDefinition for a tag/prefab name. Returns null when not found.
        /// </summary>
        private EffectDefinition ResolveEffectDefinition(string tag, bool refreshOnMiss = true)
        {
            if (string.IsNullOrWhiteSpace(tag))
                return null;

            EnsureEffectDefinitionLookup(false);

            if (m_EffectByTag != null && m_EffectByTag.TryGetValue(tag, out EffectDefinition def))
                return def;

            // Actors may have spawned after the last build — refresh once on miss.
            if (refreshOnMiss)
            {
                EnsureEffectDefinitionLookup(true);
                if (m_EffectByTag != null && m_EffectByTag.TryGetValue(tag, out def))
                    return def;
            }

            return null;
        }

        private static IEnumerable<EffectDefinition> EnumerateSceneEffectDefinitions()
        {
            var emittedProfiles = new HashSet<NpcDialogueProfile>();
#if UNITY_2023_1_OR_NEWER
            NpcDialogueActor[] actors = FindObjectsByType<NpcDialogueActor>();
#else
            NpcDialogueActor[] actors = FindObjectsOfType<NpcDialogueActor>();
#endif
            if (actors == null)
            {
                yield break;
            }

            for (int actorIndex = 0; actorIndex < actors.Length; actorIndex++)
            {
                NpcDialogueActor actor = actors[actorIndex];
                NpcDialogueProfile profile = actor != null ? actor.Profile : null;
                if (profile == null || !emittedProfiles.Add(profile))
                {
                    continue;
                }

                EffectDefinition[] effects = profile.Effects;
                if (effects == null)
                {
                    continue;
                }

                for (int effectIndex = 0; effectIndex < effects.Length; effectIndex++)
                {
                    EffectDefinition effect = effects[effectIndex];
                    if (effect != null)
                    {
                        yield return effect;
                    }
                }
            }
        }

        private static void CollectSceneEffectTags(ISet<string> tags)
        {
            foreach (EffectDefinition effect in EnumerateSceneEffectDefinitions())
            {
                if (effect == null || !effect.enabled)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(effect.effectTag))
                {
                    tags.Add(effect.effectTag.Trim());
                }

                if (effect.alternativeTags == null)
                {
                    continue;
                }

                for (int i = 0; i < effect.alternativeTags.Length; i++)
                {
                    string alt = effect.alternativeTags[i];
                    if (!string.IsNullOrWhiteSpace(alt))
                    {
                        tags.Add(alt.Trim());
                    }
                }
            }
        }


        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        internal static ParticleSystem.MinMaxCurve ScaleCurve(
            ParticleSystem.MinMaxCurve curve,
            float scale
        )
        {
            float clampedScale = Mathf.Max(0f, scale);
            curve.constant *= clampedScale;
            curve.constantMin *= clampedScale;
            curve.constantMax *= clampedScale;
            curve.curveMultiplier *= clampedScale;
            return curve;
        }

        internal static float ResolveMaxStartLifetimeSeconds(
            ParticleSystem.MinMaxCurve lifetimeCurve
        )
        {
            float max = Mathf.Max(lifetimeCurve.constant, lifetimeCurve.constantMax);
            max = Mathf.Max(max, lifetimeCurve.constantMin);
            max = Mathf.Max(max, lifetimeCurve.curveMultiplier);
            return Mathf.Max(0.05f, max);
        }

        internal static float ResolveCurveMax(ParticleSystem.MinMaxCurve curve)
        {
            float max = Mathf.Max(curve.constant, curve.constantMax);
            max = Mathf.Max(max, curve.constantMin);
            max = Mathf.Max(max, curve.curveMultiplier);
            return Mathf.Max(0.01f, max);
        }

        private IEnumerator DestroyParticleSystemAfter(GameObject target, float lifetimeSeconds)
        {
            yield return new WaitForSeconds(Mathf.Max(0.01f, lifetimeSeconds));
            if (target != null)
            {
                Destroy(target);
            }
        }

        private static void EmitEffectApplied(AppliedEffectInfo info)
        {
            Action<AppliedEffectInfo> handler = OnEffectApplied;
            if (handler == null)
            {
                return;
            }

            try
            {
                handler.Invoke(info);
            }
            catch (Exception ex)
            {
                NGLog.Warn(
                    "DialogueFX",
                    NGLog.Format("Effect observer callback failed", ("error", ex.Message ?? string.Empty))
                );
            }
        }

        #region Player Special Effects

        // NOTE: Dissolve effects, respawn FX, and surface material overrides have been removed
        // from this class to follow Single Responsibility Principle.
        // These features should be implemented in separate components:
        // - PlayerDissolveController (for player invisibility/respawn FX)
        // - SurfaceMaterialEffectController (for floor/wall material overrides)
        // - DialogueParticleCollisionDamage (already exists, self-configures)

        public void ApplyDissolveEffect(ulong targetNetworkObjectId, float durationSeconds = 5f, string actionId = "")
        {
            NGLog.Warn("DialogueFX", "ApplyDissolveEffect is deprecated. Use PlayerDissolveController instead.");
        }

        public void ApplyFloorDissolveEffect(float durationSeconds = 8f, string actionId = "")
        {
            NGLog.Warn("DialogueFX", "ApplyFloorDissolveEffect is deprecated. Use SurfaceMaterialEffectController instead.");
        }

        public void ApplyRespawnEffect(ulong targetNetworkObjectId, string actionId = "")
        {
            NGLog.Warn("DialogueFX", "ApplyRespawnEffect is deprecated. Use PlayerDissolveController instead.");
        }

        public void ApplySurfaceMaterialEffect(string effectTag, Vector3 referencePosition, float durationSeconds, ulong sourceNetworkObjectId = 0, ulong targetNetworkObjectId = 0, string actionId = "")
        {
            NGLog.Warn("DialogueFX", "ApplySurfaceMaterialEffect is deprecated. Use SurfaceMaterialEffectController instead.");
        }

        public void ApplySurfaceMaterialEffect(string effectTag, string surfaceId, string rendererHierarchyPath, int materialSlotIndex, float durationSeconds, ulong sourceNetworkObjectId = 0, ulong targetNetworkObjectId = 0, string actionId = "")
        {
            NGLog.Warn("DialogueFX", "ApplySurfaceMaterialEffect is deprecated. Use SurfaceMaterialEffectController instead.");
        }

        #endregion
    }
}
