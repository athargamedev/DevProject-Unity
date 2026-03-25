using System;
using System.Text;
using Network_Game.Dialogue.Effects;
using UnityEngine;

namespace Network_Game.Dialogue
{
    public partial class NetworkDialogueService
    {
        private static bool WantsPlayerAnchor(string effectContext)
        {
            if (string.IsNullOrWhiteSpace(effectContext))
            {
                return false;
            }

            string lower = effectContext.ToLowerInvariant();
            return lower.Contains("at player", StringComparison.Ordinal)
                || lower.Contains("on player", StringComparison.Ordinal)
                || lower.Contains("around player", StringComparison.Ordinal)
                || lower.Contains("player position", StringComparison.Ordinal)
                || lower.Contains("at me", StringComparison.Ordinal)
                || lower.Contains("on me", StringComparison.Ordinal)
                || lower.Contains("around me", StringComparison.Ordinal)
                || lower.Contains("my position", StringComparison.Ordinal);
        }

        private static bool TryResolveObjectAnchor(string effectContext, out GameObject targetObject)
        {
            targetObject = null;
            if (!TryExtractObjectAnchorName(effectContext, out string objectName))
            {
                return false;
            }

            targetObject = FindSceneObjectByName(objectName);
            return targetObject != null;
        }

        private static bool TryExtractObjectAnchorName(
            string effectContext,
            out string objectAnchorName
        )
        {
            objectAnchorName = string.Empty;
            if (string.IsNullOrWhiteSpace(effectContext))
            {
                return false;
            }

            string lower = effectContext.ToLowerInvariant();
            string[] markers =
            {
                "at object ",
                "on object ",
                "near object ",
                "around object ",
                "at the object ",
                "on the object ",
                "near the object ",
                "around the object ",
                "at wall ",
                "on wall ",
                "near wall ",
                "around wall ",
                "at the wall ",
                "on the wall ",
                "near the wall ",
                "around the wall ",
            };

            for (int i = 0; i < markers.Length; i++)
            {
                string marker = markers[i];
                int markerIndex = lower.IndexOf(marker, StringComparison.Ordinal);
                if (markerIndex < 0)
                {
                    continue;
                }

                int start = markerIndex + marker.Length;
                if (start >= effectContext.Length)
                {
                    continue;
                }

                int end = effectContext.Length;
                for (int c = start; c < effectContext.Length; c++)
                {
                    char ch = effectContext[c];
                    if (
                        ch == '\n'
                        || ch == '\r'
                        || ch == ','
                        || ch == '.'
                        || ch == ';'
                        || ch == '!'
                        || ch == '?'
                    )
                    {
                        end = c;
                        break;
                    }
                }

                string candidate = effectContext.Substring(start, end - start).Trim();
                int connector = candidate.IndexOf(" for ", StringComparison.OrdinalIgnoreCase);
                if (connector >= 0)
                {
                    candidate = candidate.Substring(0, connector).Trim();
                }

                connector = candidate.IndexOf(" with ", StringComparison.OrdinalIgnoreCase);
                if (connector >= 0)
                {
                    candidate = candidate.Substring(0, connector).Trim();
                }

                connector = candidate.IndexOf(" and ", StringComparison.OrdinalIgnoreCase);
                if (connector >= 0)
                {
                    candidate = candidate.Substring(0, connector).Trim();
                }

                candidate = candidate.Trim('"', '\'');
                if (candidate.StartsWith("the ", StringComparison.OrdinalIgnoreCase))
                {
                    candidate = candidate.Substring(4).Trim();
                }

                if (!string.IsNullOrWhiteSpace(candidate) && candidate.Length >= 2)
                {
                    objectAnchorName = candidate;
                    return true;
                }
            }

            return false;
        }

        private static GameObject FindSceneObjectByName(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return null;
            }

            string trimmed = objectName.Trim();
            if (trimmed.Length < 2)
            {
                return null;
            }

            if (DialogueSceneTargetRegistry.TryResolveSceneObject(trimmed, out GameObject cachedTarget))
            {
                return cachedTarget;
            }

            if (TryFindSceneObjectBySemanticTag(trimmed, out GameObject semanticMatch))
            {
                return semanticMatch;
            }

            GameObject exact = GameObject.Find(trimmed);
            if (exact != null)
            {
                return exact;
            }

#if UNITY_2023_1_OR_NEWER
            Transform[] transforms = UnityEngine.Object.FindObjectsByType<Transform>(
                findObjectsInactive: FindObjectsInactive.Exclude
            );
#else
            Transform[] transforms = UnityEngine.Object.FindObjectsOfType<Transform>();
#endif

            GameObject partialMatch = null;
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate == null || candidate.gameObject == null)
                {
                    continue;
                }

                string candidateName = candidate.name ?? string.Empty;
                if (candidateName.Length == 0)
                {
                    continue;
                }

                if (string.Equals(candidateName, trimmed, StringComparison.OrdinalIgnoreCase))
                {
                    return candidate.gameObject;
                }

