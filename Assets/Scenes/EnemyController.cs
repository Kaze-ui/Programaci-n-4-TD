using UnityEngine;
using System.Collections;

public enum EnemyTier { Tier1, Tier2, Tier3, Tier4 }

public class EnemyController : MonoBehaviour, IDamageable
{
    [Header("Tier")]
    public EnemyTier tier;

    [Header("Vida y puntaje")]
    public int maxHealth = 1;
    private float currentHealth;
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

    // Estructuras por hilera (row) para acumular hasta 10 enemigos por hilera sin superponerse
    private const int MAX_ENEMIES_PER_ROW = 10;
    public float rowVerticalSpacing = 140f; // Distancia vertical entre hileras

    private static System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<float>> reservedTier1Rows = 
        new System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<float>>();

    private static System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<float>> reservedTier2Rows = 
        new System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<float>>();

    private static System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<float>> reservedTier4Rows = 
        new System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<float>>();

    private static int activeTier3Count = 0;

    private float myReservedX;
    private int myRowIndex = 0;
    private float myAssignedSettleY;
    private bool hasReservedPosition = false;

    [Header("Disparo")]
    public GameObject bulletPrefab;
    public GameObject bigBulletPrefab; // solo Tier3
    public GameObject laserPrefab;     // solo Tier4
    public float fireCadence = 1f;
    public float tier1BulletSpeed = 240f; // 40% menos de velocidad que la base (400f -> 240f)

    [Header("Láser y Señalización (Tier 4)")]
    public GameObject telegraphPrefab;
    public float laserCooldown = 3f;             // Intervalo de ciclo de disparo (cada 3 segundos)
    public float laserTelegraphDuration = 1.5f;   // Duración de la mira/aviso rojo antes del láser real (1.5 segundos)
    public float laserBeamLength = 5250f;         // Longitud para atravesar toda la pantalla (+50% respecto a 3500)
    public float laserBeamWidth = 60f;            // Ancho del láser real
    public float laserTelegraphWidth = 25f;       // Ancho del aviso rojo
    private GameObject activeTelegraph;

    private int patrolDirection = 1;
    private Transform playerTransform;

    [Header("Límites de pantalla")]
    public float enemyScreenPadding = 25f;
    private Camera mainCam;
    private bool hasEnteredScreen = false;

    [Header("Comportamiento de disparo Tier 1")]
    [Range(0f, 1f)] public float tier1AimChance = 0.60f;
    private bool aimsAtPlayer = false;

    [Header("Comportamiento de disparo Tier 2")]
    [Range(0f, 1f)] public float tier2AimChance = 0.40f;
    private bool tier2AimsAtPlayer = false;

    [Header("Comportamiento de disparo Tier 3")]
    [Range(0f, 1f)] public float tier3AimChance = 0.55f;
    private bool tier3AimsAtPlayer = false;

    [Header("Offset de aparición de balas")]
    public float bigBulletSpawnOffset = 180f;

    public float GetScreenTopY(float extraPadding = 150f)
    {
        if (mainCam == null) mainCam = Camera.main;
        if (mainCam != null)
        {
            float paddingY = enemyScreenPadding;
            if (TryGetComponent<SpriteRenderer>(out var sr) && sr.sprite != null)
                paddingY = Mathf.Max(paddingY, sr.bounds.extents.y);
            else if (TryGetComponent<Collider2D>(out var col))
                paddingY = Mathf.Max(paddingY, col.bounds.extents.y);
            return mainCam.transform.position.y + mainCam.orthographicSize + paddingY + extraPadding;
        }
        return 1800f;
    }

