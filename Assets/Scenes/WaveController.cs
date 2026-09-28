using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[System.Serializable]
public class EnemySpawnEntry
{
    public GameObject enemyPrefab;
    public int count = 3;
}

[System.Serializable]
public class WaveDefinition
{
    public string waveName = "Oleada";
    public float waveDuration = 45f;
    public EnemySpawnEntry[] enemies;
    public float spawnStagger = 1.2f;
}

public class WaveController : MonoBehaviour
{
    public static WaveController Instance { get; private set; }

    [Header("Configuración de las 5 oleadas")]
    public WaveDefinition[] waves = new WaveDefinition[5];

    [Header("Puntos de aparición")]
    public Transform spawnTop;
    public Transform spawnLeft;
    public Transform spawnRight;

    [Header("Panel de mejoras (entre oleadas)")]
    public GameObject upgradePanel;

    private int currentWaveIndex = 0;
    private int enemiesAliveInWave = 0;
    private float waveTimer;
    private bool waveActive = false;
    private List<EnemyController> aliveEnemies = new List<EnemyController>();
    private bool isSpawning = false;
    private Coroutine spawnCoroutine;

    [Header("Prefabs de enemigos para generación dinámica")]
    public GameObject tier1Prefab;
    public GameObject tier2Prefab;
    public GameObject tier3Prefab;
    public GameObject tier4Prefab;

    void Awake()
    {
        Instance = this;

        // Asegurar duraciones por defecto de las oleadas (Oleada 1: 45s, Oleada 2: 50s, etc.)
        float[] defaultDurations = { 45f, 50f, 55f, 60f, 60f };
        if (waves != null)
        {
            for (int i = 0; i < waves.Length && i < defaultDurations.Length; i++)
            {
                if (waves[i] != null && waves[i].waveDuration <= 0f)
                {
                    waves[i].waveDuration = defaultDurations[i];
                }
            }
        }

        CacheEnemyPrefabs();
    }

    private void CacheEnemyPrefabs()
    {
        if (waves == null) return;
        foreach (var w in waves)
        {
            if (w == null || w.enemies == null) continue;
            foreach (var e in w.enemies)
            {
                if (e != null && e.enemyPrefab != null)
                {
                    EnemyController ec = e.enemyPrefab.GetComponent<EnemyController>();
                    if (ec != null)
                    {
                        if (ec.tier == EnemyTier.Tier1 && tier1Prefab == null) tier1Prefab = e.enemyPrefab;
                        else if (ec.tier == EnemyTier.Tier2 && tier2Prefab == null) tier2Prefab = e.enemyPrefab;
                        else if (ec.tier == EnemyTier.Tier3 && tier3Prefab == null) tier3Prefab = e.enemyPrefab;
                        else if (ec.tier == EnemyTier.Tier4 && tier4Prefab == null) tier4Prefab = e.enemyPrefab;
                    }
                }
            }
        }
    }

    void Update()
    {
        // Tecla Suprimir (Delete) con el nuevo Input System: saltea la oleada actual e ingresa directamente a su pantalla de mejoras
        bool deletePressed = false;
        if (UnityEngine.InputSystem.Keyboard.current != null)
        {
            deletePressed = UnityEngine.InputSystem.Keyboard.current.deleteKey.wasPressedThisFrame;
        }

        if (deletePressed)
        {
            if (waveActive)
            {
                SkipCurrentWave();
            }
            else
            {
                // Si ya está en la pelea del jefe, derrotar al jefe con Supr para testear
                BossController boss = FindAnyObjectByType<BossController>();
                if (boss != null)
                {
                    boss.TakeDamage(9999);
                }
            }
        }
    }

    public void SkipCurrentWave()
    {
        if (!waveActive) return;

        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
            spawnCoroutine = null;
        }
        isSpawning = false;

        // Elimina los enemigos vivos restantes
        foreach (var e in aliveEnemies)
        {
            if (e != null)
            {
                Destroy(e.gameObject);
            }
        }
        aliveEnemies.Clear();
        enemiesAliveInWave = 0;

        // Limpia balas enemigas en pantalla (sin parámetro deprecado)
        EnemyBullet[] bullets = FindObjectsByType<EnemyBullet>();
        foreach (var b in bullets)
        {
            if (b != null) Destroy(b.gameObject);
        }

