using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    public void PlayGame()
    {
        if (SceneController.Instance != null)
        {
            SceneController.Instance.LoadGameScene();
        }
        else
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("InGame");
        }
    }

    public void QuitGame()
    {
        if (SceneController.Instance != null)
        {
            SceneController.Instance.QuitGame();
        }
        else
        {
            Debug.Log("Cerrando el juego...");
            Application.Quit();

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }
}