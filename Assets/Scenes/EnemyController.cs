using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public enum EnemyTier { Tier1, Tier2, Tier3, Tier4 }

/// <summary>
/// Controlador de naves enemigas (Tiers 1 al 4).
/// Gestiona entrada vertical, patrullaje/posicionamiento en hileras sin solapamiento, ataques y animaciones.
/// </summary>
public class EnemyController : MonoBehaviour, IDamageable
{
    [Header("Tier")]
    public EnemyTier tier;

    [Header("Vida y puntaje")]
    public int maxHealth = 1;
    private float currentHealth;
    public int scoreValue = 1;
    private bool givesScore = true;
    private bool isDead = false;

    [Header("Efecto de Explosión")]
    public Sprite explosionSprite;

    [Header("Movimiento")]
    public float moveSpeed = 100f;
    public float minX = -400f;
    public float maxX = 400f;
    public float settleY = 250f;
    public float entrySpeed = 600f;
    public float entryHeightAboveSettle = 400f;

    [Header("Reposicionamiento horizontal (Tier 1 y Tier 2)")]
    public float repositionMinX = -780f;
    public float repositionMaxX = 2700f;
    public float repositionSpeed = 600f;
    public float minSeparationFromOthers = 200f;
    private const int MAX_ENEMIES_PER_ROW = 10;
    public float rowVerticalSpacing = 140f;

    private static Dictionary<int, List<float>> reservedTier1Rows = new Dictionary<int, List<float>>();
    private static Dictionary<int, List<float>> reservedTier2Rows = new Dictionary<int, List<float>>();
    private static Dictionary<int, List<float>> reservedTier4Rows = new Dictionary<int, List<float>>();
    private static int activeTier3Count = 0;

    private float myReservedX;
    private int myRowIndex = 0;
    private float myAssignedSettleY;
    private bool hasReservedPosition = false;

    [Header("Disparo")]
    public GameObject bulletPrefab;
    public GameObject bigBulletPrefab;
    public GameObject laserPrefab;
    public float fireCadence = 1f;
    public float tier1BulletSpeed = 207f; // -25% velocidad bala normal (276 * 0.75)
    public float tier3BigBulletSpeed = 240f;

    [Header("Láser y Señalización (Tier 4)")]
    public GameObject telegraphPrefab;
    public float laserCooldown = 4f;
    public float laserTelegraphDuration = 1.667f; // -1/3 de duración (2.5s * 2/3 = 1.667s)
    public float laserBeamLength = 5250f;
    public float laserBeamWidth = 60f;
    public float laserTelegraphWidth = 25f;
    private GameObject activeTelegraph;

    private int patrolDirection = 1;
    private Transform playerTransform;
    public float enemyScreenPadding = 25f;
    private Camera mainCam;
    private bool hasEnteredScreen = false;

    [Header("Probabilidad de Apuntado")]
    [Range(0f, 1f)] public float tier1AimChance = 0.60f;
    [Range(0f, 1f)] public float tier2AimChance = 0.60f;
    [Range(0f, 1f)] public float tier3AimChance = 0.60f;
    private bool aimsAtPlayer = false;
    private bool tier2AimsAtPlayer = false;
    private bool tier3AimsAtPlayer = false;

    public float bigBulletSpawnOffset = 180f;

    // Animaciones mecánicas por Tier (flotación de propulsores, inclinación al moverse, carga y retroceso rígido)
    private Vector3 baseScale = Vector3.one;
    private float animPhaseOffset = 0f;
    private float prevPosX = 0f;
    private float bankAmount = 0f;
    private float recoilTimer = 0f;
    private float recoilStrength = 1f;
    private float prevHoverY = 0f;
    private float prevRecoilY = 0f;
    private bool hasSettledPos = false;
    private bool isRepositioning = false;
    private float chargeVisualProgress = 0f;
    private SpriteRenderer vfxCoreGlowSr;
    private Transform vfxCoreVortexTr;
    private SpriteRenderer vfxCoreVortexSr;

    public float GetScreenTopY(float extraPadding = 50f)
    {
        if (mainCam == null) mainCam = Camera.main;
        if (mainCam != null)
        {
            float paddingY = enemyScreenPadding;
            if (TryGetComponent<SpriteRenderer>(out var sr) && sr.sprite != null) paddingY = Mathf.Max(paddingY, sr.bounds.extents.y);
            else if (TryGetComponent<Collider2D>(out var col)) paddingY = Mathf.Max(paddingY, col.bounds.extents.y);
            return mainCam.transform.position.y + mainCam.orthographicSize + paddingY + extraPadding;
        }
        return 1800f;
    }

    public float GetScreenTopEdge()
    {
        if (mainCam == null) mainCam = Camera.main;
        if (mainCam != null)
        {
            return mainCam.transform.position.y + mainCam.orthographicSize;
        }
        return 1542f;
    }

