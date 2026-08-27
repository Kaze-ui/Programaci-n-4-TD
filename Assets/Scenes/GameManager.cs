using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum GameState { Playing, BossFight, Won, Lost }
    private GameState currentState;

    [Header("Referencias")]
    public PlayerController player;
    public EnemySpawner enemySpawner;
    public HUDManager hudManager;
    public GameOverManager gameOverManager;

    [Header("Jefe")]
    public GameObject bossPrefab;
    public Transform bossSpawnPoint;

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
        if (currentState == GameState.Won || currentState == GameState.Lost) return;

        elapsedTime += Time.deltaTime; // el tiempo total sigue corriendo durante la pelea contra el jefe

        if (currentState != GameState.Playing) return; // el timer de la oleada no aplica durante BossFight

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
            StartBossFight();
        }
    }

    void StartBossFight()
    {
        currentState = GameState.BossFight;

        if (enemySpawner != null)
        {
            enemySpawner.StopSpawning();
        }

        if (hudManager != null)
        {
            hudManager.UpdateWave(2, 2); // "oleada 2 de 2": la pelea contra el jefe
        }

        if (bossPrefab != null)
        {
            Vector3 spawnPos = bossSpawnPoint != null ? bossSpawnPoint.position : Vector3.zero;
            Instantiate(bossPrefab, spawnPos, Quaternion.identity);
        }
    }

    // Llamado por BossController cuando el jugador lo derrota
    public void OnBossDefeated(int bonusScore)
    {
        if (currentState != GameState.BossFight) return;

        currentScore += bonusScore;

        if (hudManager != null)
        {
            hudManager.UpdateScore(currentScore);
        }

        WinGame();
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
        if (currentState == GameState.Won || currentState == GameState.Lost) return;
        currentState = GameState.Won;
        EndGame();
    }

    void LoseGame()
    {
        if (currentState == GameState.Won || currentState == GameState.Lost) return;
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

        // Si el jefe seguía vivo (por ejemplo, el jugador murió durante la pelea), lo limpiamos
        BossController remainingBoss = FindAnyObjectByType<BossController>();
        if (remainingBoss != null)
        {
            Destroy(remainingBoss.gameObject);
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