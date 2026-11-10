using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneSwitcher : MonoBehaviour
{
    public void OpenBackStory()
    {
        SceneManager.LoadScene("BackStory");
    }
}