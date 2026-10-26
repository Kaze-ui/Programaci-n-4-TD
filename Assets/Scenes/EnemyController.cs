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
    public float tier1BulletSpeed = 276f; // +15% velocidad bala normal (240 * 1.15)
    public float tier3BigBulletSpeed = 240f;

    [Header("Láser y Señalización (Tier 4)")]
    public GameObject telegraphPrefab;
    public float laserCooldown = 4f;
    public float laserTelegraphDuration = 2.5f;
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
    [Range(0f, 1f)] public float tier2AimChance = 0.40f;
    [Range(0f, 1f)] public float tier3AimChance = 0.55f;
    private bool aimsAtPlayer = false;
    private bool tier2AimsAtPlayer = false;
    private bool tier3AimsAtPlayer = false;

    public float bigBulletSpawnOffset = 180f;

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

    void Start()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsPlayer2Joined)
            maxHealth = Mathf.Max(1, Mathf.RoundToInt(maxHealth * 1.9f));

        currentHealth = maxHealth;
        mainCam = Camera.main;

        // Asignación de puntajes por tier
        scoreValue = tier switch
        {
            EnemyTier.Tier1 => 1,
            EnemyTier.Tier2 => 3,
            EnemyTier.Tier3 => 6,
            EnemyTier.Tier4 => 10,
            _ => 1
        };

        if (tier == EnemyTier.Tier1)
        {
            aimsAtPlayer = Random.value < tier1AimChance;
            if (Mathf.Approximately(fireCadence, 1.275f) || Mathf.Approximately(fireCadence, 1.02f) || Mathf.Approximately(fireCadence, 1f) || Mathf.Approximately(fireCadence, 2.17f))
                fireCadence = 2.50f; // -15% velocidad de generación (2.17s * 1.15)
            else
                fireCadence *= 1.15f;

            if (Mathf.Approximately(tier1BulletSpeed, 240f) || Mathf.Approximately(tier1BulletSpeed, 168f))
                tier1BulletSpeed = 276f; // +15% velocidad bala (240 * 1.15)
        }
        else if (tier == EnemyTier.Tier2)
        {
            tier2AimsAtPlayer = Random.value < tier2AimChance;
            if (Mathf.Approximately(fireCadence, 1f))
                fireCadence = 1.15f; // -15% velocidad de generación (1.0s * 1.15)
            else
                fireCadence *= 1.15f;
        }
        else if (tier == EnemyTier.Tier3)
        {
            tier3AimsAtPlayer = Random.value < tier3AimChance;
            if (Mathf.Approximately(fireCadence, 1f))
                fireCadence = 1.15f; // -15% velocidad de generación (1.0s * 1.15)
            else
                fireCadence *= 1.15f;
        }
        else if (tier == EnemyTier.Tier4)
        {
            laserCooldown *= 1.15f; // -15% frecuencia de disparo del láser
            bool isBossFight = (GameManager.Instance != null && GameManager.Instance.IsInBossFight);
            float baseTier4Y = isBossFight ? 1380f : 1150f;

            if (!isBossFight)
            {
                int activeT1 = GetActiveRowCount(reservedTier1Rows);
                int activeT2 = GetActiveRowCount(reservedTier2Rows);
                float lowestT1 = (activeT1 > 0) ? (1450f - (activeT1 * rowVerticalSpacing)) : 1600f;
                float lowestT2 = (activeT2 > 0) ? (1300f - (activeT2 * rowVerticalSpacing)) : 1600f;
                float lowestAbove = Mathf.Min(lowestT1, lowestT2);
                if (lowestAbove < 1600f) baseTier4Y = Mathf.Min(baseTier4Y, lowestAbove - 20f);
            }

            ReserveRowPosition(reservedTier4Rows, baseTier4Y);
            transform.position = new Vector3(myReservedX, transform.position.y, transform.position.z);
        }

        float spawnY = Mathf.Max(settleY + entryHeightAboveSettle, GetScreenTopY(150f));
        transform.position = new Vector3(transform.position.x, spawnY, transform.position.z);

        var pc = FindAnyObjectByType<PlayerController>();
        if (pc != null) playerTransform = pc.transform;

        StartCoroutine(EnterAndBehave());
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
                hasEnteredScreen = true;
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
        if (tier == EnemyTier.Tier1)
        {
            ReserveRowPosition(reservedTier1Rows, settleY);
        }
        else if (tier == EnemyTier.Tier2)
        {
            int activeTier1Rows = GetActiveRowCount(reservedTier1Rows);
            float baseTier2Y = (activeTier1Rows > 0) ? (1450f - (activeTier1Rows * rowVerticalSpacing)) : settleY;
            ReserveRowPosition(reservedTier2Rows, baseTier2Y);
        }
        else if (tier == EnemyTier.Tier3)
        {
            float screenLeft = GetScreenMinX();
            float screenRight = GetScreenMaxX();
            transform.position = new Vector3(Mathf.Clamp(transform.position.x, screenLeft, screenRight), transform.position.y, transform.position.z);

            int lane = activeTier3Count % 3;
            activeTier3Count++;
            myAssignedSettleY = settleY - (lane * 80f);

            if (transform.position.x > (screenLeft + screenRight) * 0.5f) patrolDirection = -1;
            else patrolDirection = 1;
        }

        float targetSettleY = (tier == EnemyTier.Tier1 || tier == EnemyTier.Tier2 || tier == EnemyTier.Tier4 || tier == EnemyTier.Tier3)
            ? myAssignedSettleY : settleY;

        while (transform.position.y > targetSettleY)
        {
            transform.position += Vector3.down * entrySpeed * Time.deltaTime;
            yield return null;
        }

        transform.position = new Vector3(transform.position.x, targetSettleY, transform.position.z);

        if (tier == EnemyTier.Tier1)
        {
            StartCoroutine(RepositionToTarget());
            StartCoroutine(FireTier1());
        }
        else if (tier == EnemyTier.Tier2)
        {
            StartCoroutine(RepositionToTarget());
            StartCoroutine(FireTier2());
        }
        else if (tier == EnemyTier.Tier3)
        {
            StartCoroutine(PatrolHorizontalTier3());
            StartCoroutine(FireTier3());
        }
        else if (tier == EnemyTier.Tier4)
        {
            StartCoroutine(FireTier4());
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
        while (true)
        {
            yield return new WaitForSeconds(fireCadence);
            FireBullet(bulletPrefab, tier1BulletSpeed, GetAimDirection(aimsAtPlayer));
        }
    }

    IEnumerator FireTier2()
    {
        while (true)
        {
            yield return new WaitForSeconds(fireCadence);
            FireBullet(bulletPrefab, 460f, GetAimDirection(tier2AimsAtPlayer));
            yield return new WaitForSeconds(0.575f);
            FireBullet(bulletPrefab, 460f, GetAimDirection(tier2AimsAtPlayer));
        }
    }

    IEnumerator FireTier3()
    {
        while (true)
        {
            yield return new WaitForSeconds(fireCadence);
            for (int i = 0; i < 3; i++)
            {
                FireBullet(bulletPrefab, 460f, GetAimDirection(tier3AimsAtPlayer));
                yield return new WaitForSeconds(0.575f);
            }
            yield return new WaitForSeconds(1.725f);
            Vector3 dir = GetAimDirection(true);
            FireBullet(bigBulletPrefab, tier3BigBulletSpeed, dir, dir * bigBulletSpawnOffset);
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
                if (activeTelegraph != null) activeTelegraph.transform.position = transform.position + dir * (laserBeamLength * 0.5f);
                yield return null;
            }

            if (activeTelegraph != null) { Destroy(activeTelegraph); activeTelegraph = null; }

            Vector3 finalBeamCenter = transform.position + dir * (laserBeamLength * 0.5f);
            if (laserPrefab != null)
            {
                GameObject realLaser = Instantiate(laserPrefab, finalBeamCenter, rot);
                realLaser.transform.localScale = new Vector3(laserBeamWidth, laserBeamLength, 1f);
                SoundController.Instance?.PlayTier4LaserSfx();
            }

            yield return new WaitForSeconds(Mathf.Max(0.1f, laserCooldown - laserTelegraphDuration));
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
        Vector3 target = new Vector3(myReservedX, myAssignedSettleY, 0f);
        while (Vector3.Distance(transform.position, target) > 5f)
        {
            transform.position = Vector3.MoveTowards(transform.position, target, repositionSpeed * Time.deltaTime);
            yield return null;
        }
        transform.position = target;
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
        if (bulletPrefab != null && bulletPrefab.TryGetComponent<SpriteRenderer>(out var bSr))
        {
            sr.sprite = bSr.sprite;
            sr.material = bSr.sharedMaterial;
        }
        sr.color = new Color(1f, 0.12f, 0.12f, 0.38f);
        sr.sortingOrder = 4;
        return obj;
    }

    void FireBullet(GameObject prefab, float speed, Vector3? customDirection = null, Vector3? spawnOffset = null)
    {
        if (prefab == null) return;
        Vector3 dir = customDirection ?? Vector3.down;
        float angle = (dir.sqrMagnitude > 0.001f) ? Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + 90f : 0f;

        GameObject obj = Instantiate(prefab, transform.position + (spawnOffset ?? Vector3.zero), Quaternion.Euler(0f, 0f, angle));
        if (obj.TryGetComponent<EnemyBullet>(out var eb))
        {
            eb.speed = speed;
            eb.direction = dir;
            eb.AlignRotationWithDirection();
        }
    }

    private void ReserveRowPosition(Dictionary<int, List<float>> rowDict, float baseY)
    {
        int targetRow = 0;
        while (true)
        {
            if (!rowDict.ContainsKey(targetRow)) rowDict[targetRow] = new List<float>();
            if (rowDict[targetRow].Count < MAX_ENEMIES_PER_ROW) break;
            targetRow++;
        }

        myRowIndex = targetRow;
        myAssignedSettleY = baseY - (myRowIndex * rowVerticalSpacing);
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
        if (Time.timeScale == 0f || isDead) return;
        currentHealth -= amount;
        if (currentHealth <= 0.001f) Die();
    }

    public void DoubleHealth()
    {
        maxHealth = Mathf.Max(1, Mathf.RoundToInt(maxHealth * 1.9f));
        currentHealth *= 1.9f;
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
        EnemyExplosion.Spawn(transform.position, tier, transform.localScale, mat, explosionSprite);
        Destroy(gameObject);
    }

    public void DisableScoring() => givesScore = false;
}