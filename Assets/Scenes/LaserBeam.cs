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
            player.TakeDamage(damage);
            nextDamageTime = Time.time + damageTickInterval;
        }
    }
}