    public static bool HasTierInWave(EnemyTier checkTier)
    {
        if (checkTier == EnemyTier.Tier1 && GetActiveTier1RowCount() > 0) return true;
        if (checkTier == EnemyTier.Tier2 && GetActiveTier2RowCount() > 0) return true;
        if (checkTier == EnemyTier.Tier3 && activeTier3Count > 0) return true;
        if (checkTier == EnemyTier.Tier4 && GetActiveTier4RowCount() > 0) return true;

        if (WaveController.Instance != null && WaveController.Instance.waves != null)
        {
            int wIdx = WaveController.Instance.CurrentWaveIndex;
            if (wIdx >= 0 && wIdx < WaveController.Instance.waves.Length)
            {
                var wave = WaveController.Instance.waves[wIdx];
                if (wave != null && wave.enemies != null)
                {
                    for (int i = 0; i < wave.enemies.Length; i++)
                    {
                        var entry = wave.enemies[i];
                        if (entry != null && entry.enemyPrefab != null && entry.count > 0)
                        {
                            if (entry.enemyPrefab.TryGetComponent<EnemyController>(out var ec))
                            {
                                if (ec.tier == checkTier) return true;
                            }
                        }
                    }
                }
            }
        }
        return false;
    }

    public static float GetLowestTier1Y(float topEdge)
    {
        float baseT1 = topEdge - 58f;
        int activeRows = GetActiveTier1RowCount();
        if (activeRows > 0) return baseT1 - ((activeRows - 1) * 120f);
        return baseT1;
    }

    public static float GetLowestTier2Y(float topEdge)
    {
        bool hasT1 = HasTierInWave(EnemyTier.Tier1);
        float baseT2 = hasT1 ? (GetLowestTier1Y(topEdge) - 125f) : (topEdge - 72f);
        int activeRows = GetActiveTier2RowCount();
        if (activeRows > 0) return baseT2 - ((activeRows - 1) * 135f);
        return baseT2;
    }

    public static float GetLowestTier3Y(float topEdge)
    {
        bool hasT2 = HasTierInWave(EnemyTier.Tier2);
        bool hasT1 = HasTierInWave(EnemyTier.Tier1);
        float baseT3 = hasT2 ? (GetLowestTier2Y(topEdge) - 145f)
                     : (hasT1 ? (GetLowestTier1Y(topEdge) - 145f)
                     : (topEdge - 88f));
        int lanes = (activeTier3Count > 0) ? Mathf.Min(activeTier3Count, 2) : 1;
        return baseT3 - ((lanes - 1) * 110f);
    }

    private void ComputeAndAssignFormationPosition()
    {
        float topEdge = GetScreenTopEdge();

        if (tier == EnemyTier.Tier1)
        {
            // Tier 1: Máxima prioridad en el borde superior de la pantalla
            float baseTier1Y = topEdge - 58f;
            ReserveRowPosition(reservedTier1Rows, baseTier1Y, 120f);
        }
        else if (tier == EnemyTier.Tier2)
        {
            // Tier 2: Se coloca debajo de Tier 1 si existe; si no, toma la cima
            bool hasT1 = HasTierInWave(EnemyTier.Tier1);
            float baseTier2Y = hasT1 ? (GetLowestTier1Y(topEdge) - 125f) : (topEdge - 72f);
            ReserveRowPosition(reservedTier2Rows, baseTier2Y, 135f);
        }
        else if (tier == EnemyTier.Tier3)
        {
            // Tier 3: Se coloca debajo de Tier 2 (o de Tier 1 si no hay Tier 2, o arriba si no hay menores)
            bool hasT2 = HasTierInWave(EnemyTier.Tier2);
            bool hasT1 = HasTierInWave(EnemyTier.Tier1);
            float baseTier3Y = hasT2 ? (GetLowestTier2Y(topEdge) - 145f)
                             : (hasT1 ? (GetLowestTier1Y(topEdge) - 145f)
                             : (topEdge - 88f));

            float screenLeft = GetScreenMinX();
            float screenRight = GetScreenMaxX();
            transform.position = new Vector3(Mathf.Clamp(transform.position.x, screenLeft, screenRight), transform.position.y, transform.position.z);

            int lane = activeTier3Count % 2;
            activeTier3Count++;
            myAssignedSettleY = baseTier3Y - (lane * 110f);
            settleY = myAssignedSettleY;

            if (transform.position.x > (screenLeft + screenRight) * 0.5f) patrolDirection = -1;
            else patrolDirection = 1;
        }
        else if (tier == EnemyTier.Tier4)
        {
            bool isBossFight = (GameManager.Instance != null && GameManager.Instance.IsInBossFight);
            float baseTier4Y;
            if (isBossFight)
            {
                baseTier4Y = 1380f;
            }
            else
            {
                bool hasT3 = HasTierInWave(EnemyTier.Tier3);
                bool hasT2 = HasTierInWave(EnemyTier.Tier2);
                bool hasT1 = HasTierInWave(EnemyTier.Tier1);

                if (hasT3) baseTier4Y = GetLowestTier3Y(topEdge) - 140f;
                else if (hasT2) baseTier4Y = GetLowestTier2Y(topEdge) - 135f;
                else if (hasT1) baseTier4Y = GetLowestTier1Y(topEdge) - 130f;
                else baseTier4Y = topEdge - 72f;
            }

            ReserveRowPosition(reservedTier4Rows, baseTier4Y, 135f);
        }
    }

