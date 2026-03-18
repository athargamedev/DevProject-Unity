using System;
using System.Collections.Generic;
using System.Text;
using Network_Game.Auth;
using Network_Game.Combat;
using Network_Game.Dialogue;
using PlayerController = Network_Game.ThirdPersonController.ThirdPersonController;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Network_Game.Diagnostics
{
    public static class SceneProjectionBuilder
    {
        public static AuthoritativeSceneSnapshot Build(int maxObjects = 12, float maxDistance = 120f)
        {
            ResolveProbe(out Vector3 probeOrigin, out string probeName, out ulong probeNetworkObjectId);

            List<SceneObjectDescriptor> descriptors = CollectDescriptors(
                probeOrigin,
                Mathf.Max(1, maxObjects),
                Mathf.Max(5f, maxDistance)
            );

            string sceneName = SceneManager.GetActiveScene().name;
            string semanticSummary = BuildSemanticSummary(descriptors);
            string snapshotId = $"scene-{sceneName}-{Time.frameCount}";
            string snapshotHash = Hash128
                .Compute($"{sceneName}|{semanticSummary}|{descriptors.Count}|{probeNetworkObjectId}")
                .ToString();

            return new AuthoritativeSceneSnapshot
            {
                SnapshotId = snapshotId,
                SnapshotHash = snapshotHash,
                SceneName = sceneName,
                Frame = Time.frameCount,
                RealtimeSinceStartup = Time.realtimeSinceStartup,
                ProbeListenerNetworkObjectId = probeNetworkObjectId,
                ProbeListenerName = probeName ?? string.Empty,
                ProbeOrigin = probeOrigin,
                MaxDistance = Mathf.Max(5f, maxDistance),
                SemanticSummary = semanticSummary,
                Objects = descriptors.ToArray(),
            };
        }

        private static void ResolveProbe(
            out Vector3 probeOrigin,
            out string probeName,
            out ulong probeNetworkObjectId
        )
        {
            probeOrigin = Vector3.zero;
            probeName = string.Empty;
            probeNetworkObjectId = 0;

            NetworkManager manager = NetworkManager.Singleton;
            if (manager != null && manager.LocalClient != null && manager.LocalClient.PlayerObject != null)
            {
                NetworkObject localPlayer = manager.LocalClient.PlayerObject;
                probeOrigin = localPlayer.transform.position;
                probeName = localPlayer.name;
                probeNetworkObjectId = localPlayer.NetworkObjectId;
                return;
            }

            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                probeOrigin = mainCamera.transform.position;
                probeName = mainCamera.name;
                return;
            }

            probeName = "scene_origin";
        }

        private static List<SceneObjectDescriptor> CollectDescriptors(
            Vector3 probeOrigin,
            int maxObjects,
            float maxDistance
        )
        {
            var results = new List<SceneObjectDescriptor>(maxObjects);
            var seenObjects = new HashSet<GameObject>();
            var candidates = new List<Candidate>(32);

            DialogueSemanticTag[] semanticTags = UnityEngine.Object.FindObjectsByType<DialogueSemanticTag>(
                FindObjectsInactive.Exclude
            );
            for (int i = 0; i < semanticTags.Length; i++)
            {
                DialogueSemanticTag tag = semanticTags[i];
                if (tag == null || !tag.IncludeInSceneSnapshots || tag.gameObject == null)
                {
                    continue;
                }

                float distance = Vector3.Distance(probeOrigin, tag.transform.position);
                if (distance > maxDistance)
                {
                    continue;
                }

                candidates.Add(
                    new Candidate
                    {
                        Target = tag.gameObject,
                        SemanticTag = tag,
                        Distance = distance,
                        Priority = tag.Priority,
                    }
                );
            }

            candidates.Sort(CompareCandidates);
            for (int i = 0; i < candidates.Count && results.Count < maxObjects; i++)
            {
                Candidate candidate = candidates[i];
                if (candidate.Target == null || !seenObjects.Add(candidate.Target))
                {
                    continue;
                }

                results.Add(BuildDescriptor(candidate.Target, candidate.SemanticTag, candidate.Distance));
            }

            GameObject localPlayerObject = ResolveLocalPlayerObject();
            if (localPlayerObject != null && seenObjects.Add(localPlayerObject))
            {
                if (results.Count >= maxObjects && results.Count > 0)
                {
                    results.RemoveAt(results.Count - 1);
                }

                results.Insert(
                    0,
                    BuildDescriptor(
                        localPlayerObject,
                        localPlayerObject.GetComponent<DialogueSemanticTag>(),
                        Vector3.Distance(probeOrigin, localPlayerObject.transform.position)
                    )
                );
            }

            return results;
        }

        private static GameObject ResolveLocalPlayerObject()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager != null && manager.LocalClient != null && manager.LocalClient.PlayerObject != null)
            {
                return manager.LocalClient.PlayerObject.gameObject;
            }

            LocalPlayerAuthService authService = LocalPlayerAuthService.Instance;
            if (
                manager != null
                && manager.SpawnManager != null
                && authService != null
                && authService.LocalPlayerNetworkId != 0
                && manager.SpawnManager.SpawnedObjects.TryGetValue(
                    authService.LocalPlayerNetworkId,
                    out NetworkObject spawned
                )
            )
            {
                return spawned != null ? spawned.gameObject : null;
            }

            return null;
        }

        private static SceneObjectDescriptor BuildDescriptor(
            GameObject target,
            DialogueSemanticTag semanticTag,
            float distance
        )
        {
            NetworkObject networkObject = target.GetComponent<NetworkObject>();
            Renderer renderer = target.GetComponentInChildren<Renderer>(true);
            Animator animator = target.GetComponentInChildren<Animator>(true);
            CombatHealth health = target.GetComponent<CombatHealth>();
            if (health == null)
            {
                health = target.GetComponentInChildren<CombatHealth>(true);
            }

            Bounds bounds = ResolveBounds(target, renderer);
            string displayName = semanticTag != null ? semanticTag.ResolveDisplayName(target) : target.name;
            string role = ResolveRole(target, semanticTag);

            return new SceneObjectDescriptor
            {
                ObjectId = BuildObjectId(target, semanticTag, networkObject),
                SemanticId = semanticTag != null ? semanticTag.SemanticId : string.Empty,
                DisplayName = displayName,
                Role = role,
                Aliases = BuildAliases(target, semanticTag),
                IsNetworkObject = networkObject != null,
                NetworkObjectId = networkObject != null ? networkObject.NetworkObjectId : 0,
                OwnerClientId = networkObject != null ? networkObject.OwnerClientId : 0,
                IsSpawned = networkObject != null && networkObject.IsSpawned,
                Position = target.transform.position,
                EulerAngles = target.transform.eulerAngles,
                BoundsSize = bounds.size,
                DistanceFromProbe = Mathf.Max(0f, distance),
                RendererSummary = BuildRendererSummary(renderer),
                MaterialSummary = BuildMaterialSummary(renderer),
                MeshSummary = BuildMeshSummary(target, renderer),
                AnimationSummary = BuildAnimationSummary(animator),
                GameplaySummary = BuildGameplaySummary(networkObject, health),
                MutableSurfaces = BuildMutableSurfaces(target, networkObject, renderer, animator, health, role),
            };
        }

        private static string[] BuildAliases(GameObject target, DialogueSemanticTag semanticTag)
        {
            if (semanticTag == null || semanticTag.Aliases == null || semanticTag.Aliases.Length == 0)
            {
                return new[] { target.name };
            }

            var aliases = new List<string>(semanticTag.Aliases.Length + 1) { target.name };
            for (int i = 0; i < semanticTag.Aliases.Length; i++)
            {
                string alias = semanticTag.Aliases[i];
                if (string.IsNullOrWhiteSpace(alias))
                {
                    continue;
                }

                string trimmed = alias.Trim();
                if (!aliases.Contains(trimmed))
                {
                    aliases.Add(trimmed);
                }
            }

            return aliases.ToArray();
        }

        private static string ResolveRole(GameObject target, DialogueSemanticTag semanticTag)
        {
            if (semanticTag != null)
            {
                return semanticTag.RoleKey;
            }

            if (target.GetComponent<NpcDialogueActor>() != null)
            {
                return "npc";
            }

            if (target.GetComponent<PlayerController>() != null || target.CompareTag("Player"))
            {
                return "player";
            }

            return "object";
        }

        private static string BuildObjectId(
            GameObject target,
            DialogueSemanticTag semanticTag,
            NetworkObject networkObject
        )
        {
            if (semanticTag != null && !string.IsNullOrWhiteSpace(semanticTag.SemanticId))
            {
                return $"semantic:{semanticTag.SemanticId}";
            }

            if (networkObject != null)
            {
                return $"net:{networkObject.NetworkObjectId}";
            }

            return BuildHierarchyPath(target.transform);
        }

        private static string BuildHierarchyPath(Transform transform)
        {
            if (transform == null)
            {
                return string.Empty;
            }

            var names = new Stack<string>();
            Transform cursor = transform;
            while (cursor != null)
            {
                names.Push(cursor.name);
                cursor = cursor.parent;
            }

            return string.Join("/", names);
        }

        private static Bounds ResolveBounds(GameObject target, Renderer renderer)
        {
            if (renderer != null)
            {
                return renderer.bounds;
            }

            Collider collider = target.GetComponentInChildren<Collider>(true);
            if (collider != null)
            {
                return collider.bounds;
            }

            return new Bounds(target.transform.position, Vector3.zero);
        }

        private static string BuildRendererSummary(Renderer renderer)
        {
            if (renderer == null)
            {
                return "renderer:none";
            }

            return $"{renderer.GetType().Name} enabled={renderer.enabled}";
        }

        private static string BuildMaterialSummary(Renderer renderer)
        {
            if (renderer == null)
            {
                return "materials:none";
            }

            Material[] materials = renderer.sharedMaterials;
            if (materials == null || materials.Length == 0)
            {
                return "materials:none";
            }

            var names = new List<string>(materials.Length);
            for (int i = 0; i < materials.Length && i < 4; i++)
            {
                Material material = materials[i];
                if (material == null)
                {
                    continue;
                }

                names.Add(material.name);
            }

            return names.Count == 0 ? "materials:none" : string.Join(", ", names);
        }

        private static string BuildMeshSummary(GameObject target, Renderer renderer)
        {
            MeshFilter meshFilter = target.GetComponentInChildren<MeshFilter>(true);
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                return meshFilter.sharedMesh.name;
            }

            SkinnedMeshRenderer skinnedRenderer = renderer as SkinnedMeshRenderer;
            if (skinnedRenderer != null && skinnedRenderer.sharedMesh != null)
            {
                return skinnedRenderer.sharedMesh.name;
            }

            return "mesh:none";
        }

        private static string BuildAnimationSummary(Animator animator)
        {
            if (animator == null)
            {
                return "animation:none";
            }

            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            return $"animator enabled={animator.enabled} hash={stateInfo.shortNameHash}";
        }

        private static string BuildGameplaySummary(NetworkObject networkObject, CombatHealth health)
        {
            var builder = new StringBuilder(128);
            if (networkObject != null)
            {
                builder.Append("spawned=").Append(networkObject.IsSpawned);
                builder.Append(" owner=").Append(networkObject.OwnerClientId);
            }

            if (health != null)
            {
                if (builder.Length > 0)
                {
                    builder.Append(' ');
                }

                builder.Append("health=")
                    .Append(health.CurrentHealth.ToString("0.#"))
                    .Append('/')
                    .Append(health.MaxHealth.ToString("0.#"))
                    .Append(" dead=")
                    .Append(health.IsDead);
            }

            return builder.Length == 0 ? "gameplay:none" : builder.ToString();
        }

        private static MutableSurfaceDescriptor[] BuildMutableSurfaces(
            GameObject target,
            NetworkObject networkObject,
            Renderer renderer,
            Animator animator,
            CombatHealth health,
            string role
        )
        {
            var surfaces = new List<MutableSurfaceDescriptor>(4)
            {
                new MutableSurfaceDescriptor
                {
                    Kind = MutableSurfaceKind.Transform,
                    SurfaceId = $"{target.name}:transform",
                    DisplayName = "Transform",
                    AllowedProperties = new[] { "position", "rotation", "scale" },
                    AllowedOperations = new[] { "inspect", "move", "rotate" },
                    RequiredAuthority = RequiresOwnerAuthority(role) ? "owner_or_server" : "server",
                    Replicated = networkObject != null,
                },
            };

            if (renderer != null)
            {
                surfaces.Add(
                    new MutableSurfaceDescriptor
                    {
                        Kind = MutableSurfaceKind.Material,
                        SurfaceId = $"{target.name}:material",
                        DisplayName = "Material",
                        AllowedProperties = new[] { "color", "emission", "alpha" },
                        AllowedOperations = new[] { "inspect", "set" },
                        RequiredAuthority = networkObject != null ? "server" : "scene_authority",
                        Replicated = networkObject != null,
                    }
                );
            }

            if (animator != null)
            {
                surfaces.Add(
                    new MutableSurfaceDescriptor
                    {
                        Kind = MutableSurfaceKind.Animation,
                        SurfaceId = $"{target.name}:animation",
                        DisplayName = "Animation",
                        AllowedProperties = new[] { "trigger", "state" },
                        AllowedOperations = new[] { "inspect", "trigger" },
                        RequiredAuthority = networkObject != null ? "server" : "scene_authority",
                        Replicated = networkObject != null,
                    }
                );
            }

            if (health != null)
            {
                surfaces.Add(
                    new MutableSurfaceDescriptor
                    {
                        Kind = MutableSurfaceKind.GameplayStat,
                        SurfaceId = $"{target.name}:health",
                        DisplayName = "Health",
                        AllowedProperties = new[] { "current_health", "max_health", "dead" },
                        AllowedOperations = new[] { "inspect", "damage", "restore" },
                        RequiredAuthority = "server",
                        Replicated = true,
                    }
                );
            }

            return surfaces.ToArray();
        }

        private static bool RequiresOwnerAuthority(string role)
        {
            return string.Equals(role, "player", StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildSemanticSummary(List<SceneObjectDescriptor> descriptors)
        {
            if (descriptors == null || descriptors.Count == 0)
            {
                string registrySummary = DialogueSceneTargetRegistry.GetSceneContextSummary();
                return string.IsNullOrWhiteSpace(registrySummary)
                    ? "No semantic scene targets available."
                    : registrySummary;
            }

            var summaryParts = new List<string>(descriptors.Count);
            for (int i = 0; i < descriptors.Count; i++)
            {
                SceneObjectDescriptor descriptor = descriptors[i];
                summaryParts.Add(
                    $"\"{descriptor.DisplayName}\" role={descriptor.Role} dist={descriptor.DistanceFromProbe:F1}m"
                );
            }

            return string.Join("; ", summaryParts);
        }

        private static int CompareCandidates(Candidate left, Candidate right)
        {
            int priority = right.Priority.CompareTo(left.Priority);
            if (priority != 0)
            {
                return priority;
            }

            int distance = left.Distance.CompareTo(right.Distance);
            if (distance != 0)
            {
                return distance;
            }

            string leftName = left.Target != null ? left.Target.name : string.Empty;
            string rightName = right.Target != null ? right.Target.name : string.Empty;
            return string.Compare(leftName, rightName, StringComparison.OrdinalIgnoreCase);
        }

        private sealed class Candidate
        {
            public GameObject Target;
            public DialogueSemanticTag SemanticTag;
            public float Distance;
            public int Priority;
        }
    }
}
