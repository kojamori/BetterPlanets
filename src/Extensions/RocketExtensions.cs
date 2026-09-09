using UnityEngine;
using SFS.World;
using SFS.Parts;

namespace BetterPlanets.Extensions;

public static class RocketExtensions
{

    extension(Rocket rocket)
    {
        public Bounds GetGlobalBoundingBox()
        {
            if (rocket == null || rocket.partHolder == null || rocket.partHolder.parts.Count == 0)
            {
                return new Bounds(Vector3.zero, Vector3.zero);
            }

            Bounds totalBounds = new Bounds();
            bool initialized = false;

            foreach (Part part in rocket.partHolder.parts)
            {
                // Skip inactive parts to match what the player actually sees/interacts with
                if (!part.gameObject.activeInHierarchy) continue;

                // Try to get bounds from renderers first (most accurate for visual size)
                Renderer[] renderers = part.GetComponentsInChildren<Renderer>();
                foreach (Renderer r in renderers)
                {
                    if (r.enabled && !r.isPartOfStaticBatch) // isPartOfStaticBatch check avoids some editor-only artifacts
                    {
                        if (!initialized)
                        {
                            totalBounds = r.bounds;
                            initialized = true;
                        }
                        else
                        {
                            totalBounds.Encapsulate(r.bounds);
                        }
                    }
                }

                // Fallback: if no renderers (e.g. invisible logic parts), use colliders
                if (!initialized)
                {
                    Collider2D[] colliders = part.GetComponentsInChildren<Collider2D>();
                    foreach (Collider2D c in colliders)
                    {
                        if (c.enabled && !c.isTrigger)
                        {
                            // Convert 2D bounds to 3D bounds for encapsulation
                            Bounds b = c.bounds;
                            if (!initialized)
                            {
                                totalBounds = b;
                                initialized = true;
                            }
                            else
                            {
                                totalBounds.Encapsulate(b);
                            }
                        }
                    }
                }
            }

            if (!initialized)
            {
                // Fallback to root position if nothing has geometry
                return new Bounds(rocket.transform.position, Vector3.one);
            }

            return totalBounds;
        }
    }
}