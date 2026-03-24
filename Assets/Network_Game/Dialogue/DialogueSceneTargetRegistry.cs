using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Network_Game.Dialogue
{
    /// <summary>
    /// Caches scene targets and semantic anchors so dialogue/effect targeting does not
    /// scan the full scene on every request.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-470)]
    public sealed class DialogueSceneTargetRegistry : MonoBehaviour
    {
        private sealed class Candidate
        {
            public string Name;
            public GameObject GameObject;
        }

        private static DialogueSceneTargetRegistry s_Instance;

        private readonly Dictionary<string, GameObject> m_ExactNameIndex =
            new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, GameObject> m_SemanticIdIndex =
            new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, GameObject> m_RoleIndex =
            new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
        private readonly List<Candidate> m_PartialNameIndex = new List<Candidate>(256);
        private readonly List<DialogueSemanticTag> m_SnapshotSemanticTags =
            new List<DialogueSemanticTag>(32);

        private bool m_Dirty = true;
        private string m_ActiveScenePath = string.Empty;
        private string m_SceneContextSummary = string.Empty;

        public static DialogueSceneTargetRegistry Instance
        {
            get
            {
                if (s_Instance == null)
                {
                    s_Instance = FindAnyObjectByType<DialogueSceneTargetRegistry>();
                }

                return s_Instance;
            }
        }

        public static DialogueSceneTargetRegistry EnsureAvailable()
        {
            DialogueSceneTargetRegistry existing = Instance;
            if (existing != null)
            {
                return existing;
            }

            GameObject registryRoot = new GameObject(nameof(DialogueSceneTargetRegistry));
            return registryRoot.AddComponent<DialogueSceneTargetRegistry>();
        }

        public static void MarkDirty()
        {
            if (Instance != null)
            {
                Instance.m_Dirty = true;
            }
        }

        public static bool TryResolveSceneObject(string query, out GameObject target)
        {
            target = null;
            if (string.IsNullOrWhiteSpace(query))
            {
                return false;
            }

            DialogueSceneTargetRegistry registry = EnsureAvailable();
            return registry != null && registry.TryResolveSceneObjectInternal(query, out target);
        }

        public static int GetTargetsForRoles(
            List<GameObject> results,
            params DialogueSemanticRole[] roles
        )
        {
            if (results == null)
            {
                return 0;
            }

            results.Clear();
            if (roles == null || roles.Length == 0)
            {
                return 0;
            }

            DialogueSceneTargetRegistry registry = Instance;
            if (registry == null)
            {
                return 0;
            }

            registry.RefreshCacheIfNeeded();
            return registry.CollectTargetsForRoles(results, roles);
        }

        public static string GetSceneContextSummary()
        {
            DialogueSceneTargetRegistry registry = Instance;
            if (registry == null)
            {
                return string.Empty;
            }

            registry.RefreshCacheIfNeeded();
            return registry.m_SceneContextSummary ?? string.Empty;
        }

        private void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                Destroy(this);
                return;
            }

            s_Instance = this;
            m_Dirty = true;
        }

        private void OnEnable()
        {
            if (s_Instance == null)
            {
                s_Instance = this;
            }

            SceneManager.activeSceneChanged += HandleActiveSceneChanged;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            m_Dirty = true;
        }

        private void OnDisable()
        {
            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
            SceneManager.sceneLoaded -= HandleSceneLoaded;

            if (s_Instance == this)
            {
                s_Instance = null;
            }
        }

        private void HandleActiveSceneChanged(Scene _, Scene __)
        {
            m_Dirty = true;
        }

        private void HandleSceneLoaded(Scene _, LoadSceneMode __)
        {
            m_Dirty = true;
        }

        private bool TryResolveSceneObjectInternal(string query, out GameObject target)
        {
            RefreshCacheIfNeeded();

            target = null;
            string raw = query.Trim().Trim('"', '\'');
            if (raw.Length == 0)
            {
                return false;
            }

            string lower = raw.ToLowerInvariant();
            if (lower.StartsWith("role:", StringComparison.Ordinal))
            {
                string role = raw.Substring("role:".Length).Trim();
                return !string.IsNullOrWhiteSpace(role)
                    && m_RoleIndex.TryGetValue(role, out target)
                    && target != null;
            }

            if (
                lower.StartsWith("id:", StringComparison.Ordinal)
                || lower.StartsWith("semantic:", StringComparison.Ordinal)
            )
            {
                string semanticId = lower.StartsWith("id:", StringComparison.Ordinal)
                    ? raw.Substring("id:".Length).Trim()
                    : raw.Substring("semantic:".Length).Trim();
                return !string.IsNullOrWhiteSpace(semanticId)
                    && m_SemanticIdIndex.TryGetValue(semanticId, out target)
                    && target != null;
            }

            if (m_ExactNameIndex.TryGetValue(raw, out target) && target != null)
            {
                return true;
            }

            for (int i = 0; i < m_PartialNameIndex.Count; i++)
            {
                Candidate candidate = m_PartialNameIndex[i];
                if (
                    candidate?.GameObject != null
                    && !string.IsNullOrWhiteSpace(candidate.Name)
                    && candidate.Name.IndexOf(raw, StringComparison.OrdinalIgnoreCase) >= 0
                )
                {
                    target = candidate.GameObject;
                    return true;
                }
            }

            return false;
        }

        private int CollectTargetsForRoles(List<GameObject> results, DialogueSemanticRole[] roles)
        {
            var seen = new HashSet<GameObject>();
            for (int i = 0; i < roles.Length; i++)
            {
                string roleKey = roles[i].ToString().ToLowerInvariant();
                if (!m_RoleIndex.TryGetValue(roleKey, out GameObject target) || target == null)
                {
                    continue;
                }

                if (seen.Add(target))
                {
                    results.Add(target);
                }
            }

            return results.Count;
        }

        private void RefreshCacheIfNeeded()
        {
            string activeScenePath = SceneManager.GetActiveScene().path ?? string.Empty;
            if (!m_Dirty && string.Equals(activeScenePath, m_ActiveScenePath, StringComparison.Ordinal))
            {
                return;
            }

            m_ActiveScenePath = activeScenePath;
            RebuildCache();
            m_Dirty = false;
        }

        private void RebuildCache()
        {
            m_ExactNameIndex.Clear();
            m_SemanticIdIndex.Clear();
            m_RoleIndex.Clear();
            m_PartialNameIndex.Clear();
            m_SnapshotSemanticTags.Clear();

            DialogueSemanticTag[] semanticTags = FindSemanticTags();
            for (int i = 0; i < semanticTags.Length; i++)
            {
                DialogueSemanticTag tag = semanticTags[i];
                if (tag == null || tag.gameObject == null || !tag.gameObject.scene.IsValid())
                {
                    continue;
                }

                GameObject target = tag.gameObject;
                RegisterExact(tag.ResolveDisplayName(target), target);
                RegisterExact(target.name, target);
                RegisterRole(tag.RoleKey, target);
                RegisterSemanticId(tag.SemanticId, target);

                string[] aliases = tag.Aliases;
                for (int aliasIndex = 0; aliasIndex < aliases.Length; aliasIndex++)
                {
                    RegisterExact(aliases[aliasIndex], target);
                }

                RegisterPartial(tag.ResolveDisplayName(target), target);
                RegisterPartial(target.name, target);

                if (tag.IncludeInSceneSnapshots)
                {
                    m_SnapshotSemanticTags.Add(tag);
                }
            }

            Transform[] transforms = FindSceneTransforms();
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform transformCandidate = transforms[i];
                if (
                    transformCandidate == null
                    || transformCandidate.gameObject == null
                    || !transformCandidate.gameObject.scene.IsValid()
                )
                {
                    continue;
                }

                string candidateName = transformCandidate.name;
                if (string.IsNullOrWhiteSpace(candidateName))
                {
                    continue;
                }

                RegisterExact(candidateName, transformCandidate.gameObject);
                RegisterPartial(candidateName, transformCandidate.gameObject);
            }

            m_SceneContextSummary = BuildSceneContextSummary();
        }

        private string BuildSceneContextSummary()
        {
            var sb = new StringBuilder(512);
            AppendSemanticSceneRoles(sb);
            AppendGroundSummary(sb);
            AppendInterestingRootObjects(sb);
            return sb.ToString().TrimEnd();
        }

        private void AppendSemanticSceneRoles(StringBuilder sb)
        {
            if (m_SnapshotSemanticTags.Count == 0)
            {
                return;
            }

            m_SnapshotSemanticTags.Sort(CompareSemanticTags);

            var entries = new List<string>(12);
            for (int i = 0; i < m_SnapshotSemanticTags.Count && entries.Count < 12; i++)
            {
                DialogueSemanticTag semantic = m_SnapshotSemanticTags[i];
                if (semantic == null)
                {
                    continue;
                }

                string name = semantic.ResolveDisplayName(semantic.gameObject);
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                string role = semantic.RoleKey;
                string[] aliases = semantic.GetCompactAliases(2);
                string aliasPart =
                    aliases.Length > 0 ? $" aliases={string.Join("/", aliases)}" : string.Empty;
                string desc = semantic.Description;
                string descPart = !string.IsNullOrEmpty(desc) ? $" — {desc}" : string.Empty;
                entries.Add($"\"{name}\" role={role}{aliasPart}{descPart}");
            }

            if (entries.Count == 0)
            {
                return;
            }

            sb.Append("Semantic roles: ");
            sb.AppendLine(string.Join(", ", entries) + ".");
        }

        private void AppendGroundSummary(StringBuilder sb)
        {
            GameObject ground = ResolveGroundObject();
            if (ground == null)
            {
                return;
            }

            Vector3 pos = ground.transform.position;
            Renderer rend = ground.GetComponent<Renderer>();
            if (rend != null)
            {
                Vector3 size = rend.bounds.size;
                sb.AppendLine($"Ground: \"{ground.name}\" size={size.x:F1}x{size.z:F1}m at y={pos.y:F1}.");
                sb.AppendLine(
                    $"To cover the entire ground, use radius {Mathf.Max(size.x, size.z) * 0.5f:F0} or scale {Mathf.Max(size.x, size.z) / 10f:F1}x."
                );
                return;
            }

            Vector3 scale = ground.transform.localScale;
            float estimatedSize = Mathf.Max(scale.x, scale.z) * 10f;
            sb.AppendLine(
                $"Ground: \"{ground.name}\" estimated size ~{estimatedSize:F0}x{estimatedSize:F0}m at y={pos.y:F1}."
            );
            sb.AppendLine(
                $"To cover the entire ground, use radius {estimatedSize * 0.5f:F0} or scale {estimatedSize / 10f:F1}x."
            );
        }

        private void AppendInterestingRootObjects(StringBuilder sb)
        {
            GameObject[] rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();
            if (rootObjects == null || rootObjects.Length == 0)
            {
                return;
            }

            var interestingObjects = new List<string>(8);
            for (int i = 0; i < rootObjects.Length && interestingObjects.Count < 12; i++)
            {
                GameObject obj = rootObjects[i];
                if (obj == null || !obj.activeInHierarchy || ShouldSkipRootObject(obj.name))
                {
                    continue;
                }

                Renderer renderer = obj.GetComponentInChildren<Renderer>();
                Vector3 position = obj.transform.position;
                if (renderer != null)
                {
                    Vector3 size = renderer.bounds.size;
                    interestingObjects.Add(
                        $"\"{obj.name}\" ({size.x:F1}x{size.y:F1}x{size.z:F1}m at [{position.x:F1},{position.y:F1},{position.z:F1}])"
                    );
                }
                else
                {
                    interestingObjects.Add($"\"{obj.name}\" (at [{position.x:F1},{position.y:F1},{position.z:F1}])");
                }
            }

            if (interestingObjects.Count == 0)
            {
                return;
            }

            sb.Append("Scene objects: ");
            sb.AppendLine(string.Join(", ", interestingObjects) + ".");
        }

        private GameObject ResolveGroundObject()
        {
            if (m_RoleIndex.TryGetValue("floor", out GameObject floor) && floor != null)
            {
                return floor;
            }

            if (m_RoleIndex.TryGetValue("terrain", out GameObject terrain) && terrain != null)
            {
                return terrain;
            }

            string[] preferredNames = { "Ground", "Plane", "Floor", "Arena_Floor", "Terrain" };
            for (int i = 0; i < preferredNames.Length; i++)
            {
                if (m_ExactNameIndex.TryGetValue(preferredNames[i], out GameObject named) && named != null)
                {
                    return named;
                }
            }

            return null;
        }

        private void RegisterExact(string key, GameObject target)
        {
            if (string.IsNullOrWhiteSpace(key) || target == null || m_ExactNameIndex.ContainsKey(key))
            {
                return;
            }

            m_ExactNameIndex[key.Trim()] = target;
        }

        private void RegisterRole(string roleKey, GameObject target)
        {
            if (string.IsNullOrWhiteSpace(roleKey) || target == null || m_RoleIndex.ContainsKey(roleKey))
            {
                return;
            }

            m_RoleIndex[roleKey.Trim()] = target;
        }

        private void RegisterSemanticId(string semanticId, GameObject target)
        {
            if (
                string.IsNullOrWhiteSpace(semanticId)
                || target == null
                || m_SemanticIdIndex.ContainsKey(semanticId)
            )
            {
                return;
            }

            m_SemanticIdIndex[semanticId.Trim()] = target;
        }

        private void RegisterPartial(string key, GameObject target)
        {
            if (string.IsNullOrWhiteSpace(key) || target == null)
            {
                return;
            }

            string trimmed = key.Trim();
            for (int i = 0; i < m_PartialNameIndex.Count; i++)
            {
                Candidate candidate = m_PartialNameIndex[i];
                if (
                    candidate != null
                    && candidate.GameObject == target
                    && string.Equals(candidate.Name, trimmed, StringComparison.OrdinalIgnoreCase)
                )
                {
                    return;
                }
            }

            m_PartialNameIndex.Add(new Candidate { Name = trimmed, GameObject = target });
        }

        private static int CompareSemanticTags(DialogueSemanticTag left, DialogueSemanticTag right)
        {
            if (left == right)
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            int priority = right.Priority.CompareTo(left.Priority);
            if (priority != 0)
            {
                return priority;
            }

            string leftName = left.ResolveDisplayName(left.gameObject);
            string rightName = right.ResolveDisplayName(right.gameObject);
            return string.Compare(leftName, rightName, StringComparison.OrdinalIgnoreCase);
        }

        private static bool ShouldSkipRootObject(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return true;
            }

            return objectName.StartsWith("Directional", StringComparison.OrdinalIgnoreCase)
                || objectName.StartsWith("EventSystem", StringComparison.OrdinalIgnoreCase)
                || objectName.StartsWith("Canvas", StringComparison.OrdinalIgnoreCase)
                || objectName.StartsWith("Dialogue", StringComparison.OrdinalIgnoreCase)
                || objectName.StartsWith("Network", StringComparison.OrdinalIgnoreCase)
                || objectName.StartsWith("---", StringComparison.Ordinal)
                || objectName.StartsWith("_", StringComparison.Ordinal);
        }

        private static DialogueSemanticTag[] FindSemanticTags()
        {
#if UNITY_2023_1_OR_NEWER
            return FindObjectsByType<DialogueSemanticTag>(FindObjectsInactive.Exclude);
#else
            return FindObjectsOfType<DialogueSemanticTag>();
#endif
        }

        private static Transform[] FindSceneTransforms()
        {
#if UNITY_2023_1_OR_NEWER
            return FindObjectsByType<Transform>(FindObjectsInactive.Exclude);
#else
            return FindObjectsOfType<Transform>();
#endif
        }
    }
}
