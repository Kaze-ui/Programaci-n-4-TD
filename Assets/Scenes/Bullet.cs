using UnityEngine;

public class Bullet : MonoBehaviour
{
    [Header("Movimiento")]
    public float speed = 500f;

    [Header("Daño")]
    public int damage = 1;

    [Header("Límite de vida")]
    public float lifeTime = 3f; // se autodestruye después de este tiempo por las dudas

    void Start()
    {
        Destroy(gameObject, lifeTime);
    }

    void Update()
    {
        transform.position += Vector3.up * speed * Time.deltaTime;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Enemy"))
        {
            IDamageable damageable = other.GetComponent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(damage);
            }

            Destroy(gameObject);
        }
    }
}