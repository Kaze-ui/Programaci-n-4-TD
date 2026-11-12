using UnityEngine;
using UnityEngine.UI;

public class PlayerController : MonoBehaviour
{
    [Header("Identificador de Jugador")]
    public int playerIndex = 1; // 1 = P1 (Teclado/Mouse, o Mando 1 si hay 2+ mandos), 2 = P2 (Mando 1 o Mando 2)

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
    public float chargeTimeRequired = 2.2f; // Tiempo de carga manteniendo presionado el botón
    public float chargeSoundThreshold = 0.20f; // Tiempo mínimo antes de activar el sonido de carga (evita sonar al hacer clics rápidos)
    public float maxChargeHoldTime = 5.0f; // Tiempo máximo que se puede retener la bala cargada sin dispararse (almacenada)
    private float currentChargeTime = 0f;
    private bool isFullyCharged = false;
    private float chargeHoldTimer = 0f;

    // Indicadores visuales de carga (círculo frontal y barra radial arriba a la derecha)
    private GameObject chargeAuraObj;
    private SpriteRenderer chargeAuraSr;
    private GameObject chargeBarRoot;
    private Image chargeBarFillImage;
    private Transform chargeBarCanvasTransform;
    private Vector3 baseBarCanvasScale = Vector3.one;
    private Transform badgeTransform;
    private Vector3 baseBadgeScale = Vector3.one;
    private Vector3 baseShipScale = new Vector3(70f, 70f, 1f);
    private float currentBank = 0f;

    // Daño base de cada bala disparada. Empieza en 1 y sube con la mejora de Daño
    // comprada en el UpgradePanel (ver UpgradeManager.IncreaseDamage).
    public float bulletDamage = 0.5f;

    [Header("Vida")]
    public long maxHealth = 10;
    private long currentHealth = 10;

    [Header("Invulnerabilidad")]
    public float invulnerabilityDuration = 3.0f;
    private float invulnerabilityTimer = 0f;
    private SpriteRenderer shipSpriteRenderer;

    // Anti-camping en la parte superior de la pantalla
    private float topCampTimer = 0f;
    private const float TopCampTriggerSeconds = 2.0f;
    private const float TopCampPunishBulletSpeed = 2800f * (4f / 3f); // +1/3 de velocidad
    public bool IsInvulnerable => invulnerabilityTimer > 0f;

    [Header("Límites de pantalla")]
    public float screenPadding = 25f;
    private Camera mainCam;

    void Start()
    {
        if (Mathf.Approximately(fireRate, 0.3f) || Mathf.Approximately(fireRate, 0.25f)) fireRate = 0.217f;
        if (Mathf.Approximately(slowSpeedReduction, 0.60f)) slowSpeedReduction = 0.45f;
        if (Mathf.Approximately(chargeTimeRequired, 2.5f)) chargeTimeRequired = 2.2f;
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
        EnsureChargeVisuals();

        // Respaldo seguro usando scale.y por si Player2 se clona mientras Player1 está inclinado
        float initY = Mathf.Abs(transform.localScale.y) > 0.01f ? Mathf.Abs(transform.localScale.y) : 70f;
        baseShipScale = new Vector3(Mathf.Sign(transform.localScale.x) * initY, transform.localScale.y, transform.localScale.z);
        transform.localScale = baseShipScale;
        transform.localRotation = Quaternion.identity;
        currentBank = 0f;

        chargeBarCanvasTransform = transform.Find("ChargeBarCanvas");
        if (chargeBarCanvasTransform != null)
        {
            float barY = Mathf.Abs(chargeBarCanvasTransform.localScale.y) > 0.0001f ? Mathf.Abs(chargeBarCanvasTransform.localScale.y) : 1f;
            baseBarCanvasScale = new Vector3(barY, chargeBarCanvasTransform.localScale.y, chargeBarCanvasTransform.localScale.z);
            chargeBarCanvasTransform.localScale = baseBarCanvasScale;
            chargeBarCanvasTransform.localRotation = Quaternion.identity;
        }

        badgeTransform = transform.Find("PlayerBadge") ?? transform.Find("2P_Badge");
        if (badgeTransform != null)
        {
            float bY = Mathf.Abs(badgeTransform.localScale.y) > 0.001f ? Mathf.Abs(badgeTransform.localScale.y) : 1f;
            baseBadgeScale = new Vector3(bY, badgeTransform.localScale.y, badgeTransform.localScale.z);
            badgeTransform.localScale = baseBadgeScale;
            badgeTransform.localRotation = Quaternion.identity;
        }

        if (GameManager.Instance != null && playerIndex == 1)
        {
            GameManager.Instance.OnPlayerHealthChanged(this, currentHealth);
        }
    }

