using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Events;

public class Level1FishCatch : MonoBehaviour
{
    [Header("Scene References")]
    public Level1CastingDemo casting;
    public FishSpawner spawner;

    [Header("Canvas Buttons")]
    public Button hookButton;
    public Button holdReelButton;

    [Header("Struggle Settings")]
    [Min(0.1f)]
    public float holdToReelSeconds = 2f;

    [Min(1f)]
    public float secondsToEscape = 6f;

    [Header("Catch Bar (optional)")]
    [Tooltip("Drag the CatchPanel from the CatchBarCanvas prefab here. " +
             "If empty, the old HOLD REEL step is used.")]
    public CatchBar catchBar;

    [Min(0f)]
    public float catchBarResultSeconds = 1.2f;

    [Header("Prototype Statistics")]
    public int totalCaught = 0;
    public int testCoins = 0;

    private FishAI bitingFish;
    private FishAI hookedFish;

    private float reelProgress;
    private float escapeTimer;
    private bool reelHeld;

    private void Start()
    {
        if (casting == null)
            casting = FindAnyObjectByType<Level1CastingDemo>();

        if (spawner == null)
            spawner = FindAnyObjectByType<FishSpawner>();

        // The HOOK and HOLD REEL buttons are only needed without the catch bar.
        bool missingButtons =
            catchBar == null &&
            (hookButton == null || holdReelButton == null);

        if (casting == null ||
            spawner == null ||
            missingButtons)
        {
            Debug.LogError(
                "Level1FishCatch: Assign Casting and Spawner, plus either " +
                "the Catch Bar or the Hook Button and Hold Reel Button."
            );

            enabled = false;
            return;
        }

        // Receive bite notifications from Level1CastingDemo.
        casting.FishBitten += OnFishBitten;

        // Optional catch bar: hidden until a fish is hooked.
        if (catchBar != null)
        {
            catchBar.startOnEnable = false;
            catchBar.onCaught.AddListener(OnCatchBarCaught);
            catchBar.onEscaped.AddListener(OnCatchBarEscaped);
            catchBar.gameObject.SetActive(false);
        }

        // The HOOK button responds to a normal tap.
        if (hookButton != null)
            hookButton.onClick.AddListener(HookFish);

        // HOLD REEL uses actual press and release events.
        if (holdReelButton != null)
            ConfigureHoldButton();

        // Start with both catch buttons hidden.
        ShowButtons(false, false);
    }

    private void ConfigureHoldButton()
    {
        EventTrigger trigger =
            holdReelButton.GetComponent<EventTrigger>();

        if (trigger == null)
        {
            trigger =
                holdReelButton.gameObject.AddComponent<EventTrigger>();
        }

        AddPointerEvent(
            trigger,
            EventTriggerType.PointerDown,
            data =>
            {
                if (hookedFish != null)
                    reelHeld = true;
            }
        );

        AddPointerEvent(
            trigger,
            EventTriggerType.PointerUp,
            data =>
            {
                reelHeld = false;
            }
        );

        // Stop holding if the finger leaves the button.
        AddPointerEvent(
            trigger,
            EventTriggerType.PointerExit,
            data =>
            {
                reelHeld = false;
            }
        );
    }

    private void AddPointerEvent(
        EventTrigger trigger,
        EventTriggerType eventType,
        UnityAction<BaseEventData> callback)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry();

        entry.eventID = eventType;
        entry.callback.AddListener(callback);

