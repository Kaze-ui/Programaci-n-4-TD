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

    void Awake()
    {
        Instance = this;
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
        waveTimer = wave.waveDuration;
        waveActive = true;
        aliveEnemies.Clear();
        enemiesAliveInWave = 0;

        if (GameManager.Instance != null && GameManager.Instance.hudManager != null)
        {
            GameManager.Instance.hudManager.UpdateWave(index + 1, waves.Length + 1); // +1 = jefe
        }

        foreach (var entry in wave.enemies)
        {
            for (int i = 0; i < entry.count; i++)
            {
                SpawnEnemy(entry.enemyPrefab);
                yield return new WaitForSeconds(wave.spawnStagger);
            }
        }

        while (waveActive)
        {
            waveTimer -= Time.deltaTime;

            if (enemiesAliveInWave <= 0)
            {
                waveActive = false; // completada de verdad
            }
            else if (waveTimer <= 0f)
            {
                waveActive = false; // se acabó el tiempo: pasa igual, sin puntos de los sobrevivientes
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
    }
}