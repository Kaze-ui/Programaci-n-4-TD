using UnityEngine;

public class EnemyBullet : MonoBehaviour
{
    public float speed = 258.75f; // -25% velocidad de movimiento de balas normales (345 * 0.75)
    public int damage = 1;
    public Vector3 direction = Vector3.down;
    public float lifeTime = 5f;

    [Header("Seguimiento / Homing")]
    public bool isHoming = false;
    public float homingTurnSpeed = 120f; // grados por segundo
    private Transform targetPlayer;

    public void EnableHoming(Transform target, float turnSpeed = 120f)
    {
        isHoming = true;
        targetPlayer = target;
        homingTurnSpeed = turnSpeed;
    }

    [Header("Tipo de proyectil")]
    public bool isBigBullet = false;

    [Header("Explosión en Racimo (Tier 3)")]
    private bool shouldExplodeCluster = false;
    private GameObject clusterFragmentPrefab;
    private float clusterExplodeDistance = 350f;
    private float clusterFragmentSpeed = 262.5f; // -25% de velocidad (350 * 0.75)
    private float startClusterY = 0f;
    private bool hasExplodedCluster = false;

    public void SetupClusterExplosion(GameObject fragmentPrefab, float explodeDistanceY, float fragmentSpeed = 262.5f)
    {
        shouldExplodeCluster = true;
        clusterFragmentPrefab = fragmentPrefab;
        clusterExplodeDistance = explodeDistanceY;
        clusterFragmentSpeed = fragmentSpeed;
        startClusterY = transform.position.y;
    }

    [Header("Ralentización por Proximidad")]
    private float currentSlowMultiplier = 1.0f;
    private GameObject slowRedOutlineObj;
    private static Sprite cachedRedOutlineRingSprite;

