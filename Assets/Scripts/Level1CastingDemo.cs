
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Temporary Level 1 presentation controller.
/// CAST -> animate bait into a random point inside the Water sprite bounds
/// -> existing FishAI approaches -> wait at least 5-6 seconds -> notify slider.
/// Does not spawn, catch, score, or despawn fish.
/// </summary>
public class Level1CastingDemo : MonoBehaviour
{
    [Header("Level 1 references")]
    [Tooltip("Create a child named RodTip on Fisherman and place it at the visible tip of the rod.")]
    public Transform rodTip;
    [Tooltip("Drag Level1/Water here. The visible SpriteRenderer bounds define the cast area.")]
    public SpriteRenderer water;
    [Tooltip("Optional: drag Assets/Scripts/Bait.prefab here. A bait object is also created if omitted.")]
    public GameObject baitPrefab;
    public Camera gameCamera;

    [Header("Optional UI (created automatically if empty)")]
    
public Button castButton;
public Button reelBackButton;
public TMP_Text statusText;

[Header("Reeling")]
[Min(0.1f)] public float reelBackDuration = 1.2f;


    [Header("Casting")]
    [Min(0.1f)] public float flightTime = 0.8f;
    [Min(0f)] public float arcHeight = 1.2f;
    [Min(0f)] public float waterEdgePadding = 0.65f;
    [Min(0.01f)] public float lineWidth = 0.035f;
    [Min(0f)] public float lineSag = 0.16f;

    [Header("Bite timing")]
    [Min(0f)] public float minimumWait = 5f;
    [Min(0f)] public float maximumWait = 6f;
    [Min(0.05f)] public float biteDistance = 0.5f;
    [Tooltip("0 means wait for Alyssa's slider to call FinishCatch or FinishMiss. Use 2 for independent testing.")]
    [Min(0f)] public float autoResetAfterBiteSeconds = 0f;

    [Header("Integration with Alyssa's slider")]
    [Tooltip("Connect the slider's public ShowSlider() method here once it exists.")]
    public UnityEvent onFishBite = new UnityEvent();

    public event Action<FishAI> FishBitten;
    public FishAI BitingFish { get; private set; }
    public Transform ActiveBait => baitObject != null && baitObject.activeInHierarchy
        ? baitObject.transform : null;

    
private enum CastState
{
    Ready,
    Flying,
    Waiting,
    Bitten,
    Reeling
}

    private CastState state = CastState.Ready;
    private GameObject baitObject;
    
    [Header("Fishing Line")]
    public LineRenderer fishingLine;

    private FishAI targetFish;
    private float originalDetectionRange;
    private float originalSwimSpeed;
    private float biteAllowedTime;
    private float nextFishSearch;
    private Texture2D baitTexture;
    private Sprite baitSprite;
    private Material lineMaterial;

    private void Awake()
    {
        if (gameCamera == null) gameCamera = Camera.main;
        if (rodTip == null || water == null || gameCamera == null || water.sprite == null)
        {
            Debug.LogError("Level1CastingDemo: assign RodTip, Water (SpriteRenderer), and Main Camera.", this);
            enabled = false;
            return;
        }

        CreateBait();
        
    if (fishingLine == null)
    {
        CreateFishingLine();
    }
    else
    {
        fishingLine.positionCount = 16;
        fishingLine.useWorldSpace = true;
        fishingLine.enabled = false;
    }

    }

    
private void Start()
{
    if (!enabled) return;

    if (castButton == null)
        CreateCastButton();

    if (statusText == null)
        CreateStatusText();

    if (castButton != null)
        castButton.onClick.AddListener(CastBait);

    if (reelBackButton != null)
    {
        reelBackButton.onClick.AddListener(ReelBack);
        reelBackButton.gameObject.SetActive(false);
    }

    SetStatus("Press CAST BAIT to start fishing");
}


    private void Update()
    {
        if (state == CastState.Flying || state == CastState.Waiting || state == CastState.Bitten||state == CastState.Reeling)
            UpdateFishingLine();

        if (state != CastState.Waiting) return;

        // Prasil's fish can spawn at any time, including after the bait lands.
        if (targetFish == null || !targetFish.gameObject.activeInHierarchy)
        {
            targetFish = null;
            if (Time.time >= nextFishSearch)
            {
                nextFishSearch = Time.time + 0.25f;
                FindApproachingFish();
            }
        }
        if (targetFish == null) return;

        // Retain the target even if another script briefly clears its bait reference.
        if (targetFish.bait != baitObject.transform)
            targetFish.bait = baitObject.transform;

        bool reachedBait = Vector2.Distance(targetFish.transform.position,
                                             baitObject.transform.position) <= biteDistance;
        if (reachedBait && Time.time >= biteAllowedTime)
            ReportBite();
    }