    [Header("Invulnerabilidad de Entrada")]
    public float entryInvulnerabilityDuration = 1.0f;
    private float entryInvulnerabilityTimer = 0f;
    public bool IsEntryInvulnerable => entryInvulnerabilityTimer > 0f;

    void Start()
    {
        // 50% más de salud a los enemigos en modo dos jugadores
        if (GameManager.Instance != null && GameManager.Instance.IsPlayer2Joined)
            maxHealth = Mathf.Max(1, Mathf.RoundToInt(maxHealth * 1.50f));

        currentHealth = maxHealth;
        mainCam = Camera.main;

        // 1 segundo de invulnerabilidad al entrar con efecto visual blanco temporal
        entryInvulnerabilityTimer = entryInvulnerabilityDuration;
        SpriteHitFlash.TriggerOn(gameObject, entryInvulnerabilityDuration, 0.75f);

        // Asignación de puntajes por tier
        scoreValue = tier switch
        {
            EnemyTier.Tier1 => 1,
            EnemyTier.Tier2 => 3,
            EnemyTier.Tier3 => 6,
            EnemyTier.Tier4 => 10,
            _ => 1
        };

        // +8% de generación de balas para todos los enemigos (multiplica los tiempos de recarga por 1 / 1.08)
        const float bulletRateBoost = 1f / 1.08f;
        // +70% de tiempo de espera para disparar (tardan 70% más en disparar)
        const float enemyFireDelayBoost = 1.70f;
        // -15% de velocidad de generación de balas enemigas para todos los enemigos
        const float bulletGenReduction = 1.15f;

        if (tier == EnemyTier.Tier1)
        {
            aimsAtPlayer = Random.value < tier1AimChance;
            if (Mathf.Approximately(fireCadence, 1.275f) || Mathf.Approximately(fireCadence, 1.02f) || Mathf.Approximately(fireCadence, 1f) || Mathf.Approximately(fireCadence, 2.17f))
                fireCadence = (2.875f * bulletRateBoost) * enemyFireDelayBoost * bulletGenReduction; // +70% delay y -15% velocidad de generación
            else
                fireCadence *= (1.3225f * bulletRateBoost) * enemyFireDelayBoost * bulletGenReduction;

            if (Mathf.Approximately(tier1BulletSpeed, 240f) || Mathf.Approximately(tier1BulletSpeed, 168f) || Mathf.Approximately(tier1BulletSpeed, 276f) || Mathf.Approximately(tier1BulletSpeed, 207f))
                tier1BulletSpeed = 207f; // -25% velocidad bala (276 * 0.75)
            else
                tier1BulletSpeed *= 0.75f;
        }
        else if (tier == EnemyTier.Tier2)
        {
            tier2AimsAtPlayer = Random.value < tier2AimChance;
            if (Mathf.Approximately(fireCadence, 1f))
                fireCadence = (1.3225f * bulletRateBoost) * enemyFireDelayBoost * bulletGenReduction; // +70% delay y -15% velocidad de generación
            else
                fireCadence *= (1.3225f * bulletRateBoost) * enemyFireDelayBoost * bulletGenReduction;
        }
        else if (tier == EnemyTier.Tier3)
        {
            tier3AimsAtPlayer = Random.value < tier3AimChance;
            if (Mathf.Approximately(fireCadence, 1f))
                fireCadence = (1.3225f * bulletRateBoost) * enemyFireDelayBoost * bulletGenReduction; // +70% delay y -15% velocidad de generación
            else
                fireCadence *= (1.3225f * bulletRateBoost) * enemyFireDelayBoost * bulletGenReduction;
        }
        else if (tier == EnemyTier.Tier4)
        {
            laserTelegraphDuration = 2.5f * (2f / 3f); // -1/3 de duración de la marca antes de disparar (1.667s)
            laserCooldown *= (1.3225f * bulletRateBoost) * enemyFireDelayBoost * bulletGenReduction; // -15% velocidad de generación de láser
        }

        ComputeAndAssignFormationPosition();

        float spawnY = Mathf.Max(settleY + entryHeightAboveSettle, GetScreenTopY(150f));
        float spawnX = hasReservedPosition ? myReservedX : transform.position.x;
        transform.position = new Vector3(spawnX, spawnY, transform.position.z);

        var pc = FindAnyObjectByType<PlayerController>();
        if (pc != null) playerTransform = pc.transform;

        InitEnemyAnimation();
        StartCoroutine(EnterAndBehave());
    }

    void Update()
    {
        if (Time.timeScale == 0f || isDead) return;
        if (entryInvulnerabilityTimer > 0f)
        {
            entryInvulnerabilityTimer -= Time.deltaTime;
        }
        UpdateEnemyAnimation();
    }

