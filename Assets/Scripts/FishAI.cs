using UnityEngine;

/// <summary>
/// Controls a 2D fish while it patrols, investigates bait, and waits to be hooked.
/// Attach to the fish GameObject and assign Bait in the Inspector, or use SetBait().
/// </summary>
public class FishAI : MonoBehaviour
{
    public enum FishState { Patrolling, Approaching, Biting, Hooked }

    [Header("Swimming")]
    public float swimSpeed = 2f;
    public float swimDistance = 3f;

    [Header("Bait")]
    public Transform bait;
    public float detectionRange = 7f;
    public float biteDistance = 0.4f;
    public float biteDelay = 0.6f;

    public FishState CurrentState { get; private set; } = FishState.Patrolling;
    public bool IsBiting => CurrentState == FishState.Biting;
    public bool IsHooked => CurrentState == FishState.Hooked;

    private Vector3 startingPosition;
    private int direction = 1;
    private float biteTimer;
    private SpriteRenderer fishSprite;

    private void Start()
    {
        startingPosition = transform.position;
        fishSprite = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        if (CurrentState == FishState.Hooked)
            return; // Fishing/reeling controller takes over once hooked.

        if (bait == null)
        {
            Patrol();
            return;
        }

        float distance = Vector2.Distance(transform.position, bait.position);

        if (distance > detectionRange && CurrentState != FishState.Biting)
        {
            Patrol();
            return;
        }

        if (distance > biteDistance)
        {
            biteTimer = 0f;
            CurrentState = FishState.Approaching;
            MoveTowardBait();
        }
        else
        {
            CurrentState = FishState.Biting;
            biteTimer += Time.deltaTime;

            // Fish has reached bait and is ready for the player to hook it.
            if (biteTimer >= biteDelay)
                OnBiteReady();
        }
    }

    private void Patrol()
    {
        CurrentState = FishState.Patrolling;
        biteTimer = 0f;

        transform.position += Vector3.right * (direction * swimSpeed * Time.deltaTime);
        if (transform.position.x >= startingPosition.x + swimDistance)
            direction = -1;
        else if (transform.position.x <= startingPosition.x - swimDistance)
            direction = 1;

        FaceDirection(direction);
    }

    private void MoveTowardBait()
    {
        float xDirection = Mathf.Sign(bait.position.x - transform.position.x);
        if (Mathf.Abs(bait.position.x - transform.position.x) > 0.01f)
            FaceDirection(xDirection);

        // Preserve the fish's Z position for 2D rendering.
        Vector3 target = new Vector3(bait.position.x, bait.position.y, transform.position.z);
        transform.position = Vector3.MoveTowards(
            transform.position, target, swimSpeed * Time.deltaTime);
    }

    private void FaceDirection(float xDirection)
    {
        if (fishSprite != null && Mathf.Abs(xDirection) > 0.01f)
            fishSprite.flipX = xDirection < 0f;
    }

    private void OnBiteReady()
    {
        // The player's on-screen hook button can check IsBiting and call HookFish().
        // Does not automatically hook the fish.
    }

    /// <summary>Called when a new bait is cast into the water.</summary>
    public void SetBait(Transform newBait)
    {
        bait = newBait;
        biteTimer = 0f;
        if (CurrentState != FishState.Hooked)
            CurrentState = FishState.Patrolling;
    }

    /// <summary>Call from the mobile fishing controller when the player hooks the fish.</summary>
    public bool HookFish()
    {
        if (CurrentState != FishState.Biting || biteTimer < biteDelay)
            return false;

        CurrentState = FishState.Hooked;
        return true;
    }

    /// <summary>Call after a catch or escape to return the fish to swimming.</summary>
    public void ResetFish()
    {
        bait = null;
        biteTimer = 0f;
        startingPosition = transform.position;
        CurrentState = FishState.Patrolling;
    }
}
