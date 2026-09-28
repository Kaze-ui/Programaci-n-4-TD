using System.Collections.Generic;
using UnityEngine;

public class Bullet : MonoBehaviour
{
    [Header("Movimiento")]
    public float speed = 2000f;

    [Header("Daño")]
    public float damage = 1f;

    [Header("Límite de vida")]
    public float lifeTime = 3f; // se autodestruye después de este tiempo por las dudas

    [Header("Propiedades de Bala Grande")]
    public bool isBigBullet = false;

    // Evita que la misma bala dañe repetidamente al mismo enemigo si lo atraviesa
    private HashSet<GameObject> hitEnemies = new HashSet<GameObject>();

    void Start()
    {
        Destroy(gameObject, lifeTime);
    }

    void Update()
    {
        if (Time.timeScale == 0f) return;
        transform.position += Vector3.up * speed * Time.deltaTime;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        HandleHit(other);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        // En balas grandes, permite detectar enemigos adicionales que toquen el colisionador durante el vuelo
        if (isBigBullet)
        {
            HandleHit(other);
        }
    }

    private void HandleHit(Collider2D other)
    {
        if (Time.timeScale == 0f || other == null) return;

        if (other.CompareTag("Enemy"))
        {
            GameObject enemyObj = other.gameObject;
            if (hitEnemies.Contains(enemyObj)) return;
            hitEnemies.Add(enemyObj);

            IDamageable damageable = enemyObj.GetComponent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(damage);
            }

            if (isBigBullet)
            {
                // Si la bala gigante derrotó al enemigo (murió o fue destruido),
                // la bala NO se destruye: atraviesa y sigue su curso pudiendo golpear y derrotar a otros enemigos.
                // Si el enemigo sobrevive (como el Jefe o un enemigo con mucha vida),
                // la bala gigante se consume y se destruye en el impacto.
                bool isDefeated = (damageable == null) || damageable.IsDead() || (enemyObj == null);
                if (!isDefeated)
                {
                    Destroy(gameObject);
                }
            }
            else
            {
                // Balas normales y mini balas se destruyen en el primer impacto
                Destroy(gameObject);
            }
        }
    }
}