using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    // Any button can call this and say which scene to open
    public void LoadScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }
}