    private void InitEnemyAnimation()
    {
        baseScale = transform.localScale;
        animPhaseOffset = Random.Range(0f, Mathf.PI * 2f);
        prevPosX = transform.position.x;

        SpriteRenderer rootSr = GetComponent<SpriteRenderer>();
        int baseOrder = rootSr != null ? rootSr.sortingOrder : 0;
        Material sharedMat = rootSr != null ? rootSr.sharedMaterial : null;

        GameObject glowObj = new GameObject("Vfx_CoreGlow");
        glowObj.transform.SetParent(transform, false);
        glowObj.transform.localPosition = tier == EnemyTier.Tier3 || tier == EnemyTier.Tier4
            ? Vector3.zero
            : new Vector3(0f, 0.25f, 0f);
        glowObj.transform.localScale = new Vector3(0.95f, 0.95f, 1f);
        vfxCoreGlowSr = glowObj.AddComponent<SpriteRenderer>();
        vfxCoreGlowSr.sprite = ChargeVisualUtils.GetGlowCircleSprite();
        if (sharedMat != null) vfxCoreGlowSr.sharedMaterial = sharedMat;
        vfxCoreGlowSr.sortingOrder = baseOrder - 1;

        if (tier == EnemyTier.Tier3 || tier == EnemyTier.Tier4)
        {
            GameObject vortexObj = new GameObject("Vfx_ChargeVortex");
            vortexObj.transform.SetParent(transform, false);
            vortexObj.transform.localPosition = Vector3.zero;
            vortexObj.transform.localScale = new Vector3(1.15f, 1.15f, 1f);
            vfxCoreVortexSr = vortexObj.AddComponent<SpriteRenderer>();
            vfxCoreVortexSr.sprite = ChargeVisualUtils.GetEnergyVortexSprite();
            if (sharedMat != null) vfxCoreVortexSr.sharedMaterial = sharedMat;
            vfxCoreVortexSr.sortingOrder = baseOrder + 1;
            vfxCoreVortexTr = vortexObj.transform;
            vfxCoreVortexSr.color = new Color(1f, 0.4f, 0.2f, 0f);
        }
    }

    private void TriggerRecoil(float strength = 1f)
    {
        recoilTimer = 0.14f;
        recoilStrength = Mathf.Max(0.5f, strength);
    }

    private void UpdateEnemyAnimation()
    {
        float dt = Time.deltaTime;
        float velX = dt > 0.0001f ? (transform.position.x - prevPosX) / dt : 0f;
        prevPosX = transform.position.x;

        float normInputX = Mathf.Clamp(velX / 240f, -1f, 1f);
        float maxBankDeg = tier switch
        {
            EnemyTier.Tier1 => 13f,
            EnemyTier.Tier2 => 10.5f,
            EnemyTier.Tier3 => 9f,
            _ => 6f
        };
        float foreshorten = tier == EnemyTier.Tier3 ? 0.09f : 0.08f;
        ChargeVisualUtils.EvalShipBank(ref bankAmount, normInputX, dt, maxBankDeg, foreshorten, out _, out float rollX);

        float t = Time.time + animPhaseOffset;

        float hoverFreq = tier switch { EnemyTier.Tier1 => 2.3f, EnemyTier.Tier2 => 2.0f, EnemyTier.Tier3 => 1.55f, _ => 1.75f };
        float hoverAmp = tier switch { EnemyTier.Tier1 => 1.5f, EnemyTier.Tier2 => 3.0f, EnemyTier.Tier3 => 4.0f, _ => 4.0f };

        float recoilKickY = 0f;
        if (recoilTimer > 0f)
        {
            recoilTimer = Mathf.Max(0f, recoilTimer - dt);
            float kickCurve = Mathf.Sin((recoilTimer / 0.14f) * Mathf.PI);
            float kickScale = tier switch
            {
                EnemyTier.Tier1 => 2.0f,
                EnemyTier.Tier2 => 3.5f,
                _ => 14f
            };
            recoilKickY = kickScale * recoilStrength * kickCurve;
        }

        if (hasSettledPos && !isRepositioning)
        {
            float hoverY = Mathf.Sin(t * hoverFreq) * hoverAmp;
            float deltaY = (hoverY - prevHoverY) + (recoilKickY - prevRecoilY);
            prevHoverY = hoverY;
            prevRecoilY = recoilKickY;
            if (Mathf.Abs(deltaY) > 0.0001f)
            {
                transform.position += new Vector3(0f, deltaY, 0f);
            }
        }
        else
        {
            prevHoverY = 0f;
            prevRecoilY = 0f;
        }

        // Mantener el casco siempre orientado verticalmente hacia abajo (sin giros diagonales al disparar recto)
        transform.localRotation = Quaternion.identity;
        transform.localScale = new Vector3(baseScale.x * rollX, baseScale.y, baseScale.z);

        UpdateEnemyVfx(t, dt);
    }

