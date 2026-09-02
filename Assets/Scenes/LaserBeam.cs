using UnityEngine;

public class LaserBeam : MonoBehaviour
{
    public int damage = 2;
    public float activeDuration = 0.4f;
    public float damageTickInterval = 0.2f;
    private float nextDamageTime = 0f;

    void Start()
    {
        Destroy(gameObject, activeDuration);
    }

    void OnTriggerStay2D(Collider2D other)
    {
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