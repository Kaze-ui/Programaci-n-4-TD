using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum GameState { Playing, BossFight, Won, Lost }
    public GameState currentState { get; private set; }
    public bool IsInBossFight => currentState == GameState.BossFight;

    [Header("Referencias")]
    public PlayerController player;
    public PlayerController player2 { get; private set; }
    public bool IsPlayer2Joined => player2 != null;
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
        EnsureBackground();
        StartGame();
    }

    private void EnsureBackground()
    {
        if (FindAnyObjectByType<BackgroundScroller>() == null)
        {
            GameObject bgObj = new GameObject("BackgroundScroller");
            bgObj.AddComponent<BackgroundScroller>();
        }
    }

    void Update()
    {
        if (currentState == GameState.Won || currentState == GameState.Lost) return;

        elapsedTime += Time.deltaTime;

        CheckPlayer2Join();
    }

    private void CheckPlayer2Join()
    {
        if (currentState != GameState.Playing && currentState != GameState.BossFight) return;

        bool canJoin = (waveController == null || waveController.CanPlayer2Join);
        bool isConnected = IsGamepadConnected();

        if (player2 == null)
        {
            if (!canJoin)
            {
                // Si ya se superó la primera oleada (o se llegó a las mejoras de la oleada 1), el 2P no se puede unir más
                if (hudManager != null)
                {
                    hudManager.UpdateP2Status(false, false, 0, 0);
                }
                return;
            }

            if (hudManager != null)
            {
                hudManager.UpdateP2Status(isConnected, false, 0, 0);
            }

            if (isConnected && IsGamepadStartPressed())
            {
                SpawnPlayer2();
            }
        }
        else
        {
            if (hudManager != null)
            {
                hudManager.UpdateP2Status(true, true, player2.GetCurrentHealth(), player2.maxHealth);
            }
        }
    }

    public void SpawnPlayer2()
    {
        if (player2 != null || player == null) return;

        Vector3 spawnPos = player.transform.position + new Vector3(80f, -20f, 0f);
        GameObject p2Obj = Instantiate(player.gameObject, spawnPos, Quaternion.identity);
        p2Obj.name = "Player2";

        player2 = p2Obj.GetComponent<PlayerController>();
        player2.playerIndex = 2;
        player2.bulletDamage = player.bulletDamage;
        player2.moveSpeed = player.moveSpeed;
        player2.maxHealth = 10;
        player2.SetHealth(10);

        player2.ApplyShipVisuals();

        // Al aparecer el segundo jugador, automáticamente se escala (+90%) la salud de TODOS los enemigos presentes
        EnemyController[] existingEnemies = FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
        foreach (var e in existingEnemies)
        {
            if (e != null && !e.IsDead())
            {
                e.DoubleHealth();
            }
        }

        // Duplicar salud del jefe final si estuviese presente
        BossController boss = FindAnyObjectByType<BossController>();
        if (boss != null && !boss.IsDead())
        {
            boss.DoubleHealth();
        }

        if (SoundController.HasInstance)
        {
            SoundController.Instance.PlayUpgradePurchasedSfx();
        }

        if (waveController != null)
        {
            waveController.OnPlayer2Joined();
        }

        if (hudManager != null)
        {
            hudManager.UpdateP2Status(true, true, player2.GetCurrentHealth(), player2.maxHealth);
            hudManager.UpdateHealth(player.GetCurrentHealth(), player.maxHealth, true);
            hudManager.ShowWaveBanner("¡JUGADOR 2 UNIDO!", "¡Mando asignado al Jugador 2!");
        }
    }

    public void ReviveDeadPlayersForNextWave()
    {
        bool anyRevived = false;

        // 1. Revisar si Jugador 1 cayó
        if (player != null && (player.GetCurrentHealth() <= 0 || !player.gameObject.activeInHierarchy))
        {
            player.gameObject.SetActive(true);
            player.SetHealth(5);
            if (player2 != null && player2.gameObject.activeInHierarchy)
            {
                player.transform.position = player2.transform.position + new Vector3(-80f, 0f, 0f);
            }
            if (hudManager != null)
            {
                hudManager.UpdateHealth(5, player.maxHealth, player2 != null);
            }
            anyRevived = true;
        }

        // 2. Revisar si Jugador 2 cayó
        if (player2 != null && (player2.GetCurrentHealth() <= 0 || !player2.gameObject.activeInHierarchy))
        {
            player2.gameObject.SetActive(true);
            player2.SetHealth(5);
            if (player != null && player.gameObject.activeInHierarchy)
            {
                player2.transform.position = player.transform.position + new Vector3(80f, 0f, 0f);
            }
            if (hudManager != null)
            {
                hudManager.UpdateP2Health(5, player2.maxHealth);
            }
            anyRevived = true;
        }

    }

    public static bool IsGamepadConnected()
    {
        if (UnityEngine.InputSystem.Gamepad.all.Count > 0) return true;

        try
        {
            string[] names = Input.GetJoystickNames();
            if (names != null)
            {
                for (int i = 0; i < names.Length; i++)
                {
                    if (!string.IsNullOrEmpty(names[i])) return true;
                }
            }
        }
        catch { }

        return false;
    }

    public static bool IsGamepadStartPressed()
    {
        if (UnityEngine.InputSystem.Gamepad.current != null)
        {
            if (UnityEngine.InputSystem.Gamepad.current.startButton.wasPressedThisFrame)
            {
                return true;
            }
        }

        try
        {
            if (Input.GetKeyDown(KeyCode.JoystickButton7) ||
                Input.GetKeyDown(KeyCode.Joystick1Button7) ||
                Input.GetKeyDown(KeyCode.JoystickButton9) ||
                Input.GetKeyDown(KeyCode.Joystick1Button9))
            {
                return true;
            }
        }
        catch { }

        return false;
    }

    public void StartGame()
    {
        currentState = GameState.Playing;
        currentScore = 0;
        elapsedTime = 0f;

        if (SoundController.Instance != null)
        {
            SoundController.Instance.PlayGameMusic();
        }

        if (hudManager != null)
        {
            hudManager.UpdateScore(currentScore);
            long startHealth = player != null ? player.GetCurrentHealth() : 0L;
            long startMaxHealth = player != null ? player.maxHealth : 0L;
            hudManager.UpdateHealth(startHealth, startMaxHealth, player2 != null);
            int maxWaves = waveController != null ? waveController.waves.Length : 5;
            hudManager.UpdateWave(1, maxWaves);
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

        ReviveDeadPlayersForNextWave();

        currentState = GameState.BossFight;

        if (hudManager != null)
        {
            hudManager.SetWaveText("¡LEVIATÁN NODRIZA!");
            hudManager.ShowWaveBanner("¡ALERTA MÁXIMA!", "¡NAVE NODRIZA DETECTADA!");
            hudManager.UpdateTimer(0f);
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

    public void OnPlayerHealthChanged(PlayerController pc, long currentHealth)
    {
        if (hudManager != null)
        {
            bool has2P = player2 != null;
            if (pc == player || pc == null)
            {
                long maxHealth = player != null ? player.maxHealth : currentHealth;
                hudManager.UpdateHealth(currentHealth, maxHealth, has2P);
            }
            else if (pc == player2)
            {
                long maxHealth = player2 != null ? player2.maxHealth : currentHealth;
                hudManager.UpdateP2Health(currentHealth, maxHealth);
            }
        }
    }

    public void OnPlayerHealthChanged(long currentHealth)
    {
        OnPlayerHealthChanged(player, currentHealth);
    }

    public void OnPlayerHealthChanged(int currentHealth)
    {
        OnPlayerHealthChanged((long)currentHealth);
    }

    public void OnPlayerDied(PlayerController pc)
    {
        bool p1Alive = player != null && player.gameObject.activeInHierarchy && player.GetCurrentHealth() > 0;
        bool p2Alive = player2 != null && player2.gameObject.activeInHierarchy && player2.GetCurrentHealth() > 0;

        if (!p1Alive && !p2Alive)
        {
            LoseGame();
        }
    }

    public void OnPlayerDied()
    {
        OnPlayerDied(player);
    }

    void WinGame()
    {
        if (currentState == GameState.Won || currentState == GameState.Lost) return;
        currentState = GameState.Won;
        EndGame(true);
    }

    void LoseGame()
    {
        if (currentState == GameState.Won || currentState == GameState.Lost) return;
        currentState = GameState.Lost;
        EndGame(false);
    }

    void EndGame(bool isVictory)
    {
        Time.timeScale = 0f;

        BossController remainingBoss = FindAnyObjectByType<BossController>();
        if (remainingBoss != null)
        {
            Destroy(remainingBoss.gameObject);
        }

        if (SoundController.Instance != null)
        {
            SoundController.Instance.StopMusic();
            if (isVictory)
            {
                SoundController.Instance.PlayUpgradeContinueSfx();
            }
            else
            {
                SoundController.Instance.PlayGameOverSfx();
            }
        }

        if (gameOverManager != null)
        {
            gameOverManager.SetResults(currentScore, elapsedTime, isVictory);
        }
    }
}