    void Start()
    {
        currentHealth = maxHealth;
        mainCam = Camera.main;

        if (tier == EnemyTier.Tier1)
        {
            aimsAtPlayer = Random.value < tier1AimChance; // 60% de probabilidad por enemigo
            // Disminución del 25% de cadencia (mayor intervalo entre disparos)
            if (Mathf.Approximately(fireCadence, 1.02f) || Mathf.Approximately(fireCadence, 1f))
            {
                fireCadence = 1.275f;
            }
        }
        else if (tier == EnemyTier.Tier2)
        {
            tier2AimsAtPlayer = Random.value < tier2AimChance; // 40% de probabilidad por enemigo
        }
        else if (tier == EnemyTier.Tier3)
        {
            tier3AimsAtPlayer = Random.value < tier3AimChance; // 55% de probabilidad por enemigo
        }
        else if (tier == EnemyTier.Tier4)
        {
            bool isBossFight = (GameManager.Instance != null && GameManager.Instance.IsInBossFight);
            float baseTier4Y = isBossFight ? 1380f : 1150f;

            if (!isBossFight)
            {
                // Si hay hileras de Tier 1 o Tier 2 activas, colocarse debajo de ellas
                int activeT1 = GetActiveTier1RowCount();
                int activeT2 = GetActiveTier2RowCount();
                float lowestT1 = (activeT1 > 0) ? (1450f - (activeT1 * rowVerticalSpacing)) : 1600f;
                float lowestT2 = (activeT2 > 0) ? (1300f - (activeT2 * rowVerticalSpacing)) : 1600f;
                float lowestAbove = Mathf.Min(lowestT1, lowestT2);
                if (lowestAbove < 1600f)
                {
                    baseTier4Y = Mathf.Min(baseTier4Y, lowestAbove - 20f);
                }
            }

            int targetRow = 0;
            while (true)
            {
                if (!reservedTier4Rows.ContainsKey(targetRow))
                    reservedTier4Rows[targetRow] = new System.Collections.Generic.List<float>();

                if (reservedTier4Rows[targetRow].Count < MAX_ENEMIES_PER_ROW)
                    break;

                targetRow++;
            }
            myRowIndex = targetRow;
            myAssignedSettleY = baseTier4Y - (myRowIndex * rowVerticalSpacing);
            settleY = myAssignedSettleY;

            float randomX = PickNonOverlappingX(reservedTier4Rows[myRowIndex]);
            myReservedX = randomX;
            hasReservedPosition = true;
            reservedTier4Rows[myRowIndex].Add(myReservedX);
            transform.position = new Vector3(randomX, transform.position.y, transform.position.z);
        }

        // Sin importar qué punto de spawn le tocó (arriba/izquierda/derecha), lo reposicionamos
        // en Y para que SIEMPRE arranque completamente por fuera del borde superior de la pantalla.
        // Así garantizamos que la entrada sea siempre deslizándose verticalmente desde arriba.
        float spawnY = Mathf.Max(settleY + entryHeightAboveSettle, GetScreenTopY(150f));
        transform.position = new Vector3(transform.position.x, spawnY, transform.position.z);

        PlayerController pc = FindAnyObjectByType<PlayerController>();
        if (pc != null) playerTransform = pc.transform;

        StartCoroutine(EnterAndBehave());
    }

    void LateUpdate()
    {
        if (Time.timeScale == 0f) return;
        ClampToScreen();
    }

    public float GetScreenMinX()
    {
        if (mainCam == null) mainCam = Camera.main;
        if (mainCam != null)
        {
            float horzExtent = mainCam.orthographicSize * mainCam.aspect;
            float paddingX = enemyScreenPadding;
            if (TryGetComponent<SpriteRenderer>(out var sr) && sr.sprite != null)
                paddingX = Mathf.Max(paddingX, sr.bounds.extents.x);
            else if (TryGetComponent<Collider2D>(out var col))
                paddingX = Mathf.Max(paddingX, col.bounds.extents.x);
            return mainCam.transform.position.x - horzExtent + paddingX;
        }
        return repositionMinX;
    }

    public float GetScreenMaxX()
    {
        if (mainCam == null) mainCam = Camera.main;
        if (mainCam != null)
        {
            float horzExtent = mainCam.orthographicSize * mainCam.aspect;
            float paddingX = enemyScreenPadding;
            if (TryGetComponent<SpriteRenderer>(out var sr) && sr.sprite != null)
                paddingX = Mathf.Max(paddingX, sr.bounds.extents.x);
            else if (TryGetComponent<Collider2D>(out var col))
                paddingX = Mathf.Max(paddingX, col.bounds.extents.x);
            return mainCam.transform.position.x + horzExtent - paddingX;
        }
        return repositionMaxX;
    }

