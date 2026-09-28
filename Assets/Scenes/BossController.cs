using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class BossController : MonoBehaviour, IDamageable
{
    [Header("Vida")]
    public int maxHealth = 25;
    private float currentHealth;

    [Header("Movimiento")]
    public float moveSpeed = 175f; // 70% de la velocidad base del jugador (250 * 0.70)
    public float targetY = 1100f;  // Altura máxima incrementada en 300 (de 800 a 1100)
    public float entrySpeed = 150f;
    private bool hasEntered = false;
    private int moveDirection = 1;

    [Header("Prefabs de Ataque")]
    public GameObject bulletPrefab;      // Balas chicas (Tier 1)
    public GameObject bigBulletPrefab;   // Balas grandes (Tier 3)
    public GameObject laserPrefab;       // Súper láser blanco (Tier 4)
    public GameObject telegraphPrefab;   // Súper láser rojo de aviso (Tier 4)
    public GameObject tier4Prefab;       // Minions Tier 4 que aparecen durante la batalla

    [Header("Balas Chicas (Solo lado inferior)")]
    public float smallBulletCadence = 1.02f; // Cadencia base
    public float smallBulletSpeed = 400f;   // Misma velocidad que Tier 1
    public int smallBulletCount = 12;       // Cuadriplicado: 12 balas desde la cara inferior
    public float homingTurnSpeed = 100f;

    [Header("Balas Grandes Diagonales")]
    public float bigBulletCadence = 1.0f;   // Duplicada la velocidad de generación (cada 1 segundo en lugar de 2)
    public float bigBulletSpeed = 288f;     // Duplicada la velocidad (144 * 2 = 288)
    public float bigBulletLifetime = 40f;   // Cruza todo el mapa

    [Header("Súper Láser")]
    public float laserWarningDuration = 3f;  // Aviso rojo de 3 segundos
    public float laserActiveDuration = 3f;   // Láser activo durante 3 segundos
    public float laserWidth = 150f;          // Mitad del lado del cuadrado (300 / 2 = 150)
    public float laserLength = 3500f;        // Longitud para cruzar la pantalla completa

    [Header("Daño por contacto")]
    public int contactDamage = 2;
    public float contactCooldown = 1f;
    private float nextContactTime = 0f;

    [Header("Puntaje al derrotarlo")]
    public int scoreValue = 10;

    private Camera mainCam;
    private PlayerController playerController;
    private GameObject activeWarningLaser;
    private GameObject activeRealLaser;
    private List<GameObject> spawnedMinions = new List<GameObject>();

    void Start()
    {
        currentHealth = maxHealth;
        mainCam = Camera.main;
        playerController = FindAnyObjectByType<PlayerController>();

        // Arranca por fuera de la pantalla superior para descender suavemente
        if (mainCam != null)
        {
            float screenTop = mainCam.transform.position.y + mainCam.orthographicSize + 250f;
            transform.position = new Vector3(transform.position.x, Mathf.Max(transform.position.y, screenTop), transform.position.z);
        }
    }

    void Update()
    {
        if (Time.timeScale == 0f) return;

        if (!hasEntered)
        {
            Enter();
        }
        else
        {
            Patrol();
        }
    }

    void LateUpdate()
    {
        if (Time.timeScale == 0f) return;

        ClampToScreen();
        UpdateLaserPosition();
    }

    void Enter()
    {
        transform.position += Vector3.down * entrySpeed * Time.deltaTime;

        if (transform.position.y <= targetY)
        {
            transform.position = new Vector3(transform.position.x, targetY, transform.position.z);
            hasEntered = true;

            StartCoroutine(SmallBulletBottomRoutine());
            StartCoroutine(BigBulletDiagonalRoutine());
            StartCoroutine(SuperLaserRoutine());
            StartCoroutine(SpawnTier4MinionsRoutine());
        }
    }

    void Patrol()
    {
        // Se mueve horizontalmente al 70% de la velocidad actual del jugador
        float currentSpeed = GetCurrentHorizontalSpeed();
        transform.position += Vector3.right * moveDirection * currentSpeed * Time.deltaTime;
    }

    float GetCurrentHorizontalSpeed()
    {
        if (playerController == null)
        {
            playerController = FindAnyObjectByType<PlayerController>();
        }

        if (playerController != null)
        {
            return playerController.moveSpeed * 0.70f;
        }

        return moveSpeed;
    }

    void ClampToScreen()
    {
        if (!hasEntered) return;
        if (mainCam == null) mainCam = Camera.main;
        if (mainCam == null) return;

        float vertExtent = mainCam.orthographicSize;
        float horzExtent = vertExtent * mainCam.aspect;

        float paddingX = 150f; // Mitad del ancho del jefe (300 / 2)
        if (TryGetComponent<SpriteRenderer>(out var sr) && sr.sprite != null)
        {
            paddingX = Mathf.Max(paddingX, sr.bounds.extents.x);
        }

        float minScreenX = mainCam.transform.position.x - horzExtent + paddingX;
        float maxScreenX = mainCam.transform.position.x + horzExtent - paddingX;

        Vector3 pos = transform.position;
        pos.x = Mathf.Clamp(pos.x, minScreenX, maxScreenX);
        pos.y = targetY; // Fija su altura máxima Y
        transform.position = pos;

        if (pos.x >= maxScreenX)
        {
            moveDirection = -1;
        }
        else if (pos.x <= minScreenX)
        {
            moveDirection = 1;
        }
    }

    // Mantiene el láser sincronizado con el movimiento horizontal del jefe en coordenadas de mundo
    void UpdateLaserPosition()
    {
        float laserCenterY = transform.position.y - 150f - (laserLength * 0.5f);
        Vector3 targetPos = new Vector3(transform.position.x, laserCenterY, 0f);

        if (activeWarningLaser != null)
        {
            activeWarningLaser.transform.position = targetPos;
        }

        if (activeRealLaser != null)
        {
            activeRealLaser.transform.position = targetPos;
        }
    }

    // 1. Balas normales: SOLO desde el lado inferior del cuadrado, cuadriplicadas (12 balas) con autoapuntado inicial hacia el jugador (como Tier 1)
    IEnumerator SmallBulletBottomRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(smallBulletCadence);

            if (bulletPrefab == null) continue;

            if (playerController == null)
            {
                playerController = FindAnyObjectByType<PlayerController>();
            }

            float halfSide = 150f;

            for (int i = 0; i < smallBulletCount; i++)
            {
                float t = (float)i / (smallBulletCount - 1);
                float offX = Mathf.Lerp(-135f, 135f, t);
                Vector3 spawnPos = transform.position + new Vector3(offX, -halfSide, 0f);

                // Autoapuntado hacia la posición actual del jugador al momento del disparo (igual que Tier 1)
                Vector3 dir = Vector3.down;
                if (playerController != null)
                {
                    Vector3 toPlayer = playerController.transform.position - spawnPos;
                    if (toPlayer.sqrMagnitude > 0.001f)
                    {
                        dir = toPlayer.normalized;
                    }
                }

                // Disparo recto sin seguimiento continuo en vuelo (homing = false)
                FireBullet(bulletPrefab, smallBulletSpeed, dir, spawnPos, null, false, null);
            }
        }
    }

    // 2. Balas grandes: generadas cada 1s en las diagonales INFERIORES (sin disparar hacia arriba)
    IEnumerator BigBulletDiagonalRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(bigBulletCadence);

            if (bigBulletPrefab == null) continue;

            float halfSide = 150f;

            Vector3[] cornerOffsets = new Vector3[]
            {
                new Vector3(-halfSide, -halfSide, 0f), // Vértice inferior izquierdo
                new Vector3(halfSide, -halfSide, 0f)   // Vértice inferior derecho
            };

            Vector3[] cornerDirections = new Vector3[]
            {
                new Vector3(-1f, -1f, 0f).normalized, // Hacia la diagonal inferior izquierda
                new Vector3(1f, -1f, 0f).normalized   // Hacia la diagonal inferior derecha
            };

            for (int i = 0; i < cornerOffsets.Length; i++)
            {
                Vector3 spawnPos = transform.position + cornerOffsets[i];
                FireBullet(bigBulletPrefab, bigBulletSpeed, cornerDirections[i], spawnPos, bigBulletLifetime, false, null);
            }
        }
    }

    // 3. Súper Láser: aviso de 3s y rayo real de 44s garantizado sin bugs de duración permanente
    IEnumerator SuperLaserRoutine()
    {
        while (true)
        {
            CleanupLasersOnly();

            float laserCenterY = transform.position.y - 150f - (laserLength * 0.5f);
            Vector3 worldPos = new Vector3(transform.position.x, laserCenterY, 0f);

            // AVISO: Súper Láser rojo y transparente (3 segundos)
            if (telegraphPrefab != null)
            {
                activeWarningLaser = Instantiate(telegraphPrefab, worldPos, Quaternion.identity);
                activeWarningLaser.transform.localScale = new Vector3(laserWidth, laserLength, 1f);
            }
            else
            {
                activeWarningLaser = CreateRuntimeLaserObject("BossLaserWarning_Auto", worldPos, laserWidth, laserLength, new Color(1f, 0.12f, 0.12f, 0.38f), 4);
            }

            // Destrucción programada a nivel de motor para garantizar que jamás quede permanente
            Destroy(activeWarningLaser, laserWarningDuration);

            yield return new WaitForSeconds(laserWarningDuration);

            if (activeWarningLaser != null)
            {
                Destroy(activeWarningLaser);
                activeWarningLaser = null;
            }

            // DISPARO: Súper Láser real blanco y continuo (3 segundos)
            worldPos = new Vector3(transform.position.x, transform.position.y - 150f - (laserLength * 0.5f), 0f);
            if (laserPrefab != null)
            {
                activeRealLaser = Instantiate(laserPrefab, worldPos, Quaternion.identity);
                activeRealLaser.transform.localScale = new Vector3(laserWidth, laserLength, 1f);

                LaserBeam lb = activeRealLaser.GetComponent<LaserBeam>();
                if (lb != null)
                {
                    lb.activeDuration = laserActiveDuration;
                    lb.autoDestroy = true; // Auto-destrucción garantizada tanto por timer como por Start
                }

                if (SoundController.Instance != null)
                {
                    SoundController.Instance.PlayBossLaserSfx();
                }
            }
            else
            {
                activeRealLaser = CreateRuntimeLaserObject("BossRealLaser_Auto", worldPos, laserWidth, laserLength, Color.white, 5, true);

                if (SoundController.Instance != null)
                {
                    SoundController.Instance.PlayBossLaserSfx();
                }
            }

            // Destrucción de seguridad programada en el motor
            Destroy(activeRealLaser, laserActiveDuration);

            yield return new WaitForSeconds(laserActiveDuration);

            if (activeRealLaser != null)
            {
                Destroy(activeRealLaser);
                activeRealLaser = null;
            }

            // Pausa de 5 segundos entre ciclos de láser
            yield return new WaitForSeconds(5f);
        }
    }

    // 4. Aparición de Tier 4 durante la batalla del jefe: grupos de 2 cada 7 segundos
    IEnumerator SpawnTier4MinionsRoutine()
    {
        yield return new WaitForSeconds(7f); // Primer grupo a los 7 segundos de combate

        while (true)
        {
            if (tier4Prefab != null)
            {
                for (int i = 0; i < 2; i++)
                {
                    SpawnOneTier4Minion();
                    yield return new WaitForSeconds(0.4f); // Leve intervalo natural entre ambos, tal como en las oleadas
                }
            }

            yield return new WaitForSeconds(6.6f); // 0.4s + 6.6s = 7 segundos en total por ciclo
        }
    }

    void SpawnOneTier4Minion()
    {
        if (tier4Prefab == null) return;

        // Se instancia arriba de la pantalla; EnemyController se encarga automáticamente
        // de elegir su X sin solaparse y hacerlo descender suavemente hacia settleY (Y = 1380, arriba del jefe y debajo del techo)
        float spawnY = (mainCam != null) ? mainCam.transform.position.y + mainCam.orthographicSize + 200f : 1800f;
        float defaultX = (mainCam != null) ? mainCam.transform.position.x : 957f;
        GameObject minion = Instantiate(tier4Prefab, new Vector3(defaultX, spawnY, 0f), Quaternion.identity);
        spawnedMinions.Add(minion);
    }

    GameObject CreateRuntimeLaserObject(string objName, Vector3 worldPos, float width, float length, Color color, int sortingOrder, bool addDamage = false)
    {
        GameObject obj = new GameObject(objName);
        obj.transform.position = worldPos;
        obj.transform.rotation = Quaternion.identity;
        obj.transform.localScale = new Vector3(width, length, 1f);

        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
        if (bulletPrefab != null && bulletPrefab.TryGetComponent<SpriteRenderer>(out var bulletSr))
        {
            sr.sprite = bulletSr.sprite;
            sr.material = bulletSr.sharedMaterial;
        }
        else if (TryGetComponent<SpriteRenderer>(out var mySr))
        {
            sr.sprite = mySr.sprite;
            sr.material = mySr.sharedMaterial;
        }
        sr.color = color;
        sr.sortingOrder = sortingOrder;

        if (addDamage)
        {
            BoxCollider2D col = obj.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = Vector2.one;

            LaserBeam lb = obj.AddComponent<LaserBeam>();
            lb.damage = 2;
            lb.autoDestroy = false;
        }

        return obj;
    }

    void FireBullet(GameObject prefab, float speed, Vector3 dir, Vector3 worldSpawnPos, float? customLifetime = null, bool homing = false, Transform target = null)
    {
        if (prefab == null) return;

        Quaternion rot = Quaternion.identity;
        if (dir.sqrMagnitude > 0.001f)
        {
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + 90f;
            rot = Quaternion.Euler(0f, 0f, angle);
        }

        GameObject obj = Instantiate(prefab, worldSpawnPos, rot);
        EnemyBullet eb = obj.GetComponent<EnemyBullet>();
        if (eb != null)
        {
            eb.speed = speed;
            eb.direction = dir;
            if (customLifetime.HasValue)
            {
                eb.lifeTime = customLifetime.Value;
            }
            if (homing && target != null)
            {
                eb.EnableHoming(target, homingTurnSpeed);
            }
            eb.AlignRotationWithDirection();
        }
    }

    public void TakeDamage(int amount)
    {
        TakeDamage((float)amount);
    }

    public void TakeDamage(float amount)
    {
        if (Time.timeScale == 0f) return;

        currentHealth -= amount;

        if (currentHealth <= 0.001f)
        {
            Die();
        }
    }

    public bool IsDead()
    {
        return currentHealth <= 0.001f;
    }

    void Die()
    {
        CleanupAll();

        if (SoundController.Instance != null)
        {
            SoundController.Instance.PlayBossDeathSfx();
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnBossDefeated(scoreValue);
        }

        Destroy(gameObject);
    }

    void OnDisable()
    {
        CleanupAll();
    }

    void OnDestroy()
    {
        CleanupAll();
    }

    void CleanupLasersOnly()
    {
        if (activeWarningLaser != null)
        {
            Destroy(activeWarningLaser);
            activeWarningLaser = null;
        }

        if (activeRealLaser != null)
        {
            Destroy(activeRealLaser);
            activeRealLaser = null;
        }
    }

    void CleanupAll()
    {
        CleanupLasersOnly();

        foreach (var m in spawnedMinions)
        {
            if (m != null) Destroy(m);
        }
        spawnedMinions.Clear();
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        TryDealContactDamage(collision.gameObject);
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        TryDealContactDamage(collision.gameObject);
    }

    void TryDealContactDamage(GameObject other)
    {
        if (Time.timeScale == 0f) return;
        if (!other.CompareTag("Player")) return;
        if (Time.time < nextContactTime) return;

        PlayerController player = other.GetComponent<PlayerController>();
        if (player != null)
        {
            player.TakeDamage(contactDamage);
            nextContactTime = Time.time + contactCooldown;
        }
    }
}