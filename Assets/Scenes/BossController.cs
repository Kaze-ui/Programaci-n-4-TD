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
    public float targetY = 1240f; // Elevado para dar más margen inferior y reducir camping superior
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
    public float smallBulletSpeed = 345f; // -25% velocidad bala normal (460 * 0.75)
    public int smallBulletCount = 12;
    public float bigBulletCadence = 5f; // Disparo cada 5 segundos exactos
    public float bossClusterFragmentSpeed = 180f; // Velocidad reducida de las balitas al explotar
    public float bigBulletSpeed = 288f;
    public float bigBulletLifetime = 40f;

    [Header("Súper Láser")]
    public float laserWarningDuration = 2f; // -1/3 de duración (3s * 2/3 = 2s)
    public float laserActiveDuration = 3f;
    public float laserWidth = 150f;
    public float laserLength = 5250f; // 50% más largo (3500 * 1.5 = 5250) para cubrir completamente la pantalla en diagonal

    [Header("Fase 2 (A partir de 250 HP)")]
    public float phase2HealthThreshold = 250f;
    public float phase2DamageReduction = 0.30f; // 30% de reducción de daño en Fase 2
    public float phase2HealFractionOfLost = 0.20f; // Cura el 20% de la vida perdida en Fase 1
    public float hyperBeamWarningDuration = 3.0f; // 3 segundos de aviso rojo antes del hiper rayo
    public float hyperBeamActiveDuration = 2.8f;
    public float phase2RetreatSpeed = 680f;
    public float phase2SweepLaserDuration = 5.6f; // Barrido lento de 180° de lado a lado en Fase 2

    [Header("Daño por contacto")]
    public int contactDamage = 2;
    public float contactCooldown = 1f;
    private float nextContactTime = 0f;
    public int scoreValue = 10;

    private Camera mainCam;
    private PlayerController playerController;
    private GameObject activeWarningLaser;
    private GameObject activeRealLaser;
    private GameObject hyperWarningLeftEdge;
    private GameObject hyperWarningRightEdge;
    private GameObject hyperBeamCoreVisual;
    private GameObject hyperBeamGlowVisual;
    private List<GameObject> spawnedMinions = new List<GameObject>();
    private bool hasAnnouncedEntry = false;

    private bool isPhase2 = false;
    private bool isTransitioningPhase2 = false;
    private Coroutine smallBulletCoroutine;
    private Coroutine bigBulletCoroutine;
    private Coroutine superLaserCoroutine;
    private Coroutine phase2PulseCoroutine;
    private Coroutine tier4MinionsCoroutine;
    private Coroutine phase1MinionsCoroutine;
    private Coroutine phase2TransitionCoroutine;

    private Vector3 baseScale;
    private float bossAnimPhase;
    private float bankAmount;
    private float recoilTimer;
    private float recoilStrength;
    private int recoilAlternation = 1;
    private float laserChargeProgress;
    private bool isFiringLaser;
    private float currentLaserAngle = 0f; // 0° = recto hacia abajo; +90° = derecha horizontal; -90° = izquierda horizontal
    private bool isSweepingLaser = false;
    private SpriteRenderer vfxAuraGlowSr;
    private Transform vfxOuterVortexTr;
    private SpriteRenderer vfxOuterVortexSr;
    private Transform vfxInnerVortexTr;
    private SpriteRenderer vfxInnerVortexSr;
    private SpriteRenderer vfxPhase2ShieldSr;
    private Transform vfxPhase2ShieldRingTr;
    private SpriteRenderer vfxPhase2ShieldRingSr;
    private float baseAuraScale;
    private float baseOuterVortexScale;
    private float baseInnerVortexScale;
    private float basePhase2ShieldScale;
    private float basePhase2ShieldRingScale;
    private float shieldHitFlashTimer = 0f;
    private static Sprite cachedBossShieldRingSprite;

    void Start()
    {
        bool isCoop = GameManager.Instance != null && GameManager.Instance.IsPlayer2Joined;
        maxHealth = isCoop ? 1000 : 500;
        if (Mathf.Approximately(moveSpeed, 175f) || Mathf.Approximately(moveSpeed, 122.5f)) moveSpeed = 367.5f;

        // +8% de generación de balas para todos los enemigos (multiplica los tiempos de recarga por 1 / 1.08)
        // y -50% de reducción de tiempo de generación para el jefe (doble de producción de balas)
        // y -15% de reducción de velocidad de generación de balas para todos los enemigos incluyendo el jefe
        const float bulletRateBoost = 1f / 1.08f;
        const float bulletGenReduction = 1.15f;
        smallBulletCadence *= (1.3225f * bulletRateBoost) * 0.5f * bulletGenReduction;
        bigBulletCadence = 5f * bulletGenReduction; // Disparo de balas grandes cada 5.75 segundos (-15% velocidad de generación)
        laserWarningDuration = 2f; // -1/3 de duración de la marca previa (3s * 2/3 = 2s)
        laserLength = 5250f; // 50% más largo (3500 * 1.5 = 5250) para cubrir completamente la pantalla en disparos diagonales
        if (Mathf.Approximately(smallBulletSpeed, 400f) || Mathf.Approximately(smallBulletSpeed, 280f) || Mathf.Approximately(smallBulletSpeed, 460f)) smallBulletSpeed = 345f;
        else smallBulletSpeed *= 0.75f;

        currentHealth = maxHealth;
        mainCam = Camera.main;
        playerController = FindAnyObjectByType<PlayerController>();

        if (Mathf.Approximately(targetY, 1100f) || targetY < 1200f)
        {
            targetY = 1240f;
        }

        if (mainCam != null)
        {
            float screenTop = mainCam.transform.position.y + mainCam.orthographicSize + 250f;
            transform.position = new Vector3(transform.position.x, Mathf.Max(transform.position.y, screenTop), transform.position.z);
        }

        InitBossAnimation();
        GameManager.Instance?.hudManager?.ActivateBossHealthBar("Leviatán nodriza", maxHealth);
        GameManager.Instance?.hudManager?.SetBossInvulnerable(true);
    }

    void Update()
    {
        if (Time.timeScale == 0f || isDead) return;

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
                GameManager.Instance?.hudManager?.SetBossInvulnerable(false);
                if (!hasAnnouncedEntry)
                {
                    hasAnnouncedEntry = true;
                    SoundController.Instance?.PlayBossEntrySfx();
                }
                // Fase 1: NO invoca Tier 4 todavía (solo sus ataques principales)
                StartNormalAttackRoutines();
            }
        }
        else if (!isTransitioningPhase2)
        {
            float baseSpeed = moveSpeed; // Desacoplado: NO se incrementa cuando el jugador mejora su velocidad
            float rageMult = (isPhase2 || currentHealth <= maxHealth * 0.5f) ? 1.32f : 1f;
            float sweepSlowdown = isSweepingLaser ? 0.42f : 1f;
            transform.position += Vector3.right * moveDirection * (baseSpeed * rageMult * sweepSlowdown) * Time.deltaTime;

            // Garantiza que las corrutinas de ataque (incluidos los láseres y pulsaciones de Fase 2) sigan activas en Fase 1 y Fase 2
            if (!isDead)
            {
                if (smallBulletCoroutine == null) smallBulletCoroutine = StartCoroutine(SmallBulletBottomRoutine());
                if (bigBulletCoroutine == null) bigBulletCoroutine = StartCoroutine(BigBulletDiagonalRoutine());
                if (superLaserCoroutine == null) superLaserCoroutine = StartCoroutine(SuperLaserRoutine());
                if (isPhase2 && phase2PulseCoroutine == null) phase2PulseCoroutine = StartCoroutine(Phase2PulseRoutine());
            }
        }

        UpdateBossAnimation();
        UpdateBossVfx();
    }

    private void StartNormalAttackRoutines()
    {
        StopNormalAttackRoutines();
        smallBulletCoroutine = StartCoroutine(SmallBulletBottomRoutine());
        bigBulletCoroutine = StartCoroutine(BigBulletDiagonalRoutine());
        superLaserCoroutine = StartCoroutine(SuperLaserRoutine());
        if (isPhase2) phase2PulseCoroutine = StartCoroutine(Phase2PulseRoutine());
        if (!isPhase2 && phase1MinionsCoroutine == null) phase1MinionsCoroutine = StartCoroutine(SpawnPhase1MinionsRoutine());
    }

    private void StopNormalAttackRoutines()
    {
        if (smallBulletCoroutine != null) { StopCoroutine(smallBulletCoroutine); smallBulletCoroutine = null; }
        if (bigBulletCoroutine != null) { StopCoroutine(bigBulletCoroutine); bigBulletCoroutine = null; }
        if (superLaserCoroutine != null) { StopCoroutine(superLaserCoroutine); superLaserCoroutine = null; }
        if (phase2PulseCoroutine != null) { StopCoroutine(phase2PulseCoroutine); phase2PulseCoroutine = null; }
        if (phase1MinionsCoroutine != null) { StopCoroutine(phase1MinionsCoroutine); phase1MinionsCoroutine = null; }
    }

    void LateUpdate()
    {
        if (Time.timeScale == 0f || isDead) return;
        if (!isTransitioningPhase2)
        {
            ClampToScreen();
            UpdateLaserPosition();
        }
    }

    void ClampToScreen()
    {
        if (!hasEntered || isTransitioningPhase2 || isDead || mainCam == null) return;

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
        if (isTransitioningPhase2) return;
        Vector3 origin = transform.position + new Vector3(0f, -150f, 0f);
        Quaternion rot = Quaternion.Euler(0f, 0f, currentLaserAngle);
        Vector3 dir = rot * Vector3.down;
        Vector3 targetPos = origin + dir * (laserLength * 0.5f);

        if (activeWarningLaser != null)
        {
            activeWarningLaser.transform.position = targetPos;
            activeWarningLaser.transform.rotation = rot;
        }
        if (activeRealLaser != null)
        {
            activeRealLaser.transform.position = targetPos;
            activeRealLaser.transform.rotation = rot;
        }
    }

    private Vector3 GetAimDirectionToPlayer()
    {
        var players = PlayerController.ActivePlayers;
        PlayerController target = null;
        float minDistSq = float.MaxValue;
        Vector3 bossCenter = transform.position;

        if (players != null)
        {
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (p != null && p.gameObject.activeInHierarchy && p.GetCurrentHealth() > 0)
                {
                    float dSq = (p.transform.position - bossCenter).sqrMagnitude;
                    if (dSq < minDistSq)
                    {
                        minDistSq = dSq;
                        target = p;
                    }
                }
            }
        }

        if (target == null && playerController != null && playerController.GetCurrentHealth() > 0)
        {
            target = playerController;
        }

        if (target != null)
        {
            Vector3 diff = target.GetHitboxCenter() - bossCenter;
            if (diff.sqrMagnitude > 0.001f)
            {
                return diff.normalized;
            }
        }

        return Vector3.down;
    }

    // Ataque 1: Disparo quíntuple de balas normales en abanico apuntando hacia el jugador:
    // - Si el jugador intenta colocarse arriba o a los costados, el jefe dispara su abanico directamente hacia él
    // - Costados exteriores: diagonales más abiertas (-36° izq, +36° der respecto al jugador)
    // - Costados interiores: diagonales menos abiertas (-18° izq, +18° der respecto al jugador)
    // - Centro (3er disparo): directo hacia el jugador (0°)
    IEnumerator SmallBulletBottomRoutine()
    {
        float[] cannonOffsets = { -155f, -75f, 0f, 75f, 155f };
        float[] cannonAnglesDeg = { -36f, -18f, 0f, 18f, 36f };

        while (!isDead)
        {
            // En Fase 2 genera las balas normales con 2 segundos más de delay entre oleada y oleada
            float cadenceMultiplier = isPhase2 ? (1.10f * 1.15f) : 1.35f;
            float extraPhase2Delay = isPhase2 ? 2.0f : 0f;
            yield return new WaitForSeconds((smallBulletCadence * cadenceMultiplier) + extraPhase2Delay);
            if (isDead || isTransitioningPhase2 || bulletPrefab == null) continue;

            Vector3 aimDir = GetAimDirectionToPlayer();
            Vector3 perpDir = new Vector3(-aimDir.y, aimDir.x, 0f).normalized;

            for (int i = 0; i < cannonOffsets.Length; i++)
            {
                Vector3 spawnPos = transform.position + (aimDir * 130f) + (perpDir * cannonOffsets[i]);
                Vector3 dir = Quaternion.Euler(0f, 0f, cannonAnglesDeg[i]) * aimDir;
                FireBullet(bulletPrefab, smallBulletSpeed, dir, spawnPos);
            }
            TriggerRecoil(0.55f);
        }
    }

    // Ataque 2: Balas gigantes por lado (izq y der) disparadas hacia sus respectivas diagonales con racimo tipo Tier 3
    IEnumerator BigBulletDiagonalRoutine()
    {
        yield return new WaitForSeconds(1.5f); // Breve respiro inicial tras entrar a pantalla
        Vector3[] offsets = { new Vector3(-150f, -150f, 0f), new Vector3(150f, -150f, 0f) };
        float[] diagAnglesDeg = { -26f, 26f }; // Izquierda -> diagonal abajo-izq; Derecha -> diagonal abajo-der

        while (!isDead)
        {
            if (!isTransitioningPhase2 && bigBulletPrefab != null)
            {
                // Dispara una bala grande por cada lado en diagonal simultáneamente
                for (int i = 0; i < offsets.Length; i++)
                {
                    Vector3 spawnPos = transform.position + offsets[i];
                    Vector3 diagDir = Quaternion.Euler(0f, 0f, diagAnglesDeg[i]) * Vector3.down;
                    GameObject bigBullet = FireBullet(bigBulletPrefab, bigBulletSpeed, diagDir, spawnPos, bigBulletLifetime);
                    if (bigBullet != null && bigBullet.TryGetComponent<EnemyBullet>(out var eb))
                    {
                        // Comportamiento idéntico al Tier 3: explota en racimo tras recorrer una distancia en Y
                        float randomTravelDistY = Random.Range(280f, 550f);
                        eb.SetupClusterExplosion(bulletPrefab, randomTravelDistY, bossClusterFragmentSpeed);
                    }
                }
                TriggerRecoil(1.1f);
            }

            // En Fase 2: 1 segundo menos de delay entre ráfagas de balas grandes
            float waitCadence = isPhase2 ? Mathf.Max(0.5f, (bigBulletCadence / 1.5f) - 1.0f) : bigBulletCadence;
            yield return new WaitForSeconds(waitCadence);
        }
    }

    // Ataque 4 (Fase 2): Cada 4 segundos dispara muchas balas en forma de pulsación radial en 360°
    IEnumerator Phase2PulseRoutine()
    {
        yield return new WaitForSeconds(1.5f);
        while (!isDead && isPhase2)
        {
            yield return new WaitForSeconds(4.0f * 1.15f); // -15% velocidad de generación en pulsación radial (4.6s)
            if (isDead || !isPhase2 || isTransitioningPhase2 || bulletPrefab == null) continue;

            const int pulseCount = 20;
            float angleStep = 360f / pulseCount;
            Vector3 centerPos = transform.position + new Vector3(0f, -80f, 0f);

            for (int i = 0; i < pulseCount; i++)
            {
                float deg = i * angleStep;
                float rad = deg * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f);
                FireBullet(bulletPrefab, smallBulletSpeed * 1.05f, dir, centerPos);
            }

            TriggerRecoil(0.85f);
            SoundController.Instance?.PlayNormalShootSfx();
        }
    }

    // Ataque 3: Súper Láser con aviso previo
    // Fase 1: siempre recto hacia abajo
    // Fase 2: 50% recto hacia abajo / 50% barrido lento de 180° (50% inicia derecha -> baja -> sube izq; 50% inicia izq -> baja -> sube der)
    IEnumerator SuperLaserRoutine()
    {
        // En Fase 2 dar una breve pausa inicial de 1.0s al regresar tras el Hiper Rayo para que el aviso sea clarísimo
        if (isPhase2)
        {
            float initDelay = 0f;
            while (!isDead && initDelay < 1.0f)
            {
                initDelay += Time.deltaTime;
                yield return null;
            }
        }

        while (!isDead)
        {
            if (isTransitioningPhase2)
            {
                yield return null;
                continue;
            }

            CleanupLasersOnly();
            isSweepingLaser = false;

            // En Fase 2: 50% chance de rayo recto, 50% chance de barrido horizontal de 180°
            bool doSweep = isPhase2 && (UnityEngine.Random.value < 0.5f);
            bool startRight = doSweep && (UnityEngine.Random.value < 0.5f);
            float startAngle = doSweep ? (startRight ? 90f : -90f) : 0f;
            float endAngle = doSweep ? (startRight ? -90f : 90f) : 0f;
            currentLaserAngle = startAngle;

            Vector3 origin = transform.position + new Vector3(0f, -150f, 0f);
            Quaternion startRot = Quaternion.Euler(0f, 0f, startAngle);
            Vector3 startDir = startRot * Vector3.down;
            Vector3 worldPos = origin + startDir * (laserLength * 0.5f);

            // Aviso rojo en la dirección inicial (sortingOrder 6 para verse siempre por encima del escudo de Fase 2)
            activeWarningLaser = telegraphPrefab != null
                ? Instantiate(telegraphPrefab, worldPos, startRot)
                : CreateRuntimeLaser("BossLaserWarning_Auto", worldPos, new Color(1f, 0.12f, 0.12f, 0.38f), 6);
            activeWarningLaser.transform.localScale = new Vector3(laserWidth, laserLength, 1f);
            activeWarningLaser.transform.rotation = startRot;
            if (activeWarningLaser.TryGetComponent<SpriteRenderer>(out var warnSr))
                warnSr.sortingOrder = 6;

            float warnElapsed = 0f;
            while (!isDead && !isTransitioningPhase2 && warnElapsed < laserWarningDuration)
            {
                warnElapsed += Time.deltaTime;
                laserChargeProgress = Mathf.Clamp01(warnElapsed / Mathf.Max(0.01f, laserWarningDuration));
                yield return null;
            }
            laserChargeProgress = 0f;
            DestroySafe(ref activeWarningLaser);
            if (isDead || isTransitioningPhase2) continue;

            // Láser destructivo (2 de vida sin escudo, 20 de escudo con escudo)
            origin = transform.position + new Vector3(0f, -150f, 0f);
            worldPos = origin + startDir * (laserLength * 0.5f);
            float activeDuration = doSweep ? Mathf.Max(4.5f, phase2SweepLaserDuration) : laserActiveDuration;

            activeRealLaser = laserPrefab != null
                ? Instantiate(laserPrefab, worldPos, startRot)
                : CreateRuntimeLaser("BossRealLaser_Auto", worldPos, Color.white, 7, true);
            activeRealLaser.transform.localScale = new Vector3(laserWidth, laserLength, 1f);
            activeRealLaser.transform.rotation = startRot;
            if (activeRealLaser.TryGetComponent<SpriteRenderer>(out var realSr))
                realSr.sortingOrder = 7;

            if (activeRealLaser.TryGetComponent<LaserBeam>(out var lb))
            {
                lb.damage = 2;
                lb.shieldDamage = 20f;
                lb.drainsAllShield = false;
                lb.activeDuration = activeDuration + 0.35f;
                lb.autoDestroy = true;
            }

            SoundController.Instance?.PlayBossLaserSfx();
            isFiringLaser = true;
            isSweepingLaser = doSweep;
            TriggerRecoil(1.45f);

            float activeElapsed = 0f;
            while (!isDead && !isTransitioningPhase2 && activeElapsed < activeDuration)
            {
                activeElapsed += Time.deltaTime;
                if (doSweep)
                {
                    // Barrido lento de 180° desde horizontal (+90° -> 0° -> -90° o -90° -> 0° -> +90°)
                    float sweepT = Mathf.Clamp01(activeElapsed / activeDuration);
                    currentLaserAngle = Mathf.Lerp(startAngle, endAngle, sweepT);
                    UpdateLaserPosition();
                }
                yield return null;
            }

            isFiringLaser = false;
            isSweepingLaser = false;
            currentLaserAngle = 0f;
            DestroySafe(ref activeRealLaser);
            if (isDead || isTransitioningPhase2) continue;

            // En Fase 2 el láser vuelve un poco más seguido (4.2s vs ~6.0s en Fase 1) con -15% de velocidad de generación
            float baseCooldown = Mathf.Max(5.75f, (laserWarningDuration + laserActiveDuration + 5f) * 1.15f - laserWarningDuration - laserActiveDuration) * 1.15f;
            float cycleCooldown = (isPhase2 ? 4.2f : baseCooldown) * 1.15f;
            float cdElapsed = 0f;
            while (!isDead && !isTransitioningPhase2 && cdElapsed < cycleCooldown)
            {
                cdElapsed += Time.deltaTime;
                yield return null;
            }
        }
    }

    // Invocación en Fase 1: enemigos aleatorios del juego (Tier 1, 2 o 3, excepto Tier 4) cada 3 a 9 segundos
    IEnumerator SpawnPhase1MinionsRoutine()
    {
        yield return new WaitForSeconds(Random.Range(3f, 9f));
        while (!isDead && !isPhase2)
        {
            if (!isTransitioningPhase2)
            {
                GameObject chosenPrefab = null;
                int r = Random.Range(0, 3);
                if (r == 0) chosenPrefab = WaveController.Instance?.tier1Prefab ?? Resources.Load<GameObject>("Prefabs/Enemy_Tier1");
                else if (r == 1) chosenPrefab = WaveController.Instance?.tier2Prefab ?? Resources.Load<GameObject>("Prefabs/Enemy_Tier2");
                else chosenPrefab = WaveController.Instance?.tier3Prefab ?? Resources.Load<GameObject>("Prefabs/Enemy_Tier3");

                if (chosenPrefab != null)
                {
                    float spawnY = (mainCam != null) ? mainCam.transform.position.y + mainCam.orthographicSize + 150f : 1800f;
                    float horz = (mainCam != null) ? (mainCam.orthographicSize * mainCam.aspect * 0.70f) : 600f;
                    float camX = (mainCam != null) ? mainCam.transform.position.x : 957f;
                    float spawnX = Random.Range(camX - horz, camX + horz);

                    GameObject minion = Instantiate(chosenPrefab, new Vector3(spawnX, spawnY, 0f), Quaternion.identity);
                    spawnedMinions.Add(minion);
                }
            }
            yield return new WaitForSeconds(Random.Range(3f, 9f));
        }
    }

    // Ataque 4: Incursión periódica de esbirros Tier 4 en Fase 2 (20% más rápido: cadencia 5.28s en vez de 6.6s)
    IEnumerator SpawnTier4MinionsRoutine(float initialDelay = 0.96f)
    {
        if (initialDelay > 0f) yield return new WaitForSeconds(initialDelay);
        while (!isDead)
        {
            if (!isTransitioningPhase2 && tier4Prefab != null)
            {
                for (int i = 0; i < 2; i++)
                {
                    if (isDead) yield break;
                    float spawnY = (mainCam != null) ? mainCam.transform.position.y + mainCam.orthographicSize + 200f : 1800f;
                    float spawnX = (mainCam != null) ? mainCam.transform.position.x : 957f;
                    spawnedMinions.Add(Instantiate(tier4Prefab, new Vector3(spawnX, spawnY, 0f), Quaternion.identity));
                    yield return new WaitForSeconds(0.32f);
                }
            }
            yield return new WaitForSeconds(5.28f);
        }
    }

    private float GetPhase2Threshold()
    {
        return maxHealth * 0.5f;
    }

    private void SpawnShieldShatterVfx(Vector3 center)
    {
        int count = 16;
        for (int i = 0; i < count; i++)
        {
            float angle = i * (360f / count);
            Vector3 dir = Quaternion.Euler(0f, 0f, angle) * Vector3.up;
            if (bulletPrefab != null)
            {
                GameObject shard = Instantiate(bulletPrefab, center + dir * 60f, Quaternion.identity);
                shard.transform.localScale = Vector3.one * 0.7f;
                if (shard.TryGetComponent<EnemyBullet>(out var eb))
                {
                    eb.damage = 0;
                }
                if (shard.TryGetComponent<Rigidbody2D>(out var rb))
                {
                    rb.linearVelocity = dir * 450f;
                }
                Destroy(shard, 0.55f);
            }
        }
    }

    private IEnumerator ShieldBreakAnimationRoutine()
    {
        Vector3 origPos = transform.position;
        float duration = 1.4f;
        float elapsed = 0f;

        SoundController.Instance?.PlayBossEntrySfx();

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            float shakeIntensity = Mathf.Lerp(4f, 24f, t);
            float offsetX = Random.Range(-shakeIntensity, shakeIntensity);
            float offsetY = Random.Range(-shakeIntensity, shakeIntensity);
            transform.position = origPos + new Vector3(offsetX, offsetY, 0f);

            float pulse = Mathf.Sin(elapsed * 35f);
            float expandScale = Mathf.Lerp(1.0f, 1.45f, t) + (pulse * 0.08f);

            if (vfxPhase2ShieldSr != null)
            {
                vfxPhase2ShieldSr.gameObject.SetActive(true);
                vfxPhase2ShieldSr.transform.localScale = Vector3.one * (basePhase2ShieldScale * expandScale);
                Color crackCol = Color.Lerp(new Color(0.2f, 0.8f, 1f, 0.85f), new Color(1f, 0.3f, 0.1f, 1f), t);
                vfxPhase2ShieldSr.color = (pulse > 0f) ? crackCol : Color.white;
            }

            if (vfxPhase2ShieldRingTr != null && vfxPhase2ShieldRingSr != null)
            {
                vfxPhase2ShieldRingSr.gameObject.SetActive(true);
                vfxPhase2ShieldRingTr.Rotate(0f, 0f, 400f * Time.deltaTime);
                vfxPhase2ShieldRingTr.localScale = Vector3.one * (basePhase2ShieldRingScale * expandScale);
                Color ringCrackCol = Color.Lerp(new Color(0.4f, 0.9f, 1f, 0.9f), Color.yellow, t);
                vfxPhase2ShieldRingSr.color = (pulse > 0f) ? ringCrackCol : Color.white;
            }

            yield return null;
        }

        transform.position = origPos;

        if (SoundController.HasInstance)
        {
            SoundController.Instance.PlayUpgradePurchasedSfx();
        }

        SpawnShieldShatterVfx(origPos);

        if (vfxPhase2ShieldSr != null) vfxPhase2ShieldSr.gameObject.SetActive(false);
        if (vfxPhase2ShieldRingSr != null) vfxPhase2ShieldRingSr.gameObject.SetActive(false);

        yield return new WaitForSeconds(0.25f);
    }

    private void EnterPhase2()
    {
        if (isPhase2 || isDead) return;

        hasEntered = true;
        isPhase2 = true;
        isTransitioningPhase2 = true;

        // Curar el 20% de la vida perdida durante la Fase 1
        float lostHealth = Mathf.Max(0f, maxHealth - currentHealth);
        float healAmount = lostHealth * phase2HealFractionOfLost;
        currentHealth = Mathf.Min(maxHealth, currentHealth + healAmount);

        // Detener ataques de Fase 1 y limpiar láseres previos antes del Hiper Rayo
        StopNormalAttackRoutines();
        CleanupLasersOnly();
        laserChargeProgress = 0f;
        isFiringLaser = false;
        isSweepingLaser = false;
        currentLaserAngle = 0f;

        GameManager.Instance?.hudManager?.SetBossPhase2(true);
        GameManager.Instance?.hudManager?.SetBossInvulnerable(true);
        GameManager.Instance?.hudManager?.UpdateBossHealth(currentHealth);
        GameManager.Instance?.hudManager?.ShowPhase2EnemyAlert("¡ESCUDO DEL JEFE DESTRUIDO!", 3.0f);

        if (phase2TransitionCoroutine != null) StopCoroutine(phase2TransitionCoroutine);
        phase2TransitionCoroutine = StartCoroutine(Phase2HyperBeamSequence());
    }

    /// <summary>
    /// Secuencia de entrada a Fase 2:
    /// 1) El jefe se retira hacia arriba fuera de la pantalla.
    /// 2) Proyecta un láser rojo de advertencia central durante 3 segundos (solo los costados son seguros).
    /// 3) Dispara un Hiper Rayo masivo por el centro de la pantalla con sonido agresivo.
    /// 4) Regresa a la arena y comienza a invocar esbirros Tier 4 junto con sus ataques.
    /// </summary>
    private IEnumerator Phase2HyperBeamSequence()
    {
        // 0. Animación donde se rompe el escudo del jefe (mientras hace la animación es invencible) antes de salir de la pantalla
        yield return StartCoroutine(ShieldBreakAnimationRoutine());

        if (mainCam == null) mainCam = Camera.main;

        float camX = mainCam != null ? mainCam.transform.position.x : transform.position.x;
        float camY = mainCam != null ? mainCam.transform.position.y : 540f;
        float vertExtent = mainCam != null ? mainCam.orthographicSize : 600f;
        float horzExtent = mainCam != null ? vertExtent * mainCam.aspect : 1066f;
        float exitY = camY + vertExtent + 320f;

        // 1. Retirarse hacia arriba fuera de la pantalla mientras se centra horizontalmente
        while (!isDead && transform.position.y < exitY - 2f)
        {
            Vector3 pos = transform.position;
            pos.x = Mathf.MoveTowards(pos.x, camX, phase2RetreatSpeed * 0.9f * Time.deltaTime);
            pos.y = Mathf.MoveTowards(pos.y, exitY, phase2RetreatSpeed * Time.deltaTime);
            transform.position = pos;
            yield return null;
        }
        if (isDead) yield break;

        transform.position = new Vector3(camX, exitY, transform.position.z);

        // 2. Calcular el ancho del Hiper Rayo central dejando pasillos seguros únicamente a los costados
        float screenWidth = horzExtent * 2f;
        float safeSideCorridor = Mathf.Max(220f, screenWidth * 0.135f);
        float hyperWidth = Mathf.Max(laserWidth * 2.5f, screenWidth - safeSideCorridor * 2f);
        float hyperHeight = vertExtent * 2.8f;
        Vector3 beamCenter = new Vector3(camX, camY, 0f);

        // Láser rojo de aviso durante 3 segundos exactos
        Sprite origLaserSprite = Resources.Load<Sprite>("Sprites/LaserBeam");
        activeWarningLaser = telegraphPrefab != null
            ? Instantiate(telegraphPrefab, beamCenter, Quaternion.identity)
            : CreateRuntimeLaser("BossHyperWarning_Auto", beamCenter, new Color(1f, 0.10f, 0.10f, 0.38f), 4);
        activeWarningLaser.transform.localScale = new Vector3(hyperWidth, hyperHeight, 1f);

        // Líneas rojas nítidas delimitando el borde izquierdo y derecho de la zona letal
        SpriteRenderer warnSr = activeWarningLaser.GetComponent<SpriteRenderer>();
        if (warnSr != null && origLaserSprite != null) warnSr.sprite = origLaserSprite; // Sprite original del LaserBeam
        Sprite unitSprite = origLaserSprite != null ? origLaserSprite : GetHyperBeamSolidSprite();
        Material unitMat = warnSr != null ? warnSr.sharedMaterial : null;
        hyperWarningLeftEdge = CreateHyperStripVisual("BossHyperWarnLeftEdge",
            new Vector3(camX - hyperWidth * 0.5f, camY, 0f),
            new Vector3(14f, hyperHeight, 1f),
            new Color(1f, 0.15f, 0.15f, 0.92f), 5, unitSprite, unitMat);
        hyperWarningRightEdge = CreateHyperStripVisual("BossHyperWarnRightEdge",
            new Vector3(camX + hyperWidth * 0.5f, camY, 0f),
            new Vector3(14f, hyperHeight, 1f),
            new Color(1f, 0.15f, 0.15f, 0.92f), 5, unitSprite, unitMat);

        SpriteRenderer leftEdgeSr = hyperWarningLeftEdge != null ? hyperWarningLeftEdge.GetComponent<SpriteRenderer>() : null;
        SpriteRenderer rightEdgeSr = hyperWarningRightEdge != null ? hyperWarningRightEdge.GetComponent<SpriteRenderer>() : null;

        SoundController.Instance?.PlayBossHyperWarningSfx();

        float warnElapsed = 0f;
        float warnDuration = Mathf.Max(3.0f, hyperBeamWarningDuration);
        while (!isDead && warnElapsed < warnDuration)
        {
            warnElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(warnElapsed / warnDuration);
            laserChargeProgress = t;

            // Pulso cada vez más frenético al acercarse el disparo
            float pulseFreq = Mathf.Lerp(7f, 26f, t);
            float pulse = 0.5f + 0.5f * Mathf.Sin(warnElapsed * pulseFreq);
            if (warnSr != null)
            {
                warnSr.color = new Color(1f, Mathf.Lerp(0.08f, 0.30f, pulse * t), 0.08f, Mathf.Lerp(0.24f, 0.54f, pulse));
            }
            Color edgeCol = Color.Lerp(new Color(1f, 0.14f, 0.14f, 0.78f), new Color(1f, 0.82f, 0.20f, 1f), pulse * t);
            if (leftEdgeSr != null) leftEdgeSr.color = edgeCol;
            if (rightEdgeSr != null) rightEdgeSr.color = edgeCol;

            yield return null;
        }
        laserChargeProgress = 0f;
        DestroySafe(ref activeWarningLaser);
        DestroySafe(ref hyperWarningLeftEdge);
        DestroySafe(ref hyperWarningRightEdge);
        if (isDead) yield break;

        // 3. Disparo del Hiper Rayo masivo usando el sprite original del rayo (sin pantallas blancas)
        activeRealLaser = laserPrefab != null
            ? Instantiate(laserPrefab, beamCenter, Quaternion.identity)
            : CreateRuntimeLaser("BossHyperBeam_Auto", beamCenter, Color.white, 7, true);
        activeRealLaser.transform.localScale = new Vector3(hyperWidth, hyperHeight, 1f);

        SpriteRenderer realSr = activeRealLaser.GetComponent<SpriteRenderer>();
        if (realSr != null)
        {
            if (origLaserSprite != null) realSr.sprite = origLaserSprite; // Sprite original del LaserBeam
            realSr.color = Color.white;
            realSr.sortingOrder = 7;
        }

        // Asegurar que la hitbox del BoxCollider2D coincida exactamente al 100% con el ancho visual del sprite
        if (activeRealLaser.TryGetComponent<BoxCollider2D>(out var beamCol))
        {
            beamCol.size = new Vector2(1f, 1f);
            beamCol.offset = Vector2.zero;
        }
        if (activeRealLaser.TryGetComponent<LaserBeam>(out var lb))
        {
            lb.damage = 4; // El Hiper Láser saca 4 de vida si no hay escudo
            lb.drainsAllShield = true; // Si el jugador tiene escudo, le quita todo el escudo restante
            lb.activeDuration = hyperBeamActiveDuration;
            lb.damageTickInterval = 0.18f;
            lb.autoDestroy = true;
        }

        SoundController.Instance?.PlayBossHyperBeamSfx();
        isFiringLaser = true;

        float fireElapsed = 0f;
        while (!isDead && fireElapsed < hyperBeamActiveDuration)
        {
            fireElapsed += Time.deltaTime;
            float jitter = 1f + Mathf.Sin(fireElapsed * 62f) * 0.022f;
            if (activeRealLaser != null)
                activeRealLaser.transform.localScale = new Vector3(hyperWidth * jitter, hyperHeight, 1f);
            yield return null;
        }

        isFiringLaser = false;
        CleanupLasersOnly();
        if (isDead) yield break;

        // 4. Regresar desde arriba a la posición de combate
        while (!isDead && transform.position.y > targetY + 2f)
        {
            Vector3 pos = transform.position;
            pos.y = Mathf.MoveTowards(pos.y, targetY, phase2RetreatSpeed * 0.85f * Time.deltaTime);
            transform.position = pos;
            yield return null;
        }
        if (isDead) yield break;

        transform.position = new Vector3(camX, targetY, transform.position.z);
        isTransitioningPhase2 = false;
        phase2TransitionCoroutine = null;
        GameManager.Instance?.hudManager?.SetBossInvulnerable(false);

        // Reanudar ataques normales y AHORA comenzar a invocar esbirros Tier 4 (20% más rápido)
        StartNormalAttackRoutines();
        if (tier4MinionsCoroutine != null) StopCoroutine(tier4MinionsCoroutine);
        tier4MinionsCoroutine = StartCoroutine(SpawnTier4MinionsRoutine(0.96f));
    }

    private static Sprite s_HyperBeamSolidSprite;
    private static Sprite GetHyperBeamSolidSprite()
    {
        if (s_HyperBeamSolidSprite != null) return s_HyperBeamSolidSprite;
        Texture2D tex = Texture2D.whiteTexture;
        s_HyperBeamSolidSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 1f);
        return s_HyperBeamSolidSprite;
    }

    private GameObject CreateHyperStripVisual(string name, Vector3 pos, Vector3 scale, Color color, int sortingOrder, Sprite sprite, Material mat)
    {
        GameObject obj = new GameObject(name);
        obj.transform.position = pos;
        obj.transform.localScale = scale;

        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
        if (sprite != null)
        {
            sr.sprite = sprite;
        }
        else if (bulletPrefab != null && bulletPrefab.TryGetComponent<SpriteRenderer>(out var bSr))
        {
            sr.sprite = bSr.sprite;
            mat = mat ?? bSr.sharedMaterial;
        }
        if (mat != null) sr.material = mat;
        sr.color = color;
        sr.sortingOrder = sortingOrder;
        return obj;
    }

    private GameObject CreateRuntimeLaser(string name, Vector3 pos, Color color, int order, bool damage = false)
    {
        GameObject obj = new GameObject(name);
        obj.transform.position = pos;
        obj.transform.localScale = new Vector3(laserWidth, laserLength, 1f);

        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
        Sprite beamSprite = Resources.Load<Sprite>("Sprites/LaserBeam");
        if (beamSprite != null)
        {
            sr.sprite = beamSprite;
        }
        else if (bulletPrefab != null && bulletPrefab.TryGetComponent<SpriteRenderer>(out var bSr))
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

    private GameObject FireBullet(GameObject prefab, float speed, Vector3 dir, Vector3 spawnPos, float? lifetime = null)
    {
        if (prefab == null) return null;
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
        return obj;
    }

    public void TakeDamage(int amount) => TakeDamage((float)amount);

    public void TakeDamage(float amount)
    {
        if (Time.timeScale == 0f || isDead || amount <= 0f || IsInvulnerable) return;

        // Fase 1: Escudo activo con reducción de daño (30% en 1P, 40% en 2P)
        // Fase 2: Escudo roto, 0% de reducción de daño
        float reduction = 0f;
        if (!isPhase2)
        {
            bool isCoop = GameManager.Instance != null && GameManager.Instance.IsPlayer2Joined;
            reduction = isCoop ? 0.40f : 0.30f;
        }
        float effectiveDamage = amount * (1f - reduction);

        // Protección anti one-shot: ningún disparo individual puede bajar más de la mitad de la vida máxima del jefe
        float maxSingleShotDamage = maxHealth * 0.5f;
        effectiveDamage = Mathf.Min(effectiveDamage, maxSingleShotDamage);

        currentHealth -= effectiveDamage;

        // Verificar transición a Fase 2 al llegar a 250 HP (o 500 HP en 2P)
        if (!isPhase2 && currentHealth <= GetPhase2Threshold())
        {
            // Impedir que un disparo masivo en Fase 1 baje por debajo del umbral antes de activar la Fase 2
            currentHealth = Mathf.Max(GetPhase2Threshold(), currentHealth);
            EnterPhase2();
            return;
        }

        if (!isPhase2)
        {
            shieldHitFlashTimer = 0.16f;
        }

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
        maxHealth = 1000;
        currentHealth = Mathf.Min(maxHealth, currentHealth * 1.50f);
        GameManager.Instance?.hudManager?.ActivateBossHealthBar("Leviatán nodriza", maxHealth);
        GameManager.Instance?.hudManager?.UpdateBossHealth(currentHealth);
        if (isPhase2) GameManager.Instance?.hudManager?.SetBossPhase2(true);
        GameManager.Instance?.hudManager?.SetBossInvulnerable(IsInvulnerable);
    }

    public bool IsDead() => isDead || currentHealth <= 0.001f;
    public bool IsInvulnerable => !isDead && (!hasEntered || isTransitioningPhase2);
    public bool IsEnraged => !isDead && (isPhase2 || currentHealth <= maxHealth * 0.5f);
    public bool IsLaserActive => !isDead && isFiringLaser;
    public bool IsPhase2 => isPhase2;

    void Die()
    {
        if (isDead) return;
        isDead = true;
        isFiringLaser = false;
        isSweepingLaser = false;
        isTransitioningPhase2 = false;
        currentLaserAngle = 0f;

        // Detener todos los ataques del jefe, limpiar láseres y esbirros
        StopNormalAttackRoutines();
        if (tier4MinionsCoroutine != null) { StopCoroutine(tier4MinionsCoroutine); tier4MinionsCoroutine = null; }
        if (phase2TransitionCoroutine != null) { StopCoroutine(phase2TransitionCoroutine); phase2TransitionCoroutine = null; }
        CleanupAll();

        // Limpiar balas enemigas activas para que los jugadores puedan disfrutar la explosión final sin morir
        EnemyBullet[] remainingBullets = FindObjectsByType<EnemyBullet>(FindObjectsSortMode.None);
        for (int i = 0; i < remainingBullets.Length; i++)
        {
            if (remainingBullets[i] != null) Destroy(remainingBullets[i].gameObject);
        }

        // Desactivar colisiones del jefe durante su agonía explosiva
        Collider2D[] cols = GetComponentsInChildren<Collider2D>();
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i] != null) cols[i].enabled = false;
        }

        GameManager.Instance?.hudManager?.HidePhase2AlertImmediately();
        GameManager.Instance?.hudManager?.OnBossDefeated();
        SoundController.Instance?.StopMusic(true);

        StartCoroutine(BossDeathSequenceRoutine());
    }

    /// <summary>
    /// Animación cinematográfica de destrucción final del jefe:
    /// 1) Sacudida violenta, sobrecarga luminosa y cadena de 10 explosiones en todo el casco.
    /// 2) Pico crítico blanco-dorado del reactor.
    /// 3) Detonación final cataclísmica con onda expansiva gigante.
    /// 4) Breve pausa dramática y recién entonces muestra la pantalla de victoria.
    /// </summary>
    private IEnumerator BossDeathSequenceRoutine()
    {
        Vector3 deathOrigin = transform.position;
        SpriteRenderer mainSr = GetComponent<SpriteRenderer>();
        Material mat = mainSr != null ? mainSr.sharedMaterial : null;
        float spanX = (mainSr != null && mainSr.sprite != null) ? Mathf.Max(180f, mainSr.bounds.extents.x) : 210f;
        float spanY = (mainSr != null && mainSr.sprite != null) ? Mathf.Max(110f, mainSr.bounds.extents.y) : 130f;

        // Fase 1: Cadena de explosiones en el chasis + temblor creciente (2.15s)
        const float cascadeDur = 2.15f;
        float elapsed = 0f;
        float nextBlastTime = 0f;
        int blastIndex = 0;

        while (elapsed < cascadeDur)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / cascadeDur);

            // Sacudida creciente + ligera caída e inclinación al perder estabilidad
            float shakeMag = Mathf.Lerp(10f, 36f, t * t);
            Vector3 shakeOffset = new Vector3(
                UnityEngine.Random.Range(-shakeMag, shakeMag),
                UnityEngine.Random.Range(-shakeMag * 0.7f, shakeMag * 0.7f) - t * 28f,
                0f);
            transform.position = deathOrigin + shakeOffset;
            transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(elapsed * 26f) * (3f + t * 8f) - t * 6f);

            // Parpadeo de sobrecarga entre blanco crítico, naranja fuego y rojo alarma
            if (mainSr != null)
            {
                float flash = Mathf.Sin(elapsed * Mathf.Lerp(24f, 65f, t)) * 0.5f + 0.5f;
                Color hot = Color.Lerp(new Color(1f, 0.35f, 0.15f, 1f), Color.white, flash);
                mainSr.color = Color.Lerp(Color.white, hot, 0.75f);
            }

            // Explosiones escalonadas en diferentes zonas del casco
            if (elapsed >= nextBlastTime)
            {
                Vector3 expOffset = new Vector3(
                    UnityEngine.Random.Range(-spanX * 0.85f, spanX * 0.85f),
                    UnityEngine.Random.Range(-spanY * 0.70f, spanY * 0.70f),
                    0f);
                float expSize = Mathf.Lerp(115f, 195f, t);
                EnemyExplosion.Spawn(deathOrigin + expOffset, (EnemyTier)(blastIndex % 4), new Vector3(expSize, expSize, 1f), mat);
                if (blastIndex % 2 == 0)
                {
                    SoundController.Instance?.PlayEnemyDeathSfx();
                }
                blastIndex++;
                nextBlastTime = elapsed + Mathf.Lerp(0.24f, 0.13f, t);
            }

            yield return null;
        }

        // Fase 2: Sobrecarga crítica final del núcleo (0.30s)
        const float surgeDur = 0.30f;
        elapsed = 0f;
        while (elapsed < surgeDur)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / surgeDur);
            float swell = 1f + t * 0.14f;
            transform.localScale = new Vector3(baseScale.x * swell, baseScale.y * swell, baseScale.z);
            if (mainSr != null) mainSr.color = Color.Lerp(new Color(1f, 0.85f, 0.45f, 1f), Color.white, t);
            yield return null;
        }

        // Fase 3: ¡DETONACIÓN FINAL CATACLÍSMICA!
        Vector3 finalPos = transform.position;
        SoundController.Instance?.PlayBossDeathSfx();
        EnemyExplosion.SpawnBossSequence(finalPos, baseScale * 1.45f, mat);

        // Ocultar la nave del jefe y todos sus VFX hijos al instante de explotar
        SpriteRenderer[] allRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < allRenderers.Length; i++)
        {
            if (allRenderers[i] != null) allRenderers[i].enabled = false;
        }

        // Onda expansiva radial gigante de luz y fuego
        GameObject ringObj = new GameObject("BossFinalShockwaveRing");
        ringObj.transform.position = finalPos;
        SpriteRenderer ringSr = ringObj.AddComponent<SpriteRenderer>();
        ringSr.sprite = ChargeVisualUtils.GetRadialRingSprite();
        ringSr.sortingOrder = 22;

        GameObject flashObj = new GameObject("BossFinalCoreFlash");
        flashObj.transform.position = finalPos;
        SpriteRenderer flashSr = flashObj.AddComponent<SpriteRenderer>();
        flashSr.sprite = ChargeVisualUtils.GetGlowCircleSprite();
        flashSr.sortingOrder = 21;

        // Fase 4: Dejar respirar la gran explosión final durante 1.25s antes de abrir la pantalla de victoria
        const float aftermathDur = 1.25f;
        elapsed = 0f;
        while (elapsed < aftermathDur)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / aftermathDur);
            float ease = 1f - (1f - t) * (1f - t);

            if (ringObj != null && ringSr != null)
            {
                float ringScale = Mathf.Lerp(180f, 1450f, ease);
                ringObj.transform.localScale = new Vector3(ringScale, ringScale, 1f);
                ringSr.color = Color.Lerp(new Color(1f, 0.92f, 0.55f, 0.95f), new Color(1f, 0.30f, 0.10f, 0f), t);
            }
            if (flashObj != null && flashSr != null)
            {
                float flashScale = Mathf.Lerp(420f, 1150f, ease);
                flashObj.transform.localScale = new Vector3(flashScale, flashScale, 1f);
                flashSr.color = new Color(1f, 0.85f, 0.45f, Mathf.Clamp01(1f - t * 1.45f));
            }
            yield return null;
        }

        if (ringObj != null) Destroy(ringObj);
        if (flashObj != null) Destroy(flashObj);

        // Ahora sí: abrir la pantalla de victoria tras completarse la destrucción final del jefe
        GameManager.Instance?.OnBossDefeated(scoreValue);
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

        // Escudo visual blindado de Fase 1 (campo energético azul + anillo hexagonal giratorio)
        Sprite shieldAuraSprite = ChargeVisualUtils.GetForceFieldAuraSprite();
        if (shieldAuraSprite != null)
        {
            vfxPhase2ShieldSr = CreateBossVfxChild("Vfx_BossPhase2Shield", Vector3.zero, shieldAuraSprite, parentSr, 4, 1.38f, bossSpan, new Color(0.18f, 0.72f, 1f, 0.62f), out basePhase2ShieldScale);
            vfxPhase2ShieldSr.gameObject.SetActive(true);
        }

        Sprite shieldRingSprite = GetBossShieldRingSprite();
        if (shieldRingSprite != null)
        {
            vfxPhase2ShieldRingSr = CreateBossVfxChild("Vfx_BossPhase2ShieldRing", Vector3.zero, shieldRingSprite, parentSr, 5, 1.48f, bossSpan, new Color(0.45f, 0.90f, 1f, 0.78f), out basePhase2ShieldRingScale);
            vfxPhase2ShieldRingTr = vfxPhase2ShieldRingSr.transform;
            vfxPhase2ShieldRingSr.gameObject.SetActive(true);
        }
    }

    private static Sprite GetBossShieldRingSprite()
    {
        if (cachedBossShieldRingSprite != null) return cachedBossShieldRingSprite;

        const int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        float half = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - half) / half;
                float dy = (y - half) / half;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float angle = Mathf.Atan2(dy, dx);

                // Anillo exterior segmentado en 6 placas blindadas hexagonales + aro interior fino
                float hexWave = Mathf.Abs(Mathf.Cos(angle * 3f));
                float segMask = Mathf.SmoothStep(0.18f, 0.34f, hexWave);

                float outerBand = Mathf.Exp(-Mathf.Pow((r - 0.88f) / 0.055f, 2f)) * segMask;
                float innerThin = Mathf.Exp(-Mathf.Pow((r - 0.74f) / 0.025f, 2f)) * 0.55f;
                float hexFill = (r < 0.86f) ? Mathf.Pow(r / 0.86f, 3f) * 0.16f : 0f;

                float alpha = Mathf.Clamp01(outerBand + innerThin + hexFill);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();

        cachedBossShieldRingSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return cachedBossShieldRingSprite;
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
        float bankInput = (hasEntered && !isTransitioningPhase2) ? moveDirection : 0f;
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
        bool enraged = isPhase2 || currentHealth <= maxHealth * 0.5f;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * (enraged ? 6.5f : 3.6f) + bossAnimPhase);
        float flare = Mathf.Max(laserChargeProgress, isFiringLaser ? 1f : 0f);

        if (shieldHitFlashTimer > 0f)
        {
            shieldHitFlashTimer = Mathf.Max(0f, shieldHitFlashTimer - Time.deltaTime);
        }

        if (vfxAuraGlowSr != null)
        {
            vfxAuraGlowSr.transform.localScale = Vector3.one * (baseAuraScale * (1f + pulse * 0.08f + flare * 0.16f));
            if (isPhase2)
            {
                Color shieldAura = new Color(0.12f, 0.56f, 1f, 0.34f + pulse * 0.16f);
                Color hotShield = new Color(0.42f, 0.88f, 1f, 0.56f + pulse * 0.18f);
                vfxAuraGlowSr.color = Color.Lerp(shieldAura, hotShield, Mathf.Max(flare, shieldHitFlashTimer / 0.16f));
            }
            else
            {
                Color calm = new Color(0.88f, 0.18f, 0.28f, 0.20f + pulse * 0.11f);
                Color angry = new Color(1f, 0.28f, 0.18f, 0.42f + pulse * 0.16f);
                vfxAuraGlowSr.color = Color.Lerp(calm, angry, enraged ? Mathf.Max(0.45f, flare) : flare);
            }
        }

        if (vfxOuterVortexTr != null && vfxOuterVortexSr != null)
        {
            float spinSpeed = 75f + (enraged ? 55f : 0f) + flare * 260f;
            vfxOuterVortexTr.Rotate(0f, 0f, -spinSpeed * Time.deltaTime);
            vfxOuterVortexTr.localScale = Vector3.one * (baseOuterVortexScale * (1f + pulse * 0.12f + flare * 0.28f));
            Color baseCol = isPhase2
                ? new Color(0.20f, 0.68f, 1f, 0.55f)
                : (enraged ? new Color(1f, 0.28f, 0.22f, 0.48f) : new Color(0.98f, 0.30f, 0.42f, 0.36f));
            Color hotCol = isPhase2 ? new Color(0.72f, 0.96f, 1f, 0.85f) : new Color(1f, 0.55f, 0.25f, 0.72f);
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

        // Escudo visual activo en Fase 1 (se destruye y apaga en Fase 2)
        if (!isPhase2 && !isTransitioningPhase2)
        {
            float hitBoost = Mathf.Clamp01(shieldHitFlashTimer / 0.16f);
            if (vfxPhase2ShieldSr != null)
            {
                if (!vfxPhase2ShieldSr.gameObject.activeSelf) vfxPhase2ShieldSr.gameObject.SetActive(true);
                float shieldScale = basePhase2ShieldScale * (1f + pulse * 0.045f + hitBoost * 0.07f);
                vfxPhase2ShieldSr.transform.localScale = Vector3.one * shieldScale;
                Color shieldBase = new Color(0.16f, 0.68f, 1f, 0.52f + pulse * 0.16f);
                Color shieldHit = new Color(0.82f, 0.97f, 1f, 0.92f);
                vfxPhase2ShieldSr.color = Color.Lerp(shieldBase, shieldHit, hitBoost);
            }

            if (vfxPhase2ShieldRingTr != null && vfxPhase2ShieldRingSr != null)
            {
                if (!vfxPhase2ShieldRingSr.gameObject.activeSelf) vfxPhase2ShieldRingSr.gameObject.SetActive(true);
                vfxPhase2ShieldRingTr.Rotate(0f, 0f, (42f + hitBoost * 110f) * Time.deltaTime);
                float ringScale = basePhase2ShieldRingScale * (1f + (1f - pulse) * 0.035f + hitBoost * 0.06f);
                vfxPhase2ShieldRingTr.localScale = Vector3.one * ringScale;
                Color ringBase = new Color(0.38f, 0.86f, 1f, 0.66f + pulse * 0.18f);
                Color ringHit = new Color(0.95f, 0.99f, 1f, 0.98f);
                vfxPhase2ShieldRingSr.color = Color.Lerp(ringBase, ringHit, hitBoost);
            }
        }
        else if (isPhase2)
        {
            if (vfxPhase2ShieldSr != null && vfxPhase2ShieldSr.gameObject.activeSelf)
                vfxPhase2ShieldSr.gameObject.SetActive(false);
            if (vfxPhase2ShieldRingTr != null && vfxPhase2ShieldRingSr != null && vfxPhase2ShieldRingSr.gameObject.activeSelf)
                vfxPhase2ShieldRingSr.gameObject.SetActive(false);
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
        DestroySafe(ref hyperWarningLeftEdge);
        DestroySafe(ref hyperWarningRightEdge);
        DestroySafe(ref hyperBeamCoreVisual);
        DestroySafe(ref hyperBeamGlowVisual);
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