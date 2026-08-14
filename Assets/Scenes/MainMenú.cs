using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    [SerializeField] private string MenúPrincipal = "Game";

    public void PlayGame()
    {
        SceneManager.LoadScene(MenúPrincipal    );
    }

    public void QuitGame()
    {
        Debug.Log("Cerrando el juego...");
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}