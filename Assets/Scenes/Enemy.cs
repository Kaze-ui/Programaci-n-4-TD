using UnityEngine;

public class Enemy : MonoBehaviour, IDamageable
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

    private Camera mainCam;
    private bool hasEnteredScreen = false;

    void Start()
    {
        currentHealth = maxHealth;
        mainCam = Camera.main;
    }

    void Update()
    {
        if (Time.timeScale == 0f) return;
        transform.position += Vector3.down * speed * Time.deltaTime;
    }

    void LateUpdate()
    {
        if (Time.timeScale == 0f) return;
        if (mainCam == null) mainCam = Camera.main;
        if (mainCam == null) return;

        float vertExtent = mainCam.orthographicSize;
        float horzExtent = vertExtent * mainCam.aspect;

        float paddingX = 25f;
        float paddingY = 25f;
        if (TryGetComponent<SpriteRenderer>(out var sr) && sr.sprite != null)
        {
            paddingX = Mathf.Max(paddingX, sr.bounds.extents.x);
            paddingY = Mathf.Max(paddingY, sr.bounds.extents.y);
        }

        float minScreenX = mainCam.transform.position.x - horzExtent + paddingX;
        float maxScreenX = mainCam.transform.position.x + horzExtent - paddingX;
        float minScreenY = mainCam.transform.position.y - vertExtent + paddingY;
        float maxScreenY = mainCam.transform.position.y + vertExtent - paddingY;

        Vector3 pos = transform.position;
        if (!hasEnteredScreen)
        {
            if (pos.x >= minScreenX && pos.x <= maxScreenX && pos.y >= minScreenY && pos.y <= maxScreenY)
            {
                hasEnteredScreen = true;
            }
        }

        if (hasEnteredScreen)
        {
            pos.x = Mathf.Clamp(pos.x, minScreenX, maxScreenX);
            pos.y = Mathf.Clamp(pos.y, minScreenY, maxScreenY);
            transform.position = pos;
        }
    }

    public void TakeDamage(int amount)
    {
        if (Time.timeScale == 0f) return;

        currentHealth -= amount;

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    public void TakeDamage(float amount)
    {
        TakeDamage(Mathf.Max(1, Mathf.RoundToInt(amount)));
    }

    public bool IsDead()
    {
        return currentHealth <= 0;
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
        if (Time.timeScale == 0f) return;
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