    void ClampToScreen()
    {
        if (mainCam == null) mainCam = Camera.main;
        if (mainCam == null) return;

        float vertExtent = mainCam.orthographicSize;
        float horzExtent = vertExtent * mainCam.aspect;

        float paddingX = enemyScreenPadding;
        float paddingY = enemyScreenPadding;

        if (TryGetComponent<SpriteRenderer>(out var sr) && sr.sprite != null)
        {
            paddingX = Mathf.Max(paddingX, sr.bounds.extents.x);
            paddingY = Mathf.Max(paddingY, sr.bounds.extents.y);
        }
        else if (TryGetComponent<Collider2D>(out var col))
        {
            paddingX = Mathf.Max(paddingX, col.bounds.extents.x);
            paddingY = Mathf.Max(paddingY, col.bounds.extents.y);
        }

        float minScreenX = mainCam.transform.position.x - horzExtent + paddingX;
        float maxScreenX = mainCam.transform.position.x + horzExtent - paddingX;
        float minScreenY = mainCam.transform.position.y - vertExtent + paddingY;
        float maxScreenY = mainCam.transform.position.y + vertExtent - paddingY;

        Vector3 pos = transform.position;

        // Detecta si el enemigo ya entró al área visible de la pantalla
        if (!hasEnteredScreen)
        {
            if (pos.x >= minScreenX && pos.x <= maxScreenX &&
                pos.y >= minScreenY && pos.y <= maxScreenY)
            {
                hasEnteredScreen = true;
            }
        }

        // Una vez dentro, se le impide salir de los límites horizontales y verticales
        if (hasEnteredScreen)
        {
            float clampedX = Mathf.Clamp(pos.x, minScreenX, maxScreenX);
            float clampedY = Mathf.Clamp(pos.y, minScreenY, maxScreenY);

            // Si es Tier 3 (patrullero horizontal), invierte dirección si toca los bordes
            if (tier == EnemyTier.Tier3)
            {
                if (pos.x >= maxScreenX) patrolDirection = -1;
                else if (pos.x <= minScreenX) patrolDirection = 1;
            }

            pos.x = clampedX;
            pos.y = clampedY;
            transform.position = pos;
        }
    }

    IEnumerator EnterAndBehave()
    {
        // Entrada: asignación de hilera y posición reservada
        if (tier == EnemyTier.Tier1)
        {
            // Busca la primera hilera que tenga menos de 10 enemigos
            int targetRow = 0;
            while (true)
            {
                if (!reservedTier1Rows.ContainsKey(targetRow))
                    reservedTier1Rows[targetRow] = new System.Collections.Generic.List<float>();

                if (reservedTier1Rows[targetRow].Count < MAX_ENEMIES_PER_ROW)
                    break;

                targetRow++;
            }
            myRowIndex = targetRow;
            myAssignedSettleY = settleY - (myRowIndex * rowVerticalSpacing);

            if (!hasReservedPosition)
            {
                myReservedX = PickNonOverlappingX(reservedTier1Rows[myRowIndex]);
                hasReservedPosition = true;
                reservedTier1Rows[myRowIndex].Add(myReservedX);
            }
        }
        else if (tier == EnemyTier.Tier2)
        {
            int targetRow = 0;
            while (true)
            {
                if (!reservedTier2Rows.ContainsKey(targetRow))
                    reservedTier2Rows[targetRow] = new System.Collections.Generic.List<float>();

                if (reservedTier2Rows[targetRow].Count < MAX_ENEMIES_PER_ROW)
                    break;

                targetRow++;
            }
            myRowIndex = targetRow;

            // Si hay hileras de Tier 1 activas, la hilera de Tier 2 se posiciona debajo de ellas
            float baseTier2Y = settleY;
            int activeTier1Rows = GetActiveTier1RowCount();
            if (activeTier1Rows > 0)
            {
                baseTier2Y = 1450f - (activeTier1Rows * rowVerticalSpacing);
            }

            myAssignedSettleY = baseTier2Y - (myRowIndex * rowVerticalSpacing);

            if (!hasReservedPosition)
            {
                myReservedX = PickNonOverlappingX(reservedTier2Rows[myRowIndex]);
                hasReservedPosition = true;
                reservedTier2Rows[myRowIndex].Add(myReservedX);
            }
        }
        else if (tier == EnemyTier.Tier4)
        {
            // Tier 4 ya asignó myRowIndex, myAssignedSettleY y myReservedX en Start()
        }
        else if (tier == EnemyTier.Tier3)
        {
            float screenLeft = GetScreenMinX();
            float screenRight = GetScreenMaxX();
            float clampedX = Mathf.Clamp(transform.position.x, screenLeft, screenRight);
            
            int lane = activeTier3Count % 3;
            activeTier3Count++;
            myAssignedSettleY = settleY - (lane * 80f);

            // Si entra por la mitad derecha, que comience patrullando hacia la izquierda
            patrolDirection = (clampedX > (screenLeft + screenRight) * 0.5f) ? -1 : 1;
        }
        else
        {
            myAssignedSettleY = settleY;
        }

        // Comienzan a disparar de inmediato: pueden disparar mientras se mueven tanto en Y como en X
        switch (tier)
        {
            case EnemyTier.Tier1:
                StartCoroutine(FireTier1());
                break;
            case EnemyTier.Tier2:
                StartCoroutine(FireTier2());
                break;
            case EnemyTier.Tier3:
                StartCoroutine(FireTier3());
                break;
            case EnemyTier.Tier4:
                StartCoroutine(FireTier4());
                break;
        }

        // 1. Movimiento en Y: descienden hacia su settleY asignado según su hilera
        Vector3 yTarget = new Vector3(transform.position.x, myAssignedSettleY, 0f);
        while (Vector3.Distance(transform.position, yTarget) > 5f)
        {
            transform.position = Vector3.MoveTowards(transform.position, yTarget, entrySpeed * Time.deltaTime);
            yield return null;
        }

        transform.position = yTarget;
        hasEnteredScreen = true;

        // 2. Movimiento en X: se reposicionan horizontalmente a su slot asignado o inician patrulla
        switch (tier)
        {
            case EnemyTier.Tier1:
                yield return StartCoroutine(RepositionTier1());
                break;
            case EnemyTier.Tier2:
                yield return StartCoroutine(RepositionTier2());
                break;
            case EnemyTier.Tier3:
                StartCoroutine(PatrolLoop());
                break;
        }
    }

