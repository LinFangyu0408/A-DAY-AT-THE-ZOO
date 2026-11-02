using UnityEngine;

// Keeps the existing players and rig, but places both at the same point on
// Island_1 after the scene loads. The collider is sampled so this also works
// when the island prefab is moved or scaled in the editor.
public static class IslandSurfaceSpawn
{
    private const float FloorClearance = 0.24f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void SnapPlayersToIsland()
    {
        GameObject island = FindLoadedObject("Island_1");
        Collider surface = island != null ? island.GetComponent<Collider>() : null;
        if (surface == null)
        {
            Debug.LogWarning("Island_1 was not found; keeping the existing player spawn.");
            return;
        }

        foreach (Locomotion locomotion in Object.FindObjectsByType<Locomotion>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            PlaceAboveIsland(locomotion.transform, surface);
        }

        foreach (DesktopPlayerController player in
                 Object.FindObjectsByType<DesktopPlayerController>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            PlaceAboveIsland(player.transform, surface);
        }
    }

    private static void PlaceAboveIsland(Transform target, Collider surface)
    {
        if (target == null)
        {
            return;
        }

        Vector3 position = target.position;
        float boundsTop = surface.bounds.max.y;
        if (TryFindSurfaceAt(surface, position.x, position.z, out RaycastHit hit))
        {
            // Preserve the authored spawn position and view direction. Only
            // lift the floor origin; never place it inside the island mesh.
            position.y = Mathf.Max(
                position.y,
                boundsTop + FloorClearance,
                hit.point.y + FloorClearance);
        }
        else
        {
            // A downward ray can miss a concave or open mesh. The island
            // bounds still provide a hard guarantee that the camera starts
            // above every part of the island.
            position.y = Mathf.Max(
                position.y,
                boundsTop + FloorClearance);
        }

        target.position = position;
        Physics.SyncTransforms();
    }

    private static bool TryFindSurfaceAt(
        Collider surface,
        float x,
        float z,
        out RaycastHit result)
    {
        result = default;
        Bounds bounds = surface.bounds;
        float sampleRadius = 0.75f;
        Vector2[] offsets =
        {
            Vector2.zero,
            Vector2.right * sampleRadius,
            Vector2.left * sampleRadius,
            Vector2.up * sampleRadius,
            Vector2.down * sampleRadius,
        };

        bool found = false;
        float bestNormal = -1f;
        foreach (Vector2 offset in offsets)
        {
            Vector3 origin = new Vector3(
                x + offset.x,
                bounds.max.y + 2f,
                z + offset.y);
            if (!surface.Raycast(
                    new Ray(origin, Vector3.down),
                    out RaycastHit hit,
                    bounds.size.y + 4f) ||
                hit.normal.y < 0.55f ||
                hit.normal.y <= bestNormal)
            {
                continue;
            }

            found = true;
            bestNormal = hit.normal.y;
            result = hit;
        }

        return found;
    }

    private static GameObject FindLoadedObject(string objectName)
    {
        foreach (GameObject candidate in Object.FindObjectsByType<GameObject>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (candidate.name == objectName &&
                candidate.scene.IsValid() &&
                candidate.scene.isLoaded)
            {
                return candidate;
            }
        }

        return null;
    }
}