    /// <summary>Bind this method to the presentation CAST BAIT button.</summary>
    public void CastBait()
    {
        if (!enabled || state != CastState.Ready) return;

        Vector3 destination = PickWaterPosition();
        biteAllowedTime = 0f;
        BitingFish = null;
        state = CastState.Flying;
        
        if (reelBackButton != null)
        {
            reelBackButton.gameObject.SetActive(true);
            reelBackButton.interactable = true;
        }
        ;
        baitObject.transform.position = rodTip.position;
        baitObject.SetActive(true);
        fishingLine.enabled = true;
        SetStatus("Casting bait...");
        StartCoroutine(AnimateCast(destination));
    }

    private IEnumerator AnimateCast(Vector3 destination)
    {
        Vector3 start = rodTip.position;
        float elapsed = 0f;
        while (elapsed < flightTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / flightTime);
            Vector3 position = Vector3.Lerp(start, destination, t);
            position.y += Mathf.Sin(t * Mathf.PI) * arcHeight;
            position.z = 0f;
            baitObject.transform.position = position;
            yield return null;
        }

        baitObject.transform.position = destination;
        state = CastState.Waiting;
        biteAllowedTime = Time.time + UnityEngine.Random.Range(
            Mathf.Min(minimumWait, maximumWait), Mathf.Max(minimumWait, maximumWait));
        nextFishSearch = 0f;
        FindApproachingFish();
        SetStatus(targetFish == null ? "Bait landed. Waiting for fish to spawn..."
                                     : "Bait landed. Fish approaching...");
    }


public void ReelBack()
{
    if (state == CastState.Ready ||
        state == CastState.Reeling ||
        state == CastState.Bitten)
        return;

    StopAllCoroutines();

    state = CastState.Reeling;

    if (reelBackButton != null)
        reelBackButton.interactable = false;

    // Stop the fish from chasing the bait.
    if (targetFish != null)
    {
        targetFish.bait = null;
        targetFish.detectionRange = originalDetectionRange;
        targetFish.swimSpeed = originalSwimSpeed;
    }

    targetFish = null;

    SetStatus("Reeling bait back...");

    StartCoroutine(AnimateReelBack());
}

