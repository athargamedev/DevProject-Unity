using UnityEngine;

namespace Network_Game.Dialogue
{
    public partial class NetworkDialogueService
    {
        // Shared geometry and anchor-context helpers for effect placement.
        private static bool TryGetObjectBounds(GameObject obj, out Bounds bounds)
        {
            bounds = default;
            if (obj == null)
            {
                return false;
            }

            bool hasBounds = false;
            Renderer[] renderers = obj.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
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

            if (!hasBounds)
            {
                Collider[] colliders = obj.GetComponentsInChildren<Collider>(true);
                for (int i = 0; i < colliders.Length; i++)
                {
                    Collider collider = colliders[i];
                    if (collider == null)
                    {
                        continue;
                    }

                    if (!hasBounds)
                    {
                        bounds = collider.bounds;
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(collider.bounds);
                    }
                }
            }

            return hasBounds;
        }

        private static void ResolveEffectSpatialContext(
            string effectContext,
            GameObject speakerObject,
            GameObject listenerObject,
            out Vector3 origin,
            out Vector3 forward,
            out string anchorLabel
        )
        {
            origin = ResolveEffectOrigin(speakerObject);
            forward = ResolveEffectForward(speakerObject, listenerObject);
            anchorLabel = "npc";

            if (TryResolveObjectAnchor(effectContext, out GameObject objectAnchor))
            {
                Vector3 objectOrigin = ResolveEffectOrigin(objectAnchor);
                if (speakerObject != null)
                {
                    origin = ResolveEffectOrigin(speakerObject);
                    Vector3 toObject = objectOrigin - origin;
                    toObject.y = 0f;
                    if (toObject.sqrMagnitude > 0.0001f)
                    {
                        forward = toObject.normalized;
                    }
                    else
                    {
                        forward = ResolveEffectForward(speakerObject, objectAnchor);
                    }
                }
                else
                {
                    origin = objectOrigin - objectAnchor.transform.forward * 0.5f;
                    forward =
                        objectAnchor.transform.forward.sqrMagnitude > 0.0001f
                            ? objectAnchor.transform.forward.normalized
                            : Vector3.forward;
                }

                anchorLabel = $"object:{objectAnchor.name}";
                return;
            }

            if (WantsPlayerAnchor(effectContext) && listenerObject != null)
            {
                origin = ResolveEffectOrigin(listenerObject);
                forward = ResolveEffectForward(speakerObject, listenerObject);
                anchorLabel = "player";
            }
        }

        private static Vector3 ResolveEffectOrigin(GameObject speakerObject)
        {
            if (speakerObject == null)
            {
                return Vector3.zero;
            }

            Vector3 origin = speakerObject.transform.position;
            Collider collider = speakerObject.GetComponentInChildren<Collider>();
            if (collider != null)
            {
                origin.y = collider.bounds.center.y;
            }

            return origin;
        }

        private static Vector3 ResolveEffectForward(GameObject speakerObject, GameObject listenerObject)
        {
            if (speakerObject == null)
            {
                return Vector3.forward;
            }

            if (listenerObject != null)
            {
                Vector3 toListener = listenerObject.transform.position - speakerObject.transform.position;
                toListener.y = 0f;
                if (toListener.sqrMagnitude > 0.0001f)
                {
                    return toListener.normalized;
                }
            }

            Vector3 forward = speakerObject.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = Vector3.forward;
            }

            return forward.normalized;
        }
    }
}
