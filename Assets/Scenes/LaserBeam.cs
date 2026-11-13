using UnityEngine;

public class LaserBeam : MonoBehaviour
{
    public int damage = 2;
    public float activeDuration = 0.4f;
    public bool autoDestroy = true;
    public float damageTickInterval = 0.2f;
    private float nextDamageTime = 0f;

    private float aliveTimer = 0f;

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
            float forceFieldRadiusSq = PlayerController.ForceFieldRadius * PlayerController.ForceFieldRadius;

            for (int i = 0; i < activePlayers.Count; i++)
            {
                PlayerController p = activePlayers[i];
                if (p != null && p.gameObject.activeInHierarchy && p.GetCurrentHealth() > 0 && p.HasShield)
                {
                    Vector3 hitboxPos = p.GetHitboxCenter();
                    Vector3 closestPt = col != null ? (Vector3)col.ClosestPoint(hitboxPos) : transform.position;
                    float distSq = (hitboxPos - closestPt).sqrMagnitude;
                    if (distSq <= forceFieldRadiusSq)
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
        if (Time.time < nextDamageTime) return;

        PlayerController player = other.GetComponent<PlayerController>();
        if (player != null)
        {
            Vector3 impactPos = other.ClosestPoint(transform.position);
            bool isDeflected = player.IsInvulnerable || player.HasShield;
            EnemyExplosion.SpawnPlayerImpact(impactPos, isDeflected, 1.15f);

            // Los láseres sacan 20 de capacidad de escudo
            bool absorbedByShield = player.TakeShieldDamage(20f);
            if (!absorbedByShield)
            {
                player.TakeDamage(damage);
            }
            nextDamageTime = Time.time + damageTickInterval;
        }
    }
}