using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum GameState { Playing, BossFight, Won, Lost }
    private GameState currentState;

    [Header("Referencias")]
    public PlayerController player;
    public HUDManager hudManager;
    public GameOverManager gameOverManager;
    public WaveController waveController;

    [Header("Jefe")]
    public GameObject bossPrefab;
    public Transform bossSpawnPoint;

    private int currentScore = 0;
    private float elapsedTime = 0f;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        Time.timeScale = 1f;
    }

    void Start()
    {
        StartGame();
    }

    void Update()
    {
        if (currentState == GameState.Won || currentState == GameState.Lost) return;

        elapsedTime += Time.deltaTime;
    }

    public void StartGame()
    {
        currentState = GameState.Playing;
        currentScore = 0;
        elapsedTime = 0f;

        if (hudManager != null)
        {
            hudManager.UpdateScore(currentScore);
            int startHealth = player != null ? player.GetCurrentHealth() : 0;
            int startMaxHealth = player != null ? player.maxHealth : 0;
            hudManager.UpdateHealth(startHealth, startMaxHealth);
            hudManager.UpdateWave(1, 6); // 5 oleadas + jefe
        }

        if (waveController != null)
        {
            waveController.StartWaves();
        }
    }

    public void AddScore(int amount)
    {
        if (currentState == GameState.Won || currentState == GameState.Lost) return;

        currentScore += amount;

        if (hudManager != null)
        {
            hudManager.UpdateScore(currentScore);
        }
    }

    // Usado por el UpgradeManager al comprar una mejora
    public bool TrySpendPoints(int cost)
    {
        if (currentScore < cost) return false;

        currentScore -= cost;

        if (hudManager != null)
        {
            hudManager.UpdateScore(currentScore);
        }

        return true;
    }

    public void TriggerBossFight()
    {
        if (currentState == GameState.Won || currentState == GameState.Lost) return;

        currentState = GameState.BossFight;

        if (hudManager != null)
        {
            hudManager.UpdateWave(6, 6);
        }

        if (bossPrefab != null)
        {
            Vector3 spawnPos = bossSpawnPoint != null ? bossSpawnPoint.position : Vector3.zero;
            Instantiate(bossPrefab, spawnPos, Quaternion.identity);
        }
    }

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
        Time.timeScale = 0f;

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