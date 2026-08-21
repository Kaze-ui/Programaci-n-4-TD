using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [Header("Prefab")]
    public GameObject enemyPrefab;

    [Header("Spawn")]
    public float spawnInterval = 2f;
    private float timer = 0f;

    [Header("Rango horizontal de aparición")]
    public float minX = -400f;
    public float maxX = 400f;

    private bool spawning = false;

    void Update()
    {
        if (!spawning) return;

        timer += Time.deltaTime;

        if (timer >= spawnInterval)
        {
            timer = 0f;
            SpawnEnemy();
        }
    }

    void SpawnEnemy()
    {
        if (enemyPrefab == null) return;

        float randomX = Random.Range(minX, maxX);
        Vector3 spawnPos = new Vector3(randomX, transform.position.y, 0f);

        Instantiate(enemyPrefab, spawnPos, Quaternion.identity);
    }

    public void StartSpawning()
    {
        spawning = true;
        timer = 0f;
    }

    public void StopSpawning()
    {
        spawning = false;
    }
}