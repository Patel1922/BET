using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Same names as the fish rarities in FishAI.
public enum CatchRarity
{
    Basic = 0,
    Royal = 1,
    Elite = 2
}

// Fishing mini-game: a fish moves up and down inside a bar at a steady speed.
// The player taps the screen while the fish is inside the zone to land a hit.
// Rarer fish need more hits.
//
// Speed = Fish Speed x level bonus x difficulty multiplier.
// Put this script on a full-screen Panel (the panel catches the taps).
// Bar, Zone and Fish must be UI objects anchored to the CENTER.
public class CatchBar : MonoBehaviour, IPointerDownHandler
{
    [Header("Drag these in from the Hierarchy")]
    public RectTransform bar;
    public RectTransform zone;
    public RectTransform fish;
    public Text statusText;                  // optional

    [Header("Speed")]
    public float fishSpeed = 400f;           // UI units per second on Level 1, Medium
    public float levelSpeedIncrease = 0f;    // extra speed per level (0 = off, 0.25 = +25% per level)
    public int level = 1;                    // used when the scene name has no number
    public bool useSceneNameForLevel = true; // "Level2" scene -> level 2

    [Header("Difficulty multipliers (chosen in Settings)")]
    public float easyMultiplier = 0.7f;
    public float mediumMultiplier = 1.0f;
    public float hardMultiplier = 1.4f;

    [Header("Zone size by level (Level 1 = the size you drew in the editor)")]
    [Range(0f, 0.5f)]
    public float zoneShrinkPerLevel = 0.15f;  // each level makes the zone this much smaller
    [Range(0.1f, 1f)]
    public float minZoneMultiplier = 0.35f;   // the zone never gets smaller than this

    [Header("Fish rarity")]
    public CatchRarity rarity = CatchRarity.Basic;
    public int basicHitsNeeded = 1;
    public int royalHitsNeeded = 2;
    public int eliteHitsNeeded = 3;

    [Header("Rules")]
    public int missesAllowed = 2;            // wrong taps before the fish escapes (0 = one try)
    public bool startOnEnable = true;        // start as soon as the panel turns on

    [Header("Events (hook these up later)")]
    public UnityEvent onCaught;
    public UnityEvent onEscaped;

    private int hits;
    private int hitsNeeded;
    private int misses;
    private float direction = 1f;
    private float currentSpeed;
    private bool running;
    private bool waitingForFishToLeaveZone;
    private float baseZoneHeight;

    private void Awake()
    {
        // Remember the zone size you set in the editor
        if (zone != null)
        {
            baseZoneHeight = zone.rect.height;
        }
    }

    private void OnEnable()
    {
        if (startOnEnable)
        {
            Begin();
        }
    }

    // Lets other scripts set the level before calling Begin()
    public void SetLevel(int newLevel)
    {
        level = Mathf.Max(1, newLevel);
        useSceneNameForLevel = false;
    }

    // Lets other scripts set the fish rarity before calling Begin()
    public void SetRarity(CatchRarity newRarity)
    {
        rarity = newRarity;
    }

    // Same thing with a number, so a button can use it: 0 Basic, 1 Royal, 2 Elite
    public void SetRarity(int index)
    {
        rarity = (CatchRarity)Mathf.Clamp(index, 0, 2);
    }

    // Call this to start (or restart) a round.
    public void Begin()
    {
        hits = 0;
        misses = 0;
        direction = 1f;
        running = true;
        waitingForFishToLeaveZone = false;
        hitsNeeded = GetHitsNeeded();

        int currentLevel = GetLevel();
        GameDifficulty difficulty = GameSettings.Difficulty;

        float levelBonus = 1f + (currentLevel - 1) * levelSpeedIncrease;
        currentSpeed = fishSpeed * levelBonus * GetDifficultyMultiplier(difficulty);

        // Higher level = smaller zone (it shrinks around its centre)
        if (zone != null)
        {
            if (baseZoneHeight <= 0f)
            {
                baseZoneHeight = zone.rect.height;
            }

            zone.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                baseZoneHeight * GetZoneMultiplier(currentLevel)
            );
        }

        Debug.Log(
            "CatchBar: level " + currentLevel + ", " + difficulty + ", " + rarity +
            " fish (" + hitsNeeded + " hits), speed " + currentSpeed.ToString("0") +
            ", zone height " + (zone != null ? zone.rect.height.ToString("0") : "none")
        );

