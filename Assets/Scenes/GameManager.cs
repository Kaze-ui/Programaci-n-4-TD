using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum GameState { Playing, Won, Lost }
    private GameState currentState;

    [Header("Referencias")]
    public PlayerController player;
    public EnemySpawner enemySpawner;
    public HUDManager hudManager;
    public GameOverManager gameOverManager;

    [Header("Configuración de la oleada (versión simplificada)")]
    public int targetScore = 5;       // puntos necesarios para "ganar" esta oleada de prueba
    public float waveDuration = 60f;  // segundos disponibles para lograrlo

    private int currentScore = 0;
    private float timeRemaining;
    private float elapsedTime = 0f; // para el tiempo total jugado (leaderboard)

    void Awake()
    {
        // Patrón singleton: si ya existe una instancia, esta se destruye (evita duplicados)
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        Time.timeScale = 1f; // por si quedó pausado de una partida anterior
    }

    void Start()
    {
        StartGame();
    }

    void Update()
    {
        if (currentState != GameState.Playing) return;

        elapsedTime += Time.deltaTime;
        timeRemaining -= Time.deltaTime;

        if (hudManager != null)
        {
            hudManager.UpdateTimer(Mathf.CeilToInt(timeRemaining));
        }

        if (timeRemaining <= 0f)
        {
            // Se acabó el tiempo sin llegar al puntaje objetivo.
            // (En el sistema final esto pasaría de oleada; por ahora, para esta entrega, es derrota)
            LoseGame();
        }
    }

    public void StartGame()
    {
        currentState = GameState.Playing;
        currentScore = 0;
        elapsedTime = 0f;
        timeRemaining = waveDuration;

        if (hudManager != null)
        {
            hudManager.UpdateScore(currentScore);
            int startHealth = player != null ? player.GetCurrentHealth() : 0;
            int startMaxHealth = player != null ? player.maxHealth : 0;
            hudManager.UpdateHealth(startHealth, startMaxHealth);
            hudManager.UpdateWave(1, 1); // oleada 1 de 1, versión simplificada para esta entrega
            hudManager.UpdateTimer(Mathf.CeilToInt(timeRemaining));
        }

        if (enemySpawner != null)
        {
            enemySpawner.StartSpawning();
        }
    }

    public void AddScore(int amount)
    {
        if (currentState != GameState.Playing) return;

        currentScore += amount;

        if (hudManager != null)
        {
            hudManager.UpdateScore(currentScore);
        }

        if (currentScore >= targetScore)
        {
            WinGame();
        }
    }

    public void OnPlayerHealthChanged(int currentHealth)
    {
        if (hudManager != null)
        {
            int maxHealth = player != null ? player.maxHealth : currentHealth;
            hudManager.UpdateHealth(currentHealth, maxHealth);
        }
    }

    public void OnPlayerDied()
    {
        LoseGame();
    }

    void WinGame()
    {
        if (currentState != GameState.Playing) return;
        currentState = GameState.Won;
        EndGame();
    }

    void LoseGame()
    {
        if (currentState != GameState.Playing) return;
        currentState = GameState.Lost;
        EndGame();
    }

    void EndGame()
    {
        Time.timeScale = 0f; // congela el juego (movimiento, disparo, spawns) sin desactivar scripts

        if (enemySpawner != null)
        {
            enemySpawner.StopSpawning();
        }

        if (SoundController.Instance != null)
        {
            SoundController.Instance.PlayGameOverSfx();
        }

        if (gameOverManager != null)
        {
            gameOverManager.SetResults(currentScore, elapsedTime);
        }
    }
}