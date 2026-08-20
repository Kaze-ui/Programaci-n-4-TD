using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Movimiento")]
    public float moveSpeed = 5f;

    [Header("Disparo")]
    public GameObject bulletPrefab;
    public Transform firePoint;
    public float fireRate = 0.3f; // segundos entre disparos
    private float nextFireTime = 0f;

    [Header("Vida")]
    public int maxHealth = 3;
    private int currentHealth;

    void Start()
    {
        currentHealth = maxHealth;
    }

    void Update()
    {
        HandleMovement();
        HandleShooting();
    }

    void HandleMovement()
    {
        float moveX = Input.GetAxisRaw("Horizontal"); // A/D o flechas izq/der
        float moveY = Input.GetAxisRaw("Vertical");    // W/S o flechas arriba/abajo

        Vector2 movement = new Vector2(moveX, moveY).normalized;
        transform.position += (Vector3)movement * moveSpeed * Time.deltaTime;
    }

    void HandleShooting()
    {
        if (Input.GetKey(KeyCode.Space) && Time.time >= nextFireTime)
        {
            nextFireTime = Time.time + fireRate;
            Shoot();
        }
    }

    void Shoot()
    {
        if (bulletPrefab != null && firePoint != null)
        {
            Instantiate(bulletPrefab, firePoint.position, Quaternion.identity);
        }
    }

    public void TakeDamage(int amount)
    {
        currentHealth -= amount;
        currentHealth = Mathf.Max(currentHealth, 0);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPlayerHealthChanged(currentHealth);
        }

        if (currentHealth <= 0 && GameManager.Instance != null)
        {
            GameManager.Instance.OnPlayerDied();
        }
    }

    public int GetCurrentHealth()
    {
        return currentHealth;
    }
}