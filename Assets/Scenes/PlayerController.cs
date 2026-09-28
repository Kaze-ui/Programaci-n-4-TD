using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Movimiento")]
    public float moveSpeed = 250f; // valor ajustado a mano por el usuario, se mantiene igual

    [Header("Modo Lento / Precisión (Toggle con Shift o Gatillo Derecho)")]
    public bool isSlowModeActive = false;
    [Range(0f, 1f)] public float slowSpeedReduction = 0.60f; // 60% de reducción de velocidad
    private bool prevRightTriggerDown = false;
    private bool prevLeftTriggerDown = false;

    public enum FireMode { Normal, HeavyCharged }

    [Header("Modo de Disparo")]
    public FireMode currentFireMode = FireMode.Normal;
    private bool hasFiredChargedShot = false;

    [Header("Disparo")]
    public GameObject bulletPrefab;
    public GameObject bigBulletPrefab; // Bala gigante cargada (Tier 3 pero -20% tamaño)
    public Transform firePoint;
    public float fireRate = 0.3f; // segundos entre disparos
    private float nextFireTime = 0f;

    [Header("Bala Gigante Cargada")]
    public float chargeTimeRequired = 2.5f; // Tiempo de carga manteniendo presionado el botón
    public float chargeSoundThreshold = 0.20f; // Tiempo mínimo antes de activar el sonido de carga (evita sonar al hacer clics rápidos)
    public float maxChargeHoldTime = 5.0f; // Tiempo máximo que se puede retener la bala cargada sin dispararse (almacenada)
    private float currentChargeTime = 0f;
    private bool isFullyCharged = false;
    private float chargeHoldTimer = 0f;

    // Daño base de cada bala disparada. Empieza en 1 y sube con la mejora de Daño
    // comprada en el UpgradePanel (ver UpgradeManager.IncreaseDamage).
    public float bulletDamage = 0.5f;

    [Header("Vida")]
    public int maxHealth = 3;
    private int currentHealth;

    [Header("Límites de pantalla")]
    public float screenPadding = 25f;
    private Camera mainCam;

    void Start()
    {
        currentHealth = maxHealth;
        mainCam = Camera.main;
    }

    void Update()
    {
        if (Time.timeScale == 0f) return;

        HandleMovement();
        HandleShooting();
    }

    void LateUpdate()
    {
        if (Time.timeScale == 0f) return;

        ClampToScreen();
    }

    void ClampToScreen()
    {
        if (mainCam == null) mainCam = Camera.main;
        if (mainCam == null) return;

        float vertExtent = mainCam.orthographicSize;
        float horzExtent = vertExtent * mainCam.aspect;

        float paddingX = screenPadding;
        float paddingY = screenPadding;

        if (TryGetComponent<SpriteRenderer>(out var sr) && sr.sprite != null)
        {
            paddingX = Mathf.Max(paddingX, sr.bounds.extents.x);
            paddingY = Mathf.Max(paddingY, sr.bounds.extents.y);
        }
        else if (TryGetComponent<Collider2D>(out var col))
        {
            paddingX = Mathf.Max(paddingX, col.bounds.extents.x);
            paddingY = Mathf.Max(paddingY, col.bounds.extents.y);
        }

        float minX = mainCam.transform.position.x - horzExtent + paddingX;
        float maxX = mainCam.transform.position.x + horzExtent - paddingX;
        float minY = mainCam.transform.position.y - vertExtent + paddingY;
        float maxY = mainCam.transform.position.y + vertExtent - paddingY;

        Vector3 pos = transform.position;
        pos.x = Mathf.Clamp(pos.x, minX, maxX);
        pos.y = Mathf.Clamp(pos.y, minY, maxY);
        transform.position = pos;
    }

    void HandleMovement()
    {
        // Alternar modo lento (-60% velocidad) con tecla Shift o Gatillo Derecho (RT) en modo toggle
        bool toggleSlowMode = false;

        if (UnityEngine.InputSystem.Keyboard.current != null)
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb.leftShiftKey.wasPressedThisFrame || kb.rightShiftKey.wasPressedThisFrame)
            {
                toggleSlowMode = true;
            }
        }

        if (UnityEngine.InputSystem.Gamepad.current != null)
        {
            var pad = UnityEngine.InputSystem.Gamepad.current;
            bool isRtDown = pad.rightTrigger.isPressed || (pad.rightTrigger.ReadValue() > 0.35f);
            if (isRtDown && !prevRightTriggerDown)
            {
                toggleSlowMode = true;
            }
            prevRightTriggerDown = isRtDown;
        }
        else
        {
            prevRightTriggerDown = false;
        }

        if (toggleSlowMode)
        {
            isSlowModeActive = !isSlowModeActive;
        }

        Vector2 movement = Vector2.zero;

        // Teclado: WASD y Flechas direccionales
        if (UnityEngine.InputSystem.Keyboard.current != null)
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) movement.y += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) movement.y -= 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) movement.x -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) movement.x += 1f;
        }

        // Gamepad: Stick izquierdo y Cruceta (D-pad)
        if (UnityEngine.InputSystem.Gamepad.current != null)
        {
            var pad = UnityEngine.InputSystem.Gamepad.current;
            Vector2 stick = pad.leftStick.ReadValue();
            if (stick.sqrMagnitude > 0.01f)
            {
                movement += stick;
            }
            if (pad.dpad.up.isPressed) movement.y += 1f;
            if (pad.dpad.down.isPressed) movement.y -= 1f;
            if (pad.dpad.left.isPressed) movement.x -= 1f;
            if (pad.dpad.right.isPressed) movement.x += 1f;
        }

        if (movement.sqrMagnitude > 1f)
        {
            movement = movement.normalized;
        }

        float effectiveSpeed = isSlowModeActive ? (moveSpeed * (1f - slowSpeedReduction)) : moveSpeed;
        transform.position += (Vector3)movement * effectiveSpeed * Time.deltaTime;
    }

    void HandleShooting()
    {
        // 1. Cambio de modo de disparo: Clic derecho (mouse), tecla Q o E (teclado), botón Y/RB o Gatillo Izquierdo (LT) (gamepad)
        bool toggleModePressed = false;
        if (UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.rightButton.wasPressedThisFrame)
        {
            toggleModePressed = true;
        }
        if (UnityEngine.InputSystem.Keyboard.current != null && (UnityEngine.InputSystem.Keyboard.current.qKey.wasPressedThisFrame || UnityEngine.InputSystem.Keyboard.current.eKey.wasPressedThisFrame))
        {
            toggleModePressed = true;
        }
        if (UnityEngine.InputSystem.Gamepad.current != null)
        {
            var pad = UnityEngine.InputSystem.Gamepad.current;

            // Gatillo izquierdo (LT) con detección de flanco confiable para alternar arma
            bool isLtDown = pad.leftTrigger.isPressed || (pad.leftTrigger.ReadValue() > 0.35f);
            if (isLtDown && !prevLeftTriggerDown)
            {
                toggleModePressed = true;
            }
            prevLeftTriggerDown = isLtDown;

            if (pad.buttonNorth.wasPressedThisFrame || pad.rightShoulder.wasPressedThisFrame || pad.leftShoulder.wasPressedThisFrame)
            {
                toggleModePressed = true;
            }
        }
        else
        {
            prevLeftTriggerDown = false;
        }

        if (toggleModePressed)
        {
            if (SoundController.HasInstance)
            {
                SoundController.Instance.StopChargeSound();
                SoundController.Instance.PlaySwitchModeSfx();
            }
            currentFireMode = (currentFireMode == FireMode.Normal) ? FireMode.HeavyCharged : FireMode.Normal;
            currentChargeTime = 0f;
            isFullyCharged = false;
            chargeHoldTimer = 0f;
            hasFiredChargedShot = false;
        }

        // 2. Detección de botón de disparo presionado
        bool isHoldingFire = false;

        if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.spaceKey.isPressed)
        {
            isHoldingFire = true;
        }

        if (UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.leftButton.isPressed)
        {
            isHoldingFire = true;
        }

        if (UnityEngine.InputSystem.Gamepad.current != null)
        {
            var pad = UnityEngine.InputSystem.Gamepad.current;
            if (pad.buttonSouth.isPressed || pad.buttonWest.isPressed)
            {
                isHoldingFire = true;
            }
        }

        // 3. Lógica según el modo activo
        if (currentFireMode == FireMode.Normal)
        {
            if (SoundController.Instance != null)
            {
                SoundController.Instance.StopChargeSound();
            }

            // Modo Normal: si dejo apretado el click, disparo muchas balas normales continuas (como siempre)
            if (isHoldingFire)
            {
                if (Time.time >= nextFireTime)
                {
                    nextFireTime = Time.time + fireRate;
                    ShootNormalBullet();
                }
            }

            currentChargeTime = 0f;
            hasFiredChargedShot = false;
        }
        else // FireMode.HeavyCharged
        {
            // Modo 2: Balas grandes (mantener 2.5s, retener hasta 5s) / Ráfaga ligera de balas pequeñas (toques rápidos)
            if (isHoldingFire)
            {
                if (!hasFiredChargedShot)
                {
                    if (!isFullyCharged)
                    {
                        currentChargeTime += Time.deltaTime;

                        // Solo activamos el sonido de carga si se mantiene presionado más allá del umbral de un clic rápido
                        if (currentChargeTime >= chargeSoundThreshold && SoundController.Instance != null)
                        {
                            float progress = Mathf.Clamp01((currentChargeTime - chargeSoundThreshold) / (chargeTimeRequired - chargeSoundThreshold));
                            SoundController.Instance.UpdateChargeSound(progress);
                        }

                        if (currentChargeTime >= chargeTimeRequired)
                        {
                            // ¡Carga máxima alcanzada! Pasa a estar almacenada lista para disparar
                            isFullyCharged = true;
                            chargeHoldTimer = maxChargeHoldTime;
                            currentChargeTime = chargeTimeRequired;
                        }
                    }
                    else
                    {
                        // La bala ya está cargada al máximo y almacenada mientras el jugador mantiene el botón apuntando
                        chargeHoldTimer -= Time.deltaTime;

                        // Si pasan los 5 segundos completos sin soltar el clic, se dispara automáticamente
                        if (chargeHoldTimer <= 0f)
                        {
                            if (SoundController.Instance != null)
                            {
                                SoundController.Instance.StopChargeSound();
                            }
                            ShootBigBullet();
                            hasFiredChargedShot = true;
                            isFullyCharged = false;
                            currentChargeTime = 0f;
                        }
                    }
                }
            }
            else
            {
                // El jugador soltó el botón de disparo
                if (SoundController.Instance != null)
                {
                    SoundController.Instance.StopChargeSound();
                }

                if (isFullyCharged)
                {
                    // ¡Soltó el clic dentro de los 5 segundos de retención! Dispara la bala gigante (x6 daño)
                    ShootBigBullet();
                    hasFiredChargedShot = false;
                    isFullyCharged = false;
                    currentChargeTime = 0f;
                }
                else if (!hasFiredChargedShot && currentChargeTime >= 1.0f)
                {
                    // Soltó entre 1 segundo y menos de la carga completa de 2.5s:
                    // Dispara la bala intermedia (daño x3, velocidad igual a la normal, tamaño intermedio)
                    ShootMediumBullet();
                    hasFiredChargedShot = false;
                    isFullyCharged = false;
                    currentChargeTime = 0f;
                }
                else if (!hasFiredChargedShot && currentChargeTime > 0f)
                {
                    // Soltó antes de llegar a 1 segundo (un clic o toques rápidos):
                    // dispara una bala normal más pequeña (-30%) que causa la mitad de daño
                    if (Time.time >= nextFireTime)
                    {
                        nextFireTime = Time.time + (fireRate * 0.75f);
                        ShootMiniBullet();
                    }
                }

                hasFiredChargedShot = false;
                isFullyCharged = false;
                currentChargeTime = 0f;
            }
        }
    }

    void OnDisable()
    {
        if (SoundController.HasInstance)
        {
            SoundController.Instance.StopChargeSound();
        }
        isFullyCharged = false;
        currentChargeTime = 0f;
    }

    void ShootNormalBullet()
    {
        if (bulletPrefab == null || firePoint == null) return;

        GameObject obj = Instantiate(bulletPrefab, firePoint.position, Quaternion.identity);

        Bullet bulletScript = obj.GetComponent<Bullet>();
        if (bulletScript != null)
        {
            bulletScript.damage = bulletDamage;
            bulletScript.speed = 2000f;
        }

        if (SoundController.Instance != null)
        {
            SoundController.Instance.PlayNormalShootSfx();
        }
    }

    void ShootBigBullet()
    {
        Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position;
        GameObject obj = null;

        if (bigBulletPrefab != null)
        {
            obj = Instantiate(bigBulletPrefab, spawnPos, Quaternion.identity);
        }
        else if (bulletPrefab != null)
        {
            obj = Instantiate(bulletPrefab, spawnPos, Quaternion.identity);
            obj.transform.localScale = new Vector3(160f, 200f, 1f);
        }

        if (obj != null)
        {
            Bullet bulletScript = obj.GetComponent<Bullet>();
            if (bulletScript != null)
            {
                bulletScript.isBigBullet = true;
                // Saca lo que sacarían 6 balas normales con el daño actual del jugador (x6)
                bulletScript.damage = bulletDamage * 6f;
                // +35% de velocidad respecto al valor previo de 1500 (1500 * 1.35 = 2025)
                bulletScript.speed = 2025f;
                bulletScript.lifeTime = 4f;
            }
        }

        if (SoundController.Instance != null)
        {
            SoundController.Instance.PlayBigShootSfx();
        }
    }

    void ShootMiniBullet()
    {
        if (bulletPrefab == null || firePoint == null) return;

        GameObject obj = Instantiate(bulletPrefab, firePoint.position, Quaternion.identity);

        // Ligeramente más pequeña de tamaño (30% menos tamaño: 15 * 0.7 = 10.5, 30 * 0.7 = 21)
        obj.transform.localScale = new Vector3(10.5f, 21f, 1f);

        Bullet bulletScript = obj.GetComponent<Bullet>();
        if (bulletScript != null)
        {
            // Hace exactamente la mitad de daño que una bala normal del primer modo de disparo
            bulletScript.damage = bulletDamage * 0.5f;
            bulletScript.speed = 2000f;
        }

        if (SoundController.Instance != null)
        {
            SoundController.Instance.PlayMiniShootSfx();
        }
    }

    void ShootMediumBullet()
    {
        if (bulletPrefab == null || firePoint == null) return;

        GameObject obj = Instantiate(bulletPrefab, firePoint.position, Quaternion.identity);

        // Tamaño intermedio: bala normal es (15, 30, 1), bala grande es (160, 200, 1).
        // Bala intermedia tiene escala (60, 90, 1), claramente más grande que la normal y más chica que la grande.
        obj.transform.localScale = new Vector3(60f, 90f, 1f);

        SpriteRenderer sr = obj.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.color = new Color(0.35f, 0.90f, 1f, 1f); // Tinte cian brillante de carga intermedia
        }

        Bullet bulletScript = obj.GetComponent<Bullet>();
        if (bulletScript != null)
        {
            // Daño x3 de una bala de disparo normal
            bulletScript.damage = bulletDamage * 3f;
            // Misma velocidad que la bala de disparo normal
            bulletScript.speed = 2000f;
            bulletScript.lifeTime = 3.5f;
        }

        if (SoundController.Instance != null)
        {
            SoundController.Instance.PlayMediumShootSfx();
        }
    }

    void OnGUI()
    {
        if (GameManager.Instance != null && GameManager.Instance.currentState != GameManager.GameState.Playing && GameManager.Instance.currentState != GameManager.GameState.BossFight) return;

        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 17;
        style.fontStyle = FontStyle.Bold;
        style.wordWrap = false;
        style.clipping = TextClipping.Overflow;

        string modeStr;
        if (currentFireMode == FireMode.Normal)
        {
            style.normal.textColor = new Color(0.4f, 0.9f, 1f);
            modeStr = "SISTEMA: CAÑÓN DE PULSOS [Clic Der / LT: Alternar Armas]";
        }
        else
        {
            if (isFullyCharged)
            {
                style.normal.textColor = new Color(1f, 0.9f, 0.2f);
                modeStr = $"SISTEMA: HIPER-PLASMA [¡NÚCLEO AL MÁXIMO! ({chargeHoldTimer:0.0}s) - Suelta para disparar]";
            }
            else if (hasFiredChargedShot)
            {
                style.normal.textColor = new Color(0.3f, 1f, 0.4f);
                modeStr = "SISTEMA: HIPER-PLASMA (¡Descarga efectuada! Reciclando energía...)";
            }
            else if (currentChargeTime >= 1.0f)
            {
                style.normal.textColor = new Color(0.25f, 0.95f, 1f);
                modeStr = $"SISTEMA: HIPER-PLASMA [¡Foco de plasma intermedio (x3)! ({currentChargeTime:0.0}s / {chargeTimeRequired:0.0}s)]";
            }
            else if (currentChargeTime >= chargeSoundThreshold)
            {
                style.normal.textColor = new Color(1f, 0.55f, 0.1f);
                modeStr = $"SISTEMA: HIPER-PLASMA (Sobrecargando condensador: {currentChargeTime:0.0}s / {chargeTimeRequired:0.0}s)";
            }
            else
            {
                style.normal.textColor = new Color(1f, 0.85f, 0.2f);
                modeStr = "SISTEMA: HIPER-PLASMA [Mantén 1-2s: Intermedia (x3) | 2.5s: Gigante (x6)]";
            }
        }

        if (isSlowModeActive)
        {
            modeStr += "  |  [PROPULSIÓN DE MANIOBRA: -60% (Shift/RT)]";
        }

        GUI.Label(new Rect(280, Screen.height - 42, Screen.width - 290, 36), modeStr, style);
    }

    public void TakeDamage(int amount)
    {
        currentHealth -= amount;
        currentHealth = Mathf.Max(currentHealth, 0);

        if (currentHealth > 0)
        {
            if (SoundController.Instance != null)
            {
                SoundController.Instance.PlayPlayerHitSfx();
            }
        }
        else
        {
            if (SoundController.Instance != null)
            {
                SoundController.Instance.PlayPlayerDeathSfx();
            }
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

    public void IncreaseDamage(float amount)
    {
        bulletDamage += amount;
    }

    public void IncreaseDamage(int amount)
    {
        IncreaseDamage((float)amount);
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