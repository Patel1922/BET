using UnityEngine;

/// <summary>
/// Minimal touchscreen fishing loop. OnGUI buttons work with touch in Android builds
/// and mouse clicks in Unity's Game view; replace with Canvas UI for the final design.
/// </summary>
public class FishingController : MonoBehaviour
{
    [Header("Scene References")]
    public FishSpawner spawner;
    [Tooltip("Bait already placed in the scene. Controller hides it until cast.")]
    public Transform bait;
    public Camera gameCamera;

    [Header("Cast Area (Camera Viewport)")]
    [Range(0f, 1f)] public float castMinX = 0.2f;
    [Range(0f, 1f)] public float castMaxX = 0.8f;
    [Range(0f, 1f)] public float castY = 0.38f;

    [Header("Reeling")]
    [Min(1f)] public float secondsToEscape = 6f;

    public int Coins { get; private set; }
    public int TotalCaught { get; private set; }
    public Transform ActiveBait => bait != null && bait.gameObject.activeInHierarchy ? bait : null;

    private FishAI hookedFish;
    private int remainingReels;
    private float escapeTimer;
    private string status = "Tap CAST to place bait";
    private GUIStyle labelStyle;
    private GUIStyle buttonStyle;

    private void Start()
    {
        if (gameCamera == null) gameCamera = Camera.main;
        if (bait != null) bait.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (hookedFish == null) return;
        escapeTimer -= Time.deltaTime;
        if (escapeTimer <= 0f)
        {
            hookedFish.ResetFish();
            hookedFish = null;
            status = "Fish escaped! Cast again.";
            ClearBait();
        }
    }

    public void Cast()
    {
        if (gameCamera == null || bait == null || spawner == null)
        {
            status = "Assign Bait, Spawner, and Camera in Inspector";
            return;
        }

        if (hookedFish != null) return;
        float x = Random.Range(Mathf.Min(castMinX, castMaxX), Mathf.Max(castMinX, castMaxX));
        float depth = Mathf.Abs(bait.position.z - gameCamera.transform.position.z);
        Vector3 destination = gameCamera.ViewportToWorldPoint(new Vector3(x, castY, depth));
        bait.position = new Vector3(destination.x, destination.y, bait.position.z);
        bait.gameObject.SetActive(true);
        spawner.SetBaitForAll(bait);
        status = "Bait cast! Wait for a fish to bite.";
    }

    public void HookOrReel()
    {
        if (hookedFish != null)
        {
            remainingReels--;
            escapeTimer = secondsToEscape; // Each successful reel refreshes the escape timer.
            if (remainingReels <= 0)
            {
                int reward = hookedFish.CoinValue;
                string rarity = hookedFish.Rarity.ToString();
                Coins += reward;
                TotalCaught++;
                spawner.RemoveCaughtFish(hookedFish);
                hookedFish = null;
                ClearBait();
                status = "Caught " + rarity + "! +" + reward + " coins. Cast again.";
            }
            else status = "Keep reeling! " + remainingReels + " taps left.";
            return;
        }

        if (spawner == null || ActiveBait == null)
        {
            status = "Cast your bait first.";
            return;
        }

        FishAI candidate = null;
        float closest = float.MaxValue;
        foreach (FishAI fish in spawner.Fish)
        {
            if (fish == null || !fish.IsBiting) continue;
            float distance = Vector2.Distance(fish.transform.position, ActiveBait.position);
            if (distance < closest) { closest = distance; candidate = fish; }
        }

        if (candidate == null || !candidate.HookFish())
        {
            status = "No fish is ready to bite yet.";
            return;
        }
        
    if (candidate.TryEscapeDuringStruggle())
    {
        status = candidate.Rarity + " escaped! Cast again.";
        ClearBait();
        return;
    }

    hookedFish = candidate;
    remainingReels = candidate.RequiredReels;
    escapeTimer = secondsToEscape;

    status = candidate.Rarity +
        " hooked! Tap REEL " + remainingReels + " times.";

    }

    private void ClearBait()
    {
        if (spawner != null) spawner.SetBaitForAll(null);
        if (bait != null) bait.gameObject.SetActive(false);
    }


    private void OnGUI()
    {
        // Scale UI for mobile and desktop.
        float width = Mathf.Min(Screen.width, Screen.height);
        float scale = Mathf.Max(0.9f, width / 500f);

        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };

            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold
            };
        }

        labelStyle.fontSize = Mathf.RoundToInt(18 * scale);
        buttonStyle.fontSize = Mathf.RoundToInt(22 * scale);

        float margin = 14f * scale;
        float panelHeight = 110f * scale;
        float buttonHeight = 66f * scale;
        float y = Screen.height - buttonHeight - margin;

        // Start with the current status message.
        string message = status;

        // Check fish activity while bait is in the water.
        if (hookedFish == null &&
            ActiveBait != null &&
            spawner != null)
        {
            bool fishApproaching = false;
            bool fishBiting = false;

            foreach (FishAI fish in spawner.Fish)
            {
                if (fish == null)
                    continue;

                // Fish is ready to be hooked.
                if (fish.IsBiting)
                {
                    fishBiting = true;
                    break;
                }

                // Fish is approaching or beginning to bite.
                if (fish.CurrentState == FishAI.FishState.Approaching ||
                    fish.CurrentState == FishAI.FishState.Biting)
                {
                    fishApproaching = true;
                }
            }

            if (fishBiting)
            {
                message = "FISH BITING! TAP HOOK NOW!";
            }
            else if (fishApproaching)
            {
                message = "Fish detected! Approaching bait...";
            }
            else
            {
                message = "Waiting for a fish to bite...";
            }
        }

        // Display coins, caught fish, and live status.
        GUI.Box(
            new Rect(
                margin,
                margin,
                Screen.width - margin * 2f,
                panelHeight
            ),
            "Coins: " + Coins +
            "    Caught: " + TotalCaught +
            "\n" + message,
            labelStyle
        );

        float buttonWidth =
            (Screen.width - margin * 3f) / 2f;

        if (GUI.Button(
            new Rect(margin, y, buttonWidth, buttonHeight),
            "CAST",
            buttonStyle))
        {
            Cast();
        }

        if (GUI.Button(
            new Rect(
                margin * 2f + buttonWidth,
                y,
                buttonWidth,
                buttonHeight
            ),
            hookedFish != null ? "REEL" : "HOOK",
            buttonStyle))
        {
            HookOrReel();
        }
    }

}