        waveActive = false; // Pasa inmediatamente al panel de mejoras
    }

    public void StartWaves()
    {
        EnemyController.ClearReservedPositions();
        currentWaveIndex = 0;
        StartCoroutine(RunWave(currentWaveIndex));
    }

    IEnumerator RunWave(int index)
    {
        WaveDefinition wave = waves[index];

        SetupWaveDynamicSet(index, wave);

        waveTimer = wave.waveDuration;
        waveActive = true;
        aliveEnemies.Clear();
        enemiesAliveInWave = 0;

        if (GameManager.Instance != null && GameManager.Instance.hudManager != null)
        {
            GameManager.Instance.hudManager.UpdateWave(index + 1, waves.Length);
            GameManager.Instance.hudManager.ShowWaveBanner($"SECTOR {index + 1}", "¡INCURSIÓN HOSTIL DETECTADA!");
            GameManager.Instance.hudManager.UpdateTimer(waveTimer);
        }

        spawnCoroutine = StartCoroutine(SpawnEnemiesRoutine(wave));

        while (waveActive)
        {
            waveTimer -= Time.deltaTime;

            if (GameManager.Instance != null && GameManager.Instance.hudManager != null)
            {
                GameManager.Instance.hudManager.UpdateTimer(Mathf.Max(0f, waveTimer));
            }

            // Remueve referencias a enemigos destruidos
            aliveEnemies.RemoveAll(e => e == null);

            // Condición robusta: termina si ya no está spawneando Y (contador <= 0 O no quedan enemigos en lista)
            if (!isSpawning && (enemiesAliveInWave <= 0 || aliveEnemies.Count == 0))
            {
                waveActive = false; // completada de verdad
            }
            else if (waveTimer <= 0f)
            {
                waveActive = false; // se acabó el tiempo: pasa igual, sin puntos de los sobrevivientes
                if (spawnCoroutine != null)
                {
                    StopCoroutine(spawnCoroutine);
                    spawnCoroutine = null;
                }
                isSpawning = false;

                foreach (var e in aliveEnemies)
                {
                    if (e != null) e.DisableScoring();
                }
            }

            yield return null;
        }

        yield return StartCoroutine(ShowUpgradePanelAndWait());

        currentWaveIndex++;
        if (currentWaveIndex < waves.Length)
        {
            StartCoroutine(RunWave(currentWaveIndex));
        }
        else if (GameManager.Instance != null)
        {
            GameManager.Instance.TriggerBossFight();
        }
    }

    IEnumerator SpawnEnemiesRoutine(WaveDefinition wave)
    {
        isSpawning = true;
        if (wave.enemies != null)
        {
            foreach (var entry in wave.enemies)
            {
                if (entry == null || entry.enemyPrefab == null) continue;
                for (int i = 0; i < entry.count; i++)
                {
                    if (!waveActive)
                    {
                        isSpawning = false;
                        yield break;
                    }
                    SpawnEnemy(entry.enemyPrefab);
                    yield return new WaitForSeconds(wave.spawnStagger);
                }
            }
        }
        isSpawning = false;
    }

    void SpawnEnemy(GameObject prefab)
    {
        if (prefab == null) return;

        Transform spawnPoint = ChooseSpawnPoint(prefab);

        // Variación aleatoria en X para que varios enemigos del mismo tier, spawneados
        // desde el mismo punto, no queden apilados unos encima de otros.
        float xJitter = Random.Range(-150f, 150f);
        Vector3 spawnPos = new Vector3(spawnPoint.position.x + xJitter, spawnPoint.position.y, 0f);

        GameObject obj = Instantiate(prefab, spawnPos, Quaternion.identity);

        EnemyController ec = obj.GetComponent<EnemyController>();
        if (ec != null)
        {
            aliveEnemies.Add(ec);
        }

        enemiesAliveInWave++;
    }

    Transform ChooseSpawnPoint(GameObject prefab)
    {
        EnemyController ec = prefab.GetComponent<EnemyController>();
        if (ec == null) return spawnTop;

        // Tier1 y Tier4 siempre entran desde arriba; los demás desde arriba o los costados
        if (ec.tier == EnemyTier.Tier1 || ec.tier == EnemyTier.Tier4)
        {
            return spawnTop;
        }

        int choice = Random.Range(0, 3);
        if (choice == 0) return spawnTop;
        if (choice == 1) return spawnLeft;
        return spawnRight;
    }

    public void OnEnemyDestroyed()
    {
        enemiesAliveInWave--;
    }

    IEnumerator ShowUpgradePanelAndWait()
    {
        if (upgradePanel == null) yield break;

        Time.timeScale = 0f; // Pausa todo el gameplay (movimiento, balas, enemigos)

        upgradePanel.SetActive(true);

        bool continuePressed = false;
        UpgradeManager um = upgradePanel.GetComponent<UpgradeManager>();
        if (um != null)
        {
            um.OnContinue = () => continuePressed = true;
        }

        while (!continuePressed)
        {
            yield return null;
        }

        upgradePanel.SetActive(false);

        Time.timeScale = 1f; // Reanuda el gameplay
    }

    private void SetupWaveDynamicSet(int index, WaveDefinition wave)
    {
        CacheEnemyPrefabs();

        switch (index)
        {
            case 0:
                SetupWave1DynamicSet(wave);
                break;
            case 1:
                SetupWave2DynamicSet(wave);
                break;
            case 2:
                SetupWave3DynamicSet(wave);
                break;
            case 3:
                SetupWave4DynamicSet(wave);
                break;
            case 4:
                SetupWave5DynamicSet(wave);
                break;
        }
    }

    private void SetupWave1DynamicSet(WaveDefinition wave)
    {
        // Probabilidades: 50% solo tier1, 30% solo tier2, 20% ambos juntos
        float roll = Random.Range(0f, 100f);
        var entries = new List<EnemySpawnEntry>();

        if (roll < 50f && tier1Prefab != null)
        {
            // Set 1 (50%): Solo Tier 1 (15 a 30 enemigos)
            int count = Random.Range(15, 31);
            entries.Add(new EnemySpawnEntry { enemyPrefab = tier1Prefab, count = count });
        }
        else if (roll < 80f && tier2Prefab != null)
        {
            // Set 2 (30%): Solo Tier 2 (10 a 15 enemigos)
            int count = Random.Range(10, 16);
            entries.Add(new EnemySpawnEntry { enemyPrefab = tier2Prefab, count = count });
        }
        else
        {
            // Set 3 (20%): Ambos juntos (máximo 15 Tier 1 y 5 Tier 2)
            if (tier1Prefab != null) entries.Add(new EnemySpawnEntry { enemyPrefab = tier1Prefab, count = 15 });
            if (tier2Prefab != null) entries.Add(new EnemySpawnEntry { enemyPrefab = tier2Prefab, count = 5 });
        }

        ApplyWaveEntries(wave, entries, 0.65f, 1.2f);
    }

    private void SetupWave2DynamicSet(WaveDefinition wave)
    {
        // Probabilidades:
        // 30% de 20 a 35 Tier 1
        // 40% de 20 Tier 2 con 5 Tier 1
        // 30% de 15 Tier 2 y 20 Tier 1
        float roll = Random.Range(0f, 100f);
        var entries = new List<EnemySpawnEntry>();

        if (roll < 30f)
        {
            // Set 1 (30%): 20 a 35 Tier 1
            int count = Random.Range(20, 36);
            if (tier1Prefab != null) entries.Add(new EnemySpawnEntry { enemyPrefab = tier1Prefab, count = count });
        }
        else if (roll < 70f)
        {
            // Set 2 (40%): 20 Tier 2 con 5 Tier 1
            if (tier2Prefab != null) entries.Add(new EnemySpawnEntry { enemyPrefab = tier2Prefab, count = 20 });
            if (tier1Prefab != null) entries.Add(new EnemySpawnEntry { enemyPrefab = tier1Prefab, count = 5 });
        }
        else
        {
            // Set 3 (30%): 15 Tier 2 y 20 Tier 1
            if (tier2Prefab != null) entries.Add(new EnemySpawnEntry { enemyPrefab = tier2Prefab, count = 15 });
            if (tier1Prefab != null) entries.Add(new EnemySpawnEntry { enemyPrefab = tier1Prefab, count = 20 });
        }

        ApplyWaveEntries(wave, entries, 0.55f, 1.0f);
    }

    private void SetupWave3DynamicSet(WaveDefinition wave)
    {
        // Probabilidades:
        // 30% de 40 Tier 1 y 10 Tier 2
        // 50% de 30 Tier 2 y 10 Tier 3
        // 20% de 5 Tier 4, 15 Tier 1, 15 Tier 2 y 10 Tier 3
        float roll = Random.Range(0f, 100f);
        var entries = new List<EnemySpawnEntry>();

        if (roll < 30f)
        {
            // Set 1 (30%): 40 Tier 1 y 10 Tier 2
            if (tier1Prefab != null) entries.Add(new EnemySpawnEntry { enemyPrefab = tier1Prefab, count = 40 });
            if (tier2Prefab != null) entries.Add(new EnemySpawnEntry { enemyPrefab = tier2Prefab, count = 10 });
        }
        else if (roll < 80f)
        {
            // Set 2 (50%): 30 Tier 2 y 10 Tier 3
            if (tier2Prefab != null) entries.Add(new EnemySpawnEntry { enemyPrefab = tier2Prefab, count = 30 });
            if (tier3Prefab != null) entries.Add(new EnemySpawnEntry { enemyPrefab = tier3Prefab, count = 10 });
        }
        else
        {
            // Set 3 (20%): 5 Tier 4, 15 Tier 1, 15 Tier 2 y 10 Tier 3
            if (tier4Prefab != null) entries.Add(new EnemySpawnEntry { enemyPrefab = tier4Prefab, count = 5 });
            if (tier1Prefab != null) entries.Add(new EnemySpawnEntry { enemyPrefab = tier1Prefab, count = 15 });
            if (tier2Prefab != null) entries.Add(new EnemySpawnEntry { enemyPrefab = tier2Prefab, count = 15 });
            if (tier3Prefab != null) entries.Add(new EnemySpawnEntry { enemyPrefab = tier3Prefab, count = 10 });
        }

        ApplyWaveEntries(wave, entries, 0.45f, 0.90f);
    }

    private void SetupWave4DynamicSet(WaveDefinition wave)
    {
        // Probabilidades:
        // 50% de 15 Tier 4, 10 Tier 3 y 5 Tier 2
        // 50% de 20 Tier 4 y 10 Tier 3
        float roll = Random.Range(0f, 100f);
        var entries = new List<EnemySpawnEntry>();

        if (roll < 50f)
        {
            // Set 1 (50%): 15 Tier 4, 10 Tier 3 y 5 Tier 2
            if (tier4Prefab != null) entries.Add(new EnemySpawnEntry { enemyPrefab = tier4Prefab, count = 15 });
            if (tier3Prefab != null) entries.Add(new EnemySpawnEntry { enemyPrefab = tier3Prefab, count = 10 });
            if (tier2Prefab != null) entries.Add(new EnemySpawnEntry { enemyPrefab = tier2Prefab, count = 5 });
        }
        else
        {
            // Set 2 (50%): 20 Tier 4 y 10 Tier 3
            if (tier4Prefab != null) entries.Add(new EnemySpawnEntry { enemyPrefab = tier4Prefab, count = 20 });
            if (tier3Prefab != null) entries.Add(new EnemySpawnEntry { enemyPrefab = tier3Prefab, count = 10 });
        }

        ApplyWaveEntries(wave, entries, 0.45f, 0.95f);
    }

    private void SetupWave5DynamicSet(WaveDefinition wave)
    {
        // Probabilidades:
        // 50% de 25 Tier 4, 15 Tier 3 y 20 Tier 1
        // 50% de 30 Tier 4, 12 Tier 3 y 15 Tier 1
        float roll = Random.Range(0f, 100f);
        var entries = new List<EnemySpawnEntry>();

        if (roll < 50f)
        {
            // Set 1 (50%): 25 Tier 4, 15 Tier 3 y 20 Tier 1
            if (tier4Prefab != null) entries.Add(new EnemySpawnEntry { enemyPrefab = tier4Prefab, count = 25 });
            if (tier3Prefab != null) entries.Add(new EnemySpawnEntry { enemyPrefab = tier3Prefab, count = 15 });
            if (tier1Prefab != null) entries.Add(new EnemySpawnEntry { enemyPrefab = tier1Prefab, count = 20 });
        }
        else
        {
            // Set 2 (50%): 30 Tier 4, 12 Tier 3 y 15 Tier 1
            if (tier4Prefab != null) entries.Add(new EnemySpawnEntry { enemyPrefab = tier4Prefab, count = 30 });
            if (tier3Prefab != null) entries.Add(new EnemySpawnEntry { enemyPrefab = tier3Prefab, count = 12 });
            if (tier1Prefab != null) entries.Add(new EnemySpawnEntry { enemyPrefab = tier1Prefab, count = 15 });
        }

        ApplyWaveEntries(wave, entries, 0.38f, 0.85f);
    }

    private void ApplyWaveEntries(WaveDefinition wave, List<EnemySpawnEntry> entries, float minStagger = 0.45f, float maxStagger = 1.0f)
    {
        if (entries == null || entries.Count == 0) return;
        wave.enemies = entries.ToArray();

        int totalCount = 0;
        foreach (var entry in entries)
        {
            totalCount += entry.count;
        }

        if (totalCount > 0)
        {
            wave.spawnStagger = Mathf.Clamp(wave.waveDuration * 0.65f / totalCount, minStagger, maxStagger);
        }
    }
}