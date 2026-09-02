using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Movimiento")]
    public float moveSpeed = 250f; // valor ajustado a mano por el usuario, se mantiene igual

    [Header("Disparo")]
    public GameObject bulletPrefab;
    public Transform firePoint;
    public float fireRate = 0.3f; // segundos entre disparos
    private float nextFireTime = 0f;

    // Daño base de cada bala disparada. Empieza en 1 y sube con la mejora de Daño
    // comprada en el UpgradePanel (ver UpgradeManager.IncreaseDamage).
    public int bulletDamage = 1;

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
        bool wantsToShoot = Input.GetKey(KeyCode.Space) || Input.GetButton("Fire1");

        if (wantsToShoot && Time.time >= nextFireTime)
        {
            nextFireTime = Time.time + fireRate;
            Shoot();
        }
    }

    void Shoot()
    {
        if (bulletPrefab != null && firePoint != null)
        {
            GameObject obj = Instantiate(bulletPrefab, firePoint.position, Quaternion.identity);

            // Le pasamos el daño actual del jugador a la bala recién instanciada,
            // así las mejoras de Daño compradas se reflejan sin tener que editar el prefab.
            Bullet bulletScript = obj.GetComponent<Bullet>();
            if (bulletScript != null)
            {
                bulletScript.damage = bulletDamage;
            }
        }

        if (SoundController.Instance != null)
        {
            SoundController.Instance.PlayShootSfx();
        }
    }

    public void TakeDamage(int amount)
    {
        currentHealth -= amount;
        currentHealth = Mathf.Max(currentHealth, 0);

        if (SoundController.Instance != null)
        {
            SoundController.Instance.PlayPlayerHitSfx();
        }

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

    // ---- Métodos llamados por UpgradeManager al comprar cada mejora ----

    public void IncreaseDamage(int amount)
    {
        bulletDamage += amount;
    }

    public void IncreaseSpeed(float amount)
    {
        moveSpeed += amount;
    }

    public void IncreaseMaxHealth(int amount)
    {
        // Sube el tope Y también cura esa misma cantidad, así la mejora se siente
        // de verdad (si solo subiera el tope, el jugador no ganaría vida real ahora).
        maxHealth += amount;
        currentHealth += amount;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPlayerHealthChanged(currentHealth);
        }
    }
}