    private void UpdateEnemyVfx(float t, float dt)
    {
        if (vfxCoreGlowSr != null)
        {
            float pulse = 0.85f + 0.15f * Mathf.Sin(t * 9f);
            Color baseGlow = tier switch
            {
                EnemyTier.Tier1 => new Color(0.35f, 0.92f, 0.65f, 0.42f * pulse),
                EnemyTier.Tier2 => new Color(0.45f, 0.78f, 1f, 0.48f * pulse),
                EnemyTier.Tier3 => Color.Lerp(new Color(1f, 0.48f, 0.18f, 0.36f * pulse), new Color(1f, 0.85f, 0.28f, 0.92f), chargeVisualProgress),
                _ => Color.Lerp(new Color(1f, 0.25f, 0.25f, 0.34f * pulse), new Color(1f, 0.72f, 0.22f, 0.95f), chargeVisualProgress)
            };
            vfxCoreGlowSr.color = baseGlow;
            float gs = Mathf.Lerp(0.95f, 1.55f, chargeVisualProgress) * (0.94f + 0.10f * Mathf.Sin(t * 10f));
            vfxCoreGlowSr.transform.localScale = new Vector3(gs, gs, 1f);
        }

        if (vfxCoreVortexTr != null && vfxCoreVortexSr != null)
        {
            float spin = Mathf.Lerp(55f, 320f, chargeVisualProgress);
            vfxCoreVortexTr.Rotate(0f, 0f, spin * dt);
            float vs = Mathf.Lerp(0.85f, 1.45f, chargeVisualProgress);
            vfxCoreVortexTr.localScale = new Vector3(vs, vs, 1f);
            Color vc = tier == EnemyTier.Tier3
                ? new Color(1f, 0.62f, 0.20f, Mathf.Lerp(0f, 0.85f, chargeVisualProgress))
                : new Color(1f, 0.28f, 0.22f, Mathf.Lerp(0f, 0.90f, chargeVisualProgress));
            vfxCoreVortexSr.color = vc;
        }
    }

    void LateUpdate()
    {
        if (Time.timeScale == 0f) return;
        ClampToScreen();
    }

    private float GetScreenHorizontalLimit(bool isMax)
    {
        if (mainCam == null) mainCam = Camera.main;
        if (mainCam != null)
        {
            float horzExtent = mainCam.orthographicSize * mainCam.aspect;
            float paddingX = enemyScreenPadding;
            if (TryGetComponent<SpriteRenderer>(out var sr) && sr.sprite != null) paddingX = Mathf.Max(paddingX, sr.bounds.extents.x);
            else if (TryGetComponent<Collider2D>(out var col)) paddingX = Mathf.Max(paddingX, col.bounds.extents.x);

            return isMax ? (mainCam.transform.position.x + horzExtent - paddingX) : (mainCam.transform.position.x - horzExtent + paddingX);
        }
        return isMax ? repositionMaxX : repositionMinX;
    }

    public float GetScreenMinX() => GetScreenHorizontalLimit(false);
    public float GetScreenMaxX() => GetScreenHorizontalLimit(true);

    void ClampToScreen()
    {
        if (mainCam == null) mainCam = Camera.main;
        if (mainCam == null) return;

        float horzExtent = mainCam.orthographicSize * mainCam.aspect;
        float vertExtent = mainCam.orthographicSize;
        float pad = enemyScreenPadding;

        float minScreenX = mainCam.transform.position.x - horzExtent + pad;
        float maxScreenX = mainCam.transform.position.x + horzExtent - pad;
        float minScreenY = mainCam.transform.position.y - vertExtent + pad;
        float maxScreenY = mainCam.transform.position.y + vertExtent - pad;

        Vector3 pos = transform.position;
        if (!hasEnteredScreen)
        {
            if (pos.x >= minScreenX && pos.x <= maxScreenX && pos.y >= minScreenY && pos.y <= maxScreenY)
            {
                hasEnteredScreen = true;
                SoundController.Instance?.PlayEnemyEntrySfx(tier);
            }
        }

        if (hasEnteredScreen)
        {
            if (tier == EnemyTier.Tier3)
            {
                if (pos.x >= maxScreenX) patrolDirection = -1;
                else if (pos.x <= minScreenX) patrolDirection = 1;
            }
            pos.x = Mathf.Clamp(pos.x, minScreenX, maxScreenX);
            pos.y = Mathf.Clamp(pos.y, minScreenY, maxScreenY);
            transform.position = pos;
        }
    }

    IEnumerator EnterAndBehave()
    {
        float targetSettleY = myAssignedSettleY;

        while (transform.position.y > targetSettleY)
        {
            transform.position += Vector3.down * entrySpeed * Time.deltaTime;
            yield return null;
        }

        transform.position = new Vector3(transform.position.x, targetSettleY, transform.position.z);
        hasSettledPos = true;

        if (tier == EnemyTier.Tier1)
        {
            StartCoroutine(RepositionToTarget());
            StartCoroutine(FireTier1());
        }
        else if (tier == EnemyTier.Tier2)
        {
            StartCoroutine(PatrolHorizontalTier2());
            StartCoroutine(FireTier2());
        }
        else if (tier == EnemyTier.Tier3)
        {
            StartCoroutine(PatrolHorizontalTier3());
            StartCoroutine(FireTier3());
        }
        else if (tier == EnemyTier.Tier4)
        {
            StartCoroutine(RepositionToTarget());
            StartCoroutine(FireTier4());
        }
    }

