using UnityEngine;

public class BossController : MonoBehaviour, IDamageable
{
    [Header("Vida")]
    public int maxHealth = 10;
    private int currentHealth;

    [Header("Movimiento (patrulla lateral)")]
    public float moveSpeed = 150f;
    public float minX = -400f;
    public float maxX = 400f;
    private int moveDirection = 1;

    [Header("Entrada (baja desde arriba antes de patrullar)")]
    public float entrySpeed = 100f;
    public float targetY = 250f; // altura en la que se queda a patrullar
    private bool hasEntered = false;

    [Header("Daño por contacto")]
    public int contactDamage = 2;
    public float contactCooldown = 1f;
    private float nextContactTime = 0f;

    [Header("Puntaje al derrotarlo")]
    public int scoreValue = 10;

    void Start()
    {
        currentHealth = maxHealth;
    }

    void Update()
    {
        if (!hasEntered)
        {
            Enter();
        }
        else
        {
            Patrol();
        }
    }

    void Enter()
    {
        transform.position += Vector3.down * entrySpeed * Time.deltaTime;

        if (transform.position.y <= targetY)
        {
            Vector3 pos = transform.position;
            pos.y = targetY; // evita pasarse de largo
            transform.position = pos;
            hasEntered = true;
        }
    }

    void Patrol()
    {
        transform.position += Vector3.right * moveDirection * moveSpeed * Time.deltaTime;

        if (transform.position.x >= maxX)
        {
            moveDirection = -1;
        }
        else if (transform.position.x <= minX)
        {
            moveDirection = 1;
        }
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
            GameManager.Instance.OnBossDefeated(scoreValue);
        }

        Destroy(gameObject);
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        TryDealContactDamage(collision.gameObject);
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        TryDealContactDamage(collision.gameObject);
    }

    void TryDealContactDamage(GameObject other)
    {
        if (!other.CompareTag("Player")) return;
        if (Time.time < nextContactTime) return;

        PlayerController player = other.GetComponent<PlayerController>();
        if (player != null)
        {
            player.TakeDamage(contactDamage);
            nextContactTime = Time.time + contactCooldown;
        }
    }
}