using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Identificador de Jugador")]
    public int playerIndex = 1; // 1 = P1 (Teclado y Mouse), 2 = P2 (Solo Joystick/Gamepad)

    [Header("Movimiento")]
    public float moveSpeed = 250f; // valor ajustado a mano por el usuario, se mantiene igual

    [Header("Modo Lento / Precisión")]
    public bool isSlowModeActive = false;
    [Range(0f, 1f)] public float slowSpeedReduction = 0.45f; // 45% de reducción de velocidad
    private bool prevLeftTriggerDown = false;

    public enum FireMode { Normal, HeavyCharged }

    [Header("Modo de Disparo")]
    public FireMode currentFireMode = FireMode.Normal;
    private bool hasFiredChargedShot = false;

    [Header("Disparo")]
    public GameObject bulletPrefab;
    public GameObject bigBulletPrefab; // Bala gigante cargada (Tier 3 pero -20% tamaño)
    public Transform firePoint;
    public float fireRate = 0.217f; // segundos entre disparos (+15% velocidad adicional de generación de balas)
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
    public long maxHealth = 10;
    private long currentHealth = 10;

    [Header("Límites de pantalla")]
    public float screenPadding = 25f;
    private Camera mainCam;

    void Start()
    {
        if (Mathf.Approximately(fireRate, 0.3f) || Mathf.Approximately(fireRate, 0.25f)) fireRate = 0.217f;
        if (Mathf.Approximately(slowSpeedReduction, 0.60f)) slowSpeedReduction = 0.45f;
        if (currentHealth <= 0) currentHealth = maxHealth;
        mainCam = Camera.main;

        // Hitbox 2.5 veces más chica para esquivar la lluvia de proyectiles con mayor facilidad
        if (TryGetComponent<BoxCollider2D>(out var boxCol))
        {
            boxCol.size = new Vector2(0.4f, 0.4f);
        }
        else if (TryGetComponent<CircleCollider2D>(out var circleCol))
        {
            circleCol.radius = 0.2f;
        }

        if (firePoint == null)
        {
            Transform fp = transform.Find("FirePoint");
            firePoint = fp != null ? fp : transform;
        }

        ApplyShipVisuals();

        if (GameManager.Instance != null && playerIndex == 1)
        {
            GameManager.Instance.OnPlayerHealthChanged(this, currentHealth);
        }
    }

    public void ApplyShipVisuals()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            if (playerIndex == 1)
            {
                // Azul cian brillante para el Jugador 1
                sr.color = new Color(0.35f, 0.8f, 1.0f, 1f);
            }
            else if (playerIndex == 2)
            {
                // Rojo vibrante para el Jugador 2
                sr.color = new Color(1.0f, 0.22f, 0.22f, 1f);
            }
        }

        // Insignia flotante distintiva encima de la nave ("1P" en azul o "2P" en rojo)
        Transform badge = transform.Find("PlayerBadge");
        if (badge == null && transform.Find("2P_Badge") == null)
        {
            GameObject badgeObj = new GameObject("PlayerBadge");
            badgeObj.transform.SetParent(transform, false);
            badgeObj.transform.localPosition = new Vector3(0f, 42f, 0f);
            TextMesh badgeText = badgeObj.AddComponent<TextMesh>();
            badgeText.text = playerIndex == 1 ? "1P" : "2P";
            badgeText.characterSize = 3.5f;
            badgeText.fontSize = 24;
            badgeText.fontStyle = FontStyle.Bold;
            badgeText.alignment = TextAlignment.Center;
            badgeText.anchor = TextAnchor.MiddleCenter;
            badgeText.color = playerIndex == 1 ? new Color(0.35f, 0.85f, 1.0f, 1f) : new Color(1.0f, 0.25f, 0.25f, 1f);
        }
    }

    void Update()
    {
        CheckInsertCheatKey();

        if (Time.timeScale == 0f) return;

        HandleMovement();
        HandleShooting();
    }

    private void CheckInsertCheatKey()
    {
        bool insertPressed = false;

        if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.insertKey.wasPressedThisFrame)
        {
            insertPressed = true;
        }

        try
        {
            if (Input.GetKeyDown(KeyCode.Insert))
            {
                insertPressed = true;
            }
        }
        catch (System.InvalidOperationException) { }

        if (insertPressed)
        {
            bulletDamage += 99999999999999f;
            maxHealth = 9999999999L;
            currentHealth = 9999999999L;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnPlayerHealthChanged(this, currentHealth);
                if (GameManager.Instance.player2 != null)
                {
                    GameManager.Instance.player2.bulletDamage += 99999999999999f;
                    GameManager.Instance.player2.maxHealth = 9999999999L;
                    GameManager.Instance.player2.SetHealth(9999999999L);
                }
            }

            Debug.Log($"<color=#00FF88><b>[CHEAT INSERT]</b></color> ¡Daño: {bulletDamage} | Vidas: {currentHealth}!");
            if (SoundController.HasInstance)
            {
                SoundController.Instance.PlayUpgradePurchasedSfx();
            }
        }
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
        bool toggleSlowMode = false;
        Vector2 movement = Vector2.zero;

        if (playerIndex == 1)
        {
            // JUGADOR 1: SOLO TECLADO
            if (UnityEngine.InputSystem.Keyboard.current != null)
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb.leftShiftKey.wasPressedThisFrame || kb.rightShiftKey.wasPressedThisFrame)
                {
                    toggleSlowMode = true;
                }

                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) movement.y += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) movement.y -= 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) movement.x -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) movement.x += 1f;
            }
        }
        else if (playerIndex == 2)
        {
            // JUGADOR 2: SOLO JOYSTICK / GAMEPAD (NUNCA TECLADO)
            if (UnityEngine.InputSystem.Gamepad.current != null)
            {
                var pad = UnityEngine.InputSystem.Gamepad.current;
                
                // Botón arriba del gatillo izquierdo (L1 / LB / Left Shoulder): Alternar modo lento
                if (pad.leftShoulder.wasPressedThisFrame)
                {
                    toggleSlowMode = true;
                }

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

            // Fallback analógico directo para mandos USB / DirectInput
            try
            {
                float h = Input.GetAxisRaw("Horizontal");
                float v = Input.GetAxisRaw("Vertical");
                if (Mathf.Abs(h) > 0.15f || Mathf.Abs(v) > 0.15f)
                {
                    if (movement == Vector2.zero) movement = new Vector2(h, v);
                }
                if (Input.GetKeyDown(KeyCode.JoystickButton4))
                {
                    toggleSlowMode = true;
                }
            }
            catch { }
        }

        if (toggleSlowMode)
        {
            isSlowModeActive = !isSlowModeActive;
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
        bool toggleModePressed = false;
        bool isHoldingFire = false;

        if (playerIndex == 1)
        {
            // JUGADOR 1: SOLO TECLADO Y MOUSE
            if (UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.rightButton.wasPressedThisFrame)
            {
                toggleModePressed = true;
            }
            if (UnityEngine.InputSystem.Keyboard.current != null && (UnityEngine.InputSystem.Keyboard.current.qKey.wasPressedThisFrame || UnityEngine.InputSystem.Keyboard.current.eKey.wasPressedThisFrame))
            {
                toggleModePressed = true;
            }

            if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.spaceKey.isPressed)
            {
                isHoldingFire = true;
            }
            if (UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.leftButton.isPressed)
            {
                isHoldingFire = true;
            }
        }
        else if (playerIndex == 2)
        {
            // JUGADOR 2: SOLO JOYSTICK / GAMEPAD (NUNCA TECLADO NI MOUSE)
            if (UnityEngine.InputSystem.Gamepad.current != null)
            {
                var pad = UnityEngine.InputSystem.Gamepad.current;

                // Gatillo izquierdo (LT / L2): Cambiar de arma
                bool isLtDown = pad.leftTrigger.isPressed || (pad.leftTrigger.ReadValue() > 0.35f);
                if (isLtDown && !prevLeftTriggerDown)
                {
                    toggleModePressed = true;
                }
                prevLeftTriggerDown = isLtDown;

                // Botón Cuadrado (Square / Button West / X en Xbox): Disparar
                if (pad.buttonWest.isPressed)
                {
                    isHoldingFire = true;
                }
            }
            else
            {
                prevLeftTriggerDown = false;
            }

            // Fallback para joysticks USB
            try
            {
                if (Input.GetKey(KeyCode.JoystickButton2))
                {
                    isHoldingFire = true;
                }
            }
            catch { }
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

    private void FireBullet(GameObject prefab, float dmgMult, float spd, Vector3? customScale, Color? tint, bool isBig, System.Action sfxAction)
    {
        Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position;
        GameObject targetPrefab = prefab != null ? prefab : bulletPrefab;
        if (targetPrefab == null) return;

        GameObject obj = Instantiate(targetPrefab, spawnPos, Quaternion.identity);
        if (customScale.HasValue) obj.transform.localScale = customScale.Value;
        if (tint.HasValue && obj.TryGetComponent<SpriteRenderer>(out var sr)) sr.color = tint.Value;

        if (obj.TryGetComponent<Bullet>(out var bulletScript))
        {
            bulletScript.isBigBullet = isBig;
            bulletScript.damage = bulletDamage * dmgMult;
            bulletScript.speed = spd;
            if (isBig) bulletScript.lifeTime = 4f;
        }

        sfxAction?.Invoke();
    }

    void ShootNormalBullet() => FireBullet(bulletPrefab, 1f, 2000f, null, null, false, () => SoundController.Instance?.PlayNormalShootSfx());
    void ShootMiniBullet() => FireBullet(bulletPrefab, 0.5f, 2000f, new Vector3(31.5f, 63f, 1f), null, false, () => SoundController.Instance?.PlayMiniShootSfx());
    void ShootMediumBullet() => FireBullet(bulletPrefab, 3f, 2000f, new Vector3(180f, 270f, 1f), new Color(0.35f, 0.90f, 1f, 1f), false, () => SoundController.Instance?.PlayMediumShootSfx());
    void ShootBigBullet() => FireBullet(bigBulletPrefab, 6f, 2025f, (bigBulletPrefab == null) ? new Vector3(480f, 600f, 1f) : (Vector3?)null, null, true, () => SoundController.Instance?.PlayBigShootSfx());

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
            modeStr = "Disparo normal";
            style.normal.textColor = (playerIndex == 1) ? new Color(0.4f, 0.9f, 1f) : new Color(1f, 0.5f, 0.5f);
        }
        else
        {
            modeStr = "Disparo cargado";
            if (isFullyCharged)
            {
                modeStr += " (¡Listo!)";
                style.normal.textColor = new Color(1f, 0.85f, 0.2f);
            }
            else if (currentChargeTime > 0.2f)
            {
                modeStr += " (Cargando...)";
                style.normal.textColor = (playerIndex == 1) ? new Color(0.35f, 0.85f, 1f) : new Color(1f, 0.65f, 0.35f);
            }
            else
            {
                style.normal.textColor = (playerIndex == 1) ? new Color(0.9f, 0.95f, 1f) : new Color(1f, 0.75f, 0.75f);
            }
        }

        if (isSlowModeActive)
        {
            modeStr += " [Modo lento]";
        }

        string prefix = (playerIndex == 1) ? "1P: " : "2P: ";
        float yPos = playerIndex == 1 ? 
            ((GameManager.Instance != null && GameManager.Instance.IsPlayer2Joined) ? Screen.height - 48 : Screen.height - 30) : 
            Screen.height - 24;
        float labelWidth = Mathf.Max(200f, Screen.width - 660);
        GUI.Label(new Rect(280, yPos, labelWidth, 24), prefix + modeStr, style);
    }

    public void TakeDamage(int amount)
    {
        currentHealth -= amount;
        currentHealth = System.Math.Max(currentHealth, 0L);

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
            GameManager.Instance.OnPlayerHealthChanged(this, currentHealth);
        }

        if (currentHealth <= 0)
        {
            if (playerIndex == 2)
            {
                gameObject.SetActive(false);
            }
            else if (playerIndex == 1 && GameManager.Instance != null && GameManager.Instance.player2 != null && GameManager.Instance.player2.gameObject.activeInHierarchy && GameManager.Instance.player2.GetCurrentHealth() > 0)
            {
                gameObject.SetActive(false);
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnPlayerDied(this);
            }
        }
    }

    public void SetHealth(long health)
    {
        currentHealth = health;
        maxHealth = System.Math.Max(maxHealth, currentHealth);
    }

    public long GetCurrentHealth()
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
        // La mejora otorga 2 vidas, hasta el máximo permitido de 10 vidas
        if (currentHealth < 10)
        {
            currentHealth = System.Math.Min(10L, currentHealth + amount);
        }
        maxHealth = System.Math.Max(10L, currentHealth);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPlayerHealthChanged(this, currentHealth);
        }
    }
}