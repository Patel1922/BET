
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class FishAI : MonoBehaviour
{
    public enum FishState
    {
        Patrolling,
        Approaching,
        Biting,
        Hooked
    }

    public enum FishRarity
    {
        Basic,
        Royal,
        Elite
    }

    [Header("Fish Artwork")]
    public Sprite basicSprite;
    public Sprite royalSprite;
    public Sprite eliteSprite;

    [Header("Water Boundaries")]
    public SpriteRenderer waterArea;
    public float waterPadding = 0.15f;

    [Header("Leaving the Water")]
    public float exitZoneWidth = 0.6f;
    public float fadeDistance = 0.75f;

    [Header("Movement")]
    public float swimSpeed = 2f;
    public float swimDistance = 3f;

    [Header("Bait Detection")]
    public Transform bait;
    public float detectionRange = 3f;
    public float biteDistance = 0.4f;
    public float biteDelay = 0.6f;
    public float biteWindow = 5f;

    [Header("Natural Swimming")]
    public float verticalWobble = 0.15f;
    public float wobbleFrequency = 2f;

    [Header("Two-Dimensional Swimming")]
    public float verticalSwimSpeed = 0.65f;
    public float minDepthChangeTime = 2f;
    public float maxDepthChangeTime = 4f;

    [Range(0f, 1f)]
    public float minSwimY = 0.12f;

    [Range(0f, 1f)]
    public float maxSwimY = 0.85f;

    [Header("Random Swimming Behavior")]
    public float minDirectionChangeTime = 2f;
    public float maxDirectionChangeTime = 5f;

    [Range(0f, 1f)]
    public float pauseChance = 0.25f;

    public float minPauseDuration = 0.5f;
    public float maxPauseDuration = 1.5f;

    [Header("Struggle Movement")]
    public float struggleHorizontal = 0.30f;
    public float struggleVertical = 0.18f;
    public float struggleSpeed = 12f;

    public FishState CurrentState { get; private set; }
        = FishState.Patrolling;

    public FishRarity Rarity { get; private set; }
        = FishRarity.Basic;

    public bool IsBiting =>
        CurrentState == FishState.Biting &&
        biteTimer >= biteDelay;

    public bool IsHooked =>
        CurrentState == FishState.Hooked;

    public int CoinValue =>
        Rarity == FishRarity.Elite ? 250 :
        Rarity == FishRarity.Royal ? 150 : 100;

    public int RequiredReels =>
        Rarity == FishRarity.Elite ? 5 :
        Rarity == FishRarity.Royal ? 3 : 2;

    public float EscapeChance =>
        Rarity == FishRarity.Elite ? 0.30f :
        Rarity == FishRarity.Royal ? 0.20f : 0.10f;

    private SpriteRenderer fishSprite;
    private Camera swimCamera;

    private Vector3 originalScale;
    private Vector3 hookedPosition;

    private float originalSwimSpeed;
    private float originalAlpha = 1f;

    private int direction = 1;
    private bool exitingWater = false;

    private float leftBoundary;
    private float rightBoundary;
    private bool riverMode = false;

    private float biteTimer;
    private float hookWindowTimer;
    private float ignoreBaitUntil;

    private float swimTime;
    private float targetSwimY;
    private float depthChangeTimer;
    private float directionChangeTimer;
    private float pauseTimer;

    private void Awake()
    {
        fishSprite = GetComponent<SpriteRenderer>();

        originalScale = transform.localScale;
        originalSwimSpeed = swimSpeed;
    }

    public void Configure(
        FishRarity rarity,
        Transform currentBait)
    {
        Rarity = rarity;
        bait = currentBait;

        CurrentState = FishState.Patrolling;

        exitingWater = false;
        riverMode = false;

        biteTimer = 0f;
        hookWindowTimer = 0f;
        ignoreBaitUntil = 0f;

        swimCamera = Camera.main;

        direction = Random.value < 0.5f ? -1 : 1;
        swimTime = Random.Range(0f, 10f);

        pauseTimer = 0f;

        directionChangeTimer = Random.Range(
            Mathf.Min(
                minDirectionChangeTime,
                maxDirectionChangeTime),
            Mathf.Max(
                minDirectionChangeTime,
                maxDirectionChangeTime)
        );

        float sizeMultiplier =
            rarity == FishRarity.Elite ? 1.35f :
            rarity == FishRarity.Royal ? 1.15f : 1f;

        transform.localScale = originalScale * sizeMultiplier;

        Sprite selectedSprite =
            rarity == FishRarity.Elite ? eliteSprite :
            rarity == FishRarity.Royal ? royalSprite :
            basicSprite;

        if (selectedSprite != null)
        {
            fishSprite.sprite = selectedSprite;
            fishSprite.color = Color.white;
        }
        else
        {
            fishSprite.color =
                rarity == FishRarity.Elite
                    ? new Color(1f, 0.78f, 0.25f)
                    : rarity == FishRarity.Royal
                        ? new Color(0.4f, 0.85f, 1f)
                        : new Color(0.75f, 1f, 0.65f);
        }

        originalAlpha = fishSprite.color.a;

        swimSpeed = originalSwimSpeed *
            (rarity == FishRarity.Elite ? 1.45f :
             rarity == FishRarity.Royal ? 1.20f : 1f);

        FindWater();

        // Keep newly spawned fish inside the water.
        ClampInsideWater(true);

        targetSwimY = transform.position.y;
        depthChangeTimer = 0f;

        FaceDirection(direction);
    }

    public void SetRiverMovement(
        int newDirection,
        float leftEdge,
        float rightEdge)
    {
        riverMode = true;

        direction = newDirection >= 0 ? 1 : -1;

        leftBoundary = leftEdge;
        rightBoundary = rightEdge;

        FaceDirection(direction);
    }

    private void FindWater()
    {
        if (waterArea != null)
            return;

        GameObject water = GameObject.Find("Water");

        if (water != null)
        {
            waterArea = water.GetComponent<SpriteRenderer>();
        }
    }

    private void Update()
    {
        // Hooked fish continue fighting normally.
        if (CurrentState == FishState.Hooked)
        {
            StruggleMovement();
            return;
        }

        // Fish waiting to be hooked.
        if (CurrentState == FishState.Biting)
        {
            biteTimer += Time.deltaTime;
            hookWindowTimer += Time.deltaTime;

            if (hookWindowTimer >= biteWindow)
            {
                ResetFish();
                ignoreBaitUntil = Time.time + 3f;
            }

            return;
        }

        // Fish already leaving cannot chase bait.
        if (!exitingWater)
        {
            bool baitAvailable =
                bait != null &&
                bait.gameObject.activeInHierarchy &&
                Time.time >= ignoreBaitUntil;

            if (baitAvailable)
            {
                float distance = Vector2.Distance(
                    transform.position,
                    bait.position
                );

                if (distance <= detectionRange)
                {
                    if (distance > biteDistance)
                    {
                        CurrentState = FishState.Approaching;
                        MoveTowardBait();
                    }
                    else
                    {
                        CurrentState = FishState.Biting;
                        biteTimer = 0f;
                        hookWindowTimer = 0f;
                    }

                    return;
                }
            }
        }

        SwimNormally();
    }

    private void SwimNormally()
    {
        CurrentState = FishState.Patrolling;

        FindWater();

        // Check whether fish reached an exit.
        if (!exitingWater && waterArea != null)
        {
            Bounds water = waterArea.bounds;

            float leftExit =
                water.min.x + Mathf.Max(0f, exitZoneWidth);

            float rightExit =
                water.max.x - Mathf.Max(0f, exitZoneWidth);

            // Only begin exiting when already swimming
            // toward the nearest boundary.
            if (direction < 0 &&
                transform.position.x <= leftExit)
            {
                exitingWater = true;
                pauseTimer = 0f;
            }
            else if (direction > 0 &&
                     transform.position.x >= rightExit)
            {
                exitingWater = true;
                pauseTimer = 0f;
            }
        }

        // Normal fish can pause or change direction.
        // Exiting fish cannot.
        if (!exitingWater)
        {
            UpdateRandomSwimming();

            if (pauseTimer > 0f)
                return;
        }

        // Swim horizontally.
        Vector3 position = transform.position;

        position.x +=
            direction * swimSpeed * Time.deltaTime;

        transform.position = position;

        // Continue natural vertical movement.
        SwimVertically();

        FaceDirection(direction);

        // Remove fish once completely beyond water.
        if (waterArea != null && fishSprite != null)
        {
            Bounds water = waterArea.bounds;
            Bounds fish = fishSprite.bounds;

            if (fish.max.x < water.min.x ||
                fish.min.x > water.max.x)
            {
                Destroy(gameObject);
            }
        }
        else if (riverMode)
        {
            if (position.x < leftBoundary - 1f ||
                position.x > rightBoundary + 1f)
            {
                Destroy(gameObject);
            }
        }
    }

    private void UpdateRandomSwimming()
    {
        if (pauseTimer > 0f)
        {
            pauseTimer -= Time.deltaTime;
            return;
        }

        directionChangeTimer -= Time.deltaTime;

        if (directionChangeTimer > 0f)
            return;

        directionChangeTimer = Random.Range(
            Mathf.Min(
                minDirectionChangeTime,
                maxDirectionChangeTime),
            Mathf.Max(
                minDirectionChangeTime,
                maxDirectionChangeTime)
        );

        if (Random.value < pauseChance)
        {
            pauseTimer = Random.Range(
                Mathf.Min(
                    minPauseDuration,
                    maxPauseDuration),
                Mathf.Max(
                    minPauseDuration,
                    maxPauseDuration)
            );

            return;
        }

        // Pick a random horizontal direction.
        direction = Random.value < 0.5f ? -1 : 1;

        FaceDirection(direction);
    }

    private void ChooseNewSwimDepth()
    {
        FindWater();

        if (waterArea != null)
        {
            Bounds water = waterArea.bounds;
            Bounds fish = fishSprite.bounds;

            float minY =
                water.min.y +
                fish.extents.y +
                waterPadding;

            float maxY =
                water.max.y -
                fish.extents.y -
                waterPadding;

            targetSwimY = minY <= maxY
                ? Random.Range(minY, maxY)
                : water.center.y;
        }
        else
        {
            if (swimCamera == null)
                swimCamera = Camera.main;

            if (swimCamera != null)
            {
                float depth = Mathf.Abs(
                    transform.position.z -
                    swimCamera.transform.position.z
                );

                float viewportY = Random.Range(
                    Mathf.Min(minSwimY, maxSwimY),
                    Mathf.Max(minSwimY, maxSwimY)
                );

                Vector3 worldPoint =
                    swimCamera.ViewportToWorldPoint(
                        new Vector3(
                            0.5f,
                            viewportY,
                            depth
                        )
                    );

                targetSwimY = worldPoint.y;
            }
            else
            {
                targetSwimY = transform.position.y;
            }
        }

        depthChangeTimer = Random.Range(
            Mathf.Min(
                minDepthChangeTime,
                maxDepthChangeTime),
            Mathf.Max(
                minDepthChangeTime,
                maxDepthChangeTime)
        );
    }

    private void SwimVertically()
    {
        depthChangeTimer -= Time.deltaTime;

        if (depthChangeTimer <= 0f)
        {
            ChooseNewSwimDepth();
        }

        Vector3 position = transform.position;

        position.y = Mathf.MoveTowards(
            position.y,
            targetSwimY,
            verticalSwimSpeed * Time.deltaTime
        );

        swimTime += Time.deltaTime;

        position.y +=
            Mathf.Sin(swimTime * wobbleFrequency) *
            verticalWobble *
            Time.deltaTime;

        transform.position = position;
    }

    private void LateUpdate()
    {
        FindWater();

        // Keep the fish underwater vertically.
        ClampInsideWater(false);

        // Fade fish near left and right edges.
        FadeAtEdges();
    }

    private void ClampInsideWater(bool horizontal)
    {
        if (waterArea == null ||
            fishSprite == null ||
            fishSprite.sprite == null)
        {
            return;
        }

        Bounds water = waterArea.bounds;
        Bounds fish = fishSprite.bounds;

        Vector3 position = transform.position;

        // Account for off-center sprite pivots.
        Vector3 offset = fish.center - position;

        float minY =
            water.min.y +
            fish.extents.y +
            waterPadding -
            offset.y;

        float maxY =
            water.max.y -
            fish.extents.y -
            waterPadding -
            offset.y;

        if (minY <= maxY)
        {
            position.y = Mathf.Clamp(
                position.y,
                minY,
                maxY
            );
        }

        // Horizontal clamping ONLY on initial spawn.
        // Never clamp a fish while it is leaving.
        if (horizontal)
        {
            float minX =
                water.min.x +
                fish.extents.x +
                waterPadding -
                offset.x;

            float maxX =
                water.max.x -
                fish.extents.x -
                waterPadding -
                offset.x;

            if (minX <= maxX)
            {
                position.x = Mathf.Clamp(
                    position.x,
                    minX,
                    maxX
                );
            }
        }

        transform.position = position;
    }

    private void FadeAtEdges()
    {
        if (fishSprite == null ||
            waterArea == null ||
            fishSprite.sprite == null)
        {
            return;
        }

        Color color = fishSprite.color;

        if (!exitingWater)
        {
            color.a = originalAlpha;
            fishSprite.color = color;
            return;
        }

        Bounds water = waterArea.bounds;
        Bounds fish = fishSprite.bounds;

        // Start fading while still inside the water.
        float distanceInside;

        if (direction < 0)
        {
            distanceInside =
                fish.center.x - water.min.x;
        }
        else
        {
            distanceInside =
                water.max.x - fish.center.x;
        }

        float distance = Mathf.Max(
            0.01f,
            fadeDistance
        );

        float alpha = Mathf.Clamp01(
            distanceInside / distance
        );

        color.a = originalAlpha * alpha;

        fishSprite.color = color;
    }

    private void MoveTowardBait()
    {
        if (bait == null)
            return;

        float dx =
            bait.position.x - transform.position.x;

        FaceDirection(dx);

        Vector3 target = new Vector3(
            bait.position.x,
            bait.position.y,
            transform.position.z
        );

        transform.position = Vector3.MoveTowards(
            transform.position,
            target,
            swimSpeed * Time.deltaTime
        );
    }

    private void StruggleMovement()
    {
        float t = Time.time * struggleSpeed;

        float offsetX =
            Mathf.Sin(t) * struggleHorizontal;

        float offsetY =
            Mathf.Cos(t * 1.4f) * struggleVertical;

        transform.position = hookedPosition +
            new Vector3(offsetX, offsetY, 0f);

        fishSprite.flipX = Mathf.Sin(t) < 0f;
    }

    private void FaceDirection(float dx)
    {
        if (Mathf.Abs(dx) > 0.01f)
        {
            fishSprite.flipX = dx < 0f;
        }
    }

    public void SetBait(Transform newBait)
    {
        // Fish that are leaving ignore new bait.
        if (exitingWater)
            return;

        bait = newBait;

        biteTimer = 0f;
        hookWindowTimer = 0f;
        ignoreBaitUntil = 0f;

        if (!IsHooked)
        {
            CurrentState = FishState.Patrolling;
        }
    }

    public bool HookFish()
    {
        if (!IsBiting)
            return false;

        CurrentState = FishState.Hooked;
        hookedPosition = transform.position;

        return true;
    }

    public bool TryEscapeDuringStruggle()
    {
        if (!IsHooked)
            return false;

        bool escaped =
            Random.value < EscapeChance;

        if (escaped)
        {
            ResetFish();

            Debug.Log(
                name + " escaped during struggle!"
            );
        }

        return escaped;
    }

    public void ResetFish()
    {
        bait = null;

        biteTimer = 0f;
        hookWindowTimer = 0f;

        exitingWater = false;

        Color color = fishSprite.color;
        color.a = originalAlpha;
        fishSprite.color = color;

        ClampInsideWater(false);

        depthChangeTimer = 0f;
        pauseTimer = 0f;

        CurrentState = FishState.Patrolling;
    }
}
