using System;
using System.Collections.Generic;
using Network_Game.Combat;
using Unity.Netcode;
using UnityEngine;

namespace Network_Game.Dialogue.Effects
{
    /// <summary>
    /// Resolves semantic effect intents (player, floor, wall) into concrete scene/runtime targets.
    /// </summary>
    public static class EffectTargetResolverService
    {
        public sealed class PlayerTarget
        {
            public int OrderedIndex; // 1-based for human-facing P1/P2 naming.
            public CombatHealthV2 HealthComponent;
            public NetworkObject NetworkObject;
            public Transform Transform;

            public float CurrentHealth => HealthComponent != null ? HealthComponent.CurrentHealth : 0f;
            public float MaxHealth => HealthComponent != null ? HealthComponent.MaxHealth : 0f;
            public bool IsDead => HealthComponent != null && HealthComponent.IsDead;
        }

        public sealed class SurfaceTarget
        {
            public RaycastHit Hit;
            public Collider Collider;
            public Renderer Renderer;
            public EffectSurface EffectSurface;
            public int MaterialSlotIndex;
            public string Classification; // floor / wall / unknown
            public string ResolverReason;
        }

        private static readonly List<CombatHealthV2> s_PlayerBuffer = new List<CombatHealthV2>(16);

        public static void GetOrderedPlayers(List<PlayerTarget> output, bool includeDead = true)
        {
            if (output == null)
            {
                return;
            }

            output.Clear();
            s_PlayerBuffer.Clear();

            CombatHealthV2[] found = UnityEngine.Object.FindObjectsByType<CombatHealthV2>(
                FindObjectsInactive.Exclude
            );
            if (found == null || found.Length == 0)
            {
                return;
            }

            for (int i = 0; i < found.Length; i++)
            {
                CombatHealthV2 health = found[i];
                if (health == null || !health.isActiveAndEnabled)
                {
                    continue;
                }

                if (!includeDead && health.IsDead)
                {
                    continue;
                }

                NetworkObject networkObject = health.CachedNetworkObject;
                if (networkObject == null)
                {
                    networkObject = health.GetComponent<NetworkObject>();
                }

                if (networkObject == null)
                {
                    continue;
                }

                s_PlayerBuffer.Add(health);
            }

            s_PlayerBuffer.Sort(ComparePlayers);

            for (int i = 0; i < s_PlayerBuffer.Count; i++)
            {
                CombatHealthV2 health = s_PlayerBuffer[i];
                NetworkObject networkObject = health.CachedNetworkObject;
                if (networkObject == null)
                {
                    networkObject = health.GetComponent<NetworkObject>();
                }

                output.Add(new PlayerTarget
                {
                    OrderedIndex = i + 1,
                    HealthComponent = health,
                    NetworkObject = networkObject,
                    Transform = health.transform,
                });
            }
        }

        private static int ComparePlayers(CombatHealthV2 a, CombatHealthV2 b)
        {
            if (ReferenceEquals(a, b))
            {
                return 0;
            }

            if (a == null)
            {
                return 1;
            }

            if (b == null)
            {
                return -1;
            }

            NetworkObject netA = a.CachedNetworkObject != null ? a.CachedNetworkObject : a.GetComponent<NetworkObject>();
            NetworkObject netB = b.CachedNetworkObject != null ? b.CachedNetworkObject : b.GetComponent<NetworkObject>();

            ulong idA = netA != null ? netA.NetworkObjectId : ulong.MaxValue;
            ulong idB = netB != null ? netB.NetworkObjectId : ulong.MaxValue;
            return idA.CompareTo(idB);
        }

        /// <summary>
        /// Finds the closest surface target in front of the given position.
        /// </summary>
        public static bool TryResolveSurfaceTarget(
            Vector3 origin,
            Vector3 forward,
            float maxDistance,
            LayerMask layerMask,
            out SurfaceTarget target
        )
        {
            target = null;
            if (forward.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            Ray ray = new Ray(origin, forward.normalized);
            if (!Physics.Raycast(ray, out RaycastHit hit, maxDistance, layerMask, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            target = new SurfaceTarget
            {
                Hit = hit,
                Collider = hit.collider,
                Renderer = hit.collider != null ? hit.collider.GetComponent<Renderer>() : null,
                Classification = ClassifySurface(hit.normal),
                ResolverReason = "raycast_hit",
            };

            if (hit.collider != null)
            {
                target.EffectSurface = hit.collider.GetComponent<EffectSurface>();
            }

            return true;
        }

        private static string ClassifySurface(Vector3 normal)
        {
            float dot = Vector3.Dot(normal.normalized, Vector3.up);
            if (dot > 0.7f)
            {
                return "floor";
            }

            if (dot < -0.7f)
            {
                return "ceiling";
            }

            return "wall";
        }
    }
}
