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
    public int maxHealth = 500;
    private float currentHealth;
    private bool isDead = false;

    [Header("Movimiento")]
    public float moveSpeed = 367.5f; // Triple de velocidad (122.5f * 3)
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
    public float smallBulletCadence = 0.51f; // -50% tiempo de generación de balas (doble de cadencia)
    public float smallBulletSpeed = 460f; // +15% velocidad de bala normal (400 * 1.15)
    public int smallBulletCount = 12;
    public float bigBulletCadence = 0.5f; // -50% tiempo de generación de balas (doble de cadencia)
    public float bigBulletSpeed = 288f;
    public float bigBulletLifetime = 40f;

    [Header("Súper Láser")]
    public float laserWarningDuration = 2f; // -1/3 de duración (3s * 2/3 = 2s)
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
    private bool hasAnnouncedEntry = false;

    private Vector3 baseScale;
    private float bossAnimPhase;
    private float bankAmount;
    private float recoilTimer;
    private float recoilStrength;
    private int recoilAlternation = 1;
    private float laserChargeProgress;
    private bool isFiringLaser;
    private SpriteRenderer vfxAuraGlowSr;
    private Transform vfxOuterVortexTr;
    private SpriteRenderer vfxOuterVortexSr;
    private Transform vfxInnerVortexTr;
    private SpriteRenderer vfxInnerVortexSr;
    private float baseAuraScale;
    private float baseOuterVortexScale;
    private float baseInnerVortexScale;

    void Start()
    {
        if (maxHealth <= 250) maxHealth = 500;
        if (Mathf.Approximately(moveSpeed, 175f) || Mathf.Approximately(moveSpeed, 122.5f)) moveSpeed = 367.5f;

        if (GameManager.Instance != null && GameManager.Instance.IsPlayer2Joined)
            maxHealth = Mathf.Max(1, Mathf.RoundToInt(maxHealth * 1.9f));

        // +8% de generación de balas para todos los enemigos (multiplica los tiempos de recarga por 1 / 1.08)
        // y -50% de reducción de tiempo de generación para el jefe (doble de producción de balas)
        const float bulletRateBoost = 1f / 1.08f;
        smallBulletCadence *= (1.3225f * bulletRateBoost) * 0.5f;
        bigBulletCadence *= (1.3225f * bulletRateBoost) * 0.5f;
        laserWarningDuration = 2f; // -1/3 de duración de la marca previa (3s * 2/3 = 2s)
        if (Mathf.Approximately(smallBulletSpeed, 400f) || Mathf.Approximately(smallBulletSpeed, 280f)) smallBulletSpeed = 460f;

        currentHealth = maxHealth;
        mainCam = Camera.main;
        playerController = FindAnyObjectByType<PlayerController>();

        if (mainCam != null)
        {
            float screenTop = mainCam.transform.position.y + mainCam.orthographicSize + 250f;
            transform.position = new Vector3(transform.position.x, Mathf.Max(transform.position.y, screenTop), transform.position.z);
        }

        InitBossAnimation();
        GameManager.Instance?.hudManager?.ActivateBossHealthBar("Leviatán nodriza", maxHealth);
    }

    void Update()
    {
        if (Time.timeScale == 0f) return;

        if (!hasEntered)
        {
            transform.position += Vector3.down * entrySpeed * Time.deltaTime;
            if (!hasAnnouncedEntry && mainCam != null && transform.position.y <= mainCam.transform.position.y + mainCam.orthographicSize)
            {
                hasAnnouncedEntry = true;
                SoundController.Instance?.PlayBossEntrySfx();
            }
            if (transform.position.y <= targetY)
            {
                transform.position = new Vector3(transform.position.x, targetY, transform.position.z);
                hasEntered = true;
                if (!hasAnnouncedEntry)
                {
                    hasAnnouncedEntry = true;
                    SoundController.Instance?.PlayBossEntrySfx();
                }
                StartCoroutine(SmallBulletBottomRoutine());
                StartCoroutine(BigBulletDiagonalRoutine());
                StartCoroutine(SuperLaserRoutine());
                StartCoroutine(SpawnTier4MinionsRoutine());
            }
        }
        else
        {
            float baseSpeed = moveSpeed; // Desacoplado: NO se incrementa cuando el jugador mejora su velocidad
            float rageMult = (currentHealth <= maxHealth * 0.5f) ? 1.32f : 1f;
            transform.position += Vector3.right * moveDirection * (baseSpeed * rageMult) * Time.deltaTime;
        }

        UpdateBossAnimation();
        UpdateBossVfx();
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
            TriggerRecoil(0.55f);
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
            TriggerRecoil(0.95f);
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

            float warnElapsed = 0f;
            while (warnElapsed < laserWarningDuration)
            {
                warnElapsed += Time.deltaTime;
                laserChargeProgress = Mathf.Clamp01(warnElapsed / Mathf.Max(0.01f, laserWarningDuration));
                yield return null;
            }
            laserChargeProgress = 0f;
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
            isFiringLaser = true;
            TriggerRecoil(1.45f);
            Destroy(activeRealLaser, laserActiveDuration);

            yield return new WaitForSeconds(laserActiveDuration);
            isFiringLaser = false;
            DestroySafe(ref activeRealLaser);

            float cycleCooldown = Mathf.Max(5.75f, (laserWarningDuration + laserActiveDuration + 5f) * 1.15f - laserWarningDuration - laserActiveDuration);
            yield return new WaitForSeconds(cycleCooldown);
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
            eb.isBigBullet = (prefab == bigBulletPrefab);
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
        if (currentHealth <= 0.001f)
        {
            Die();
            return;
        }
        SpriteHitFlash.TriggerOn(gameObject);
    }

    public void DoubleHealth()
    {
        maxHealth = Mathf.Max(1, Mathf.RoundToInt(maxHealth * 1.9f));
        currentHealth *= 1.9f;
        GameManager.Instance?.hudManager?.ActivateBossHealthBar("Leviatán nodriza", maxHealth);
        GameManager.Instance?.hudManager?.UpdateBossHealth(currentHealth);
    }

    public bool IsDead() => isDead || currentHealth <= 0.001f;
    public bool IsEnraged => !isDead && currentHealth <= maxHealth * 0.5f;
    public bool IsLaserActive => !isDead && isFiringLaser;

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
        EnemyExplosion.SpawnBossSequence(transform.position, baseScale, mat);
        Destroy(gameObject);
    }

    private void InitBossAnimation()
    {
        baseScale = transform.localScale;
        bossAnimPhase = UnityEngine.Random.Range(0f, Mathf.PI * 2f);

        SpriteRenderer parentSr = GetComponent<SpriteRenderer>();
        float bossSpan = (parentSr != null && parentSr.sprite != null)
            ? Mathf.Max(parentSr.sprite.bounds.size.x, parentSr.sprite.bounds.size.y)
            : 1f;

        Sprite glowSprite = ChargeVisualUtils.GetGlowCircleSprite();
        if (glowSprite != null)
        {
            vfxAuraGlowSr = CreateBossVfxChild("Vfx_BossAuraGlow", Vector3.zero, glowSprite, parentSr, -1, 1.24f, bossSpan, new Color(0.90f, 0.18f, 0.28f, 0.22f), out baseAuraScale);
        }

        Sprite vortexSprite = ChargeVisualUtils.GetEnergyVortexSprite();
        if (vfxSpriteExists(vortexSprite))
        {
            Vector3 coreOffset = new Vector3(0f, -bossSpan * 0.08f, 0f);
            vfxOuterVortexSr = CreateBossVfxChild("Vfx_BossOuterVortex", coreOffset, vortexSprite, parentSr, 1, 0.44f, bossSpan, new Color(1f, 0.32f, 0.38f, 0.36f), out baseOuterVortexScale);
            vfxOuterVortexTr = vfxOuterVortexSr.transform;

            vfxInnerVortexSr = CreateBossVfxChild("Vfx_BossInnerVortex", coreOffset, vortexSprite, parentSr, 2, 0.24f, bossSpan, new Color(0.55f, 0.90f, 1f, 0.52f), out baseInnerVortexScale);
            vfxInnerVortexTr = vfxInnerVortexSr.transform;
        }
    }

    private static bool vfxSpriteExists(Sprite sp) => sp != null;

    private SpriteRenderer CreateBossVfxChild(string name, Vector3 localPos, Sprite sprite, SpriteRenderer parentSr, int orderOffset, float ratioOfBossSpan, float bossSpan, Color color, out float computedScale)
    {
        float spSpan = Mathf.Max(0.001f, Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y));
        computedScale = ratioOfBossSpan * (bossSpan / spSpan);

        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = Vector3.one * computedScale;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        if (parentSr != null)
        {
            sr.sortingLayerID = parentSr.sortingLayerID;
            sr.sortingOrder = parentSr.sortingOrder + orderOffset;
        }
        sr.color = color;
        return sr;
    }

    private void TriggerRecoil(float strength)
    {
        recoilTimer = 0.16f;
        recoilStrength = Mathf.Max(recoilStrength, strength);
        recoilAlternation = -recoilAlternation;
    }

    private void UpdateBossAnimation()
    {
        float bankInput = hasEntered ? moveDirection : 0f;
        ChargeVisualUtils.EvalShipBank(ref bankAmount, bankInput, Time.deltaTime, 9.5f, 0.14f, out _, out float bankScaleX);

        if (recoilTimer > 0f)
        {
            recoilTimer = Mathf.Max(0f, recoilTimer - Time.deltaTime);
            if (recoilTimer <= 0f) recoilStrength = 0f;
        }

        // Mantener la nave nodriza recta hacia abajo (sin rotaciones diagonales)
        transform.localRotation = Quaternion.identity;
        transform.localScale = new Vector3(
            baseScale.x * bankScaleX,
            baseScale.y,
            baseScale.z);
    }

    private void UpdateBossVfx()
    {
        bool enraged = currentHealth <= maxHealth * 0.5f;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * (enraged ? 6.5f : 3.6f) + bossAnimPhase);
        float flare = Mathf.Max(laserChargeProgress, isFiringLaser ? 1f : 0f);

        if (vfxAuraGlowSr != null)
        {
            vfxAuraGlowSr.transform.localScale = Vector3.one * (baseAuraScale * (1f + pulse * 0.08f + flare * 0.16f));
            Color calm = new Color(0.88f, 0.18f, 0.28f, 0.20f + pulse * 0.11f);
            Color angry = new Color(1f, 0.28f, 0.18f, 0.42f + pulse * 0.16f);
            vfxAuraGlowSr.color = Color.Lerp(calm, angry, enraged ? Mathf.Max(0.45f, flare) : flare);
        }

        if (vfxOuterVortexTr != null && vfxOuterVortexSr != null)
        {
            float spinSpeed = 75f + (enraged ? 55f : 0f) + flare * 260f;
            vfxOuterVortexTr.Rotate(0f, 0f, -spinSpeed * Time.deltaTime);
            vfxOuterVortexTr.localScale = Vector3.one * (baseOuterVortexScale * (1f + pulse * 0.12f + flare * 0.28f));
            Color baseCol = enraged ? new Color(1f, 0.28f, 0.22f, 0.48f) : new Color(0.98f, 0.30f, 0.42f, 0.36f);
            Color hotCol = new Color(1f, 0.55f, 0.25f, 0.72f);
            vfxOuterVortexSr.color = Color.Lerp(baseCol, hotCol, flare);
        }

        if (vfxInnerVortexTr != null && vfxInnerVortexSr != null)
        {
            float spinSpeed = 125f + (enraged ? 85f : 0f) + flare * 380f;
            vfxInnerVortexTr.Rotate(0f, 0f, spinSpeed * Time.deltaTime);
            vfxInnerVortexTr.localScale = Vector3.one * (baseInnerVortexScale * (1f + (1f - pulse) * 0.14f + flare * 0.34f));
            Color baseCol = new Color(0.50f, 0.88f, 1f, 0.45f + pulse * 0.15f);
            Color beamCol = new Color(0.85f, 0.96f, 1f, 0.90f);
            vfxInnerVortexSr.color = Color.Lerp(baseCol, beamCol, isFiringLaser ? 1f : laserChargeProgress * 0.75f);
        }
    }

    void OnDisable()
    {
        laserChargeProgress = 0f;
        isFiringLaser = false;
        CleanupAll();
    }
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
            bool absorbed = player.TakeShieldDamage(25f);
            if (!absorbed)
            {
                player.TakeDamage(contactDamage);
            }
            nextContactTime = Time.time + contactCooldown;
        }
    }
}