private IEnumerator AnimateReelBack()
{
    Vector3 startingPosition = baitObject.transform.position;

    float elapsed = 0f;

    while (elapsed < reelBackDuration)
    {
        elapsed += Time.deltaTime;

        float t = Mathf.Clamp01(
            elapsed / reelBackDuration
        );

        // Smooth movement back to the rod tip.
        t = Mathf.SmoothStep(0f, 1f, t);

        baitObject.transform.position =
            Vector3.Lerp(
                startingPosition,
                rodTip.position,
                t
            );

        UpdateFishingLine();

        yield return null;
    }

    baitObject.transform.position = rodTip.position;

    FinishAttempt(
        "Bait retrieved! Press CAST BAIT to fish again."
    );
}



    private Vector3 PickWaterPosition()
    {
        Bounds area = water.bounds;
        float cameraHalfHeight = gameCamera.orthographicSize;
        float cameraHalfWidth = cameraHalfHeight * gameCamera.aspect;
        Vector3 center = gameCamera.transform.position;

        // Keep the bait inside BOTH the actual Water sprite AND the visible camera.
        float minX = Mathf.Max(area.min.x + waterEdgePadding,
                               center.x - cameraHalfWidth + waterEdgePadding);
        float maxX = Mathf.Min(area.max.x - waterEdgePadding,
                               center.x + cameraHalfWidth - waterEdgePadding);
        float minY = Mathf.Max(area.min.y + waterEdgePadding,
                               center.y - cameraHalfHeight + waterEdgePadding);
        float maxY = Mathf.Min(area.max.y - waterEdgePadding,
                               center.y + cameraHalfHeight - waterEdgePadding);

        if (minX > maxX) minX = maxX = area.center.x;
        if (minY > maxY) minY = maxY = area.center.y;
        return new Vector3(UnityEngine.Random.Range(minX, maxX),
                           UnityEngine.Random.Range(minY, maxY), 0f);
    }

    private void FindApproachingFish()
    {
        FishAI[] fishes = FindObjectsByType<FishAI>();
        FishAI nearest = null;
        float closest = float.MaxValue;

        foreach (FishAI fish in fishes)
        {
            if (fish == null || !fish.isActiveAndEnabled) continue;
            float distance = Vector2.Distance(fish.transform.position, baitObject.transform.position);
            if (distance < closest)
            {
                nearest = fish;
                closest = distance;
            }
        }
        if (nearest == null) return;

        targetFish = nearest;
        originalDetectionRange = targetFish.detectionRange;
        originalSwimSpeed = targetFish.swimSpeed;
        targetFish.bait = baitObject.transform;
        // Presentation helpers: notice a distant cast, and arrive near the 5-6 second mark.
        targetFish.detectionRange = Mathf.Max(targetFish.detectionRange, closest + 1f);
        float remaining = Mathf.Max(0.5f, biteAllowedTime - Time.time);
        targetFish.swimSpeed = Mathf.Max(targetFish.swimSpeed,
            Mathf.Max(0f, closest - biteDistance) / remaining * 1.1f);
        SetStatus("Fish approaching the bait...");
    }

    private void ReportBite()
    {
        if (state != CastState.Waiting) return;
        state = CastState.Bitten;
        BitingFish = targetFish;
        SetStatus("Fish on the bait! Open the catching slider.");
        Debug.Log("Level1CastingDemo: Fish has bitten!", this);

        // Only signal the slider. Alyssa owns the minigame; Hiya owns despawning.
        onFishBite.Invoke();
        FishBitten?.Invoke(BitingFish);
        if (autoResetAfterBiteSeconds > 0f)
            StartCoroutine(AutoResetAfterBite());
    }

    private IEnumerator AutoResetAfterBite()
    {
        yield return new WaitForSeconds(autoResetAfterBiteSeconds);
        if (state == CastState.Bitten) FinishMiss();
    }

    /// <summary>Alyssa calls this when the slider says the player caught the fish.</summary>
    public void FinishCatch()
    {
        FinishAttempt("Catch completed! Press CAST BAIT to fish again.");
    }

    /// <summary>Alyssa calls this when the slider says the player missed.</summary>
    public void FinishMiss()
    {
        FinishAttempt("Fish escaped. Press CAST BAIT to try again.");
    }

    private void FinishAttempt(string message)
    {
        if (state == CastState.Ready) return;
        StopAllCoroutines();
        if (targetFish != null)
        {
            targetFish.bait = null;
            targetFish.detectionRange = originalDetectionRange;
            targetFish.swimSpeed = originalSwimSpeed;
        }
        targetFish = null;
        BitingFish = null;
        baitObject.SetActive(false);
        fishingLine.enabled = false;
        state = CastState.Ready;
        if (reelBackButton != null){reelBackButton.gameObject.SetActive(false);}
        SetStatus(message);
    }

    [ContextMenu("Test - Reset current cast")]
    public void ResetCast()
    {
        if (Application.isPlaying)
            FinishAttempt("Ready. Press CAST BAIT.");
    }

    
