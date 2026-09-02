using UnityEngine;

public class MainMenuScriptPC : MonoBehaviour
{
    public void LoadLobbyScene()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene("Final_Lobby");
    }

    public void ApplicationClose()
    {
        Application.Quit();
    }
}