    public void ApplyShipVisuals()
    {
        if (shipSpriteRenderer == null) shipSpriteRenderer = GetComponent<SpriteRenderer>();
        invulnerabilityTimer = 0f;
        RestoreNormalShipColor();

        // Insignia flotante distintiva encima de la nave ("1P" en azul o "2P" en rojo)
        Transform badge = transform.Find("PlayerBadge") ?? transform.Find("2P_Badge");
        if (badge == null)
        {
            GameObject badgeObj = new GameObject("PlayerBadge");
            badgeObj.transform.SetParent(transform, false);
            badgeObj.transform.localPosition = new Vector3(0f, 42f, 0f);
            badge = badgeObj.transform;
        }
        else
        {
            badge.gameObject.name = "PlayerBadge";
        }

        TextMesh badgeText = badge.GetComponent<TextMesh>() ?? badge.gameObject.AddComponent<TextMesh>();
        badgeText.text = playerIndex == 1 ? "1P" : "2P";
        badgeText.characterSize = 3.5f;
        badgeText.fontSize = 24;
        badgeText.fontStyle = FontStyle.Bold;
        badgeText.alignment = TextAlignment.Center;
        badgeText.anchor = TextAnchor.MiddleCenter;
        badgeText.color = playerIndex == 1 ? new Color(0.35f, 0.85f, 1.0f, 1f) : new Color(1.0f, 0.25f, 0.25f, 1f);
        badgeTransform = badge;
    }

    private void EnsureChargeVisuals()
    {
        if (mainCam == null) mainCam = Camera.main;

        SpriteRenderer shipSr = GetComponent<SpriteRenderer>();
        float extX = (shipSr != null && shipSr.sprite != null) ? shipSr.sprite.bounds.extents.x : 0.5f;
        float extY = (shipSr != null && shipSr.sprite != null) ? shipSr.sprite.bounds.extents.y : 0.5f;

        // 1. Círculo energético delante de la nave durante la carga
        Transform existingAura = transform.Find("ChargeAura");
        if (existingAura == null)
        {
            chargeAuraObj = new GameObject("ChargeAura");
            chargeAuraObj.transform.SetParent(transform, false);
            chargeAuraObj.transform.localPosition = new Vector3(0f, extY * 1.15f, 0f);
            chargeAuraSr = chargeAuraObj.AddComponent<SpriteRenderer>();
            chargeAuraSr.sprite = ChargeVisualUtils.GetGlowCircleSprite();
            chargeAuraSr.sortingOrder = (shipSr != null ? shipSr.sortingOrder : 0) + 15;
            chargeAuraObj.SetActive(false);
        }
        else
        {
            chargeAuraObj = existingAura.gameObject;
            chargeAuraSr = chargeAuraObj.GetComponent<SpriteRenderer>();
            chargeAuraObj.SetActive(false);
        }

        // 2. Barra de carga circular arriba a la derecha de la nave
        Transform existingCanvas = transform.Find("ChargeBarCanvas");
        if (existingCanvas == null)
        {
            GameObject barCanvasObj = new GameObject("ChargeBarCanvas", typeof(RectTransform), typeof(Canvas));
            barCanvasObj.transform.SetParent(transform, false);

            Canvas c = barCanvasObj.GetComponent<Canvas>();
            c.renderMode = RenderMode.WorldSpace;
            c.worldCamera = mainCam;
            c.overrideSorting = true;
            c.sortingOrder = (shipSr != null ? shipSr.sortingOrder : 0) + 25;

            RectTransform canvasRt = barCanvasObj.GetComponent<RectTransform>();
            canvasRt.localPosition = new Vector3(extX * 1.32f, extY * 1.12f, 0f);
            canvasRt.sizeDelta = new Vector2(100f, 100f);
            float barScale = (extY * 2f) * 0.46f / 100f;
            canvasRt.localScale = new Vector3(barScale, barScale, 1f);

            chargeBarRoot = ChargeVisualUtils.CreateRadialChargeBar(barCanvasObj.transform, Vector2.zero, new Vector2(100f, 100f), out chargeBarFillImage);
        }
        else
        {
            Transform barTrans = existingCanvas.Find("ChargeRadialBar");
            if (barTrans != null)
            {
                chargeBarRoot = barTrans.gameObject;
                Transform fillTrans = barTrans.Find("Fill");
                if (fillTrans != null) chargeBarFillImage = fillTrans.GetComponent<Image>();
                chargeBarRoot.SetActive(false);
            }
        }
    }

