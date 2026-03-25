using System;
using System.IO;
using System.Text;
using Network_Game.Diagnostics;
using Network_Game.Dialogue.Effects;
using Unity.Netcode;
using UnityEngine;

namespace Network_Game.Dialogue
{
    public partial class NetworkDialogueService
    {
        private static void LogPrefabPowerSuccess(
            string prompt,
            string response,
            string powerName,
            string prefabName,
            float scale,
            float duration,
            float damage,
            float damageRadius,
            bool gameplay,
            bool homing,
            ulong sourceId,
            ulong targetId
        )
        {
            try
            {
                string logDir = Path.Combine(Application.persistentDataPath, "DialogueLogs");
                if (!Directory.Exists(logDir))
                {
                    Directory.CreateDirectory(logDir);
                }

                string logPath = Path.Combine(logDir, "prefab_power_success.jsonl");
                string timestamp = DateTime.UtcNow.ToString("o");
                string safePrompt = EscapeJsonString(prompt ?? string.Empty);
                string safeResponse = EscapeJsonString(
                    response != null && response.Length > 300
                        ? response.Substring(0, 300)
                        : response ?? string.Empty
                );
                string safePower = EscapeJsonString(powerName ?? string.Empty);
                string safePrefab = EscapeJsonString(prefabName ?? string.Empty);

                string json = string.Concat(
                    "{\"ts\":\"",
                    timestamp,
                    "\",\"power\":\"",
                    safePower,
                    "\",\"prefab\":\"",
                    safePrefab,
                    "\",\"scale\":",
                    scale.ToString("F2"),
                    ",\"duration\":",
                    duration.ToString("F2"),
                    ",\"damage\":",
                    damage.ToString("F2"),
                    ",\"damageRadius\":",
                    damageRadius.ToString("F2"),
                    ",\"gameplay\":",
                    gameplay ? "true" : "false",
                    ",\"homing\":",
                    homing ? "true" : "false",
                    ",\"source\":",
                    sourceId.ToString(),
                    ",\"target\":",
                    targetId.ToString(),
                    ",\"prompt\":\"",
                    safePrompt,
                    "\",\"response\":\"",
                    safeResponse,
                    "\"}"
                );

                File.AppendAllText(logPath, json + "\n", Encoding.UTF8);
            }
            catch (Exception ex)
            {
                NGLog.Warn("DialogueFX", $"Failed to log prefab power success: {ex.Message}");
            }
        }