    private static Sprite GetRedOutlineRingSprite()
    {
        if (cachedRedOutlineRingSprite != null) return cachedRedOutlineRingSprite;

        const int size = 64;
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

                // Contorno ROJO muy grueso, intenso y llamativo alrededor de toda la bala + halo rojo exterior
                float innerCut = Mathf.SmoothStep(0.42f, 0.52f, r);
                float solidBand = innerCut * (1f - Mathf.SmoothStep(0.76f, 0.85f, r));
                float outerGlow = innerCut * (1f - Mathf.SmoothStep(0.76f, 1.00f, r)) * 0.78f;
                float alpha = Mathf.Clamp01(Mathf.Max(solidBand, outerGlow));

                // Rojo puro ultrasaturado (#FF0008) para máxima visibilidad a simple vista
                tex.SetPixel(x, y, new Color(1f, 0.0f, 0.03f, alpha));
            }
        }
        tex.Apply();

        // PPU = size para que el sprite mida exactamente 1x1 unidad local
        cachedRedOutlineRingSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return cachedRedOutlineRingSprite;
    }

    private Vector3 slowOutlineBaseScale = Vector3.one;

    private void UpdateSlowRedOutlineVisual(bool isSlowed)
    {
        if (!isSlowed)
        {
            if (slowRedOutlineObj != null && slowRedOutlineObj.activeSelf)
                slowRedOutlineObj.SetActive(false);
            return;
        }

        if (slowRedOutlineObj == null)
        {
            slowRedOutlineObj = new GameObject("SlowRedOutline");
            slowRedOutlineObj.transform.SetParent(transform, false);
            slowRedOutlineObj.transform.localPosition = Vector3.zero;
            slowRedOutlineObj.transform.localRotation = Quaternion.identity;

            SpriteRenderer outlineSr = slowRedOutlineObj.AddComponent<SpriteRenderer>();
            outlineSr.sprite = GetRedOutlineRingSprite();
            outlineSr.color = new Color(1f, 0.02f, 0.04f, 1f);

            SpriteRenderer bulletSr = GetComponent<SpriteRenderer>();
            if (bulletSr != null)
            {
                outlineSr.sortingLayerID = bulletSr.sortingLayerID;
                outlineSr.sortingOrder = bulletSr.sortingOrder + 3;
                if (bulletSr.sprite != null)
                {
                    Vector2 spSize = bulletSr.sprite.bounds.size;
                    float spanX = Mathf.Max(0.25f, spSize.x);
                    float spanY = Mathf.Max(0.25f, spSize.y);
                    // Contorno rojo grueso y brillante claramente visible alrededor de toda la bala
                    slowOutlineBaseScale = new Vector3(spanX * 1.65f, spanY * 1.65f, 1f);
                }
                else
                {
                    slowOutlineBaseScale = new Vector3(1.65f, 1.65f, 1f);
                }
            }
            else
            {
                slowOutlineBaseScale = new Vector3(1.65f, 1.65f, 1f);
            }
            slowRedOutlineObj.transform.localScale = slowOutlineBaseScale;
        }

        if (!slowRedOutlineObj.activeSelf)
            slowRedOutlineObj.SetActive(true);

        // Pulso sutil y enérgico para que las balas ralentizadas resalten al instante
        float pulse = 1.0f + 0.07f * Mathf.Sin(Time.time * 18f);
        slowRedOutlineObj.transform.localScale = slowOutlineBaseScale * pulse;
    }

    void Start()
    {
        Destroy(gameObject, lifeTime);
        AlignRotationWithDirection();
    }

    public void AlignRotationWithDirection()
    {
        if (direction.sqrMagnitude > 0.001f)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + 90f;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }
    }

    void Update()
    {
        if (Time.timeScale == 0f) return;

        // Comprobación de detonación en racimo por distancia en eje Y (Tier 3)
        if (shouldExplodeCluster && !hasExplodedCluster)
        {
            if (Mathf.Abs(startClusterY - transform.position.y) >= clusterExplodeDistance)
            {
                ExplodeIntoCluster();
                return;
            }
        }

        if (isHoming)
        {
            if (targetPlayer == null)
            {
                PlayerController pc = FindAnyObjectByType<PlayerController>();
                if (pc != null) targetPlayer = pc.transform;
            }

            if (targetPlayer != null)
            {
                Vector3 desiredDir = (targetPlayer.position - transform.position).normalized;
                direction = Vector3.RotateTowards(direction, desiredDir, homingTurnSpeed * Mathf.Deg2Rad * Time.deltaTime, 0f);
                AlignRotationWithDirection();
            }
        }

        // Detección de proximidad con jugadores activos (ralentiza incluso si el jugador está SIN escudo):
        bool inForceFieldOfAnyPlayer = false;
        var activePlayers = PlayerController.ActivePlayers;
        if (activePlayers != null && activePlayers.Count > 0)
        {
            Vector3 myPos = transform.position;
            float forceFieldRadiusSq = PlayerController.ForceFieldRadius * PlayerController.ForceFieldRadius;

            for (int i = 0; i < activePlayers.Count; i++)
            {
                PlayerController p = activePlayers[i];
                if (p != null && p.gameObject.activeInHierarchy && p.GetCurrentHealth() > 0)
                {
                    Vector3 hitboxPos = p.GetHitboxCenter();
                    float distSq = (myPos - hitboxPos).sqrMagnitude;
                    if (distSq <= forceFieldRadiusSq)
                    {
                        inForceFieldOfAnyPlayer = true;
                        p.NotifyBulletInForceField();
                    }
                }
            }
        }

        // Contorno ROJO mientras la bala está siendo ralentizada; se oculta al salir del campo
        UpdateSlowRedOutlineVisual(inForceFieldOfAnyPlayer);

        // Ralentiza la velocidad de las balas ÚNICAMENTE dentro del área del campo de fuerza
        currentSlowMultiplier = inForceFieldOfAnyPlayer ? 0.25f : 1.0f;

        transform.position += direction.normalized * (speed * currentSlowMultiplier) * Time.deltaTime;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (Time.timeScale == 0f) return;

        if (other.CompareTag("Player"))
        {
            PlayerController player = other.GetComponent<PlayerController>();
            bool deflected = player != null && (player.IsInvulnerable || player.HasShield);

            Vector3 impactPos = other.ClosestPoint(transform.position);
            float bulletExtent = Mathf.Max(Mathf.Abs(transform.localScale.x), Mathf.Abs(transform.localScale.y));
            float vfxScale = Mathf.Clamp(bulletExtent / 45f, 0.85f, 2.2f);
            Material mat = TryGetComponent<SpriteRenderer>(out var sr) ? sr.sharedMaterial : null;
            EnemyExplosion.SpawnPlayerImpact(impactPos, deflected, vfxScale, mat);

            if (player != null)
            {
                // El campo de fuerza tiene 100 de capacidad: bala normal saca 10, bala grande saca 25
                bool isBig = isBigBullet || bulletExtent >= 150f;
                float shieldCost = isBig ? 25f : 10f;
                bool absorbedByShield = player.TakeShieldDamage(shieldCost);
                if (!absorbedByShield)
                {
                    player.TakeDamage(damage);
                }
            }

            Destroy(gameObject);
        }
    }

    private void ExplodeIntoCluster()
    {
        hasExplodedCluster = true;
        Vector3 spawnPos = transform.position;

        Material mat = TryGetComponent<SpriteRenderer>(out var sr) ? sr.sharedMaterial : null;
        EnemyExplosion.Spawn(spawnPos, EnemyTier.Tier3, transform.localScale * 0.9f, mat);
        SoundController.Instance?.PlayEnemyDeathSfx();

        if (clusterFragmentPrefab != null)
        {
            // 20 proyectiles:
            // - 4 en los extremos de la cruz horizontal y vertical: 0° (Derecha), 90° (Arriba), 180° (Izquierda), 270° (Abajo)
            // - 4 por cada cara / cuadrante de la circunferencia (16 adicionales):
            //   Cuadrante 1 (arriba-derecha): 18°, 36°, 54°, 72°
            //   Cuadrante 2 (arriba-izquierda): 108°, 126°, 144°, 162°
            //   Cuadrante 3 (abajo-izquierda): 198°, 216°, 234°, 252°
            //   Cuadrante 4 (abajo-derecha): 288°, 306°, 324°, 342°
            // En total 20 proyectiles con separación uniforme exacta de 360° / 20 = 18°
            const int totalBullets = 20;
            const float angleStep = 360f / totalBullets; // 18 grados

            for (int i = 0; i < totalBullets; i++)
            {
                float angleDeg = i * angleStep;
                float angleRad = angleDeg * Mathf.Deg2Rad;
                Vector3 bulletDir = new Vector3(Mathf.Cos(angleRad), Mathf.Sin(angleRad), 0f).normalized;

                float rotZ = angleDeg + 90f;
                GameObject fragment = Instantiate(clusterFragmentPrefab, spawnPos, Quaternion.Euler(0f, 0f, rotZ));
                if (fragment.TryGetComponent<EnemyBullet>(out var eb))
                {
                    eb.isBigBullet = false;
                    eb.speed = clusterFragmentSpeed;
                    eb.direction = bulletDir;
                    eb.AlignRotationWithDirection();
                }
            }
        }

        Destroy(gameObject);
    }
}