    private void UpdateChargeVisuals(bool visible, float progress, bool full)
    {
        if (chargeAuraObj == null || chargeBarRoot == null) EnsureChargeVisuals();

        if (!visible)
        {
            if (chargeAuraObj != null && chargeAuraObj.activeSelf) chargeAuraObj.SetActive(false);
            if (chargeBarRoot != null && chargeBarRoot.activeSelf) chargeBarRoot.SetActive(false);
            return;
        }

        Color col = ChargeVisualUtils.GetChargeColor(progress, full);

        if (chargeAuraObj != null && chargeAuraSr != null)
        {
            if (!chargeAuraObj.activeSelf) chargeAuraObj.SetActive(true);
            float pulseScale = full ? (Mathf.Sin(Time.time * 18f) * 0.06f) : 0f;
            float baseScale = Mathf.Lerp(0.22f, full ? 0.78f : 0.62f, progress) + pulseScale;
            chargeAuraObj.transform.localScale = new Vector3(baseScale, baseScale, 1f);
            Color auraCol = col;
            auraCol.a = full ? (0.82f + Mathf.Sin(Time.time * 18f) * 0.18f) : Mathf.Lerp(0.28f, 0.85f, progress);
            chargeAuraSr.color = auraCol;
        }

        if (chargeBarRoot != null && chargeBarFillImage != null)
        {
            if (!chargeBarRoot.activeSelf) chargeBarRoot.SetActive(true);
            chargeBarFillImage.fillAmount = Mathf.Clamp01(progress);
            chargeBarFillImage.color = col;
        }
    }

    void Update()
    {
        CheckInsertCheatKey();

        if (Time.timeScale == 0f) return;

        HandleInvulnerabilityVisuals();
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
        HandleTopCampPunishment();
    }

    /// <summary>
    /// Castiga al jugador si permanece 2 segundos continuos en la zona superior muerta de la pantalla
    /// lanzando una bala grande de Tier 3 horizontalmente de izquierda a derecha a gran velocidad.
    /// </summary>
    private void HandleTopCampPunishment()
    {
        if (currentHealth <= 0)
        {
            topCampTimer = 0f;
            return;
        }

        if (GameManager.Instance != null &&
            GameManager.Instance.currentState != GameManager.GameState.Playing &&
            GameManager.Instance.currentState != GameManager.GameState.BossFight)
        {
            topCampTimer = 0f;
            return;
        }

        if (mainCam == null) mainCam = Camera.main;
        if (mainCam == null) return;

        float vertExtent = mainCam.orthographicSize;
        float topEdge = mainCam.transform.position.y + vertExtent;
        float topCampThresholdY = topEdge - (vertExtent * 0.22f);

        if (transform.position.y >= topCampThresholdY)
        {
            topCampTimer += Time.deltaTime;
            if (topCampTimer >= TopCampTriggerSeconds)
            {
                topCampTimer = 0f;
                SpawnTopCampPunishmentBullet(mainCam.transform.position.x, vertExtent);
            }
        }
        else
        {
            topCampTimer = 0f;
        }
    }

