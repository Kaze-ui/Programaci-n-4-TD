using UnityEngine;

public class Enemy : MonoBehaviour
{
    [Header("Vida")]
    public int maxHealth = 1;
    private int currentHealth;

    [Header("Movimiento")]
    public float speed = 100f;

    [Header("Puntaje")]
    public int scoreValue = 1;

    [Header("Daño al jugador")]
    public int damageToPlayer = 1;

    void Start()
    {
        currentHealth = maxHealth;
    }

    void Update()
    {
        transform.position += Vector3.down * speed * Time.deltaTime;
    }

    public void TakeDamage(int amount)
    {
        currentHealth -= amount;

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        if (SoundController.Instance != null)
        {
            SoundController.Instance.PlayEnemyDeathSfx();
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddScore(scoreValue);
        }

        Destroy(gameObject);
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            PlayerController player = collision.gameObject.GetComponent<PlayerController>();
            if (player != null)
            {
                player.TakeDamage(damageToPlayer);
            }

            Destroy(gameObject);
        }
    }
}