    IEnumerator PatrolHorizontalTier2()
    {
        // 50% y 50% de probabilidad para iniciar a la izquierda (-1) o derecha (+1)
        int dir = (Random.value < 0.5f) ? -1 : 1;
        float changeTimer = Random.Range(4f, 9f);

        while (true)
        {
            float dt = Time.deltaTime;
            transform.position += Vector3.right * dir * moveSpeed * dt;
            float leftBound = GetScreenMinX();
            float rightBound = GetScreenMaxX();

            if (transform.position.x >= rightBound)
            {
                transform.position = new Vector3(rightBound, transform.position.y, transform.position.z);
                dir = -1;
            }
            else if (transform.position.x <= leftBound)
            {
                transform.position = new Vector3(leftBound, transform.position.y, transform.position.z);
                dir = 1;
            }

            changeTimer -= dt;
            if (changeTimer <= 0f)
            {
                dir = -dir;
                changeTimer = Random.Range(4f, 9f);
            }

            yield return null;
        }
    }

    IEnumerator PatrolHorizontalTier3()
    {
        while (true)
        {
            transform.position += Vector3.right * patrolDirection * moveSpeed * Time.deltaTime;
            float leftBound = GetScreenMinX();
            float rightBound = GetScreenMaxX();

            if (transform.position.x >= rightBound)
            {
                transform.position = new Vector3(rightBound, transform.position.y, transform.position.z);
                patrolDirection = -1;
            }
            else if (transform.position.x <= leftBound)
            {
                transform.position = new Vector3(leftBound, transform.position.y, transform.position.z);
                patrolDirection = 1;
            }
            yield return null;
        }
    }

    private Vector3 GetAimDirection(bool shouldAim = true)
    {
        if (shouldAim)
        {
            if (playerTransform == null)
            {
                var pc = FindAnyObjectByType<PlayerController>();
                if (pc != null) playerTransform = pc.transform;
            }
            if (playerTransform != null)
            {
                Vector3 diff = playerTransform.position - transform.position;
                if (diff.sqrMagnitude > 0.001f) return diff.normalized;
            }
        }
        return Vector3.down;
    }

    IEnumerator FireTier1()
    {
        yield return new WaitForSeconds(0.8f); // Respiro inicial al posicionarse en pantalla
        while (true)
        {
            FireTier1TripleShot();
            TriggerRecoil(0.35f);
            yield return new WaitForSeconds(fireCadence);
        }
    }

    private void FireTier1TripleShot()
    {
        if (bulletPrefab == null) return;

        // 60% hacia el jugador, 40% simplemente hacia abajo
        Vector3 baseDir = (Random.value < tier1AimChance) ? GetAimDirection(true) : Vector3.down;

        const float spreadAngle = 40f; // Dispersión aumentada de los proyectiles laterales
        Vector3 dirCenter = baseDir;
        Vector3 dirLeft = Quaternion.Euler(0f, 0f, -spreadAngle) * baseDir;
        Vector3 dirRight = Quaternion.Euler(0f, 0f, spreadAngle) * baseDir;

        FireBullet(bulletPrefab, tier1BulletSpeed, dirCenter);
        FireBullet(bulletPrefab, tier1BulletSpeed, dirLeft);
        FireBullet(bulletPrefab, tier1BulletSpeed, dirRight);
    }

    IEnumerator FireTier2()
    {
        yield return new WaitForSeconds(1.0f); // Respiro inicial al posicionarse en pantalla
        while (true)
        {
            FireTier2SemicirclePulse();
            TriggerRecoil(0.5f);
            yield return new WaitForSeconds(fireCadence);
        }
    }

    private void FireTier2SemicirclePulse()
    {
        if (bulletPrefab == null) return;

        // 60% hacia el jugador, 40% simplemente hacia abajo
        Vector3 aimDir = (Random.value < tier2AimChance) ? GetAimDirection(true) : Vector3.down;

        // Media pulsación: semicircunferencia (180 grados) de proyectiles
        // 7 proyectiles distribuidos uniformemente desde -90° hasta +90° respecto a aimDir
        const int bulletCount = 7;
        const float totalArc = 180f;
        float startAngle = -totalArc * 0.5f; // -90°
        float step = totalArc / (bulletCount - 1); // 30° entre cada proyectil

        for (int i = 0; i < bulletCount; i++)
        {
            float offsetAngle = startAngle + (i * step);
            Vector3 bulletDir = (Quaternion.Euler(0f, 0f, offsetAngle) * aimDir).normalized;
            FireBullet(bulletPrefab, 300f, bulletDir); // -25% de velocidad (400 * 0.75)
        }
    }

