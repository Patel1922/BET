using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class FishAI : MonoBehaviour
{
    public enum FishState { Patrolling, Approaching, Biting, Hooked }
    public enum FishRarity { Basic, Royal, Elite }

    [Header("Movement")]
    public float swimSpeed = 2f;
    public float swimDistance = 3f;

    [Header("Bait Detection")]
    public Transform bait;
    public float detectionRange = 7f;
    public float biteDistance = 0.4f;
    public float biteDelay = 0.6f;
    public float biteWindow = 5f;

    public FishState CurrentState { get; private set; } = FishState.Patrolling;
    public FishRarity Rarity { get; private set; } = FishRarity.Basic;
    public bool IsBiting => CurrentState == FishState.Biting && biteTimer >= biteDelay;
    public bool IsHooked => CurrentState == FishState.Hooked;
    public int CoinValue => Rarity == FishRarity.Elite ? 250 : Rarity == FishRarity.Royal ? 150 : 100;
    public int RequiredReels => Rarity == FishRarity.Elite ? 5 : Rarity == FishRarity.Royal ? 3 : 2;

    private Vector3 startingPosition;
    private int direction = 1;
    private float biteTimer;
    private float hookWindowTimer;
    private SpriteRenderer fishSprite;
    private bool initialized;

    private void Awake()
    {
        fishSprite = GetComponent<SpriteRenderer>();
        startingPosition = transform.position;
        initialized = true;
    }

    public void Configure(FishRarity rarity, Transform currentBait)
    {
        Rarity = rarity;
        bait = currentBait;
        if (!initialized) Awake();
        startingPosition = transform.position;
        CurrentState = FishState.Patrolling;
        biteTimer = hookWindowTimer = 0f;
        float scale = rarity == FishRarity.Elite ? 1.35f : rarity == FishRarity.Royal ? 1.15f : 1f;
        transform.localScale *= scale;
        fishSprite.color = rarity == FishRarity.Elite
            ? new Color(1f, 0.78f, 0.25f)
            : rarity == FishRarity.Royal ? new Color(0.4f, 0.85f, 1f)
            : new Color(0.75f, 1f, 0.65f);
        swimSpeed *= rarity == FishRarity.Elite ? 1.45f : rarity == FishRarity.Royal ? 1.2f : 1f;
    }

    private void Update()
    {
        if (CurrentState == FishState.Hooked) return;
        if (bait == null || !bait.gameObject.activeInHierarchy)
        {
            Patrol();
            return;
        }

        float distance = Vector2.Distance(transform.position, bait.position);
        if (CurrentState == FishState.Biting)
        {
            // Fish stays beside the bait for a limited time, then loses interest.
            hookWindowTimer += Time.deltaTime;
            if (hookWindowTimer > biteWindow)
            {
                ResetFish();
            }
            return;
        }

        if (distance > detectionRange)
        {
            Patrol();
        }
        else if (distance > biteDistance)
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

        if (CurrentState == FishState.Biting)
            biteTimer += Time.deltaTime;
    }

    private void Patrol()
    {
        CurrentState = FishState.Patrolling;
        biteTimer = 0f;
        transform.position += Vector3.right * (direction * swimSpeed * Time.deltaTime);
        if (transform.position.x >= startingPosition.x + swimDistance) direction = -1;
        else if (transform.position.x <= startingPosition.x - swimDistance) direction = 1;
        FaceDirection(direction);
    }

    private void MoveTowardBait()
    {
        float dx = bait.position.x - transform.position.x;
        FaceDirection(dx);
        Vector3 destination = new Vector3(bait.position.x, bait.position.y, transform.position.z);
        transform.position = Vector3.MoveTowards(transform.position, destination, swimSpeed * Time.deltaTime);
    }

    private void FaceDirection(float dx)
    {
        if (Mathf.Abs(dx) > 0.01f) fishSprite.flipX = dx < 0f;
    }

    public void SetBait(Transform newBait)
    {
        bait = newBait;
        biteTimer = hookWindowTimer = 0f;
        if (!IsHooked) CurrentState = FishState.Patrolling;
    }

    public bool HookFish()
    {
        if (!IsBiting) return false;
        CurrentState = FishState.Hooked;
        return true;
    }

    public void ResetFish()
    {
        bait = null;
        biteTimer = hookWindowTimer = 0f;
        startingPosition = transform.position;
        CurrentState = FishState.Patrolling;
    }
}
