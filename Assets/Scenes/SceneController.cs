using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneController : MonoBehaviour
{
    private static SceneController instance;
    public static SceneController Instance
    {
        get
        {
            if (instance == null)
            {
                instance = Object.FindAnyObjectByType<SceneController>();
                if (instance == null)
                {
                    GameObject obj = new GameObject("SceneController");
                    instance = obj.AddComponent<SceneController>();
                    DontDestroyOnLoad(obj);
                }
            }
            return instance;
        }
        private set => instance = value;
    }

    [Header("Nombres de escenas (única fuente de verdad)")]
    [SerializeField] private string gameSceneName = "InGame";
    [SerializeField] private string mainMenuSceneName = "MenúPrincipal";

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void LoadGameScene()
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