using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneController : MonoBehaviour
{
    public static SceneController Instance { get; private set; }

    [Header("Nombres de escenas (única fuente de verdad)")]
    [SerializeField] private string gameSceneName = "InGame";
    [SerializeField] private string mainMenuSceneName = "MenúPrincipal";

    void Awake()
    {
        Instance = this;
    }
           public void LoadGameScene(     
    {
        Time.timeScale = 1f; // por si veníamos de una partida pausada/terminada
        SceneManager.LoadScene(gameSceneName);
    }

    public void LoadMainMenuScene()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    public void RestartCurrentScene()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
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