    IEnumerator PatrolLoop()
    {
        while (true)
        {
            float leftBound = GetScreenMinX();
            float rightBound = GetScreenMaxX();

            transform.position += Vector3.right * patrolDirection * moveSpeed * Time.deltaTime;

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

            Vector3 dir = Vector3.down;
            if (aimsAtPlayer)
            {
                if (playerTransform == null)
                {
                    PlayerController pc = FindAnyObjectByType<PlayerController>();
                    if (pc != null) playerTransform = pc.transform;
                }

                if (playerTransform != null)
                {
                    dir = (playerTransform.position - transform.position).normalized;
                }
            }

            FireBullet(bulletPrefab, tier1BulletSpeed, dir);
        }
    }

    public static int GetActiveTier1RowCount()
    {
        int count = 0;
        foreach (var kvp in reservedTier1Rows)
        {
            if (kvp.Value != null && kvp.Value.Count > 0)
            {
                count = Mathf.Max(count, kvp.Key + 1);
            }
        }
        return count;
    }

    public static int GetActiveTier2RowCount()
    {
        int count = 0;
        foreach (var kvp in reservedTier2Rows)
        {
            if (kvp.Value != null && kvp.Value.Count > 0)
            {
                count = Mathf.Max(count, kvp.Key + 1);
            }
        }
        return count;
    }

    public static int GetActiveTier4RowCount()
    {
        int count = 0;
        foreach (var kvp in reservedTier4Rows)
        {
            if (kvp.Value != null && kvp.Value.Count > 0)
            {
                count = Mathf.Max(count, kvp.Key + 1);
            }
        }
        return count;
    }

    IEnumerator RepositionTier1()
    {
        Vector3 target = new Vector3(myReservedX, myAssignedSettleY, 0f);

        while (Vector3.Distance(transform.position, target) > 5f)
        {
            transform.position = Vector3.MoveTowards(transform.position, target, repositionSpeed * Time.deltaTime);
            yield return null;
        }

        transform.position = target;
    }

    IEnumerator RepositionTier2()
    {
        Vector3 target = new Vector3(myReservedX, myAssignedSettleY, 0f);

        while (Vector3.Distance(transform.position, target) > 5f)
        {
            transform.position = Vector3.MoveTowards(transform.position, target, repositionSpeed * Time.deltaTime);
            yield return null;
        }

        transform.position = target;
    }

