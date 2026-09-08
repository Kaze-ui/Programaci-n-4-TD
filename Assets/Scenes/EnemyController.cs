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

    [Header("Entrada (antes de empezar a disparar)")]
    public float entrySpeed = 300f; // velocidad de la caída inicial, distinta de moveSpeed (patrulla)
    public float entryHeightAboveSettle = 400f; // garantiza que SIEMPRE arranque por encima de settleY, sin importar el punto de spawn

    [Header("Reposicionamiento horizontal (Tier 1 y Tier 2)")]
    public float repositionMinX = -780f;
    public float repositionMaxX = 2700f;
    public float repositionSpeed = 300f;
    public float minSeparationFromOthers = 200f; // distancia mínima en X respecto a otros enemigos vivos del mismo tier

    // Listas compartidas entre instancias para que ninguna otra elija una posición demasiado cercana
    private static System.Collections.Generic.List<float> reservedTier1Positions = new System.Collections.Generic.List<float>();
    private static System.Collections.Generic.List<float> reservedTier2Positions = new System.Collections.Generic.List<float>();
    private float myReservedX;
    private bool hasReservedPosition = false;

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

        // Sin importar qué punto de spawn le tocó (arriba/izquierda/derecha), lo reposicionamos
        // en Y para que SIEMPRE arranque por encima de settleY. Así garantizamos que la entrada
        // sea "cayendo desde arriba" y nunca "subiendo" (que pasaba si el spawn point quedaba
        // más abajo que settleY). El X se conserva tal cual spawneó.
        if (tier != EnemyTier.Tier5)
        {
            transform.position = new Vector3(transform.position.x, settleY + entryHeightAboveSettle, transform.position.z);
        }

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

        // Entrada: los enemigos bajan hacia settleY. Para Tier1, descienden verticalmente
        // (manteniendo su X) hasta alcanzar settleY antes de reposicionarse horizontalmente.
        Vector3 target = (tier == EnemyTier.Tier1)
            ? new Vector3(transform.position.x, settleY, 0f)
            : new Vector3(Mathf.Clamp(transform.position.x, minX, maxX), settleY, 0f);

        while (Vector3.Distance(transform.position, target) > 5f)
        {
            transform.position = Vector3.MoveTowards(transform.position, target, entrySpeed * Time.deltaTime);
            yield return null;
        }

        switch (tier)
        {
            case EnemyTier.Tier1:
                StartCoroutine(RepositionThenFireTier1());
                break;
            case EnemyTier.Tier2:
                StartCoroutine(RepositionThenFireTier2());
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

    IEnumerator RepositionThenFireTier1()
    {
        float targetX = PickNonOverlappingX(reservedTier1Positions);
        myReservedX = targetX;
        hasReservedPosition = true;
        reservedTier1Positions.Add(myReservedX);

        Vector3 target = new Vector3(targetX, transform.position.y, 0f);

        while (Vector3.Distance(transform.position, target) > 5f)
        {
            transform.position = Vector3.MoveTowards(transform.position, target, repositionSpeed * Time.deltaTime);
            yield return null;
        }

        StartCoroutine(FireTier1());
    }

    IEnumerator RepositionThenFireTier2()
    {
        float targetX = PickNonOverlappingX(reservedTier2Positions);
        myReservedX = targetX;
        hasReservedPosition = true;
        reservedTier2Positions.Add(myReservedX);

        Vector3 target = new Vector3(targetX, transform.position.y, 0f);

        while (Vector3.Distance(transform.position, target) > 5f)
        {
            transform.position = Vector3.MoveTowards(transform.position, target, repositionSpeed * Time.deltaTime);
            yield return null;
        }

        StartCoroutine(FireTier2());
    }

    // Intenta encontrar una X que esté a más de minSeparationFromOthers de cualquier
    // otro enemigo del mismo tier que ya haya reservado posición.
    // Si tras varios intentos aleatorios no encuentra una libre (campo muy poblado),
    // busca el espacio o hueco más grande entre las posiciones existentes para evitar superposiciones.
    float PickNonOverlappingX(System.Collections.Generic.List<float> reservedPositions)
    {
        const int maxAttempts = 50;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            float candidate = Random.Range(repositionMinX, repositionMaxX);
            bool tooClose = false;

            foreach (float reserved in reservedPositions)
            {
                if (Mathf.Abs(candidate - reserved) < minSeparationFromOthers)
                {
                    tooClose = true;
                    break;
                }
            }

            if (!tooClose)
            {
                return candidate;
            }
        }

        // Fallback inteligente: si la pantalla está llena y ningún punto aleatorio cumple la distancia mínima,
        // colocamos el enemigo en el centro del intervalo más amplio disponible.
        if (reservedPositions.Count > 0)
        {
            System.Collections.Generic.List<float> sorted = new System.Collections.Generic.List<float>(reservedPositions);
            sorted.Sort();

            float bestX = (repositionMinX + repositionMaxX) * 0.5f;
            float maxGap = 0f;

            float leftGap = sorted[0] - repositionMinX;
            if (leftGap > maxGap)
            {
                maxGap = leftGap;
                bestX = repositionMinX + leftGap * 0.5f;
            }

            for (int i = 0; i < sorted.Count - 1; i++)
            {
                float gap = sorted[i + 1] - sorted[i];
                if (gap > maxGap)
                {
                    maxGap = gap;
                    bestX = sorted[i] + gap * 0.5f;
                }
            }

            float rightGap = repositionMaxX - sorted[sorted.Count - 1];
            if (rightGap > maxGap)
            {
                bestX = sorted[sorted.Count - 1] + rightGap * 0.5f;
            }

            return bestX;
        }

        return Random.Range(repositionMinX, repositionMaxX);
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

    void OnDestroy()
    {
        // Libera su lugar en el campo de batalla para que otros enemigos puedan usarlo,
        // sin importar si murió por una bala, terminó la partida, o se cambió de escena.
        if (hasReservedPosition)
        {
            if (tier == EnemyTier.Tier1)
            {
                reservedTier1Positions.Remove(myReservedX);
            }
            else if (tier == EnemyTier.Tier2)
            {
                reservedTier2Positions.Remove(myReservedX);
            }
        }
    }

    // Llamado por WaveController al arrancar la Oleada 1, por si quedó algo residual
    // de una partida anterior en la misma sesión de Play.
    public static void ClearReservedPositions()
    {
        reservedTier1Positions.Clear();
        reservedTier2Positions.Clear();
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