using UnityEngine;

public class LaserBeam : MonoBehaviour
{
    public int damage = 2;
    public float shieldDamage = 20f;
    public bool drainsAllShield = false; // Si es true (Hiper Rayo del Jefe), destruye todo el escudo restante de un golpe
    public float activeDuration = 0.4f;
    public bool autoDestroy = true;
    public float damageTickInterval = 0.2f;
    private float nextDamageTime = 0f;

    private float aliveTimer = 0f;

    void Awake()
    {
        if (TryGetComponent<SpriteRenderer>(out var sr))
        {
            if (sr.sprite == null || sr.sprite.name == "Square" || sr.sprite.name.Contains("Default"))
            {
                Sprite beamSp = Resources.Load<Sprite>("Sprites/LaserBeam");
                if (beamSp != null) sr.sprite = beamSp;
            }
        }
    }

    void Start()
    {
        if (autoDestroy && activeDuration > 0f)
        {
            Destroy(gameObject, activeDuration);
        }
    }

    void Update()
    {
        if (Time.timeScale == 0f) return;

        if (activeDuration > 0f)
        {
            aliveTimer += Time.deltaTime;
            if (aliveTimer >= activeDuration)
            {
                Destroy(gameObject);
                return;
            }
        }

        // Proximidad del láser respecto al campo de fuerza del jugador
        var activePlayers = PlayerController.ActivePlayers;
        if (activePlayers != null && activePlayers.Count > 0)
        {
            Collider2D col = GetComponent<Collider2D>();

            for (int i = 0; i < activePlayers.Count; i++)
            {
                PlayerController p = activePlayers[i];
                if (p != null && p.gameObject.activeInHierarchy && p.GetCurrentHealth() > 0)
                {
                    Vector3 hitboxPos = p.GetHitboxCenter();
                    Vector3 closestPt = col != null ? (Vector3)col.ClosestPoint(hitboxPos) : transform.position;
                    float distSq = (hitboxPos - closestPt).sqrMagnitude;
                    if (distSq <= p.GetForceFieldRadiusSq())
                    {
                        p.NotifyBulletInForceField();
                    }
                }
            }
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        TryDealDamage(other);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        TryDealDamage(other);
    }

    private void TryDealDamage(Collider2D other)
    {
        if (Time.timeScale == 0f) return;
        if (!other.CompareTag("Player")) return;

        PlayerController player = other.GetComponent<PlayerController>();
        if (player != null)
        {
            if (player.IsInvulnerable || player.IsDying) return;
            if (!drainsAllShield && Time.time < nextDamageTime) return;

            Vector3 impactPos = other.ClosestPoint(transform.position);
            bool isDeflected = player.HasShield;
            EnemyExplosion.SpawnPlayerImpact(impactPos, isDeflected, drainsAllShield ? 1.5f : 1.15f);

            // Si es el Hiper Rayo y el jugador tiene escudo, le saca TODO el escudo restante.
            // Si no tiene escudo, le saca 5 puntos de vida.
            float shieldHit = drainsAllShield ? (player.maxShieldCapacity + 1f) : shieldDamage;
            bool absorbedByShield = player.TakeShieldDamage(shieldHit);
            if (!absorbedByShield)
            {
                int hpDamage = drainsAllShield ? 5 : damage;
                player.TakeDamage(hpDamage);
            }
            nextDamageTime = Time.time + damageTickInterval;
        }
    }
}