private void CreateBait()
{
    if (baitPrefab != null)
    {
        baitObject = Instantiate(baitPrefab);
    }
    else
    {
        baitObject = new GameObject("Bait");
    }

    baitObject.name = "Bait (presentation)";
    baitObject.transform.position = rodTip.position;

    SpriteRenderer renderer =
        baitObject.GetComponent<SpriteRenderer>();

    if (renderer == null)
    {
        renderer = baitObject.AddComponent<SpriteRenderer>();
    }

    // Preserve the sprite from Bait.prefab.
    // Only generate a temporary sprite if none exists.
    if (renderer.sprite == null)
    {
        baitSprite = MakeBaitSprite();
        renderer.sprite = baitSprite;
        baitObject.transform.localScale = Vector3.one * 0.5f;
    }

    renderer.sortingOrder = 35;
    renderer.color = Color.white;

    baitObject.SetActive(false);
}


    private Sprite MakeBaitSprite()
    {
        const int width = 32;
        const int height = 48;
        baitTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        baitTexture.filterMode = FilterMode.Point;
        baitTexture.wrapMode = TextureWrapMode.Clamp;
        Color32 clear = new Color32(0, 0, 0, 0);
        Color32 outline = new Color32(30, 37, 47, 255);
        Color32 red = new Color32(236, 66, 54, 255);
        Color32 white = new Color32(255, 246, 218, 255);
        Color32 hook = new Color32(185, 199, 208, 255);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float dx = (x - 15.5f) / 12f;
                float dy = (y - 28f) / 14f;
                float r2 = dx * dx + dy * dy;
                Color32 color = clear;
                if (r2 <= 1f) color = r2 > 0.82f ? outline : y >= 29 ? red : white;
                if (y >= 41 && y <= 45 && x >= 14 && x <= 17) color = outline;
                if (y >= 5 && y <= 13 && x >= 14 && x <= 16) color = hook;
                if (y >= 5 && y <= 7 && x >= 15 && x <= 21) color = hook;
                if (y >= 7 && y <= 11 && x >= 20 && x <= 22) color = hook;
                baitTexture.SetPixel(x, y, color);
            }
        }
        baitTexture.Apply();
        return Sprite.Create(baitTexture, new Rect(0, 0, width, height),
                             new Vector2(0.5f, 0.65f), 40f);
    }

    private void CreateFishingLine()
    {
        GameObject lineObject = new GameObject("Fishing Line (rod tip to bait)");
        lineObject.transform.SetParent(transform);
        fishingLine = lineObject.AddComponent<LineRenderer>();
        fishingLine.useWorldSpace = true;
        fishingLine.positionCount = 16;
        fishingLine.widthMultiplier = lineWidth;
        fishingLine.numCapVertices = 3;
        fishingLine.startColor = new Color(0.95f, 0.95f, 0.85f, 1f);
        fishingLine.endColor = new Color(0.95f, 0.95f, 0.85f, 1f);
        fishingLine.sortingOrder = 34;

        Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader != null)
        {
            lineMaterial = new Material(shader);
            fishingLine.material = lineMaterial;
        }
        fishingLine.enabled = false;
    }

    private void UpdateFishingLine()
    {
        if (baitObject == null || !baitObject.activeInHierarchy) return;
        Vector3 from = rodTip.position;
        Vector3 to = baitObject.transform.position;
        for (int i = 0; i < fishingLine.positionCount; i++)
        {
            float t = i / (float)(fishingLine.positionCount - 1);
            Vector3 point = Vector3.Lerp(from, to, t);
            point.y -= Mathf.Sin(t * Mathf.PI) * lineSag;
            point.z = 0f;
            fishingLine.SetPosition(i, point);
        }
    }

    private void CreateCastButton()
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("Level1CastingDemo: no Canvas found for the CAST BAIT button.", this);
            return;
        }
        GameObject buttonObject = new GameObject("CAST BAIT (presentation)",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(canvas.transform, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 28f);
        rect.sizeDelta = new Vector2(280f, 76f);
        Image background = buttonObject.GetComponent<Image>();
        background.color = new Color(0.07f, 0.44f, 0.67f, 0.98f);
        castButton = buttonObject.GetComponent<Button>();
        castButton.targetGraphic = background;

        GameObject textObject = new GameObject("Label", typeof(RectTransform),
                                              typeof(CanvasRenderer), typeof(Text));
        textObject.transform.SetParent(buttonObject.transform, false);
        RectTransform labelRect = textObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
        Text label = textObject.GetComponent<Text>();
        label.text = "CAST BAIT";
        label.alignment = TextAnchor.MiddleCenter;
        label.fontSize = 30;
        label.color = Color.white;
        label.font = GetRuntimeFont();
    }

    
    private void CreateStatusText()
    {
        Debug.LogWarning(
            "FishingStatusText is not assigned in the Inspector."
        );
    }


    private Font GetRuntimeFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return font != null ? font : Font.CreateDynamicFontFromOSFont(
            new[] { "Arial", "Roboto", "Liberation Sans" }, 28);
    }

    
    private void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }

        Debug.Log("Level1CastingDemo: " + message, this);
    }


    private void OnDestroy()
    {
        if (castButton != null) castButton.onClick.RemoveListener(CastBait);
        if (baitSprite != null) Destroy(baitSprite);
        if (baitTexture != null) Destroy(baitTexture);
        if (lineMaterial != null) Destroy(lineMaterial);
        if (reelBackButton != null)reelBackButton.onClick.RemoveListener(ReelBack);
        
    }
}
