
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

    [Range(0f, 1f)] public float minSwimY = 0.12f;
    [Range(0f, 1f)] public float maxSwimY = 0.85f;

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

    // Retained for compatibility with older controller versions.
    public int RequiredReels =>
        Rarity == FishRarity.Elite ? 5 :
        Rarity == FishRarity.Royal ? 3 : 2;

    public float EscapeChance =>
        Rarity == FishRarity.Elite ? 0.30f :
        Rarity == FishRarity.Royal ? 0.20f : 0.10f;

    private SpriteRenderer fishSprite;

    private Vector3 startingPosition;
    private Vector3 originalScale;
    private Vector3 hookedPosition;

    private float originalSwimSpeed;

    private int direction = 1;

    private float leftBoundary;
    private float rightBoundary;

    private float biteTimer;
    private float hookWindowTimer;
    private float ignoreBaitUntil;

    private float swimTime;
    private float targetSwimY;
    private float depthChangeTimer;
    private float directionChangeTimer;
    private float pauseTimer;

    private Camera swimCamera;
    private bool riverMode = false;

    private void Awake()
    {
        fishSprite = GetComponent<SpriteRenderer>();

        startingPosition = transform.position;
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

        biteTimer = 0f;
        hookWindowTimer = 0f;
        ignoreBaitUntil = 0f;

        startingPosition = transform.position;

        // Initialize natural swimming.
        swimCamera = Camera.main;
        targetSwimY = transform.position.y;
        depthChangeTimer = Random.Range(1f, 3f);

        // Each fish receives independent random movement.
        directionChangeTimer = Random.Range(
            Mathf.Min(minDirectionChangeTime, maxDirectionChangeTime),
            Mathf.Max(minDirectionChangeTime, maxDirectionChangeTime)
        );

        pauseTimer = 0f;
        direction = Random.value < 0.5f ? -1 : 1;
        swimTime = Random.Range(0f, 10f);

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

        swimSpeed = originalSwimSpeed *
            (rarity == FishRarity.Elite ? 1.45f :
             rarity == FishRarity.Royal ? 1.20f : 1f);

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

        startingPosition = transform.position;
        FaceDirection(direction);
    }

    private void Update()
    {
        // Hooked fish struggle instead of swimming normally.
        if (CurrentState == FishState.Hooked)
        {
            StruggleMovement();
            return;
        }

        // Fish is biting and awaiting a hook.
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

        SwimNormally();
    }

    private void ChooseNewSwimDepth()
    {
        if (swimCamera == null)
            swimCamera = Camera.main;

        if (swimCamera == null)
        {
            targetSwimY = transform.position.y;
        }
        else
        {
            float depth = Mathf.Abs(
                transform.position.z -
                swimCamera.transform.position.z
            );

            float viewportY = Random.Range(
                Mathf.Min(minSwimY, maxSwimY),
                Mathf.Max(minSwimY, maxSwimY)
            );

            Vector3 worldPoint = swimCamera.ViewportToWorldPoint(
                new Vector3(0.5f, viewportY, depth)
            );

            targetSwimY = worldPoint.y;
        }

        depthChangeTimer = Random.Range(
            Mathf.Min(minDepthChangeTime, maxDepthChangeTime),
            Mathf.Max(minDepthChangeTime, maxDepthChangeTime)
        );
    }

    private void SwimVertically()
    {
        depthChangeTimer -= Time.deltaTime;

        if (depthChangeTimer <= 0f)
            ChooseNewSwimDepth();

        Vector3 position = transform.position;

        position.y = Mathf.MoveTowards(
            position.y,
            targetSwimY,
            verticalSwimSpeed * Time.deltaTime
        );

        // Small natural swimming wobble.
        swimTime += Time.deltaTime;

        float wobble =
            Mathf.Sin(swimTime * wobbleFrequency) *
            verticalWobble * Time.deltaTime;

        position.y += wobble;

        transform.position = position;
    }

    private void UpdateRandomSwimming()
    {
        // If resting, count down the pause.
        if (pauseTimer > 0f)
        {
            pauseTimer -= Time.deltaTime;
            return;
        }

        directionChangeTimer -= Time.deltaTime;

        if (directionChangeTimer > 0f)
            return;

        directionChangeTimer = Random.Range(
            Mathf.Min(minDirectionChangeTime, maxDirectionChangeTime),
            Mathf.Max(minDirectionChangeTime, maxDirectionChangeTime)
        );

        // Randomly pause.
        if (Random.value < pauseChance)
        {
            pauseTimer = Random.Range(
                Mathf.Min(minPauseDuration, maxPauseDuration),
                Mathf.Max(minPauseDuration, maxPauseDuration)
            );

            return;
        }

        // Change swimming direction randomly.
        direction = Random.value < 0.5f ? -1 : 1;

        FaceDirection(direction);
    }

    private void SwimNormally()
    {
        CurrentState = FishState.Patrolling;

        // Decide whether to turn or pause.
        UpdateRandomSwimming();

        // During a pause, the fish stays still.
        if (pauseTimer > 0f)
            return;

        // Horizontal swimming.
        float movementX =
            direction * swimSpeed * Time.deltaTime;

        transform.position +=
            new Vector3(movementX, 0f, 0f);

        // Vertical swimming.
        SwimVertically();

        if (riverMode)
        {
            if (transform.position.x > rightBoundary + 1f ||
                transform.position.x < leftBoundary - 1f)
            {
                Destroy(gameObject);
            }
        }
        else
        {
            // Patrol inside the fish's local boundaries.
            float leftLimit =
                startingPosition.x - swimDistance;

            float rightLimit =
                startingPosition.x + swimDistance;

            Vector3 position = transform.position;

            if (position.x >= rightLimit)
            {
                position.x = rightLimit;
                direction = -1;
            }
            else if (position.x <= leftLimit)
            {
                position.x = leftLimit;
                direction = 1;
            }

            transform.position = position;
        }

        FaceDirection(direction);
    }

    private void MoveTowardBait()
    {
        if (bait == null)
            return;

        float dx = bait.position.x - transform.position.x;

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

        float offsetX = Mathf.Sin(t) * struggleHorizontal;
        float offsetY = Mathf.Cos(t * 1.4f) * struggleVertical;

        transform.position = hookedPosition +
            new Vector3(offsetX, offsetY, 0f);

        fishSprite.flipX = Mathf.Sin(t) < 0f;
    }

    private void FaceDirection(float dx)
    {
        if (Mathf.Abs(dx) > 0.01f)
            fishSprite.flipX = dx < 0f;
    }

    public void SetBait(Transform newBait)
    {
        bait = newBait;

        biteTimer = 0f;
        hookWindowTimer = 0f;
        ignoreBaitUntil = 0f;

        if (!IsHooked)
            CurrentState = FishState.Patrolling;
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

        // One escape roll per completed struggle.
        bool escaped = Random.value < EscapeChance;

        if (escaped)
        {
            ResetFish();
            Debug.Log(name + " escaped during struggle!");
        }

        return escaped;
    }

    public void ResetFish()
    {
        bait = null;

        biteTimer = 0f;
        hookWindowTimer = 0f;

        startingPosition = transform.position;
        depthChangeTimer = 0f;
        pauseTimer = 0f;

        CurrentState = FishState.Patrolling;
    }
}
