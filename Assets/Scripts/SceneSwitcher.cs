using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneSwitcher : MonoBehaviour
{
    public void OpenBackStory()
    {
        SceneManager.LoadScene("BackStory");
    }

    public void OpenRulesScreen()
    {
        SceneManager.LoadScene("RulesScreen");
    }

    public void OpenMainMenu()
    {
        SceneManager.LoadScene("MainMenu");
    }
}