    IEnumerator FireTier3()
    {
        yield return new WaitForSeconds(1.0f); // Breve espera inicial tras posicionarse
        while (true)
        {
            // Fase de carga de energía / telegrafiado visual de la bala grande
            float chargeElapsed = 0f;
            const float bigOrbWindup = 1.1f;
            while (chargeElapsed < bigOrbWindup)
            {
                chargeElapsed += Time.deltaTime;
                chargeVisualProgress = Mathf.Clamp01(chargeElapsed / bigOrbWindup);
                yield return null;
            }
            chargeVisualProgress = 0f;

            // 60% hacia el jugador, 40% simplemente hacia abajo
            Vector3 shootDir = (Random.value < tier3AimChance) ? GetAimDirection(true) : Vector3.down;
            Vector3 spawnOffset = shootDir * bigBulletSpawnOffset;
            GameObject bigBullet = FireBullet(bigBulletPrefab, tier3BigBulletSpeed, shootDir, spawnOffset);

            if (bigBullet != null && bigBullet.TryGetComponent<EnemyBullet>(out var eb))
            {
                // Distancia antes de estallar en racimo de 20 fragmentos
                float randomTravelDist = Random.Range(240f, 540f);
                eb.SetupClusterExplosion(bulletPrefab, randomTravelDist, 262.5f); // -25% de velocidad fragmentos (350 * 0.75)
            }

            TriggerRecoil(1.25f);

            // Intervalo de recarga antes del siguiente disparo
            yield return new WaitForSeconds(fireCadence);
        }
    }

    IEnumerator FireTier4()
    {
        yield return new WaitForSeconds(1f);
        while (true)
        {
            Vector3 dir = GetAimDirection(true);
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + 90f;
            Quaternion rot = Quaternion.Euler(0f, 0f, angle);
            Vector3 beamCenter = transform.position + dir * (laserBeamLength * 0.5f);

            activeTelegraph = telegraphPrefab != null
                ? Instantiate(telegraphPrefab, beamCenter, rot)
                : CreateDefaultTelegraphLaser(beamCenter, rot, laserBeamLength, laserTelegraphWidth);
            activeTelegraph.transform.localScale = new Vector3(laserTelegraphWidth, laserBeamLength, 1f);

            float elapsed = 0f;
            while (elapsed < laserTelegraphDuration)
            {
                elapsed += Time.deltaTime;
                chargeVisualProgress = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, laserTelegraphDuration));
                if (activeTelegraph != null) activeTelegraph.transform.position = transform.position + dir * (laserBeamLength * 0.5f);
                yield return null;
            }

            chargeVisualProgress = 0f;
            if (activeTelegraph != null) { Destroy(activeTelegraph); activeTelegraph = null; }

            Vector3 finalBeamCenter = transform.position + dir * (laserBeamLength * 0.5f);
            if (laserPrefab != null)
            {
                GameObject realLaser = Instantiate(laserPrefab, finalBeamCenter, rot);
                realLaser.transform.localScale = new Vector3(laserBeamWidth, laserBeamLength, 1f);
                SoundController.Instance?.PlayTier4LaserSfx();
                TriggerRecoil(1.4f);
            }

            yield return new WaitForSeconds(0.18f);