        trigger.triggers.Add(entry);
    }

    private void OnFishBitten(FishAI fish)
    {
        if (fish == null ||
            hookedFish != null ||
            bitingFish != null)
        {
            return;
        }

        bitingFish = fish;

        // REEL BACK shouldn't compete with the catch step.
        if (casting.reelBackButton != null)
            casting.reelBackButton.gameObject.SetActive(false);

        // With the catch bar there is no HOOK button:
        // the fish is hooked automatically and the bar appears.
        if (catchBar != null)
        {
            HookFish();
            return;
        }

        // The fish can now be hooked.
        ShowButtons(true, false);

        SetFeedback(
            fish.Rarity + " fish is biting! TAP HOOK!"
        );
    }

    private void Update()
    {
        if (hookedFish == null)
        {
            // The hook window expired before the player hooked.
            if (bitingFish != null && !bitingFish.IsBiting)
            {
                bitingFish.ResetFish();

                ResetEncounter();
                casting.FinishMiss();

                SetFeedback(
                    "Missed the hook! Cast again."
                );
            }

            return;
        }

        // While the catch bar is on screen, it controls the struggle.
        if (catchBar != null)
            return;

        // Count down the total struggle time.
        escapeTimer -= Time.deltaTime;

        if (escapeTimer <= 0f)
        {
            hookedFish.ResetFish();

            ResetEncounter();
            casting.FinishMiss();

            SetFeedback(
                "Fish escaped! You ran out of time."
            );

            return;
        }

        // Player must hold continuously for 2 seconds.
        if (reelHeld)
        {
            reelProgress += Time.deltaTime;

            if (reelProgress >= holdToReelSeconds)
            {
                CompleteStruggle();
                return;
            }

            SetFeedback(
                hookedFish.Rarity +
                " is struggling! HOLD REEL!\n" +
                reelProgress.ToString("0.0") + " / " +
                holdToReelSeconds.ToString("0.0") +
                " seconds"
            );
        }
        else if (reelProgress > 0f)
        {
            // Releasing the button resets the hold.
            reelProgress = 0f;

            SetFeedback(
                "Released early! Hold REEL continuously."
            );
        }
    }

    public void HookFish()
    {
        if (bitingFish == null || hookedFish != null)
            return;

        FishAI fish = bitingFish;

        if (!fish.HookFish())
        {
            fish.ResetFish();

            ResetEncounter();
            casting.FinishMiss();

            SetFeedback("Too late! Fish escaped.");
            return;
        }

        hookedFish = fish;
        bitingFish = null;

        reelProgress = 0f;
        reelHeld = false;
        escapeTimer = secondsToEscape;

        // Catch bar replaces the HOLD REEL step.
        if (catchBar != null)
        {
            ShowButtons(false, false);
            StartCatchBar(fish);
            return;
        }

        // Hide HOOK and display HOLD REEL.
        ShowButtons(false, true);

        SetFeedback(
            fish.Rarity +
            " hooked! HOLD REEL for " +
            holdToReelSeconds.ToString("0.0") +
            " seconds!"
        );
    }

    private void CompleteStruggle()
    {
        if (hookedFish == null)
            return;

        FishAI fish = hookedFish;
        string rarity = fish.Rarity.ToString();

        // Check escape probability only once.
        if (fish.TryEscapeDuringStruggle())
        {
            ResetEncounter();
            casting.FinishMiss();

            SetFeedback(
                rarity + " fish escaped! Cast again."
            );

            return;
        }

        // Fish successfully caught.
        int reward = fish.CoinValue;

        totalCaught++;
        testCoins += reward;

        spawner.RemoveCaughtFish(fish);

        ResetEncounter();

        // Tell the existing Level1 casting system
        // that the fishing attempt has finished.
        casting.FinishCatch();

        SetFeedback(
            "Caught " + rarity + " fish! +" +
            reward + " test coins."
        );
    }

    // ----- Catch bar -----

    private void StartCatchBar(FishAI fish)
    {
        catchBar.SetRarity(ToCatchRarity(fish.Rarity));
        catchBar.gameObject.SetActive(true);
        catchBar.Begin();

        SetFeedback(fish.Rarity + " fish hooked!");
    }

    private CatchRarity ToCatchRarity(FishAI.FishRarity rarity)
    {
        switch (rarity)
        {
            case FishAI.FishRarity.Elite:
                return CatchRarity.Elite;
            case FishAI.FishRarity.Royal:
                return CatchRarity.Royal;
            default:
                return CatchRarity.Basic;
        }
    }

    private void OnCatchBarCaught()
    {
        StartCoroutine(FinishCatchBar(true));
    }

    private void OnCatchBarEscaped()
    {
        StartCoroutine(FinishCatchBar(false));
    }

    private System.Collections.IEnumerator FinishCatchBar(bool caught)
    {
        // Let the player read "You caught it!" or "The fish got away!"
        yield return new WaitForSeconds(catchBarResultSeconds);

        catchBar.gameObject.SetActive(false);

        FishAI fish = hookedFish;

        if (fish == null)
            yield break;

        if (caught)
        {
            string rarity = fish.Rarity.ToString();
            int reward = fish.CoinValue;

            totalCaught++;
            testCoins += reward;

            spawner.RemoveCaughtFish(fish);

            ResetEncounter();
            casting.FinishCatch();

            SetFeedback(
                "Caught " + rarity + " fish! +" +
                reward + " test coins."
            );
        }
        else
        {
            fish.ResetFish();

            ResetEncounter();
            casting.FinishMiss();

            SetFeedback("Fish escaped! Cast again.");
        }
    }

    private void ResetEncounter()
    {
        bitingFish = null;
        hookedFish = null;

        reelHeld = false;
        reelProgress = 0f;
        escapeTimer = 0f;

        ShowButtons(false, false);
    }

    private void ShowButtons(
        bool showHook,
        bool showReel)
    {
        if (hookButton != null)
            hookButton.gameObject.SetActive(showHook);

        if (holdReelButton != null)
            holdReelButton.gameObject.SetActive(showReel);
    }

    private void SetFeedback(string message)
    {
        // Reuse the Level1 Canvas status text.
        if (casting != null && casting.statusText != null)
            casting.statusText.text = message;

        Debug.Log("Level1FishCatch: " + message);
    }

    private void OnDisable()
    {
        reelHeld = false;
    }

    private void OnDestroy()
    {
        if (casting != null)
            casting.FishBitten -= OnFishBitten;

        if (hookButton != null)
            hookButton.onClick.RemoveListener(HookFish);

        if (catchBar != null)
        {
            catchBar.onCaught.RemoveListener(OnCatchBarCaught);
            catchBar.onEscaped.RemoveListener(OnCatchBarEscaped);
        }
    }
}