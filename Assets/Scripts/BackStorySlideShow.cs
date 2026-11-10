
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class BackstorySlideshow : MonoBehaviour
{
    // The image displayed on the Backstory screen
    public Image slideImage;

    // The cartoon pictures in order
    public Sprite[] slides;

    // How long each picture stays on screen
    public float secondsPerSlide = 5f;

    void Start()
    {
        // Only start if pictures and an image are assigned
        if (slideImage != null && slides != null && slides.Length > 0)
        {
            StartCoroutine(PlaySlideshow());
        }
        else
        {
            Debug.LogWarning("Slideshow is missing images or slides!");
        }
    }

    IEnumerator PlaySlideshow()
    {
        int index = 0;

        while (true)
        {
            // Display the current picture
            slideImage.sprite = slides[index];

            // Wait before showing the next picture
            yield return new WaitForSeconds(secondsPerSlide);

            // Return to the first picture after the last
            index = (index + 1) % slides.Length;
        }
    }
}
