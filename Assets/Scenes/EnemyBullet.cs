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
    private Vector3 startClusterPos;
    private bool hasExplodedCluster = false;

    public void SetupClusterExplosion(GameObject fragmentPrefab, float explodeDistance, float fragmentSpeed = 262.5f)
    {
        shouldExplodeCluster = true;
        clusterFragmentPrefab = fragmentPrefab;
        clusterExplodeDistance = explodeDistance;
        clusterFragmentSpeed = fragmentSpeed;
        startClusterPos = transform.position;
    }

    [Header("Ralentización por Proximidad")]
    private float currentSlowMultiplier = 1.0f;
    private GameObject slowRedOutlineObj;
    private SpriteRenderer slowOutlineSr;
    private static Sprite cachedBulletOutlineSprite;

    private static Sprite GetBulletOutlineSprite(Sprite sourceSprite)
    {
        if (cachedBulletOutlineSprite != null) return cachedBulletOutlineSprite;

        // Cargar el sprite con el contorno de los bordes exactos del proyectil
        cachedBulletOutlineSprite = Resources.Load<Sprite>("Sprites/BulletAlienOutline");
        if (cachedBulletOutlineSprite != null) return cachedBulletOutlineSprite;

        // Fallback procedural en tiempo de ejecución a partir del sprite de la bala
        if (sourceSprite != null && sourceSprite.texture != null)
        {
            cachedBulletOutlineSprite = GenerateOutlineFromSprite(sourceSprite);
            if (cachedBulletOutlineSprite != null) return cachedBulletOutlineSprite;
        }

        return sourceSprite;
    }

    private static Sprite GenerateOutlineFromSprite(Sprite source)
    {
        try
        {
            Texture2D srcTex = source.texture;
            int w = Mathf.Min(srcTex.width, 256);
            int h = Mathf.Min(srcTex.height, 256);
            RenderTexture rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(srcTex, rt);

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            Texture2D copy = new Texture2D(w, h, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            if (RenderTexture.active == rt)
            {
                RenderTexture.active = (prev != rt) ? prev : null;
            }
            RenderTexture.ReleaseTemporary(rt);

            Color[] srcPixels = copy.GetPixels();
            Color[] outPixels = new Color[w * h];
            bool[] solid = new bool[w * h];

            for (int i = 0; i < srcPixels.Length; i++)
            {
                solid[i] = srcPixels[i].a >= 0.12f;
            }

            int radius = 4;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    bool cur = solid[y * w + x];
                    float minDist = float.MaxValue;

                    int ymin = Mathf.Max(0, y - radius);
                    int ymax = Mathf.Min(h - 1, y + radius);
                    int xmin = Mathf.Max(0, x - radius);
                    int xmax = Mathf.Min(w - 1, x + radius);

                    for (int ny = ymin; ny <= ymax; ny++)
                    {
                        for (int nx = xmin; nx <= xmax; nx++)
                        {
                            if (solid[ny * w + nx] != cur)
                            {
                                float d = Mathf.Sqrt((x - nx) * (x - nx) + (y - ny) * (y - ny));
                                if (d < minDist) minDist = d;
                            }
                        }
                    }

                    if (minDist <= radius * 0.75f)
                    {
                        float alphaFactor = 1.0f - (minDist / (radius * 0.75f));
                        outPixels[y * w + x] = new Color(1.0f, 0.05f, 0.08f, alphaFactor);
                    }
                    else
                    {
                        outPixels[y * w + x] = Color.clear;
                    }
                }
            }

            Texture2D outTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            outTex.filterMode = FilterMode.Bilinear;
            outTex.wrapMode = TextureWrapMode.Clamp;
            outTex.SetPixels(outPixels);
            outTex.Apply();

            return Sprite.Create(outTex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), source.pixelsPerUnit);
        }
        catch
        {
            return source;
        }
    }

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
            slowRedOutlineObj.transform.localScale = Vector3.one;

            slowOutlineSr = slowRedOutlineObj.AddComponent<SpriteRenderer>();
            SpriteRenderer bulletSr = GetComponent<SpriteRenderer>();

            Sprite outlineSp = GetBulletOutlineSprite(bulletSr != null ? bulletSr.sprite : null);
            slowOutlineSr.sprite = outlineSp;
            slowOutlineSr.color = new Color(1f, 0.05f, 0.08f, 1f);

            if (bulletSr != null)
            {
                slowOutlineSr.sortingLayerID = bulletSr.sortingLayerID;
                slowOutlineSr.sortingOrder = bulletSr.sortingOrder + 2;
            }
        }

        if (!slowRedOutlineObj.activeSelf)
            slowRedOutlineObj.SetActive(true);

        // Pulso dinámico de brillo sobre los bordes del sprite sin desfasar la silueta geométrica
        float pulse = 0.82f + 0.18f * Mathf.Sin(Time.time * 16f);
        if (slowOutlineSr != null)
        {
            slowOutlineSr.color = new Color(1f, 0.05f, 0.08f, pulse);
        }
    }

    void Awake()
    {
        // Hitbox exacta y precisa ceñida al cuerpo real del proyectil
        if (TryGetComponent<BoxCollider2D>(out var boxCol))
        {
            boxCol.size = isBigBullet ? new Vector2(0.46f, 0.76f) : new Vector2(0.42f, 0.70f);
        }
        else if (TryGetComponent<CapsuleCollider2D>(out var capCol))
        {
            capCol.size = isBigBullet ? new Vector2(0.46f, 0.76f) : new Vector2(0.42f, 0.70f);
        }
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

        // Comprobación de detonación en racimo por distancia recorrida (Tier 3)
        if (shouldExplodeCluster && !hasExplodedCluster)
        {
            if (Vector2.Distance(startClusterPos, transform.position) >= clusterExplodeDistance)
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

            for (int i = 0; i < activePlayers.Count; i++)
            {
                PlayerController p = activePlayers[i];
                if (p != null && p.gameObject.activeInHierarchy && p.GetCurrentHealth() > 0)
                {
                    Vector3 hitboxPos = p.GetHitboxCenter();
                    float distSq = (myPos - hitboxPos).sqrMagnitude;
                    if (distSq <= p.GetForceFieldRadiusSq())
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
                // El escudo inicial tiene 10 de vida: bala normal saca 5, bala grande saca 10
                bool isBig = isBigBullet || bulletExtent >= 150f;
                float shieldCost = isBig ? 10f : 5f;
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