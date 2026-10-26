using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Controlador del Jefe Final (Leviatán nodriza).
/// Gestiona movimiento de patrulla horizontal, 3 patrones de ataque y la barra de salud del HUD.
/// </summary>
public class BossController : MonoBehaviour, IDamageable
{
    [Header("Vida")]
    public int maxHealth = 250;
    private float currentHealth;
    private bool isDead = false;

    [Header("Movimiento")]
    public float moveSpeed = 175f;
    public float targetY = 1100f;
    public float entrySpeed = 150f;
    private bool hasEntered = false;
    private int moveDirection = 1;

    [Header("Prefabs de Ataque")]
    public GameObject bulletPrefab;
    public GameObject bigBulletPrefab;
    public GameObject laserPrefab;
    public GameObject telegraphPrefab;
    public GameObject tier4Prefab;

    [Header("Parámetros de Proyectiles")]
    public float smallBulletCadence = 1.02f;
    public float smallBulletSpeed = 460f; // +15% velocidad de bala normal (400 * 1.15)
    public int smallBulletCount = 12;
    public float bigBulletCadence = 1.0f;
    public float bigBulletSpeed = 288f;
    public float bigBulletLifetime = 40f;

    [Header("Súper Láser")]
    public float laserWarningDuration = 3f;
    public float laserActiveDuration = 3f;
    public float laserWidth = 150f;
    public float laserLength = 3500f;

    [Header("Daño por contacto")]
    public int contactDamage = 2;
    public float contactCooldown = 1f;
    private float nextContactTime = 0f;
    public int scoreValue = 10;

    private Camera mainCam;
    private PlayerController playerController;
    private GameObject activeWarningLaser;
    private GameObject activeRealLaser;
    private List<GameObject> spawnedMinions = new List<GameObject>();

