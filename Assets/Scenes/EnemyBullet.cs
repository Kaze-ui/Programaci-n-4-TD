using UnityEngine;

public class EnemyBullet : MonoBehaviour
{
    public float speed = 345f; // +15% velocidad base (300 * 1.15)
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

    [Header("Ralentización por Proximidad")]
    private float currentSlowMultiplier = 1.0f;

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

        // Detección de proximidad con jugadores activos:
        // Las balas enemigas SOLO se ralentizan (50%) cuando entran en el área del campo de fuerza
        bool inForceFieldOfAnyPlayer = false;
        var activePlayers = PlayerController.ActivePlayers;
        if (activePlayers != null && activePlayers.Count > 0)
        {
            Vector3 myPos = transform.position;
            float forceFieldRadiusSq = PlayerController.ForceFieldRadius * PlayerController.ForceFieldRadius;

            for (int i = 0; i < activePlayers.Count; i++)
            {
                PlayerController p = activePlayers[i];
                if (p != null && p.gameObject.activeInHierarchy && p.GetCurrentHealth() > 0 && p.HasShield)
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

        // Ralentiza la velocidad de las balas un 50% ÚNICAMENTE dentro del área del campo de fuerza
        currentSlowMultiplier = inForceFieldOfAnyPlayer ? 0.50f : 1.0f;

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
                bool isBig = isBigBullet || bulletExtent >= 40f;
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
}