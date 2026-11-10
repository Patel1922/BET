
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class BackstorySlideshow : MonoBehaviour
{
    public Image slideImage;
    public Sprite[] slides;
    public float slideDuration = 5f;

    private int currentSlide = 0;

    void Start()
    {
        if (slideImage != null && slides.Length > 0)
        {
            StartCoroutine(PlaySlideshow());
        }
    }

    IEnumerator PlaySlideshow()
    {
        while (true)
        {
            slideImage.sprite = slides[currentSlide];

            yield return new WaitForSeconds(slideDuration);

            currentSlide = (currentSlide + 1) % slides.Length;
        }
    }
}