    // Intenta encontrar una X que esté a más de minSeparationFromOthers de cualquier
    // otro enemigo del mismo tier que ya haya reservado posición.
    // Si tras varios intentos aleatorios no encuentra una libre (campo muy poblado),
    // busca el espacio o hueco más grande entre las posiciones existentes para evitar superposiciones.
    float PickNonOverlappingX(System.Collections.Generic.List<float> reservedPositions)
    {
        const int maxAttempts = 50;
        float minBound = (mainCam != null) ? GetScreenMinX() : repositionMinX;
        float maxBound = (mainCam != null) ? GetScreenMaxX() : repositionMaxX;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            float candidate = Random.Range(minBound, maxBound);
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

            float bestX = (minBound + maxBound) * 0.5f;
            float maxGap = 0f;

            float leftGap = sorted[0] - minBound;
            if (leftGap > maxGap)
            {
                maxGap = leftGap;
                bestX = minBound + leftGap * 0.5f;
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

            float rightGap = maxBound - sorted[sorted.Count - 1];
            if (rightGap > maxGap)
            {
                bestX = sorted[sorted.Count - 1] + rightGap * 0.5f;
            }

            return bestX;
        }

        return Random.Range(minBound, maxBound);
    }

    IEnumerator FireTier2()
    {
        while (true)
        {
            yield return new WaitForSeconds(fireCadence);
            FireBullet(bulletPrefab, 400f, GetTier2BulletDirection());
            yield return new WaitForSeconds(0.5f);
            FireBullet(bulletPrefab, 400f, GetTier2BulletDirection());
        }
    }

    Vector3 GetTier2BulletDirection()
    {
        if (tier2AimsAtPlayer)
        {
            if (playerTransform == null)
            {
                PlayerController pc = FindAnyObjectByType<PlayerController>();
                if (pc != null) playerTransform = pc.transform;
            }

            if (playerTransform != null)
            {
                return (playerTransform.position - transform.position).normalized;
            }
        }

        return Vector3.down;
    }

    IEnumerator FireTier3()
    {
        while (true)
        {
            yield return new WaitForSeconds(fireCadence);
            FireBullet(bulletPrefab, 400f, GetTier3BulletDirection());
            yield return new WaitForSeconds(0.5f);
            FireBullet(bulletPrefab, 400f, GetTier3BulletDirection());
            yield return new WaitForSeconds(0.5f);
            FireBullet(bulletPrefab, 400f, GetTier3BulletDirection());
            yield return new WaitForSeconds(2f);
            Vector3 dir = GetTier3BulletDirection();
            Vector3 spawnOffset = dir.normalized * bigBulletSpawnOffset;
            FireBullet(bigBulletPrefab, 120f, dir, spawnOffset);
        }
    }

    Vector3 GetTier3BulletDirection()
    {
        if (tier3AimsAtPlayer)
        {
            if (playerTransform == null)
            {
                PlayerController pc = FindAnyObjectByType<PlayerController>();
                if (pc != null) playerTransform = pc.transform;
            }

            if (playerTransform != null)
            {
                return (playerTransform.position - transform.position).normalized;
            }
        }

        return Vector3.down;
    }

    IEnumerator FireTier4()
    {
        // Breve pausa inicial tras aterrizar en Y antes de emitir la primera señal
        yield return new WaitForSeconds(1f);

        while (true)
        {
            // 1. Apuntar siempre en dirección al jugador
            if (playerTransform == null)
            {
                PlayerController pc = FindAnyObjectByType<PlayerController>();
                if (pc != null) playerTransform = pc.transform;
            }

            Vector3 dir = Vector3.down;
            if (playerTransform != null)
            {
                Vector3 diff = playerTransform.position - transform.position;
                if (diff.sqrMagnitude > 0.001f)
                {
                    dir = diff.normalized;
                }
            }

            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + 90f;
            Quaternion rot = Quaternion.Euler(0f, 0f, angle);

            // El centro del haz se ubica a la mitad de su longitud hacia adelante,
            // de modo que un extremo inicie en la nave y el rayo se extienda hacia el objetivo.
            Vector3 beamCenter = transform.position + dir * (laserBeamLength * 0.5f);

            // 2. Generar el "láser" rojo y transparente de señalización (no hace daño)
            if (telegraphPrefab != null)
            {
                activeTelegraph = Instantiate(telegraphPrefab, beamCenter, rot);
                activeTelegraph.transform.localScale = new Vector3(laserTelegraphWidth, laserBeamLength, 1f);
            }
            else
            {
                activeTelegraph = CreateDefaultTelegraphLaser(beamCenter, rot, laserBeamLength, laserTelegraphWidth);
            }

            // 3. Esperar tiempo de advertencia para que el jugador se aparte (el aviso acompaña el movimiento)
            float elapsed = 0f;
            while (elapsed < laserTelegraphDuration)
            {
                elapsed += Time.deltaTime;
                if (activeTelegraph != null)
                {
                    activeTelegraph.transform.position = transform.position + dir * (laserBeamLength * 0.5f);
                }
                yield return null;
            }

            // Remover la señalización roja
            if (activeTelegraph != null)
            {
                Destroy(activeTelegraph);
                activeTelegraph = null;
            }

            // 4. Disparar el láser real (blanco y dañino) en esa posición y dirección
            Vector3 finalBeamCenter = transform.position + dir * (laserBeamLength * 0.5f);
            if (laserPrefab != null)
            {
                GameObject realLaser = Instantiate(laserPrefab, finalBeamCenter, rot);
                realLaser.transform.localScale = new Vector3(laserBeamWidth, laserBeamLength, 1f);

                if (SoundController.Instance != null)
                {
                    SoundController.Instance.PlayTier4LaserSfx();
                }
            }

            // 5. Esperar el resto del intervalo para completar el ciclo de 3 segundos
            float remainingCooldown = Mathf.Max(0.1f, laserCooldown - laserTelegraphDuration);
            yield return new WaitForSeconds(remainingCooldown);
        }
    }

