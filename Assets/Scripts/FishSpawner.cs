
using System.Collections.Generic;
using UnityEngine;

public class FishSpawner : MonoBehaviour
{
    [Header("References")]
    public FishAI fishTemplate;
    public Camera gameCamera;
    public FishingController fishingController;

    [Header("Spawn Frequency")]
    [Min(1)] public int maxFish = 8;
    [Min(0)] public int initialFish = 4;
    [Min(0.2f)] public float spawnEverySeconds = 2.5f;

    [Header("Camera Viewport Spawn Area (0 - 1)")]
    [Range(0f, 1f)] public float minX = 0.08f;
    [Range(0f, 1f)] public float maxX = 0.92f;
    [Range(0f, 1f)] public float minY = 0.20f;
    [Range(0f, 1f)] public float maxY = 0.55f;

    [Header("Rarity Weights")]
    [Min(0)] public int basicWeight = 60;
    [Min(0)] public int royalWeight = 30;
    [Min(0)] public int eliteWeight = 10;

    [Header("Bait Attraction")]
    [Range(0f, 1f)]
    public float attractionChance = 0.75f;

    private readonly List<FishAI> spawned = new List<FishAI>();

    // Fish that already received an attraction decision this cast.
    private readonly HashSet<FishAI> evaluatedFish =
        new HashSet<FishAI>();

    // Only this fish is allowed to approach the bait.
    private FishAI baitOwner;
    private Transform activeBait;

    private float spawnTimer;

    public int ActiveFishCount => spawned.Count;
    public IReadOnlyList<FishAI> Fish => spawned;

    private void Start()
    {
        if (gameCamera == null)
            gameCamera = Camera.main;

        if (fishTemplate == null || gameCamera == null)
        {
            Debug.LogError(
                "FishSpawner: assign Fish Template and Camera."
            );
            enabled = false;
            return;
        }

        // Hide the original fish template.
        fishTemplate.gameObject.SetActive(false);

        // Spawn initial fish.
        for (int i = 0; i < Mathf.Min(initialFish, maxFish); i++)
        {
            SpawnFish();
        }
    }

    private void Update()
    {
        // Remove destroyed fish from our list.
        spawned.RemoveAll(fish => fish == null);
        evaluatedFish.RemoveWhere(fish => fish == null);

        // Keep spawning until maximum population.
        spawnTimer += Time.deltaTime;

        if (spawnTimer >= spawnEverySeconds)
        {
            spawnTimer = 0f;

            if (spawned.Count < maxFish)
                SpawnFish();
        }

        // Check the bait every frame.
        CheckBaitAttraction();
    }

    private void SpawnFish()
    {
        float x = Random.Range(
            Mathf.Min(minX, maxX),
            Mathf.Max(minX, maxX)
        );

        float y = Random.Range(
            Mathf.Min(minY, maxY),
            Mathf.Max(minY, maxY)
        );

        float depth = Mathf.Abs(
            fishTemplate.transform.position.z -
            gameCamera.transform.position.z
        );

        Vector3 position = gameCamera.ViewportToWorldPoint(
            new Vector3(x, y, depth)
        );

        position.z = fishTemplate.transform.position.z;

        FishAI fish = Instantiate(
            fishTemplate,
            position,
            Quaternion.identity,
            transform
        );

        FishAI.FishRarity rarity = ChooseRarity();

        fish.name = "Fish - " + rarity;
        fish.gameObject.SetActive(true);

        // New fish start out swimming normally.
        // Only our attraction system assigns active bait.
        fish.Configure(rarity, null);

        spawned.Add(fish);
    }

    private FishAI.FishRarity ChooseRarity()
    {
        int total = basicWeight + royalWeight + eliteWeight;

        if (total <= 0)
            return FishAI.FishRarity.Basic;

        int roll = Random.Range(0, total);

        if (roll < basicWeight)
            return FishAI.FishRarity.Basic;

        if (roll < basicWeight + royalWeight)
            return FishAI.FishRarity.Royal;

        return FishAI.FishRarity.Elite;
    }

    // Called by FishingController whenever bait is cast or removed.
    public void SetBaitForAll(Transform bait)
    {
        activeBait = bait;

        // Each new cast gets new random attraction decisions.
        evaluatedFish.Clear();
        baitOwner = null;

        // Remove the previous bait target from all free fish.
        foreach (FishAI fish in spawned)
        {
            if (fish != null && !fish.IsHooked)
                fish.SetBait(null);
        }
    }

    private void CheckBaitAttraction()
    {
        // No bait = no attraction.
        if (activeBait == null ||
            !activeBait.gameObject.activeInHierarchy)
        {
            return;
        }

        // If a fish already owns the bait, no other fish
        // can approach it.
        if (baitOwner != null)
        {
            if (baitOwner.CurrentState !=
                FishAI.FishState.Patrolling)
            {
                return;
            }

            // Previous fish abandoned the interaction.
            baitOwner = null;
        }

        // Find fish currently close enough to detect bait.
        List<FishAI> nearbyFish = new List<FishAI>();

        foreach (FishAI fish in spawned)
        {
            if (fish == null || fish.IsHooked)
                continue;

            // Only one attraction decision per fish per cast.
            if (evaluatedFish.Contains(fish))
                continue;

            float distance = Vector2.Distance(
                fish.transform.position,
                activeBait.position
            );

            if (distance <= fish.detectionRange)
                nearbyFish.Add(fish);
        }

        // Randomize the order so the same fish
        // is not always chosen first.
        for (int i = nearbyFish.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);

            FishAI temp = nearbyFish[i];
            nearbyFish[i] = nearbyFish[j];
            nearbyFish[j] = temp;
        }

        foreach (FishAI fish in nearbyFish)
        {
            // Record that this fish already decided.
            evaluatedFish.Add(fish);

            // 75% chance to approach, 25% chance to ignore.
            bool interested =
                Random.value < attractionChance;

            if (!interested)
            {
                // Ignored fish continues normal swimming.
                continue;
            }

            // Lock the bait to this fish only.
            baitOwner = fish;
            fish.SetBait(activeBait);

            Debug.Log(fish.name + " is approaching bait!");

            // Stop here to prevent crowding.
            break;
        }
    }

    public void RemoveCaughtFish(FishAI caught)
    {
        if (caught == baitOwner)
            baitOwner = null;

        evaluatedFish.Remove(caught);
        spawned.Remove(caught);

        if (caught != null)
            Destroy(caught.gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        if (gameCamera == null || !gameCamera.orthographic)
            return;

        float depth = Mathf.Abs(
            gameCamera.transform.position.z -
            transform.position.z
        );

        Vector3 bottomLeft = gameCamera.ViewportToWorldPoint(
            new Vector3(minX, minY, depth)
        );

        Vector3 topRight = gameCamera.ViewportToWorldPoint(
            new Vector3(maxX, maxY, depth)
        );

        Gizmos.color = Color.cyan;

        Gizmos.DrawWireCube(
            (bottomLeft + topRight) * 0.5f,
            topRight - bottomLeft
        );
    }
}