    void Start()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsPlayer2Joined)
            maxHealth = Mathf.Max(1, Mathf.RoundToInt(maxHealth * 1.9f));

        smallBulletCadence *= 1.15f;
        bigBulletCadence *= 1.15f;
        if (Mathf.Approximately(smallBulletSpeed, 400f) || Mathf.Approximately(smallBulletSpeed, 280f)) smallBulletSpeed = 460f;

        currentHealth = maxHealth;
        mainCam = Camera.main;
        playerController = FindAnyObjectByType<PlayerController>();

        if (mainCam != null)
        {
            float screenTop = mainCam.transform.position.y + mainCam.orthographicSize + 250f;
            transform.position = new Vector3(transform.position.x, Mathf.Max(transform.position.y, screenTop), transform.position.z);
        }

        GameManager.Instance?.hudManager?.ActivateBossHealthBar("Leviatán nodriza", maxHealth);
    }

    void Update()
    {
        if (Time.timeScale == 0f) return;

        if (!hasEntered)
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
        else
        {
            float speed = (playerController != null ? playerController.moveSpeed * 0.70f : moveSpeed);
            transform.position += Vector3.right * moveDirection * speed * Time.deltaTime;
        }
    }

    void LateUpdate()
    {
        if (Time.timeScale == 0f) return;
        ClampToScreen();
        UpdateLaserPosition();
    }

    void ClampToScreen()
    {
        if (!hasEntered || mainCam == null) return;

        float horzExtent = mainCam.orthographicSize * mainCam.aspect;
        float paddingX = 150f;
        if (TryGetComponent<SpriteRenderer>(out var sr) && sr.sprite != null)
            paddingX = Mathf.Max(paddingX, sr.bounds.extents.x);

        float minX = mainCam.transform.position.x - horzExtent + paddingX;
        float maxX = mainCam.transform.position.x + horzExtent - paddingX;

        Vector3 pos = transform.position;
        pos.x = Mathf.Clamp(pos.x, minX, maxX);
        pos.y = targetY;
        transform.position = pos;

        if (pos.x >= maxX) moveDirection = -1;
        else if (pos.x <= minX) moveDirection = 1;
    }

    void UpdateLaserPosition()
    {
        Vector3 targetPos = new Vector3(transform.position.x, transform.position.y - 150f - (laserLength * 0.5f), 0f);
        if (activeWarningLaser != null) activeWarningLaser.transform.position = targetPos;
        if (activeRealLaser != null) activeRealLaser.transform.position = targetPos;
    }

    // Ataque 1: Ráfaga en abanico inferior apuntando al jugador
    IEnumerator SmallBulletBottomRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(smallBulletCadence);
            if (bulletPrefab == null) continue;

            if (playerController == null) playerController = FindAnyObjectByType<PlayerController>();

            for (int i = 0; i < smallBulletCount; i++)
            {
                float t = (float)i / (smallBulletCount - 1);
                Vector3 spawnPos = transform.position + new Vector3(Mathf.Lerp(-135f, 135f, t), -150f, 0f);
                Vector3 dir = (playerController != null && (playerController.transform.position - spawnPos).sqrMagnitude > 0.001f)
                    ? (playerController.transform.position - spawnPos).normalized
                    : Vector3.down;

                FireBullet(bulletPrefab, smallBulletSpeed, dir, spawnPos);
            }
        }
    }

    // Ataque 2: Balas gigantes en las diagonales inferiores
    IEnumerator BigBulletDiagonalRoutine()
    {
        Vector3[] offsets = { new Vector3(-150f, -150f, 0f), new Vector3(150f, -150f, 0f) };
        Vector3[] dirs = { new Vector3(-1f, -1f, 0f).normalized, new Vector3(1f, -1f, 0f).normalized };

        while (true)
        {
            yield return new WaitForSeconds(bigBulletCadence);
            if (bigBulletPrefab == null) continue;

            for (int i = 0; i < offsets.Length; i++)
                FireBullet(bigBulletPrefab, bigBulletSpeed, dirs[i], transform.position + offsets[i], bigBulletLifetime);
        }
    }

    // Ataque 3: Súper Láser con aviso previo
    IEnumerator SuperLaserRoutine()
    {
        while (true)
        {
            CleanupLasersOnly();
            Vector3 worldPos = new Vector3(transform.position.x, transform.position.y - 150f - (laserLength * 0.5f), 0f);

            // Aviso rojo
            activeWarningLaser = telegraphPrefab != null
                ? Instantiate(telegraphPrefab, worldPos, Quaternion.identity)
                : CreateRuntimeLaser("BossLaserWarning_Auto", worldPos, new Color(1f, 0.12f, 0.12f, 0.38f), 4);
            activeWarningLaser.transform.localScale = new Vector3(laserWidth, laserLength, 1f);
            Destroy(activeWarningLaser, laserWarningDuration);

            yield return new WaitForSeconds(laserWarningDuration);
            DestroySafe(ref activeWarningLaser);

            // Láser destructivo
            worldPos.x = transform.position.x;
            activeRealLaser = laserPrefab != null
                ? Instantiate(laserPrefab, worldPos, Quaternion.identity)
                : CreateRuntimeLaser("BossRealLaser_Auto", worldPos, Color.white, 5, true);
            activeRealLaser.transform.localScale = new Vector3(laserWidth, laserLength, 1f);

            if (activeRealLaser.TryGetComponent<LaserBeam>(out var lb))
            {
                lb.activeDuration = laserActiveDuration;
                lb.autoDestroy = true;
            }

            SoundController.Instance?.PlayBossLaserSfx();
            Destroy(activeRealLaser, laserActiveDuration);

            yield return new WaitForSeconds(laserActiveDuration);
            DestroySafe(ref activeRealLaser);

            yield return new WaitForSeconds(5f);
        }
    }

    // Ataque 4: Incursión periódica de esbirros Tier 4
    IEnumerator SpawnTier4MinionsRoutine()
    {
        yield return new WaitForSeconds(7f);
        while (true)
        {
            if (tier4Prefab != null)
            {
                for (int i = 0; i < 2; i++)
                {
                    float spawnY = (mainCam != null) ? mainCam.transform.position.y + mainCam.orthographicSize + 200f : 1800f;
                    float spawnX = (mainCam != null) ? mainCam.transform.position.x : 957f;
                    spawnedMinions.Add(Instantiate(tier4Prefab, new Vector3(spawnX, spawnY, 0f), Quaternion.identity));
                    yield return new WaitForSeconds(0.4f);
                }
            }
            yield return new WaitForSeconds(6.6f);
        }
    }

    private GameObject CreateRuntimeLaser(string name, Vector3 pos, Color color, int order, bool damage = false)
    {
        GameObject obj = new GameObject(name);
        obj.transform.position = pos;
        obj.transform.localScale = new Vector3(laserWidth, laserLength, 1f);

        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
        if (bulletPrefab != null && bulletPrefab.TryGetComponent<SpriteRenderer>(out var bSr))
        {
            sr.sprite = bSr.sprite;
            sr.material = bSr.sharedMaterial;
        }
        sr.color = color;
        sr.sortingOrder = order;

        if (damage)
        {
            BoxCollider2D col = obj.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = Vector2.one;
            LaserBeam lb = obj.AddComponent<LaserBeam>();
            lb.damage = 2;
        }
        return obj;
    }

    private void FireBullet(GameObject prefab, float speed, Vector3 dir, Vector3 spawnPos, float? lifetime = null)
    {
        if (prefab == null) return;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + 90f;
        GameObject obj = Instantiate(prefab, spawnPos, Quaternion.Euler(0f, 0f, angle));

        if (obj.TryGetComponent<EnemyBullet>(out var eb))
        {
            eb.speed = speed;
            eb.direction = dir;
            if (lifetime.HasValue) eb.lifeTime = lifetime.Value;
            eb.AlignRotationWithDirection();
        }
    }

    public void TakeDamage(int amount) => TakeDamage((float)amount);

    public void TakeDamage(float amount)
    {
        if (Time.timeScale == 0f || isDead) return;
        currentHealth -= amount;
        GameManager.Instance?.hudManager?.UpdateBossHealth(currentHealth);
        if (currentHealth <= 0.001f) Die();
    }

    public void DoubleHealth()
    {
        maxHealth = Mathf.Max(1, Mathf.RoundToInt(maxHealth * 1.9f));
        currentHealth *= 1.9f;
        GameManager.Instance?.hudManager?.ActivateBossHealthBar("Leviatán nodriza", maxHealth);
        GameManager.Instance?.hudManager?.UpdateBossHealth(currentHealth);
    }

    public bool IsDead() => isDead || currentHealth <= 0.001f;

    void Die()
    {
        if (isDead) return;
        isDead = true;

        CleanupAll();
        GameManager.Instance?.hudManager?.OnBossDefeated();
        SoundController.Instance?.StopMusic();
        SoundController.Instance?.PlayBossDeathSfx();
        GameManager.Instance?.OnBossDefeated(scoreValue);

        Material mat = TryGetComponent<SpriteRenderer>(out var sr) ? sr.sharedMaterial : null;
        EnemyExplosion.SpawnBossSequence(transform.position, transform.localScale, mat);
        Destroy(gameObject);
    }

    void OnDisable() => CleanupAll();
    void OnDestroy() => CleanupAll();

    void CleanupLasersOnly()
    {
        DestroySafe(ref activeWarningLaser);
        DestroySafe(ref activeRealLaser);
    }

    void CleanupAll()
    {
        CleanupLasersOnly();
        foreach (var m in spawnedMinions) if (m != null) Destroy(m);
        spawnedMinions.Clear();
    }

    private void DestroySafe(ref GameObject obj)
    {
        if (obj != null) { Destroy(obj); obj = null; }
    }

    void OnCollisionEnter2D(Collision2D collision) => TryDealContactDamage(collision.gameObject);
    void OnCollisionStay2D(Collision2D collision) => TryDealContactDamage(collision.gameObject);

    void TryDealContactDamage(GameObject other)
    {
        if (Time.timeScale == 0f || !other.CompareTag("Player") || Time.time < nextContactTime) return;
        if (other.TryGetComponent<PlayerController>(out var player))
        {
            player.TakeDamage(contactDamage);
            nextContactTime = Time.time + contactCooldown;
        }
    }
}