    GameObject CreateDefaultTelegraphLaser(Vector3 center, Quaternion rot, float length, float width)
    {
        GameObject obj = new GameObject("TelegraphLaser_Auto");
        obj.transform.position = center;
        obj.transform.rotation = rot;
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
        sr.color = new Color(1f, 0.12f, 0.12f, 0.38f);
        sr.sortingOrder = 4;
        return obj;
    }

    void FireBullet(GameObject prefab, float speed, Vector3? customDirection = null, Vector3? spawnOffset = null)
    {
        if (prefab == null) return;

        Vector3 dir = customDirection ?? Vector3.down;
        Vector3 spawnPos = transform.position + (spawnOffset ?? Vector3.zero);

        Quaternion rot = Quaternion.identity;
        if (dir.sqrMagnitude > 0.001f)
        {
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + 90f;
            rot = Quaternion.Euler(0f, 0f, angle);
        }

        GameObject obj = Instantiate(prefab, spawnPos, rot);
        EnemyBullet eb = obj.GetComponent<EnemyBullet>();
        if (eb != null)
        {
            eb.speed = speed;
            eb.direction = dir;
            eb.AlignRotationWithDirection();
        }
    }

    void OnDestroy()
    {
        if (activeTelegraph != null)
        {
            Destroy(activeTelegraph);
            activeTelegraph = null;
        }

        // Libera su lugar en el campo de batalla para que otros enemigos puedan usarlo,
        // sin importar si murió por una bala, terminó la partida, o se cambió de escena.
        if (hasReservedPosition)
        {
            if (tier == EnemyTier.Tier1 && reservedTier1Rows.ContainsKey(myRowIndex))
            {
                reservedTier1Rows[myRowIndex].Remove(myReservedX);
            }
            else if (tier == EnemyTier.Tier2 && reservedTier2Rows.ContainsKey(myRowIndex))
            {
                reservedTier2Rows[myRowIndex].Remove(myReservedX);
            }
            else if (tier == EnemyTier.Tier4 && reservedTier4Rows.ContainsKey(myRowIndex))
            {
                reservedTier4Rows[myRowIndex].Remove(myReservedX);
            }
        }

        if (tier == EnemyTier.Tier3)
        {
            activeTier3Count = Mathf.Max(0, activeTier3Count - 1);
        }
    }

    // Llamado por WaveController al arrancar la Oleada 1, por si quedó algo residual
    // de una partida anterior en la misma sesión de Play.
    public static void ClearReservedPositions()
    {
        reservedTier1Rows.Clear();
        reservedTier2Rows.Clear();
        reservedTier4Rows.Clear();
        activeTier3Count = 0;
    }

    public void TakeDamage(int amount)
    {
        TakeDamage((float)amount);
    }

    public void TakeDamage(float amount)
    {
        if (Time.timeScale == 0f) return;

        currentHealth -= amount;
        if (currentHealth <= 0.001f) Die();
    }

    public bool IsDead()
    {
        return currentHealth <= 0.001f;
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
}