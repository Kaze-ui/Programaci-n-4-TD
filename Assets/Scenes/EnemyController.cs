using UnityEngine;
using System.Collections;

public enum EnemyTier { Tier1, Tier2, Tier3, Tier4, Tier5 }

public class EnemyController : MonoBehaviour, IDamageable
{
    [Header("Tier")]
    public EnemyTier tier;

    [Header("Vida y puntaje")]
    public int maxHealth = 1;
    private int currentHealth;
    public int scoreValue = 1;
    private bool givesScore = true;

    [Header("Movimiento")]
    public float moveSpeed = 100f;
    public float minX = -400f;
    public float maxX = 400f;
    public float settleY = 250f; // altura donde se "asienta" tras entrar (Tiers 1-4)

    [Header("Disparo")]
    public GameObject bulletPrefab;
    public GameObject bigBulletPrefab; // solo Tier3
    public GameObject laserPrefab;     // solo Tier4
    public float fireCadence = 1f;

    [Header("Daño por contacto (solo Tier5)")]
    public int contactDamage = 1;

    private int patrolDirection = 1;
    private Transform playerTransform;

    void Start()
    {
        currentHealth = maxHealth;

        PlayerController pc = FindAnyObjectByType<PlayerController>();
        if (pc != null) playerTransform = pc.transform;

        StartCoroutine(EnterAndBehave());
    }

    IEnumerator EnterAndBehave()
    {
        if (tier == EnemyTier.Tier5)
        {
            yield return new WaitForSeconds(1f); // "con retraso"
            StartCoroutine(ChasePlayer());
            yield break;
        }

        // Entrada: converge hacia una posición dentro del área jugable, sin importar si spawneó
        // arriba, a la izquierda o a la derecha.
        Vector3 target = new Vector3(Mathf.Clamp(transform.position.x, minX, maxX), settleY, 0f);
        while (Vector3.Distance(transform.position, target) > 5f)
        {
            transform.position = Vector3.MoveTowards(transform.position, target, moveSpeed * Time.deltaTime);
            yield return null;
        }

        switch (tier)
        {
            case EnemyTier.Tier1:
                StartCoroutine(PatrolLoop());
                StartCoroutine(FireTier1());
                break;
            case EnemyTier.Tier2:
                StartCoroutine(FireTier2());
                break;
            case EnemyTier.Tier3:
                StartCoroutine(PatrolLoop());
                StartCoroutine(FireTier3());
                break;
            case EnemyTier.Tier4:
                StartCoroutine(FireTier4());
                break;
        }
    }

    IEnumerator PatrolLoop()
    {
        while (true)
        {
            transform.position += Vector3.right * patrolDirection * moveSpeed * Time.deltaTime;

            if (transform.position.x >= maxX) patrolDirection = -1;
            else if (transform.position.x <= minX) patrolDirection = 1;

            yield return null;
        }
    }

    IEnumerator ChasePlayer()
    {
        while (true)
        {
            if (playerTransform != null)
            {
                Vector3 dir = (playerTransform.position - transform.position).normalized;
                transform.position += dir * moveSpeed * Time.deltaTime;
            }
            yield return null;
        }
    }

    IEnumerator FireTier1()
    {
        while (true)
        {
            yield return new WaitForSeconds(fireCadence);
            FireBullet(bulletPrefab, 250f);
        }
    }

    IEnumerator FireTier2()
    {
        while (true)
        {
            yield return new WaitForSeconds(fireCadence);
            FireBullet(bulletPrefab, 400f);
            yield return new WaitForSeconds(0.5f);
            FireBullet(bulletPrefab, 400f);
        }
    }

    IEnumerator FireTier3()
    {
        while (true)
        {
            yield return new WaitForSeconds(fireCadence);
            FireBullet(bulletPrefab, 400f);
            yield return new WaitForSeconds(0.5f);
            FireBullet(bulletPrefab, 400f);
            yield return new WaitForSeconds(0.5f);
            FireBullet(bulletPrefab, 400f);
            yield return new WaitForSeconds(2f);
            FireBullet(bigBulletPrefab, 120f);
        }
    }

    IEnumerator FireTier4()
    {
        while (true)
        {
            yield return new WaitForSeconds(fireCadence);
            if (laserPrefab != null)
            {
                Instantiate(laserPrefab, transform.position, Quaternion.identity);
            }
        }
    }

    void FireBullet(GameObject prefab, float speed)
    {
        if (prefab == null) return;

        GameObject obj = Instantiate(prefab, transform.position, Quaternion.identity);
        EnemyBullet eb = obj.GetComponent<EnemyBullet>();
        if (eb != null)
        {
            eb.speed = speed;
            eb.direction = Vector3.down;
        }
    }

    public void TakeDamage(int amount)
    {
        currentHealth -= amount;
        if (currentHealth <= 0) Die();
    }

    void Die()
    {
        if (SoundController.Instance != null)
        {
            SoundController.Instance.PlayEnemyDeathSfx();
        }

        if (givesScore && GameManager.Instance != null)
        {
            GameManager.Instance.AddScore(scoreValue);
        }

        if (WaveController.Instance != null)
        {
            WaveController.Instance.OnEnemyDestroyed();
        }

        Destroy(gameObject);
    }

    // Llamado por WaveController cuando se acaba el tiempo de la oleada:
    // el enemigo sigue vivo pero deja de dar puntos si lo matan después.
    public void DisableScoring()
    {
        givesScore = false;
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (tier != EnemyTier.Tier5) return;
        if (!collision.gameObject.CompareTag("Player")) return;

        PlayerController player = collision.gameObject.GetComponent<PlayerController>();
        if (player != null)
        {
            player.TakeDamage(contactDamage);
        }

        Destroy(gameObject);
    }
}