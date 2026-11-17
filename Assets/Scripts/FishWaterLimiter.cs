
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class FishWaterLimiter : MonoBehaviour
{
    [Header("Water Boundary")]
    public float padding = 0.15f;

    private SpriteRenderer waterRenderer;
    private SpriteRenderer fishRenderer;

    private void Start()
    {
        fishRenderer = GetComponent<SpriteRenderer>();

        GameObject water = GameObject.Find("Water");

        if (water != null)
        {
            waterRenderer = water.GetComponent<SpriteRenderer>();
        }

        if (waterRenderer == null)
        {
            Debug.LogWarning(
                "FishWaterLimiter: Cannot find Water SpriteRenderer."
            );
        }
    }

    private void LateUpdate()
    {
        if (waterRenderer == null || fishRenderer == null)
            return;

        Bounds water = waterRenderer.bounds;
        Bounds fish = fishRenderer.bounds;

        Vector3 position = transform.position;

        // Account for sprites whose pivots aren't centered.
        Vector3 centerOffset = fish.center - position;

        float minX = water.min.x + fish.extents.x
                     + padding - centerOffset.x;

        float maxX = water.max.x - fish.extents.x
                     - padding - centerOffset.x;

        float minY = water.min.y + fish.extents.y
                     + padding - centerOffset.y;

        float maxY = water.max.y - fish.extents.y
                     - padding - centerOffset.y;

        if (minX > maxX || minY > maxY)
            return;

        position.x = Mathf.Clamp(position.x, minX, maxX);
        position.y = Mathf.Clamp(position.y, minY, maxY);

        transform.position = position;
    }
}