        [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
        private void ApplyDissolveEffectClientRpc(
            ulong targetNetworkObjectId,
            float durationSeconds,
            string actionId = ""
        )
        {
            RecordLocalEffectReceipt(
                "dissolve",
                "dissolve",
                actionId: actionId,
                targetNetworkObjectId: targetNetworkObjectId
            );
            EnsurePlayerDissolveController();
            if (m_PlayerDissolveController == null)
            {
                return;
            }

            m_PlayerDissolveController.ApplyDissolveEffect(
                targetNetworkObjectId,
                durationSeconds,
                actionId
            );
        }

        [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
        private void ApplyFloorDissolveEffectClientRpc(float durationSeconds, string actionId = "")
        {
            RecordLocalEffectReceipt("dissolve", "floor_dissolve", actionId: actionId);
            EnsureSurfaceMaterialEffectController();
            if (m_SurfaceMaterialEffectController == null)
            {
                return;
            }

            m_SurfaceMaterialEffectController.ApplyFloorDissolveEffect(durationSeconds, actionId);
        }

        [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
        private void ApplyRespawnEffectClientRpc(ulong targetNetworkObjectId, string actionId = "")
        {
            RecordLocalEffectReceipt(
                "respawn",
                "respawn",
                actionId: actionId,
                targetNetworkObjectId: targetNetworkObjectId
            );
            EnsurePlayerDissolveController();
            if (m_PlayerDissolveController == null)
            {
                return;
            }

            m_PlayerDissolveController.ApplyRespawnEffect(targetNetworkObjectId, actionId);
        }

        [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
        private void ApplySurfaceMaterialEffectClientRpc(
            string effectTag,
            Vector3 referencePosition,
            float durationSeconds,
            ulong sourceNetworkObjectId,
            ulong targetNetworkObjectId,
            string actionId = ""
        )
        {
            RecordLocalEffectReceipt(
                "surface_material",
                effectTag ?? "surface_material",
                actionId: actionId,
                sourceNetworkObjectId: sourceNetworkObjectId,
                targetNetworkObjectId: targetNetworkObjectId
            );
            EnsureSurfaceMaterialEffectController();
            if (m_SurfaceMaterialEffectController == null)
            {
                return;
            }

            // Resolve effect definition for surface material parameters
            EffectDefinition definition = null;
            if (m_SceneEffectsController != null)
            {
                // Use reflection or a public method to resolve the definition
                // For now, we'll need to resolve it through the effect system
                definition = ResolveEffectDefinitionForTag(effectTag);
            }

            m_SurfaceMaterialEffectController.ApplySurfaceMaterialEffect(
                effectTag,
                referencePosition,
                durationSeconds,
                definition,
                sourceNetworkObjectId,
                targetNetworkObjectId,
                actionId
            );
        }


        /// <summary>
        /// Resolves an EffectDefinition for the given tag by querying scene NPC profiles.
        /// </summary>
        private EffectDefinition ResolveEffectDefinitionForTag(string effectTag)
        {
            if (string.IsNullOrWhiteSpace(effectTag))
                return null;

#if UNITY_2023_1_OR_NEWER
            NpcDialogueActor[] actors = FindObjectsByType<NpcDialogueActor>(FindObjectsInactive.Exclude);
#else
            NpcDialogueActor[] actors = FindObjectsOfType<NpcDialogueActor>();
#endif
            if (actors == null)
                return null;

            foreach (var actor in actors)
            {
                if (actor?.Profile?.Effects == null)
                    continue;

                foreach (var effect in actor.Profile.Effects)
                {
                    if (effect == null || !effect.enabled)
                        continue;

                    if (string.Equals(effect.effectTag, effectTag, StringComparison.OrdinalIgnoreCase))
                        return effect;

                    if (effect.alternativeTags != null)
                    {
                        foreach (var alt in effect.alternativeTags)
                        {
                            if (string.Equals(alt, effectTag, StringComparison.OrdinalIgnoreCase))
                                return effect;
                        }
                    }
                }
            }

            return null;
        }

        [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
        private void ApplyPrefabPowerEffectClientRpc(
            string prefabName,
            Vector3 position,
            Vector3 forward,
            float scale,
            float durationSeconds,
            Vector4 color,
            bool useColorOverride,
            bool enableGameplayDamage,
            bool enableHoming,
            float projectileSpeed,
            float homingTurnRateDegrees,
            float damageAmount,
            float damageRadius,
            bool affectPlayerOnly,
            string damageType,
            ulong targetNetworkObjectId,
            ulong sourceNetworkObjectId,
            bool attachToTarget,
            bool fitToTargetMesh,
            float serverSpawnTimeSeconds,
            uint effectSeed,
            string actionId = ""
        )
        {
            RecordLocalEffectReceipt(
                "prefab_power",
                prefabName,
                actionId: actionId,
                sourceNetworkObjectId: sourceNetworkObjectId,
                targetNetworkObjectId: targetNetworkObjectId
            );
            EnsureSceneEffectsController();
            if (m_SceneEffectsController == null)
            {
                return;
            }

            var effectColor = new Color(color.x, color.y, color.z, color.w);
            m_SceneEffectsController.ApplyPrefabPower(
                prefabName,
                position,
                forward,
                scale,
                durationSeconds,
                effectColor,
                useColorOverride,
                enableGameplayDamage,
                enableHoming,
                projectileSpeed,
                homingTurnRateDegrees,
                damageAmount,
                damageRadius,
                affectPlayerOnly,
                damageType,
                sourceNetworkObjectId: sourceNetworkObjectId,
                targetNetworkObjectId: targetNetworkObjectId,
                attachToTarget: attachToTarget,
                fitToTargetMesh: fitToTargetMesh,
                serverSpawnTimeSeconds: serverSpawnTimeSeconds,
                effectSeed: effectSeed,
                actionId: actionId
            );
        }

        private static float ResolveServerEffectTimeSeconds()
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager == null || !networkManager.IsListening)
            {
                return Time.time;
            }

            return networkManager.ServerTime.TimeAsFloat;
        }

        private static uint ResolveEffectSeed()
        {
            return (uint)UnityEngine.Random.Range(1, int.MaxValue);
        }

        private static float ResolveExplicitDurationSeconds(
            ParticleParameterExtractor.ParticleParameterIntent parameterIntent,
            float min,
            float max
        )
        {
            if (!parameterIntent.HasExplicitDurationSeconds)
            {
                return -1f;
            }

            return Mathf.Clamp(parameterIntent.ExplicitDurationSeconds, min, max);
        }

        private static string BuildEffectContextText(string prompt, string response)
        {
            if (string.IsNullOrWhiteSpace(prompt))
            {
                return response ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(response))
            {
                return prompt;
            }

            return $"{prompt}\n{response}";
        }

        private static bool IsGameplayProbeRequest(DialogueRequest request, string promptText)
        {
            if (request.ClientRequestId >= GameplayProbeClientRequestIdMin)
            {
                return true;
            }

            return !string.IsNullOrWhiteSpace(promptText)
                && promptText.IndexOf("Gameplay probe for ", StringComparison.OrdinalIgnoreCase)
                    >= 0;
        }
    }
}