    private void SpawnTopCampPunishmentBullet(float camCenterX, float vertExtent)
    {
        GameObject prefab = WaveController.Instance != null ? WaveController.Instance.GetTier3BigBulletPrefab() : null;
        if (prefab == null)
        {
            BossController boss = FindAnyObjectByType<BossController>();
            if (boss != null) prefab = boss.bigBulletPrefab;
        }
        if (prefab == null) return;

        float horzExtent = vertExtent * mainCam.aspect;
        float spawnX = camCenterX - horzExtent - 180f;
        Vector3 spawnPos = new Vector3(spawnX, transform.position.y, 0f);

        GameObject obj = Instantiate(prefab, spawnPos, Quaternion.identity);
        obj.transform.localScale *= (4f / 3f); // +1/3 de tamaño
        if (obj.TryGetComponent<EnemyBullet>(out var eb))
        {
            eb.isHoming = false;
            eb.direction = Vector3.right;
            eb.speed = TopCampPunishBulletSpeed;
            eb.lifeTime = 4f;
            eb.AlignRotationWithDirection();
        }

        if (SoundController.HasInstance)
        {
            SoundController.Instance.PlayBigShootSfx();
        }
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

    private void ReadGamepadMovement(UnityEngine.InputSystem.Gamepad pad, ref Vector2 movement, ref bool toggleSlowMode)
    {
        if (pad == null) return;

        // Botón arriba del gatillo izquierdo (L1 / LB / Left Shoulder): Alternar modo lento
        if (pad.leftShoulder.wasPressedThisFrame)
        {
            toggleSlowMode = true;
        }

        Vector2 stick = pad.leftStick.ReadValue();
        if (stick.sqrMagnitude > 0.04f)
        {
            movement += stick;
        }
        if (pad.dpad.up.isPressed) movement.y += 1f;
        if (pad.dpad.down.isPressed) movement.y -= 1f;
        if (pad.dpad.left.isPressed) movement.x -= 1f;
        if (pad.dpad.right.isPressed) movement.x += 1f;
    }

    private void ReadGamepadShooting(UnityEngine.InputSystem.Gamepad pad, ref bool toggleModePressed, ref bool isHoldingFire)
    {
        if (pad == null)
        {
            prevLeftTriggerDown = false;
            return;
        }

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

    void HandleMovement()
    {
        bool toggleSlowMode = false;
        Vector2 movement = Vector2.zero;
        var assignedPad = GameManager.GetGamepadForPlayer(playerIndex);

        if (playerIndex == 1)
        {
            // JUGADOR 1: Teclado + Mando 1 (cuando hay 2 o más mandos conectados simultáneamente)
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

            if (assignedPad != null)
            {
                ReadGamepadMovement(assignedPad, ref movement, ref toggleSlowMode);
            }
            else if (UnityEngine.InputSystem.Gamepad.all.Count == 0 && GameManager.GetConnectedGamepadCount() >= 2)
            {
                try
                {
                    if (Input.GetKeyDown(KeyCode.Joystick1Button4))
                    {
                        toggleSlowMode = true;
                    }
                }
                catch { }
            }
        }
        else if (playerIndex == 2)
        {
            // JUGADOR 2: SOLO SU MANDO ASIGNADO (Mando 2 si hay 2+, o Mando 1 si hay solo 1)
            if (assignedPad != null)
            {
                ReadGamepadMovement(assignedPad, ref movement, ref toggleSlowMode);
            }
            else if (UnityEngine.InputSystem.Gamepad.all.Count == 0)
            {
                // Fallback analógico directo para mandos USB / DirectInput
                try
                {
                    int joyCount = GameManager.GetConnectedGamepadCount();
                    if (joyCount == 1)
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
                    else if (joyCount >= 2 && Input.GetKeyDown(KeyCode.Joystick2Button4))
                    {
                        toggleSlowMode = true;
                    }
                }
                catch { }
            }
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
        UpdateBankVisuals(movement.x);
    }

    private void UpdateBankVisuals(float horizontalInput)
    {
        ChargeVisualUtils.EvalShipBank(ref currentBank, horizontalInput, Time.deltaTime, 13.5f, 0.19f, out float zAngle, out float scaleXMult);

        transform.localRotation = Quaternion.Euler(0f, 0f, zAngle);
        transform.localScale = new Vector3(baseShipScale.x * scaleXMult, baseShipScale.y, baseShipScale.z);

        float invWidth = 1f / Mathf.Max(0.5f, scaleXMult);
        Quaternion counterRot = Quaternion.Euler(0f, 0f, -zAngle);

        if (chargeBarCanvasTransform != null)
        {
            chargeBarCanvasTransform.localRotation = counterRot;
            chargeBarCanvasTransform.localScale = new Vector3(baseBarCanvasScale.x * invWidth, baseBarCanvasScale.y, baseBarCanvasScale.z);
        }

        if (badgeTransform != null)
        {
            badgeTransform.localRotation = counterRot;
            badgeTransform.localScale = new Vector3(baseBadgeScale.x * invWidth, baseBadgeScale.y, baseBadgeScale.z);
        }
    }

    void HandleShooting()
    {
        bool toggleModePressed = false;
        bool isHoldingFire = false;
        var assignedPad = GameManager.GetGamepadForPlayer(playerIndex);

        if (playerIndex == 1)
        {
            // JUGADOR 1: Teclado/Mouse + Mando 1 (cuando hay 2 o más mandos conectados simultáneamente)
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

            if (assignedPad != null)
            {
                ReadGamepadShooting(assignedPad, ref toggleModePressed, ref isHoldingFire);
            }
            else
            {
                prevLeftTriggerDown = false;
                if (UnityEngine.InputSystem.Gamepad.all.Count == 0 && GameManager.GetConnectedGamepadCount() >= 2)
                {
                    try
                    {
                        if (Input.GetKey(KeyCode.Joystick1Button2))
                        {
                            isHoldingFire = true;
                        }
                    }
                    catch { }
                }
            }
        }
        else if (playerIndex == 2)
        {
            // JUGADOR 2: SOLO SU MANDO ASIGNADO (Mando 2 si hay 2+, o Mando 1 si hay solo 1)
            if (assignedPad != null)
            {
                ReadGamepadShooting(assignedPad, ref toggleModePressed, ref isHoldingFire);
            }
            else
            {
                prevLeftTriggerDown = false;
                if (UnityEngine.InputSystem.Gamepad.all.Count == 0)
                {
                    try
                    {
                        int joyCount = GameManager.GetConnectedGamepadCount();
                        KeyCode fireKey = joyCount >= 2 ? KeyCode.Joystick2Button2 : KeyCode.JoystickButton2;
                        if (joyCount > 0 && Input.GetKey(fireKey))
                        {
                            isHoldingFire = true;
                        }
                    }
                    catch { }
                }
            }
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
            UpdateChargeVisuals(false, 0f, false);
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
            UpdateChargeVisuals(false, 0f, false);
        }
        else // FireMode.HeavyCharged
        {
            // Modo 2: Balas grandes (mantener 2.2s, retener hasta 5s) / Ráfaga ligera de balas pequeñas (toques rápidos)
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

                        float visualProg = Mathf.Clamp01(currentChargeTime / Mathf.Max(0.01f, chargeTimeRequired));
                        UpdateChargeVisuals(true, visualProg, isFullyCharged);
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
                            UpdateChargeVisuals(false, 0f, false);
                        }
                        else
                        {
                            UpdateChargeVisuals(true, 1f, true);
                        }
                    }
                }
                else
                {
                    UpdateChargeVisuals(false, 0f, false);
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
                    // Soltó entre 1 segundo y menos de la carga completa de 2.2s:
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
                UpdateChargeVisuals(false, 0f, false);
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
        topCampTimer = 0f;
        ClearInvulnerability();
        UpdateChargeVisuals(false, 0f, false);
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
    void ShootBigBullet() => FireBullet(bigBulletPrefab, 6f, 2025f, (bigBulletPrefab == null) ? new Vector3(441.6f, 552f, 1f) : (Vector3?)null, null, true, () => SoundController.Instance?.PlayBigShootSfx());

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
        if (amount <= 0 || currentHealth <= 0 || invulnerabilityTimer > 0f) return;

        currentHealth -= amount;
        currentHealth = System.Math.Max(currentHealth, 0L);

        if (currentHealth > 0)
        {
            invulnerabilityTimer = invulnerabilityDuration;
            ApplyInvulnerableBlinkColor(true);

            if (SoundController.Instance != null)
            {
                SoundController.Instance.PlayPlayerHitSfx();
            }
        }
        else
        {
            ClearInvulnerability();

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

    private Color GetBaseShipColor()
    {
        if (playerIndex == 1) return new Color(0.35f, 0.8f, 1.0f, 1f);
        if (playerIndex == 2) return new Color(1.0f, 0.22f, 0.22f, 1f);
        return Color.white;
    }

    private void RestoreNormalShipColor()
    {
        if (shipSpriteRenderer == null) shipSpriteRenderer = GetComponent<SpriteRenderer>();
        if (shipSpriteRenderer != null)
        {
            shipSpriteRenderer.color = GetBaseShipColor();
        }
    }

    private void ApplyInvulnerableBlinkColor(bool brightPhase)
    {
        if (shipSpriteRenderer == null) shipSpriteRenderer = GetComponent<SpriteRenderer>();
        if (shipSpriteRenderer != null)
        {
            float alpha = brightPhase ? 0.94f : 0.24f;
            shipSpriteRenderer.color = new Color(0.62f, 0.44f, 0.44f, alpha);
        }
    }

    private void HandleInvulnerabilityVisuals()
    {
        if (invulnerabilityTimer <= 0f) return;

        invulnerabilityTimer = Mathf.Max(0f, invulnerabilityTimer - Time.deltaTime);
        if (invulnerabilityTimer > 0f)
        {
            bool brightPhase = Mathf.Sin(invulnerabilityTimer * Mathf.PI * 10f) >= 0f;
            ApplyInvulnerableBlinkColor(brightPhase);
        }
        else
        {
            RestoreNormalShipColor();
        }
    }

    public void ClearInvulnerability()
    {
        invulnerabilityTimer = 0f;
        RestoreNormalShipColor();
    }

    public void ActivateInvulnerability(float duration)
    {
        invulnerabilityTimer = Mathf.Max(0f, duration);
        if (invulnerabilityTimer > 0f)
        {
            ApplyInvulnerableBlinkColor(true);
        }
        else
        {
            RestoreNormalShipColor();
        }
    }

    public void ReviveWithHealthAndInvulnerability(long targetHealth, float invulnerabilitySeconds)
    {
        gameObject.SetActive(true);
        if (maxHealth < 10L) maxHealth = 10L;
        currentHealth = System.Math.Clamp(targetHealth, 1L, maxHealth);
        topCampTimer = 0f;
        currentChargeTime = 0f;
        isFullyCharged = false;
        chargeHoldTimer = 0f;
        hasFiredChargedShot = false;
        UpdateChargeVisuals(false, 0f, false);

        ApplyShipVisuals();
        ActivateInvulnerability(invulnerabilitySeconds);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPlayerHealthChanged(this, currentHealth);
        }
    }

    public void SetHealth(long health)
    {
        currentHealth = health;
        maxHealth = System.Math.Max(maxHealth, currentHealth);
        ClearInvulnerability();
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

/// <summary>
/// Utilidades visuales compartidas para el círculo energético de carga y la barra radial circular.
/// </summary>
public static class ChargeVisualUtils
{
    private static Sprite s_GlowCircleSprite;
    private static Sprite s_RadialRingSprite;
    private static Sprite s_PlasmaStreakSprite;
    private static Sprite s_EnergyVortexSprite;

    public static Sprite GetGlowCircleSprite()
    {
        if (s_GlowCircleSprite == null)
        {
            const int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[size * size];
            float c = size * 0.5f;
            float r = size * 0.48f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(c, c));
                    float norm = Mathf.Clamp01(dist / r);
                    float alpha = norm < 0.35f ? 1f : Mathf.Pow(1f - ((norm - 0.35f) / 0.65f), 1.6f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            s_GlowCircleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }
        return s_GlowCircleSprite;
    }

    public static Sprite GetRadialRingSprite()
    {
        if (s_RadialRingSprite == null)
        {
            const int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[size * size];
            float c = size * 0.5f;
            float outerR = size * 0.46f;
            float innerR = size * 0.27f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(c, c));
                    float outerAlpha = Mathf.Clamp01(outerR - dist + 0.8f);
                    float innerAlpha = Mathf.Clamp01(dist - innerR + 0.8f);
                    float a = Mathf.Min(outerAlpha, innerAlpha);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            s_RadialRingSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }
        return s_RadialRingSprite;
    }

    public static Color GetChargeColor(float progress, bool isFullyCharged)
    {
        if (isFullyCharged)
        {
            return new Color(1f, 0.90f, 0.28f, 1f); // Dorado intenso: carga completa
        }
        if (progress >= 0.45f)
        {
            return new Color(0.25f, 0.96f, 1f, 1f); // Cian eléctrico brillante: carga intermedia
        }
        return new Color(0.22f, 0.65f, 1f, 0.96f); // Azul cian inicial
    }

    public static GameObject CreateRadialChargeBar(Transform parent, Vector2 anchoredPos, Vector2 size, out Image fillImage)
    {
        GameObject barRoot = new GameObject("ChargeRadialBar", typeof(RectTransform));
        barRoot.transform.SetParent(parent, false);
        RectTransform barRt = barRoot.GetComponent<RectTransform>();
        barRt.anchorMin = new Vector2(0.5f, 0.5f);
        barRt.anchorMax = new Vector2(0.5f, 0.5f);
        barRt.pivot = new Vector2(0.5f, 0.5f);
        barRt.anchoredPosition = anchoredPos;
        barRt.sizeDelta = size;

        Sprite ringSprite = GetRadialRingSprite();

        Image bgImg = barRoot.AddComponent<Image>();
        bgImg.sprite = ringSprite;
        bgImg.color = new Color(0.05f, 0.12f, 0.16f, 0.85f);
        bgImg.raycastTarget = false;

        GameObject fillObj = new GameObject("Fill", typeof(RectTransform));
        fillObj.transform.SetParent(barRoot.transform, false);
        RectTransform fillRt = fillObj.GetComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = Vector2.zero;
        fillRt.offsetMax = Vector2.zero;

        fillImage = fillObj.AddComponent<Image>();
        fillImage.sprite = ringSprite;
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Radial360;
        fillImage.fillOrigin = (int)Image.Origin360.Top;
        fillImage.fillClockwise = true;
        fillImage.fillAmount = 0f;
        fillImage.raycastTarget = false;

        barRoot.SetActive(false);
        return barRoot;
    }

    /// <summary>
    /// Estela de plasma alargada con pivote arriba-centro para estirar la cola hacia atrás al volar.
    /// </summary>
    public static Sprite GetPlasmaStreakSprite()
    {
        if (s_PlasmaStreakSprite == null)
        {
            const int w = 32;
            const int h = 64;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[w * h];
            float cx = w * 0.5f;

            for (int y = 0; y < h; y++)
            {
                // v = 1 arriba (punta de salida), v = 0 abajo (punta de estela)
                float v = (y + 0.5f) / h;
                float longitudinal = Mathf.Pow(v, 1.45f) * Mathf.Clamp01((1f - v) * 6.5f + 0.35f);
                float halfWidth = Mathf.Lerp(w * 0.14f, w * 0.44f, v);

                for (int x = 0; x < w; x++)
                {
                    float radial = Mathf.Clamp01(1f - Mathf.Abs((x + 0.5f) - cx) / Mathf.Max(1f, halfWidth));
                    float a = Mathf.Clamp01(longitudinal * radial * radial * 1.15f);
                    pixels[y * w + x] = new Color(1f, 1f, 1f, a);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            s_PlasmaStreakSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 1.0f));
        }
        return s_PlasmaStreakSprite;
    }

    /// <summary>
    /// Vórtice energético de 4 arcos giratorios para destacar disparos medios y cargados completos.
    /// </summary>
    public static Sprite GetEnergyVortexSprite()
    {
        if (s_EnergyVortexSprite == null)
        {
            const int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[size * size];
            float c = size * 0.5f;
            float ringR = size * 0.36f;
            float bandW = size * 0.11f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) - c;
                    float dy = (y + 0.5f) - c;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float angle = Mathf.Atan2(dy, dx);

                    float ringMask = Mathf.Clamp01(1f - Mathf.Abs(dist - ringR) / bandW);
                    float arcWave = 0.25f + 0.75f * Mathf.Pow(Mathf.Abs(Mathf.Sin(angle * 2f)), 1.6f);
                    float a = Mathf.Clamp01(ringMask * ringMask * arcWave);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            s_EnergyVortexSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }
        return s_EnergyVortexSprite;
    }

    /// <summary>
    /// Curva de animación compartida para proyectiles del jugador (InGame y Tutorial).
    /// </summary>
    public static void EvalBulletAnim(
        float age, bool isHeavy, bool isBig,
        out float scaleX, out float scaleY,
        out float trailScaleY, out float trailAlpha,
        out float coreGlowAlpha, out float spinDegPerSec)
    {
        const float launchWindow = 0.085f;
        float launchT = age < launchWindow ? (1f - age / launchWindow) : 0f;
        float snap = launchT * launchT;

        float pulseFreq = isHeavy ? 18f : 26f;
        float ampX = isHeavy ? 0.06f : 0.055f;
        float ampY = isHeavy ? 0.07f : 0.075f;

        scaleX = Mathf.Lerp(1f + ampX * Mathf.Sin(age * pulseFreq), 0.76f, snap);
        scaleY = Mathf.Lerp(1f + ampY * Mathf.Cos(age * pulseFreq), 1.28f, snap);

        float baseTrail = isBig ? 1.38f : (isHeavy ? 1.18f : 1.0f);
        trailScaleY = baseTrail + 0.28f * Mathf.Sin(age * 36f) + 0.14f * Mathf.Cos(age * 59f);
        trailAlpha = Mathf.Clamp01((isHeavy ? 0.80f : 0.68f) + 0.16f * Mathf.Sin(age * 42f));
        coreGlowAlpha = Mathf.Clamp01((isBig ? 0.62f : (isHeavy ? 0.44f : 0.28f)) + 0.12f * Mathf.Sin(age * 24f));
        spinDegPerSec = isBig ? 280f : (isHeavy ? 190f : 135f);
    }

    /// <summary>
    /// Calcula inclinación lateral (roll/tilt) para naves que se desplazan horizontalmente.
    /// </summary>
    public static void EvalShipBank(
        ref float currentBank, float inputX, float dt,
        float maxTiltDeg, float rollForeshorten,
        out float zAngle, out float scaleXMult)
    {
        float target = Mathf.Clamp(inputX, -1f, 1f);
        currentBank = Mathf.Lerp(currentBank, target, 1f - Mathf.Exp(-14f * dt));
        if (Mathf.Abs(target) < 0.01f && Mathf.Abs(currentBank) < 0.002f) currentBank = 0f;

        zAngle = -currentBank * maxTiltDeg;
        scaleXMult = 1f - (rollForeshorten * Mathf.Abs(currentBank));
    }
}
