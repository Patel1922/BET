
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

        if (casting == null ||
            spawner == null ||
            hookButton == null ||
            holdReelButton == null)
        {
            Debug.LogError(
                "Level1FishCatch: Assign Casting, Spawner, " +
                "Hook Button, and Hold Reel Button."
            );

            enabled = false;
            return;
        }

        // Receive bite notifications from Level1CastingDemo.
        casting.FishBitten += OnFishBitten;

        // The HOOK button responds to a normal tap.
        hookButton.onClick.AddListener(HookFish);

        // HOLD REEL uses actual press and release events.
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

        // The fish can now be hooked.
        ShowButtons(true, false);

        // REEL BACK shouldn't compete with HOOK.
        if (casting.reelBackButton != null)
            casting.reelBackButton.gameObject.SetActive(false);

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
    }
}