            yield return new WaitForSeconds(Mathf.Max(0.1f, laserCooldown - laserTelegraphDuration - 0.18f));
        }
    }

    public static int GetActiveRowCount(Dictionary<int, List<float>> dict)
    {
        int count = 0;
        foreach (var kvp in dict)
            if (kvp.Value != null && kvp.Value.Count > 0) count = Mathf.Max(count, kvp.Key + 1);
        return count;
    }

    public static int GetActiveTier1RowCount() => GetActiveRowCount(reservedTier1Rows);
    public static int GetActiveTier2RowCount() => GetActiveRowCount(reservedTier2Rows);
    public static int GetActiveTier4RowCount() => GetActiveRowCount(reservedTier4Rows);

    IEnumerator RepositionToTarget()
    {
        isRepositioning = true;
        Vector3 target = new Vector3(myReservedX, myAssignedSettleY, 0f);
        while (Vector3.Distance(transform.position, target) > 5f)
        {
            transform.position = Vector3.MoveTowards(transform.position, target, repositionSpeed * Time.deltaTime);
            yield return null;
        }
        transform.position = target;
        isRepositioning = false;
    }

    float PickNonOverlappingX(List<float> reservedPositions)
    {
        float minBound = (mainCam != null) ? GetScreenMinX() : repositionMinX;
        float maxBound = (mainCam != null) ? GetScreenMaxX() : repositionMaxX;

        for (int attempt = 0; attempt < 50; attempt++)
        {
            float candidate = Random.Range(minBound, maxBound);
            bool tooClose = false;
            foreach (float reserved in reservedPositions)
            {
                if (Mathf.Abs(candidate - reserved) < minSeparationFromOthers) { tooClose = true; break; }
            }
            if (!tooClose) return candidate;
        }

        if (reservedPositions.Count > 0)
        {
            List<float> sorted = new List<float>(reservedPositions);
            sorted.Sort();
            float bestX = (minBound + maxBound) * 0.5f;
            float maxGap = sorted[0] - minBound;

            for (int i = 0; i < sorted.Count - 1; i++)
            {
                float gap = sorted[i + 1] - sorted[i];
                if (gap > maxGap) { maxGap = gap; bestX = sorted[i] + gap * 0.5f; }
            }
            if (maxBound - sorted[sorted.Count - 1] > maxGap)
                bestX = sorted[sorted.Count - 1] + (maxBound - sorted[sorted.Count - 1]) * 0.5f;

            return bestX;
        }
        return Random.Range(minBound, maxBound);
    }

    GameObject CreateDefaultTelegraphLaser(Vector3 center, Quaternion rot, float length, float width)
    {
        GameObject obj = new GameObject("TelegraphLaser_Auto");
        obj.transform.position = center;
        obj.transform.rotation = rot;
        obj.transform.localScale = new Vector3(width, length, 1f);

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
        sr.color = new Color(1f, 0.12f, 0.12f, 0.38f);
        sr.sortingOrder = 4;
        return obj;
    }

    GameObject FireBullet(GameObject prefab, float speed, Vector3? customDirection = null, Vector3? spawnOffset = null)
    {
        if (prefab == null) return null;
        Vector3 dir = customDirection ?? Vector3.down;
        float angle = (dir.sqrMagnitude > 0.001f) ? Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + 90f : 0f;

        GameObject obj = Instantiate(prefab, transform.position + (spawnOffset ?? Vector3.zero), Quaternion.Euler(0f, 0f, angle));
        if (obj.TryGetComponent<EnemyBullet>(out var eb))
        {
            eb.isBigBullet = (prefab == bigBulletPrefab);
            eb.speed = speed;
            eb.direction = dir;
            eb.AlignRotationWithDirection();
        }
        return obj;
    }

    private void ReserveRowPosition(Dictionary<int, List<float>> rowDict, float baseY, float spacing = -1f)
    {
        float actualSpacing = (spacing > 0f) ? spacing : rowVerticalSpacing;
        int targetRow = 0;
        while (true)
        {
            if (!rowDict.ContainsKey(targetRow)) rowDict[targetRow] = new List<float>();
            if (rowDict[targetRow].Count < MAX_ENEMIES_PER_ROW) break;
            targetRow++;
        }

        myRowIndex = targetRow;
        myAssignedSettleY = baseY - (myRowIndex * actualSpacing);
        settleY = myAssignedSettleY;

        if (!hasReservedPosition)
        {
            myReservedX = PickNonOverlappingX(rowDict[myRowIndex]);
            hasReservedPosition = true;
            rowDict[myRowIndex].Add(myReservedX);
        }
    }

    void OnDestroy()
    {
        if (activeTelegraph != null) Destroy(activeTelegraph);

        if (hasReservedPosition)
        {
            if (tier == EnemyTier.Tier1 && reservedTier1Rows.ContainsKey(myRowIndex)) reservedTier1Rows[myRowIndex].Remove(myReservedX);
            else if (tier == EnemyTier.Tier2 && reservedTier2Rows.ContainsKey(myRowIndex)) reservedTier2Rows[myRowIndex].Remove(myReservedX);
            else if (tier == EnemyTier.Tier4 && reservedTier4Rows.ContainsKey(myRowIndex)) reservedTier4Rows[myRowIndex].Remove(myReservedX);
        }
        if (tier == EnemyTier.Tier3) activeTier3Count = Mathf.Max(0, activeTier3Count - 1);
    }

    public static void ClearReservedPositions()
    {
        reservedTier1Rows.Clear();
        reservedTier2Rows.Clear();
        reservedTier4Rows.Clear();
        activeTier3Count = 0;
    }

    public void TakeDamage(int amount) => TakeDamage((float)amount);

    public void TakeDamage(float amount)
    {
        if (Time.timeScale == 0f || isDead || entryInvulnerabilityTimer > 0f) return;
        currentHealth -= amount;
        if (currentHealth <= 0.001f)
        {
            Die();
            return;
        }
        SpriteHitFlash.TriggerOn(gameObject);
    }

    public void DoubleHealth()
    {
        maxHealth = Mathf.Max(1, Mathf.RoundToInt(maxHealth * 1.50f));
        currentHealth *= 1.50f;
    }

    public bool IsDead() => isDead || currentHealth <= 0.001f;

    void Die()
    {
        if (isDead) return;
        isDead = true;

        SoundController.Instance?.PlayEnemyDeathSfx();
        if (givesScore) GameManager.Instance?.AddScore(scoreValue);
        WaveController.Instance?.OnEnemyDestroyed();

        Material mat = TryGetComponent<SpriteRenderer>(out var sr) ? sr.sharedMaterial : null;
        EnemyExplosion.Spawn(transform.position, tier, baseScale, mat, explosionSprite);
        Destroy(gameObject);
    }

    public void DisableScoring() => givesScore = false;
}