                if (
                    partialMatch == null
                    && candidateName.IndexOf(trimmed, StringComparison.OrdinalIgnoreCase) >= 0
                )
                {
                    partialMatch = candidate.gameObject;
                }
            }

            return partialMatch;
        }

        private static bool TryFindSceneObjectBySemanticTag(string query, out GameObject target)
        {
            target = null;
            if (string.IsNullOrWhiteSpace(query))
            {
                return false;
            }

            if (DialogueSceneTargetRegistry.TryResolveSceneObject(query, out target))
            {
                return target != null;
            }

#if UNITY_2023_1_OR_NEWER
            DialogueSemanticTag[] tags = UnityEngine.Object.FindObjectsByType<DialogueSemanticTag>(
                findObjectsInactive: FindObjectsInactive.Exclude
            );
#else
            DialogueSemanticTag[] tags = UnityEngine.Object.FindObjectsOfType<DialogueSemanticTag>();
#endif
            if (tags == null || tags.Length == 0)
            {
                return false;
            }

            string raw = query.Trim().Trim('"', '\'');
            if (raw.Length == 0)
            {
                return false;
            }

            string lower = raw.ToLowerInvariant();
            bool useRoleFilter = lower.StartsWith("role:", StringComparison.Ordinal);
            bool useIdFilter =
                lower.StartsWith("id:", StringComparison.Ordinal)
                || lower.StartsWith("semantic:", StringComparison.Ordinal);
            string filterValue = raw;
            if (useRoleFilter)
            {
                filterValue = raw.Substring("role:".Length).Trim();
            }
            else if (lower.StartsWith("id:", StringComparison.Ordinal))
            {
                filterValue = raw.Substring("id:".Length).Trim();
            }
            else if (lower.StartsWith("semantic:", StringComparison.Ordinal))
            {
                filterValue = raw.Substring("semantic:".Length).Trim();
            }

            if (filterValue.Length == 0)
            {
                return false;
            }

            string filterLower = filterValue.ToLowerInvariant();
            string filterNorm = NormalizeSemanticToken(filterLower);

            int bestScore = int.MinValue;
            DialogueSemanticTag bestTag = null;
            for (int i = 0; i < tags.Length; i++)
            {
                DialogueSemanticTag tag = tags[i];
                if (tag == null || tag.gameObject == null)
                {
                    continue;
                }

                int score = ScoreSemanticTagMatch(
                    tag,
                    raw,
                    lower,
                    filterLower,
                    filterNorm,
                    useRoleFilter,
                    useIdFilter
                );
                if (score > bestScore)
                {
                    bestScore = score;
                    bestTag = tag;
                }
            }

            if (bestTag == null || bestScore <= 0)
            {
                return false;
            }

            target = bestTag.gameObject;
            return true;
        }

        private static int ScoreSemanticTagMatch(
            DialogueSemanticTag tag,
            string rawQuery,
            string lowerQuery,
            string filterLower,
            string filterNorm,
            bool useRoleFilter,
            bool useIdFilter
        )
        {
            string semanticId = tag.SemanticId ?? string.Empty;
            string semanticIdLower = semanticId.ToLowerInvariant();
            string semanticIdNorm = NormalizeSemanticToken(semanticIdLower);
            string display = tag.ResolveDisplayName(tag.gameObject) ?? string.Empty;
            string displayLower = display.ToLowerInvariant();
            string displayNorm = NormalizeSemanticToken(displayLower);
            string role = tag.RoleKey ?? string.Empty;
            string roleNorm = NormalizeSemanticToken(role);

            if (useRoleFilter && role != filterLower && roleNorm != filterNorm)
            {
                return 0;
            }

            if (useIdFilter && semanticIdLower != filterLower && semanticIdNorm != filterNorm)
            {
                return 0;
            }

            int score = 0;
            if (semanticIdLower == filterLower)
            {
                score = Mathf.Max(score, 320);
            }
            else if (semanticIdNorm.Length > 0 && semanticIdNorm == filterNorm)
            {
                score = Mathf.Max(score, 300);
            }

            if (displayLower == lowerQuery || displayLower == filterLower)
            {
                score = Mathf.Max(score, 260);
            }
            else if (
                displayNorm.Length > 0
                && (displayNorm == NormalizeSemanticToken(lowerQuery) || displayNorm == filterNorm)
            )
            {
                score = Mathf.Max(score, 235);
            }
            else if (displayLower.IndexOf(filterLower, StringComparison.Ordinal) >= 0)
            {
                score = Mathf.Max(score, 150);
            }

            if (role == filterLower || roleNorm == filterNorm)
            {
                score = Mathf.Max(score, 180);
            }

            string[] aliases = tag.Aliases;
            if (aliases != null)
            {
                for (int i = 0; i < aliases.Length; i++)
                {
                    string alias = aliases[i];
                    if (string.IsNullOrWhiteSpace(alias))
                    {
                        continue;
                    }

                    string aliasLower = alias.Trim().ToLowerInvariant();
                    string aliasNorm = NormalizeSemanticToken(aliasLower);
                    if (aliasLower == lowerQuery || aliasLower == filterLower)
                    {
                        score = Mathf.Max(score, 240);
                        break;
                    }

                    if (
                        aliasNorm.Length > 0
                        && (
                            aliasNorm == NormalizeSemanticToken(lowerQuery)
                            || aliasNorm == filterNorm
                        )
                    )
                    {
                        score = Mathf.Max(score, 220);
                        break;
                    }
                }
            }

            return score;
        }

        private static string NormalizeSemanticToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(token.Length);
            for (int i = 0; i < token.Length; i++)
            {
                char c = token[i];
                if (char.IsLetterOrDigit(c))
                {
                    builder.Append(char.ToLowerInvariant(c));
                }
            }

            return builder.ToString();
        }

        private static Vector3 ResolveGroundPlacementNearReference(
            GameObject groundObject,
            Vector3 referencePosition
        )
        {
            if (groundObject == null)
            {
                return referencePosition;
            }

            if (TryGetObjectBounds(groundObject, out Bounds bounds))
            {
                return new Vector3(
                    Mathf.Clamp(referencePosition.x, bounds.min.x, bounds.max.x),
                    bounds.max.y + 0.03f,
                    Mathf.Clamp(referencePosition.z, bounds.min.z, bounds.max.z)
                );
            }

            Vector3 origin = ResolveEffectOrigin(groundObject);
            origin.y += 0.03f;
            return origin;
        }
    }
}
