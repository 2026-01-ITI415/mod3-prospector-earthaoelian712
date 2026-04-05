using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    public void PlayProspector()
    {
        SceneManager.LoadScene("__Prospector_Scene_0");
    }

    public void PlayGolf()
    {
        SceneManager.LoadScene("__Prospector_Golf");
    }
}