        if (bar != null && fish != null)
        {
            float bottom = -bar.rect.height / 2f + fish.rect.height / 2f;
            fish.anchoredPosition = new Vector2(fish.anchoredPosition.x, bottom);
        }

        SetText(
            "Level " + currentLevel + " - " + difficulty + "\n" +
            rarity + " fish: tap in the zone " + hitsNeeded + (hitsNeeded == 1 ? " time!" : " times!")
        );
    }

    private int GetHitsNeeded()
    {
        switch (rarity)
        {
            case CatchRarity.Elite:
                return Mathf.Max(1, eliteHitsNeeded);
            case CatchRarity.Royal:
                return Mathf.Max(1, royalHitsNeeded);
            default:
                return Mathf.Max(1, basicHitsNeeded);
        }
    }

    private int GetLevel()
    {
        if (useSceneNameForLevel)
        {
            // Reads the number at the end of the scene name, e.g. "Level2" -> 2
            string sceneName = SceneManager.GetActiveScene().name;
            int end = sceneName.Length;
            int start = end;

            while (start > 0 && char.IsDigit(sceneName[start - 1]))
            {
                start--;
            }

            if (start < end)
            {
                int parsed;
                if (int.TryParse(sceneName.Substring(start), out parsed))
                {
                    return Mathf.Max(1, parsed);
                }
            }
        }

        return Mathf.Max(1, level);
    }

    private float GetDifficultyMultiplier(GameDifficulty difficulty)
    {
        switch (difficulty)
        {
            case GameDifficulty.Easy:
                return easyMultiplier;
            case GameDifficulty.Hard:
                return hardMultiplier;
            default:
                return mediumMultiplier;
        }
    }

    private float GetZoneMultiplier(int currentLevel)
    {
        // Level 1 = 100%, then smaller each level, but never below the minimum
        float multiplier = 1f - (currentLevel - 1) * zoneShrinkPerLevel;
        return Mathf.Max(minZoneMultiplier, multiplier);
    }

    private bool FishIsInZone()
    {
        float halfZone = zone.rect.height / 2f;
        float distance = Mathf.Abs(fish.anchoredPosition.y - zone.anchoredPosition.y);
        return distance <= halfZone;
    }

    private void Update()
    {
        if (!running || bar == null || fish == null || zone == null)
        {
            return;
        }

        // Steady movement, bouncing between the bottom and top of the bar
        float min = -bar.rect.height / 2f + fish.rect.height / 2f;
        float max = bar.rect.height / 2f - fish.rect.height / 2f;
        float y = fish.anchoredPosition.y + direction * currentSpeed * Time.deltaTime;

        if (y >= max)
        {
            y = max;
            direction = -1f;
        }
        else if (y <= min)
        {
            y = min;
            direction = 1f;
        }

        fish.anchoredPosition = new Vector2(fish.anchoredPosition.x, y);

        // After a hit, the fish must leave the zone before the next hit can count
        if (waitingForFishToLeaveZone && !FishIsInZone())
        {
            waitingForFishToLeaveZone = false;
        }
    }

    // Runs whenever the player taps or clicks the panel
    public void OnPointerDown(PointerEventData eventData)
    {
        if (!running || zone == null || fish == null)
        {
            return;
        }

        if (FishIsInZone())
        {
            // Ignore extra taps while the fish is still in the zone from the last hit
            if (waitingForFishToLeaveZone)
            {
                return;
            }

            hits++;

            if (hits >= hitsNeeded)
            {
                Finish(true);
            }
            else
            {
                waitingForFishToLeaveZone = true;
                SetText("Nice! " + (hitsNeeded - hits) + " more!");
            }
        }
        else
        {
            misses++;

            if (misses > missesAllowed)
            {
                Finish(false);
            }
            else
            {
                SetText("Missed! " + (missesAllowed - misses + 1) + " tries left");
            }
        }
    }

    private void Finish(bool caught)
    {
        running = false;

        if (caught)
        {
            SetText("You caught it!");
            onCaught.Invoke();
        }
        else
        {
            SetText("The fish got away!");
            onEscaped.Invoke();
        }
    }

    private void SetText(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
    }
}