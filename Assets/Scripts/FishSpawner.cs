using System.Collections.Generic;
using UnityEngine;

/// <summary>Spawns fish inside a configurable portion of the camera view.</summary>
public class FishSpawner : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Drag the Fish scene object here. It is used as the template and hidden at runtime.")]
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

    [Header("Rarity Weights (relative percentages)")]
    [Min(0)] public int basicWeight = 65;
    [Min(0)] public int royalWeight = 25;
    [Min(0)] public int eliteWeight = 10;

    private readonly List<FishAI> spawned = new List<FishAI>();
    private float spawnTimer;

    public int ActiveFishCount => spawned.Count;
    public IReadOnlyList<FishAI> Fish => spawned;

    private void Start()
    {
        if (gameCamera == null) gameCamera = Camera.main;
        if (fishTemplate == null || gameCamera == null)
        {
            Debug.LogError("FishSpawner: assign Fish Template and a Camera.");
            enabled = false;
            return;
        }

        // Disable the in-scene prototype so it is not counted as a real fish.
        fishTemplate.gameObject.SetActive(false);
        for (int i = 0; i < Mathf.Min(initialFish, maxFish); i++) SpawnFish();
    }

    private void Update()
    {
        spawned.RemoveAll(fish => fish == null);
        spawnTimer += Time.deltaTime;
        if (spawnTimer >= spawnEverySeconds)
        {
            spawnTimer = 0f;
            if (spawned.Count < maxFish) SpawnFish();
        }
    }

    private void SpawnFish()
    {
        float x = Random.Range(Mathf.Min(minX, maxX), Mathf.Max(minX, maxX));
        float y = Random.Range(Mathf.Min(minY, maxY), Mathf.Max(minY, maxY));
        float depth = Mathf.Abs(fishTemplate.transform.position.z - gameCamera.transform.position.z);
        Vector3 position = gameCamera.ViewportToWorldPoint(new Vector3(x, y, depth));
        position.z = fishTemplate.transform.position.z;

        FishAI fish = Instantiate(fishTemplate, position, Quaternion.identity, transform);
        fish.name = "Fish - " + ChooseRarity();
        fish.gameObject.SetActive(true);
        fish.Configure(ChooseRarityForName(fish.name), fishingController != null ? fishingController.ActiveBait : null);
        spawned.Add(fish);
    }

    private FishAI.FishRarity ChooseRarity()
    {
        int total = basicWeight + royalWeight + eliteWeight;
        if (total <= 0) return FishAI.FishRarity.Basic;
        int roll = Random.Range(0, total);
        if (roll < basicWeight) return FishAI.FishRarity.Basic;
        if (roll < basicWeight + royalWeight) return FishAI.FishRarity.Royal;
        return FishAI.FishRarity.Elite;
    }

    private static FishAI.FishRarity ChooseRarityForName(string objectName)
    {
        if (objectName.EndsWith("Elite")) return FishAI.FishRarity.Elite;
        if (objectName.EndsWith("Royal")) return FishAI.FishRarity.Royal;
        return FishAI.FishRarity.Basic;
    }

    public void SetBaitForAll(Transform bait)
    {
        foreach (FishAI fish in spawned)
            if (fish != null && !fish.IsHooked) fish.SetBait(bait);
    }

    public void RemoveCaughtFish(FishAI caught)
    {
        spawned.Remove(caught);
        if (caught != null) Destroy(caught.gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        if (gameCamera == null || !gameCamera.orthographic) return;
        float depth = Mathf.Abs(gameCamera.transform.position.z - transform.position.z);
        Vector3 bottomLeft = gameCamera.ViewportToWorldPoint(new Vector3(minX, minY, depth));
        Vector3 topRight = gameCamera.ViewportToWorldPoint(new Vector3(maxX, maxY, depth));
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube((bottomLeft + topRight) * 0.5f, topRight - bottomLeft);
    }
}
