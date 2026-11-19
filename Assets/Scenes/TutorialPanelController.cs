using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Panel completo del Modo Tutorial:
/// - Oculta temporalmente el menú principal y abre un campo de batalla táctico verde dentro del Canvas.
/// - Mantiene el simulador activo: nave del jugador con disparo normal/cargado y modo precisión,
///   más enemigos grises con la IA del Tier 1 y 30% menos cadencia de disparo.
/// - Muestra diagramas interactivos de Mouse (izquierda) y Teclado (derecha) con tooltips al pasar
///   el cursor y resaltado en verde más oscuro cuando se presiona cada tecla o botón.
/// </summary>
public class TutorialPanelController : MonoBehaviour
{
    public enum TutorialFireMode { Normal, HeavyCharged }

    private Canvas parentCanvas;
    private RectTransform panelRect;

    // Fondo y Arena de combate UI
    private RectTransform arenaViewportRect;
    private RawImage arenaGridRawImage;
    private RectTransform arenaEntitiesRoot;
    private TextMeshProUGUI telemetryRightTmp;
    private RectTransform closeButtonRect;

    // Paneles de controles (Teclado/Mouse vs Joystick/Mando)
    private GameObject mousePanelObj;
    private GameObject keyboardPanelObj;
    private GameObject joystickLeftPanelObj;
    private GameObject joystickRightPanelObj;
    private Image kbTabBorderImg;
    private Image kbTabFillImg;
    private TextMeshProUGUI kbTabTmp;
    private Image joyTabBorderImg;
    private Image joyTabFillImg;
    private TextMeshProUGUI joyTabTmp;
    private TextMeshProUGUI joystickStatusTmp;
    private bool showGamepadView = false;
    private int lastObservedPadCount = -1;

    // Tooltip
    private GameObject tooltipRoot;
    private RectTransform tooltipRect;
    private TextMeshProUGUI tooltipTitleTmp;
    private TextMeshProUGUI tooltipDescTmp;

    // Hermanos del menú principal ocultados al abrir el tutorial
    private readonly List<GameObject> hiddenMenuSiblings = new List<GameObject>();

    // Sprites base
    private Sprite playerSprite;
    private Sprite bulletSprite;
    private Sprite greyEnemySprite;
    private Sprite greyEnemyFlashSprite;
    private Sprite greyEnemyBulletSprite;
    private Sprite explosionSprite;
    private Sprite slicedBorderSprite;
    private Sprite slicedFillSprite;
    private Texture2D arenaGridTexture;
    private TMP_FontAsset sharedFont;

    // Estado del simulador
    private bool isTutorialActive = false;
    private float nextEnemySpawnTime = 0f;
    private float scrollIndicatorTimer = 0f;

    // Entidades activas de la arena UI
    private TutorialUIPlayer uiPlayer;
    private readonly List<TutorialUIGreyEnemy> activeEnemies = new List<TutorialUIGreyEnemy>();
    private readonly List<TutorialUIPlayerBullet> activePlayerBullets = new List<TutorialUIPlayerBullet>();
    private readonly List<TutorialUIEnemyBullet> activeEnemyBullets = new List<TutorialUIEnemyBullet>();

    // Colores para teclas y botones del mouse
    private static readonly Color KeyNormalFill = new Color(0.08f, 0.50f, 0.25f, 0.95f);
    private static readonly Color KeyHoverFill = new Color(0.16f, 0.72f, 0.38f, 0.98f);
    private static readonly Color KeyPressedFill = new Color(0.02f, 0.16f, 0.08f, 1.0f);

    void Awake()
    {
        parentCanvas = GetComponentInParent<Canvas>();
        panelRect = GetComponent<RectTransform>();
        if (panelRect == null) panelRect = gameObject.AddComponent<RectTransform>();
        LoadAssets();
    }

    private void LoadAssets()
    {
        if (playerSprite == null) playerSprite = Resources.Load<Sprite>("Sprites/PlayerShip");
        if (bulletSprite == null) bulletSprite = Resources.Load<Sprite>("Sprites/BulletPlayer");
        if (explosionSprite == null) explosionSprite = Resources.Load<Sprite>("Sprites/ExplosionTier1");

        if (greyEnemySprite == null || greyEnemyFlashSprite == null)
        {
            Sprite rawAlien = Resources.Load<Sprite>("Sprites/AlienTier1");
            if (greyEnemySprite == null) greyEnemySprite = CreateGreyscaleSprite(rawAlien, false);
            if (greyEnemyFlashSprite == null) greyEnemyFlashSprite = CreateGreyscaleSprite(rawAlien, true);
        }

        if (greyEnemyBulletSprite == null)
        {
            Sprite rawEnemyBullet = Resources.Load<Sprite>("Sprites/BulletAlien");
            greyEnemyBulletSprite = CreateGreyscaleSprite(rawEnemyBullet);
        }

        if (sharedFont == null)
        {
            TextMeshProUGUI anyTmp = FindAnyObjectByType<TextMeshProUGUI>();
            if (anyTmp != null) sharedFont = anyTmp.font;
        }

        if (slicedBorderSprite == null)
        {
            slicedBorderSprite = CreateSlicedBorderSprite(32, 8, 2);
        }
        if (slicedFillSprite == null)
        {
            slicedFillSprite = CreateSlicedFillSprite(32, 6);
        }
        if (arenaGridTexture == null)
        {
            arenaGridTexture = CreateTacticalGridTexture(128);
        }
    }

    public void OpenTutorial()
    {
        if (parentCanvas == null) parentCanvas = GetComponentInParent<Canvas>();
        if (panelRect == null) panelRect = GetComponent<RectTransform>();
        LoadAssets();

        if (Time.timeScale <= 0f) Time.timeScale = 1f;

        HideMenuSiblings();
        transform.SetAsLastSibling();
        gameObject.SetActive(true);
        isTutorialActive = true;

        CleanupSimulation();
        BuildTutorialUI();
        Canvas.ForceUpdateCanvases();
        SetControlSchemeView(GameManager.IsGamepadConnected());
        UpdateJoystickStatusText(true);
        SpawnPlayerShip();

        // Generar rápidamente los primeros enemigos de prueba
        nextEnemySpawnTime = Time.time + 0.35f;

        if (SoundController.Instance != null)
        {
            SoundController.Instance.PlayTutorialMusic(true);
        }
    }

    public void CloseTutorial()
    {
        isTutorialActive = false;
        if (SoundController.Instance != null)
        {
            SoundController.Instance.StopChargeSound();
            SoundController.Instance.PlayMenuMusic(true);
        }
        CleanupSimulation();
        gameObject.SetActive(false);
        RestoreMenuSiblings();
    }

    private void HideMenuSiblings()
    {
        if (transform.parent == null) return;
        for (int i = 0; i < transform.parent.childCount; i++)
        {
            GameObject sibling = transform.parent.GetChild(i).gameObject;
            if (sibling != gameObject && sibling.activeSelf)
            {
                sibling.SetActive(false);
                if (!hiddenMenuSiblings.Contains(sibling))
                {
                    hiddenMenuSiblings.Add(sibling);
                }
            }
        }
    }

    private void RestoreMenuSiblings()
    {
        for (int i = 0; i < hiddenMenuSiblings.Count; i++)
        {
            if (hiddenMenuSiblings[i] != null)
            {
                hiddenMenuSiblings[i].SetActive(true);
            }
        }
        hiddenMenuSiblings.Clear();
    }

    private void CleanupSimulation()
    {
        if (uiPlayer != null)
        {
            Destroy(uiPlayer.gameObject);
            uiPlayer = null;
        }

        for (int i = activeEnemies.Count - 1; i >= 0; i--)
        {
            if (activeEnemies[i] != null) Destroy(activeEnemies[i].gameObject);
        }
        activeEnemies.Clear();

        for (int i = activePlayerBullets.Count - 1; i >= 0; i--)
        {
            if (activePlayerBullets[i] != null) Destroy(activePlayerBullets[i].gameObject);
        }
        activePlayerBullets.Clear();

        for (int i = activeEnemyBullets.Count - 1; i >= 0; i--)
        {
            if (activeEnemyBullets[i] != null) Destroy(activeEnemyBullets[i].gameObject);
        }
        activeEnemyBullets.Clear();

        HideTooltip();
    }

    void Update()
    {
        if (!isTutorialActive) return;

        float dt = Time.deltaTime;

        // Seguimiento de desplazamiento de rueda para iluminación de tecla central
        if (Mouse.current != null && Mathf.Abs(Mouse.current.scroll.ReadValue().y) > 0.01f)
        {
            scrollIndicatorTimer = 0.22f;
        }
        else if (scrollIndicatorTimer > 0f)
        {
            scrollIndicatorTimer -= dt;
        }

        // Scroll continuo de la grilla táctica en la arena (distinto al gameplay espacial)
        UpdateArenaGridScroll(dt);

        // Generación continua de bichos grises (sin oleadas, 1 a 3 activos)
        activeEnemies.RemoveAll(e => e == null);
        if (Time.time >= nextEnemySpawnTime && activeEnemies.Count < 3)
        {
            SpawnGreyBicho();
            nextEnemySpawnTime = Time.time + UnityEngine.Random.Range(2.4f, 3.8f);
        }

        // Colisiones dentro de la arena UI
        UpdateArenaCollisions();

        // Actualizar etiqueta de modo / estado en la barra superior de la arena
        UpdateArenaTelemetry();

        // Actualizar conteo de mandos conectados y alternar vista al detectar uso de joystick/teclado
        UpdateJoystickStatusText(false);
        if (!showGamepadView && IsAnyGamepadActiveThisFrame())
        {
            SetControlSchemeView(true);
        }
        else if (showGamepadView && IsKeyboardGameplayKeyPressedThisFrame())
        {
            SetControlSchemeView(false);
        }

        // Permitir salir con Escape
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            CloseTutorial();
        }
    }

    private void UpdateArenaGridScroll(float dt)
    {
        if (arenaGridRawImage == null || arenaViewportRect == null) return;

        float w = Mathf.Max(128f, arenaViewportRect.rect.width);
        float h = Mathf.Max(128f, arenaViewportRect.rect.height);
        float tileSize = 96f;

        Rect uv = arenaGridRawImage.uvRect;
        uv.width = w / tileSize;
        uv.height = h / tileSize;
        uv.y += dt * 0.45f;
        if (uv.y > 10f) uv.y -= 10f;
        arenaGridRawImage.uvRect = uv;
    }

    private void UpdateArenaCollisions()
    {
        activePlayerBullets.RemoveAll(b => b == null);
        activeEnemyBullets.RemoveAll(b => b == null);
        activeEnemies.RemoveAll(e => e == null);

        // 1. Balas del jugador vs bichos grises
        for (int bIdx = activePlayerBullets.Count - 1; bIdx >= 0; bIdx--)
        {
            TutorialUIPlayerBullet bullet = activePlayerBullets[bIdx];
            if (bullet == null) continue;

            Vector2 bPos = bullet.Rect.anchoredPosition;
            float bRadiusX = bullet.Rect.sizeDelta.x * 0.45f;
            float bRadiusY = bullet.Rect.sizeDelta.y * 0.45f;

            for (int eIdx = activeEnemies.Count - 1; eIdx >= 0; eIdx--)
            {
                TutorialUIGreyEnemy enemy = activeEnemies[eIdx];
                if (enemy == null || enemy.IsDead) continue;

                if (bullet.HasHitEnemy(enemy)) continue;

                Vector2 ePos = enemy.Rect.anchoredPosition;
                float hitW = bRadiusX + enemy.HitRadius;
                float hitH = bRadiusY + enemy.HitRadius;

                if (Mathf.Abs(bPos.x - ePos.x) <= hitW && Mathf.Abs(bPos.y - ePos.y) <= hitH)
                {
                    bullet.MarkEnemyHit(enemy);
                    bool killed = enemy.TakeDamage(bullet.Damage);
                    if (killed)
                    {
                        SpawnExplosion(ePos);
                    }

                    if (!bullet.IsPiercing)
                    {
                        Destroy(bullet.gameObject);
                        activePlayerBullets.RemoveAt(bIdx);
                        break;
                    }
                }
            }
        }

        // 2. Balas enemigas vs nave del jugador
        if (uiPlayer != null)
        {
            Vector2 pPos = uiPlayer.Rect.anchoredPosition;
            float playerHitRadius = 15f; // Hitbox reducida fiel a PlayerController

            for (int bIdx = activeEnemyBullets.Count - 1; bIdx >= 0; bIdx--)
            {
                TutorialUIEnemyBullet eb = activeEnemyBullets[bIdx];
                if (eb == null) continue;

                Vector2 ebPos = eb.Rect.anchoredPosition;
                float sumR = playerHitRadius + eb.HitRadius;

                if ((ebPos - pPos).sqrMagnitude <= sumR * sumR)
                {
                    uiPlayer.TriggerHitFeedback();
                    if (SoundController.HasInstance)
                    {
                        SoundController.Instance.PlayPlayerHitSfx();
                    }
                    Destroy(eb.gameObject);
                    activeEnemyBullets.RemoveAt(bIdx);
                }
            }
        }
    }

    private void UpdateArenaTelemetry()
    {
        if (telemetryRightTmp == null || uiPlayer == null) return;

        string modeStr = uiPlayer.CurrentMode == TutorialFireMode.Normal
            ? "<color=#38BDF8>ARMA: RÁFAGA</color>"
            : "<color=#FBBF24>ARMA: CARGADA</color>";

        string speedStr = uiPlayer.IsSlowMode
            ? "<color=#4ADE80>PRECISIÓN</color>"
            : "<color=#94A3B8>VEL: NORMAL</color>";

        telemetryRightTmp.text = $"{modeStr}   |   {speedStr}";
    }

    // -------------------------------------------------------------
    // CONSTRUCCIÓN DE LA INTERFAZ DEL TUTORIAL
    // -------------------------------------------------------------
    private void BuildTutorialUI()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Destroy(transform.GetChild(i).gameObject);
        }
        tooltipRoot = null;

        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        // Fondo opaco oscuro táctico para aislar completamente del menú principal
        Image bgImg = GetComponent<Image>() ?? gameObject.AddComponent<Image>();
        bgImg.sprite = null;
        bgImg.color = new Color(0.012f, 0.038f, 0.028f, 1.0f);
        bgImg.raycastTarget = true;

        // 1. Encabezado superior
        BuildHeader();

        // 2. Selector de vista de controles (Teclado y Mouse / Joystick y Mando)
        BuildInputModeTabs();

        // 3. Campo de batalla central (Arena de Simulación con RectMask2D)
        BuildArenaViewport();

        // 4. Diagramas interactivos de Teclado y Mouse
        BuildMouseDiagram();
        BuildKeyboardDiagram();

        // 5. Diagrama interactivo y guía del Joystick / Mando (1P y 2P)
        BuildJoystickLeftDiagram();
        BuildJoystickRightGuide();

        // 6. Botón inferior: VOLVER AL MENÚ
        BuildCloseButton();

        // 7. Tooltip flotante por encima de todo
        BuildTooltip();
    }

    private void BuildHeader()
    {
        GameObject headerObj = new GameObject("Tutorial_Header", typeof(RectTransform));
        headerObj.transform.SetParent(transform, false);
        RectTransform headerRt = headerObj.GetComponent<RectTransform>();
        headerRt.anchorMin = new Vector2(0.5f, 1f);
        headerRt.anchorMax = new Vector2(0.5f, 1f);
        headerRt.pivot = new Vector2(0.5f, 1f);
        headerRt.anchoredPosition = new Vector2(0f, -12f);
        headerRt.sizeDelta = new Vector2(780f, 68f);

        TextMeshProUGUI headerTmp = headerObj.AddComponent<TextMeshProUGUI>();
        if (sharedFont != null) headerTmp.font = sharedFont;
        headerTmp.text = "<color=#4ADE80><b>CAMPO DE ENTRENAMIENTO</b></color>\n<size=15><color=#A7F3D0>Prueba tu nave • Cambia entre Teclado/Mouse y Joystick para ver todos los controles</color></size>";
        headerTmp.fontSize = 31;
        headerTmp.alignment = TextAlignmentOptions.Center;
        headerTmp.raycastTarget = false;
    }

    private void BuildInputModeTabs()
    {
        GameObject barObj = new GameObject("ControlModeTabs", typeof(RectTransform));
        barObj.transform.SetParent(transform, false);
        RectTransform barRt = barObj.GetComponent<RectTransform>();
        barRt.anchorMin = new Vector2(0f, 1f);
        barRt.anchorMax = new Vector2(1f, 1f);
        barRt.pivot = new Vector2(0.5f, 1f);
        barRt.offsetMin = new Vector2(256f, -114f);
        barRt.offsetMax = new Vector2(-282f, -82f);

        CreateControlModeTabButton(
            barObj.transform,
            "Tab_TecladoMouse",
            0f, 0.49f,
            "TECLADO Y MOUSE",
            () => SetControlSchemeView(false),
            out kbTabBorderImg,
            out kbTabFillImg,
            out kbTabTmp
        );

        CreateControlModeTabButton(
            barObj.transform,
            "Tab_Joystick",
            0.51f, 1f,
            "JOYSTICK / MANDO",
            () => SetControlSchemeView(true),
            out joyTabBorderImg,
            out joyTabFillImg,
            out joyTabTmp
        );
    }

    private void CreateControlModeTabButton(
        Transform parent,
        string objName,
        float anchorMinX,
        float anchorMaxX,
        string label,
        UnityEngine.Events.UnityAction onClick,
        out Image borderImg,
        out Image fillImg,
        out TextMeshProUGUI tmp)
    {
        GameObject btnObj = new GameObject(objName, typeof(RectTransform));
        btnObj.transform.SetParent(parent, false);
        RectTransform rt = btnObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(anchorMinX, 0f);
        rt.anchorMax = new Vector2(anchorMaxX, 1f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        borderImg = btnObj.AddComponent<Image>();
        borderImg.sprite = slicedBorderSprite;
        borderImg.type = Image.Type.Sliced;

        GameObject fillObj = new GameObject("Fill", typeof(RectTransform));
        fillObj.transform.SetParent(btnObj.transform, false);
        RectTransform fRt = fillObj.GetComponent<RectTransform>();
        fRt.anchorMin = Vector2.zero;
        fRt.anchorMax = Vector2.one;
        fRt.offsetMin = new Vector2(2f, 2f);
        fRt.offsetMax = new Vector2(-2f, -2f);
        fillImg = fillObj.AddComponent<Image>();
        fillImg.sprite = slicedFillSprite;
        fillImg.type = Image.Type.Sliced;
        fillImg.raycastTarget = false;

        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = borderImg;
        btn.onClick.AddListener(() =>
        {
            if (SoundController.HasInstance) SoundController.Instance.PlaySwitchModeSfx();
            onClick?.Invoke();
        });

        GameObject txtObj = new GameObject("Label", typeof(RectTransform));
        txtObj.transform.SetParent(btnObj.transform, false);
        RectTransform tRt = txtObj.GetComponent<RectTransform>();
        tRt.anchorMin = Vector2.zero;
        tRt.anchorMax = Vector2.one;
        tRt.offsetMin = new Vector2(6f, 2f);
        tRt.offsetMax = new Vector2(-6f, -2f);

        tmp = txtObj.AddComponent<TextMeshProUGUI>();
        if (sharedFont != null) tmp.font = sharedFont;
        tmp.text = label;
        tmp.fontSize = 13;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 9;
        tmp.fontSizeMax = 14;
        tmp.raycastTarget = false;
    }

    private void SetControlSchemeView(bool showGamepad)
    {
        showGamepadView = showGamepad;
        HideTooltip();

        if (mousePanelObj != null) mousePanelObj.SetActive(!showGamepad);
        if (keyboardPanelObj != null) keyboardPanelObj.SetActive(!showGamepad);
        if (joystickLeftPanelObj != null) joystickLeftPanelObj.SetActive(showGamepad);
        if (joystickRightPanelObj != null) joystickRightPanelObj.SetActive(showGamepad);

        ApplyTabVisualState(kbTabBorderImg, kbTabFillImg, kbTabTmp, !showGamepad);
        ApplyTabVisualState(joyTabBorderImg, joyTabFillImg, joyTabTmp, showGamepad);
    }

    private static void ApplyTabVisualState(Image border, Image fill, TextMeshProUGUI tmp, bool isActive)
    {
        if (border != null)
        {
            border.color = isActive ? new Color(0.30f, 0.98f, 0.62f, 1f) : new Color(0.14f, 0.45f, 0.28f, 0.75f);
        }
        if (fill != null)
        {
            fill.color = isActive ? new Color(0.08f, 0.34f, 0.19f, 0.98f) : new Color(0.02f, 0.10f, 0.06f, 0.90f);
        }
        if (tmp != null)
        {
            tmp.color = isActive ? new Color(0.85f, 1.0f, 0.92f, 1f) : new Color(0.55f, 0.80f, 0.66f, 0.85f);
        }
    }

    private void BuildArenaViewport()
    {
        // Marco exterior verde brillante de la Arena
        GameObject arenaContainer = new GameObject("ArenaContainer", typeof(RectTransform));
        arenaContainer.transform.SetParent(transform, false);
        RectTransform arenaRt = arenaContainer.GetComponent<RectTransform>();
        arenaRt.anchorMin = Vector2.zero;
        arenaRt.anchorMax = Vector2.one;
        arenaRt.offsetMin = new Vector2(256f, 84f);   // Espacio para panel izquierdo y Botón Cerrar (abajo)
        arenaRt.offsetMax = new Vector2(-282f, -120f); // Espacio para panel derecho, Header y Tabs (arriba)

        Image frameImg = arenaContainer.AddComponent<Image>();
        frameImg.sprite = slicedBorderSprite;
        frameImg.type = Image.Type.Sliced;
        frameImg.color = new Color(0.22f, 0.90f, 0.48f, 0.95f);
        frameImg.raycastTarget = false;

        // Viewport enmascarado interior
        GameObject viewportObj = new GameObject("ArenaViewport", typeof(RectTransform));
        viewportObj.transform.SetParent(arenaContainer.transform, false);
        arenaViewportRect = viewportObj.GetComponent<RectTransform>();
        arenaViewportRect.anchorMin = Vector2.zero;
        arenaViewportRect.anchorMax = Vector2.one;
        arenaViewportRect.offsetMin = new Vector2(3f, 3f);
        arenaViewportRect.offsetMax = new Vector2(-3f, -3f);

        viewportObj.AddComponent<RectMask2D>();

        // Capa 1: Fondo base oscuro diferente al gameplay espacial
        GameObject baseBg = new GameObject("TacticalBase", typeof(RectTransform));
        baseBg.transform.SetParent(viewportObj.transform, false);
        RectTransform baseRt = baseBg.GetComponent<RectTransform>();
        baseRt.anchorMin = Vector2.zero;
        baseRt.anchorMax = Vector2.one;
        baseRt.offsetMin = Vector2.zero;
        baseRt.offsetMax = Vector2.zero;
        Image baseImg = baseBg.AddComponent<Image>();
        baseImg.color = new Color(0.015f, 0.065f, 0.042f, 1f);
        baseImg.raycastTarget = false;

        // Capa 2: Grilla táctica holográfica en desplazamiento continuo
        GameObject gridObj = new GameObject("TacticalScrollingGrid", typeof(RectTransform));
        gridObj.transform.SetParent(viewportObj.transform, false);
        RectTransform gridRt = gridObj.GetComponent<RectTransform>();
        gridRt.anchorMin = Vector2.zero;
        gridRt.anchorMax = Vector2.one;
        gridRt.offsetMin = Vector2.zero;
        gridRt.offsetMax = Vector2.zero;

        arenaGridRawImage = gridObj.AddComponent<RawImage>();
        arenaGridRawImage.texture = arenaGridTexture;
        arenaGridRawImage.color = new Color(0.30f, 0.95f, 0.55f, 0.42f);
        arenaGridRawImage.raycastTarget = false;

        // Capa 3: Entidades de la arena (nave, enemigos grises, balas, explosiones)
        GameObject entitiesObj = new GameObject("ArenaEntities", typeof(RectTransform));
        entitiesObj.transform.SetParent(viewportObj.transform, false);
        arenaEntitiesRoot = entitiesObj.GetComponent<RectTransform>();
        arenaEntitiesRoot.anchorMin = new Vector2(0.5f, 0.5f);
        arenaEntitiesRoot.anchorMax = new Vector2(0.5f, 0.5f);
        arenaEntitiesRoot.pivot = new Vector2(0.5f, 0.5f);
        arenaEntitiesRoot.anchoredPosition = Vector2.zero;
        arenaEntitiesRoot.sizeDelta = Vector2.zero;

        // Telemetría superior izquierda de la arena
        GameObject telLeft = new GameObject("TelemetryLeft", typeof(RectTransform));
        telLeft.transform.SetParent(viewportObj.transform, false);
        RectTransform tlRt = telLeft.GetComponent<RectTransform>();
        tlRt.anchorMin = new Vector2(0f, 1f);
        tlRt.anchorMax = new Vector2(0.5f, 1f);
        tlRt.pivot = new Vector2(0f, 1f);
        tlRt.anchoredPosition = new Vector2(12f, -8f);
        tlRt.sizeDelta = new Vector2(300f, 24f);
        TextMeshProUGUI tlTmp = telLeft.AddComponent<TextMeshProUGUI>();
        if (sharedFont != null) tlTmp.font = sharedFont;
        tlTmp.text = "<color=#34D399><b>SIMULADOR TÁCTICO</b></color>";
        tlTmp.fontSize = 13;
        tlTmp.alignment = TextAlignmentOptions.TopLeft;
        tlTmp.raycastTarget = false;

        // Telemetría superior derecha (Arma y Modo Precisión en vivo)
        GameObject telRight = new GameObject("TelemetryRight", typeof(RectTransform));
        telRight.transform.SetParent(viewportObj.transform, false);
        RectTransform trRt = telRight.GetComponent<RectTransform>();
        trRt.anchorMin = new Vector2(0.5f, 1f);
        trRt.anchorMax = new Vector2(1f, 1f);
        trRt.pivot = new Vector2(1f, 1f);
        trRt.anchoredPosition = new Vector2(-12f, -8f);
        trRt.sizeDelta = new Vector2(320f, 24f);
        telemetryRightTmp = telRight.AddComponent<TextMeshProUGUI>();
        if (sharedFont != null) telemetryRightTmp.font = sharedFont;
        telemetryRightTmp.fontSize = 13;
        telemetryRightTmp.alignment = TextAlignmentOptions.TopRight;
        telemetryRightTmp.raycastTarget = false;
    }

    private void BuildCloseButton()
    {
        GameObject closeBtnObj = new GameObject("Btn_CerrarTutorial", typeof(RectTransform));
        closeBtnObj.transform.SetParent(transform, false);
        closeButtonRect = closeBtnObj.GetComponent<RectTransform>();
        closeButtonRect.anchorMin = new Vector2(0.5f, 0f);
        closeButtonRect.anchorMax = new Vector2(0.5f, 0f);
        closeButtonRect.pivot = new Vector2(0.5f, 0f);
        closeButtonRect.anchoredPosition = new Vector2(0f, 20f);
        closeButtonRect.sizeDelta = new Vector2(240f, 48f);

        Image borderImg = closeBtnObj.AddComponent<Image>();
        borderImg.sprite = slicedBorderSprite;
        borderImg.type = Image.Type.Sliced;
        borderImg.color = new Color(0.25f, 0.90f, 0.50f, 1f);

        GameObject fillObj = new GameObject("Fill", typeof(RectTransform));
        fillObj.transform.SetParent(closeBtnObj.transform, false);
        RectTransform fillRt = fillObj.GetComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = new Vector2(2f, 2f);
        fillRt.offsetMax = new Vector2(-2f, -2f);
        Image fillImg = fillObj.AddComponent<Image>();
        fillImg.sprite = slicedFillSprite;
        fillImg.type = Image.Type.Sliced;
        fillImg.color = new Color(0.04f, 0.22f, 0.12f, 0.95f);
        fillImg.raycastTarget = false;

        Button btn = closeBtnObj.AddComponent<Button>();
        btn.targetGraphic = borderImg;
        ColorBlock cb = btn.colors;
        cb.normalColor = new Color(0.25f, 0.90f, 0.50f, 1f);
        cb.highlightedColor = new Color(0.45f, 1f, 0.70f, 1f);
        cb.pressedColor = new Color(0.10f, 0.45f, 0.24f, 1f);
        btn.colors = cb;
        btn.onClick.AddListener(() =>
        {
            if (SoundController.HasInstance) SoundController.Instance.PlayUpgradeContinueSfx();
            CloseTutorial();
        });

        MenuButtonHover hover = closeBtnObj.AddComponent<MenuButtonHover>();
        hover.Setup(btn);

        GameObject txtObj = new GameObject("Text", typeof(RectTransform));
        txtObj.transform.SetParent(closeBtnObj.transform, false);
        RectTransform txtRt = txtObj.GetComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = Vector2.zero;
        txtRt.offsetMax = Vector2.zero;

        TextMeshProUGUI txtTmp = txtObj.AddComponent<TextMeshProUGUI>();
        if (sharedFont != null) txtTmp.font = sharedFont;
        txtTmp.text = "<color=#4ADE80><b>VOLVER AL MENÚ</b></color>";
        txtTmp.fontSize = 19;
        txtTmp.fontStyle = FontStyles.Bold;
        txtTmp.alignment = TextAlignmentOptions.Center;
        txtTmp.characterSpacing = 3f;
        txtTmp.raycastTarget = false;
    }

    private GameObject CreateSideDiagramPanel(string objName, bool alignLeft, Vector2 size, string titleText)
    {
        GameObject panel = new GameObject(objName, typeof(RectTransform));
        panel.transform.SetParent(transform, false);
        RectTransform panelRt = panel.GetComponent<RectTransform>();
        float anchorX = alignLeft ? 0f : 1f;
        panelRt.anchorMin = new Vector2(anchorX, 0.5f);
        panelRt.anchorMax = new Vector2(anchorX, 0.5f);
        panelRt.pivot = new Vector2(anchorX, 0.5f);
        panelRt.anchoredPosition = new Vector2(alignLeft ? 18f : -18f, 0f);
        panelRt.sizeDelta = size;

        Image bgImg = panel.AddComponent<Image>();
        bgImg.sprite = slicedFillSprite;
        bgImg.type = Image.Type.Sliced;
        bgImg.color = new Color(0.02f, 0.09f, 0.05f, 0.90f);
        bgImg.raycastTarget = false;

        GameObject borderObj = new GameObject("PanelBorder", typeof(RectTransform));
        borderObj.transform.SetParent(panel.transform, false);
        RectTransform brt = borderObj.GetComponent<RectTransform>();
        brt.anchorMin = Vector2.zero;
        brt.anchorMax = Vector2.one;
        brt.offsetMin = Vector2.zero;
        brt.offsetMax = Vector2.zero;
        Image bImg = borderObj.AddComponent<Image>();
        bImg.sprite = slicedBorderSprite;
        bImg.type = Image.Type.Sliced;
        bImg.color = new Color(0.20f, 0.80f, 0.45f, 0.75f);
        bImg.raycastTarget = false;

        GameObject titleObj = new GameObject("PanelTitle", typeof(RectTransform));
        titleObj.transform.SetParent(panel.transform, false);
        RectTransform tRt = titleObj.GetComponent<RectTransform>();
        tRt.anchorMin = new Vector2(0f, 1f);
        tRt.anchorMax = new Vector2(1f, 1f);
        tRt.pivot = new Vector2(0.5f, 1f);
        tRt.anchoredPosition = new Vector2(0f, -12f);
        tRt.sizeDelta = new Vector2(size.x - 16f, 52f);

        TextMeshProUGUI tTmp = titleObj.AddComponent<TextMeshProUGUI>();
        if (sharedFont != null) tTmp.font = sharedFont;
        tTmp.text = titleText;
        tTmp.fontSize = 18;
        tTmp.alignment = TextAlignmentOptions.Center;
        tTmp.raycastTarget = false;

        return panel;
    }

    // -------------------------------------------------------------
    // DIAGRAMA INTERACTIVO DEL MOUSE (Lado Izquierdo)
    // -------------------------------------------------------------
    private void BuildMouseDiagram()
    {
        mousePanelObj = CreateSideDiagramPanel(
            "MouseContainer",
            true,
            new Vector2(222f, 400f),
            "<color=#4ADE80><b>MOUSE</b></color>\n<size=12><color=#A7F3D0>Posa el cursor para más información</color></size>"
        );

        GameObject mouseBody = new GameObject("MouseGraphic", typeof(RectTransform));
        mouseBody.transform.SetParent(mousePanelObj.transform, false);
        RectTransform mbRt = mouseBody.GetComponent<RectTransform>();
        mbRt.anchorMin = new Vector2(0.5f, 0.5f);
        mbRt.anchorMax = new Vector2(0.5f, 0.5f);
        mbRt.pivot = new Vector2(0.5f, 0.5f);
        mbRt.anchoredPosition = new Vector2(0f, -24f);
        mbRt.sizeDelta = new Vector2(144f, 236f);

        Image mbImg = mouseBody.AddComponent<Image>();
        mbImg.sprite = CreateMouseBodySprite(144, 236);
        mbImg.color = Color.white;
        mbImg.raycastTarget = false;

        CreateInteractiveKey(
            mouseBody.transform,
            new Vector2(-34f, 62f),
            new Vector2(56f, 84f),
            "LMB",
            "CLIC IZQUIERDO (LMB)",
            "<b>DISPARAR ARMA ACTIVA</b>\n\n• Modo Ráfaga: Disparo continuo veloz.\n• Disparo cargado: bala con daño que escala según la cantidad de tiempo que cargues el disparo.",
            () => Mouse.current != null && Mouse.current.leftButton.isPressed
        );

        CreateInteractiveKey(
            mouseBody.transform,
            new Vector2(34f, 62f),
            new Vector2(56f, 84f),
            "RMB",
            "CLIC DERECHO (RMB)",
            "<b>CAMBIAR MODO DE DISPARO</b>\n\nAlterna al instante entre:\n• <i>Ráfaga Normal Convencional</i>\n• <i>Bala Gigante Cargada</i>",
            () => Mouse.current != null && Mouse.current.rightButton.isPressed
        );

        CreateInteractiveKey(
            mouseBody.transform,
            new Vector2(0f, 60f),
            new Vector2(18f, 42f),
            "",
            "RUEDA / SCROLL",
            "<b>CENTRO DEL RATÓN</b>\n\nReferencia central entre el botón de disparo primario y el selector de arma.",
            () => (Mouse.current != null && Mouse.current.middleButton.isPressed) || scrollIndicatorTimer > 0f
        );
    }

    // -------------------------------------------------------------
    // DIAGRAMA INTERACTIVO DEL TECLADO (Lado Derecho)
    // -------------------------------------------------------------
    private void BuildKeyboardDiagram()
    {
        keyboardPanelObj = CreateSideDiagramPanel(
            "KeyboardContainer",
            false,
            new Vector2(246f, 440f),
            "<color=#4ADE80><b>TECLADO</b></color>\n<size=12><color=#A7F3D0>Pasa el cursor o presiona</color></size>"
        );
        GameObject kbPanel = keyboardPanelObj;

        float startY = 122f;
        CreateInteractiveKey(kbPanel.transform, new Vector2(0f, startY), new Vector2(46f, 44f), "W",
            "TECLA W / FLECHA ARRIBA",
            "<b>MOVER NAVE HACIA ARRIBA</b>\n\nDesplaza tu nave espacial verticalmente hacia el frente de combate.",
            () => Keyboard.current != null && (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed));

        CreateInteractiveKey(kbPanel.transform, new Vector2(-52f, startY - 48f), new Vector2(46f, 44f), "A",
            "TECLA A / FLECHA IZQUIERDA",
            "<b>MOVER NAVE A LA IZQUIERDA</b>\n\nDesplaza tu nave horizontalmente hacia la izquierda esquivando proyectiles.",
            () => Keyboard.current != null && (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed));

        CreateInteractiveKey(kbPanel.transform, new Vector2(0f, startY - 48f), new Vector2(46f, 44f), "S",
            "TECLA S / FLECHA ABAJO",
            "<b>MOVER NAVE HACIA ABAJO</b>\n\nRetrocede a una posición segura en la retaguardia.",
            () => Keyboard.current != null && (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed));

        CreateInteractiveKey(kbPanel.transform, new Vector2(52f, startY - 48f), new Vector2(46f, 44f), "D",
            "TECLA D / FLECHA DERECHA",
            "<b>MOVER NAVE A LA DERECHA</b>\n\nDesplaza tu nave velozmente hacia el sector derecho.",
            () => Keyboard.current != null && (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed));

        float actionRowY = startY - 108f;
        CreateInteractiveKey(kbPanel.transform, new Vector2(-42f, actionRowY), new Vector2(72f, 40f), "Z",
            "TECLA Z (DISPARAR)",
            "<b>DISPARAR ARMA ACTIVA</b>\n\n• <b>Modo Normal:</b> Mantén presionada la tecla Z para disparar ráfagas automáticas continuas sin espammear.\n• <b>Modo Cargado:</b> Mantén presionado para acumular energía.",
            () => Keyboard.current != null && (Keyboard.current.zKey.isPressed || Keyboard.current.spaceKey.isPressed));

        CreateInteractiveKey(kbPanel.transform, new Vector2(42f, actionRowY), new Vector2(72f, 40f), "X",
            "TECLA X (CAMBIAR MODO)",
            "<b>CAMBIAR MODO DE DISPARO</b>\n\nAlterna rápidamente entre disparo continuo convencional y bala pesada cargada.",
            () => Keyboard.current != null && (Keyboard.current.xKey.isPressed || Keyboard.current.qKey.isPressed || Keyboard.current.eKey.isPressed));

        float shiftY = actionRowY - 50f;
        CreateInteractiveKey(kbPanel.transform, new Vector2(0f, shiftY), new Vector2(172f, 38f), "SHIFT",
            "TECLA SHIFT (PRECISIÓN)",
            "<b>MODO LENTO / PRECISIÓN</b>\n\nActiva o desactiva la reducción de velocidad para maniobras exactas entre balas.",
            () => Keyboard.current != null && (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed));

        float escY = shiftY - 48f;
        CreateInteractiveKey(kbPanel.transform, new Vector2(0f, escY), new Vector2(130f, 34f), "ESC",
            "TECLA ESCAPE",
            "<b>SALIR DEL TUTORIAL</b>\n\nCierra la simulación y regresa al Menú Principal.",
            () => Keyboard.current != null && Keyboard.current.escapeKey.isPressed);
    }

    // -------------------------------------------------------------
    // DIAGRAMA INTERACTIVO DEL JOYSTICK / MANDO (Izquierdo)
    // -------------------------------------------------------------
    public static bool AnyGamepadMatches(Func<Gamepad, bool> predicate)
    {
        if (predicate == null) return false;
        var pads = Gamepad.all;
        for (int i = 0; i < pads.Count; i++)
        {
            if (pads[i] != null && predicate(pads[i])) return true;
        }
        return false;
    }

    private static bool IsGamepadMoveHeld()
    {
        return AnyGamepadMatches(p =>
            p.leftStick.ReadValue().sqrMagnitude > 0.04f ||
            p.dpad.up.isPressed ||
            p.dpad.down.isPressed ||
            p.dpad.left.isPressed ||
            p.dpad.right.isPressed);
    }

    private static bool IsGamepadLeftShoulderHeld()
    {
        if (AnyGamepadMatches(p => p.leftShoulder.isPressed)) return true;
        try { return Gamepad.all.Count == 0 && (Input.GetKey(KeyCode.JoystickButton4) || Input.GetKey(KeyCode.Joystick1Button4) || Input.GetKey(KeyCode.Joystick2Button4)); }
        catch { return false; }
    }

    private static bool IsGamepadLeftTriggerHeld()
    {
        return AnyGamepadMatches(p => p.leftTrigger.isPressed || p.leftTrigger.ReadValue() > 0.35f);
    }

    private static bool IsGamepadFireHeld()
    {
        if (AnyGamepadMatches(p => p.buttonWest.isPressed)) return true;
        try { return Gamepad.all.Count == 0 && (Input.GetKey(KeyCode.JoystickButton2) || Input.GetKey(KeyCode.Joystick1Button2) || Input.GetKey(KeyCode.Joystick2Button2)); }
        catch { return false; }
    }

    private static bool IsGamepadStartHeld()
    {
        if (AnyGamepadMatches(p => p.startButton.isPressed)) return true;
        try
        {
            return Gamepad.all.Count == 0 &&
                   (Input.GetKey(KeyCode.JoystickButton7) || Input.GetKey(KeyCode.Joystick1Button7) || Input.GetKey(KeyCode.Joystick2Button7) ||
                    Input.GetKey(KeyCode.JoystickButton9) || Input.GetKey(KeyCode.Joystick1Button9) || Input.GetKey(KeyCode.Joystick2Button9));
        }
        catch { return false; }
    }

    private static bool IsAnyGamepadActiveThisFrame()
    {
        return AnyGamepadMatches(p =>
            p.leftStick.ReadValue().sqrMagnitude > 0.12f ||
            p.dpad.up.wasPressedThisFrame ||
            p.dpad.down.wasPressedThisFrame ||
            p.dpad.left.wasPressedThisFrame ||
            p.dpad.right.wasPressedThisFrame ||
            p.buttonWest.wasPressedThisFrame ||
            p.leftShoulder.wasPressedThisFrame ||
            p.leftTrigger.ReadValue() > 0.45f ||
            p.startButton.wasPressedThisFrame);
    }

    private static bool IsKeyboardGameplayKeyPressedThisFrame()
    {
        if (Keyboard.current == null) return false;
        var kb = Keyboard.current;
        return kb.wKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame ||
               kb.sKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame ||
               kb.upArrowKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame ||
               kb.leftArrowKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame ||
               kb.qKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame ||
               kb.leftShiftKey.wasPressedThisFrame || kb.rightShiftKey.wasPressedThisFrame ||
               kb.spaceKey.wasPressedThisFrame;
    }

    private void BuildJoystickLeftDiagram()
    {
        joystickLeftPanelObj = CreateSideDiagramPanel(
            "JoystickLeftContainer",
            true,
            new Vector2(222f, 440f),
            "<color=#4ADE80><b>JOYSTICK / MANDO</b></color>\n<size=11><color=#A7F3D0>Pasa el cursor o presiona en tu mando</color></size>"
        );

        GameObject padBody = new GameObject("GamepadGraphic", typeof(RectTransform));
        padBody.transform.SetParent(joystickLeftPanelObj.transform, false);
        RectTransform pbRt = padBody.GetComponent<RectTransform>();
        pbRt.anchorMin = new Vector2(0.5f, 0.5f);
        pbRt.anchorMax = new Vector2(0.5f, 0.5f);
        pbRt.pivot = new Vector2(0.5f, 0.5f);
        pbRt.anchoredPosition = new Vector2(0f, 36f);
        pbRt.sizeDelta = new Vector2(198f, 152f);

        Image pbImg = padBody.AddComponent<Image>();
        pbImg.sprite = CreateGamepadBodySprite(198, 152);
        pbImg.color = Color.white;
        pbImg.raycastTarget = false;

        // Gatillo y botón superior izquierdo sobre el diagrama del mando
        CreateInteractiveKey(
            padBody.transform,
            new Vector2(-56f, 90f),
            new Vector2(64f, 26f),
            "L2/LT",
            "GATILLO IZQ. (L2 / LT)",
            "<b>CAMBIAR MODO DE DISPARO</b>\n\nAlterna entre Ráfaga Continua y Bala Gigante Cargada.",
            IsGamepadLeftTriggerHeld
        );

        CreateInteractiveKey(
            padBody.transform,
            new Vector2(-56f, 58f),
            new Vector2(64f, 26f),
            "L1/LB",
            "BOTÓN SUP. IZQ. (L1 / LB)",
            "<b>MODO LENTO / PRECISIÓN</b>\n\nAlterna la reducción de velocidad (45%) para esquivar balas con exactitud.",
            IsGamepadLeftShoulderHeld
        );

        CreateInteractiveKey(
            padBody.transform,
            new Vector2(26f, 58f),
            new Vector2(62f, 24f),
            "START",
            "BOTÓN START",
            "<b>UNIR AL JUGADOR 2</b>\n\nPresiónalo durante la Oleada 1 para que el Jugador 2 entre al combate.",
            IsGamepadStartHeld
        );

        CreateInteractiveKey(
            padBody.transform,
            new Vector2(-50f, -2f),
            new Vector2(68f, 52f),
            "STICK/+",
            "STICK IZQ. / D-PAD",
            "<b>MOVER NAVE</b>\n\nUsa el stick analógico izquierdo o la cruceta (D-Pad) para pilotar tu nave.",
            IsGamepadMoveHeld
        );

        CreateInteractiveKey(
            padBody.transform,
            new Vector2(52f, -2f),
            new Vector2(66f, 52f),
            "CUAD/X",
            "CUADRADO (PS) / X (XBOX)",
            "<b>DISPARAR / CARGAR ARMA</b>\n\n• Modo Ráfaga: Mantén para disparo veloz.\n• Modo Cargado: Mantén 2.2s para cargar la bala gigante.",
            IsGamepadFireHeld
        );

        // Caja inferior con estado en vivo de mandos detectados
        GameObject statusBox = new GameObject("GamepadStatusBox", typeof(RectTransform));
        statusBox.transform.SetParent(joystickLeftPanelObj.transform, false);
        RectTransform sbRt = statusBox.GetComponent<RectTransform>();
        sbRt.anchorMin = new Vector2(0.5f, 0f);
        sbRt.anchorMax = new Vector2(0.5f, 0f);
        sbRt.pivot = new Vector2(0.5f, 0f);
        sbRt.anchoredPosition = new Vector2(0f, 16f);
        sbRt.sizeDelta = new Vector2(198f, 126f);

        Image sbFill = statusBox.AddComponent<Image>();
        sbFill.sprite = slicedFillSprite;
        sbFill.type = Image.Type.Sliced;
        sbFill.color = new Color(0.03f, 0.14f, 0.08f, 0.92f);
        sbFill.raycastTarget = false;

        GameObject sbBorder = new GameObject("Border", typeof(RectTransform));
        sbBorder.transform.SetParent(statusBox.transform, false);
        RectTransform sbBrt = sbBorder.GetComponent<RectTransform>();
        sbBrt.anchorMin = Vector2.zero;
        sbBrt.anchorMax = Vector2.one;
        sbBrt.offsetMin = Vector2.zero;
        sbBrt.offsetMax = Vector2.zero;
        Image sbBImg = sbBorder.AddComponent<Image>();
        sbBImg.sprite = slicedBorderSprite;
        sbBImg.type = Image.Type.Sliced;
        sbBImg.color = new Color(0.25f, 0.85f, 0.55f, 0.65f);
        sbBImg.raycastTarget = false;

        GameObject stTxtObj = new GameObject("StatusText", typeof(RectTransform));
        stTxtObj.transform.SetParent(statusBox.transform, false);
        RectTransform stRt = stTxtObj.GetComponent<RectTransform>();
        stRt.anchorMin = Vector2.zero;
        stRt.anchorMax = Vector2.one;
        stRt.offsetMin = new Vector2(10f, 8f);
        stRt.offsetMax = new Vector2(-10f, -8f);

        joystickStatusTmp = stTxtObj.AddComponent<TextMeshProUGUI>();
        if (sharedFont != null) joystickStatusTmp.font = sharedFont;
        joystickStatusTmp.fontSize = 12;
        joystickStatusTmp.alignment = TextAlignmentOptions.Center;
        joystickStatusTmp.raycastTarget = false;
    }

    private void BuildJoystickRightGuide()
    {
        joystickRightPanelObj = CreateSideDiagramPanel(
            "JoystickRightContainer",
            false,
            new Vector2(246f, 440f),
            "<color=#4ADE80><b>GUÍA DE MANDO</b></color>\n<size=11><color=#A7F3D0>Compatible PS / Xbox / USB</color></size>"
        );

        CreateGamepadGuideRow(joystickRightPanelObj.transform, 118f, "STICK/+",
            "MOVER NAVE", "Stick Izq. o Cruceta (D-Pad)",
            "STICK IZQ. / D-PAD",
            "<b>MOVER NAVE</b>\n\nControl total en 8 direcciones o movimiento analógico suave.",
            IsGamepadMoveHeld);

        CreateGamepadGuideRow(joystickRightPanelObj.transform, 64f, "CUAD/X",
            "DISPARAR / CARGAR", "Cuadrado (PS) o X (Xbox)",
            "CUADRADO (PS) / X (XBOX)",
            "<b>DISPARAR PROYECTIL</b>\n\nDispara ráfagas continuas o mantén presionado en modo pesado para cargar.",
            IsGamepadFireHeld);

        CreateGamepadGuideRow(joystickRightPanelObj.transform, 10f, "L2/LT",
            "CAMBIAR ARMA", "Gatillo Izquierdo (L2 / LT)",
            "GATILLO IZQ. (L2 / LT)",
            "<b>CAMBIAR MODO DE DISPARO</b>\n\nAlterna entre Ráfaga Normal y Bala Gigante Cargada.",
            IsGamepadLeftTriggerHeld);

        CreateGamepadGuideRow(joystickRightPanelObj.transform, -44f, "L1/LB",
            "MODO PRECISIÓN", "Botón Superior (L1 / LB)",
            "BOTÓN SUP. IZQ. (L1 / LB)",
            "<b>MODO LENTO / PRECISIÓN</b>\n\nReduce un 45% la velocidad para maniobrar entre lluvias de balas.",
            IsGamepadLeftShoulderHeld);

        CreateGamepadGuideRow(joystickRightPanelObj.transform, -98f, "START",
            "UNIR JUGADOR 2", "Botón Start (Oleada 1)",
            "BOTÓN START",
            "<b>UNIR JUGADOR 2</b>\n\nPresiona Start durante la Oleada 1 para activar el modo cooperativo 2P.",
            IsGamepadStartHeld);

        // Tarjeta destacada sobre detección de 1 vs 2 mandos simultáneos
        GameObject dualBox = new GameObject("DualControllerBox", typeof(RectTransform));
        dualBox.transform.SetParent(joystickRightPanelObj.transform, false);
        RectTransform dRt = dualBox.GetComponent<RectTransform>();
        dRt.anchorMin = new Vector2(0.5f, 0f);
        dRt.anchorMax = new Vector2(0.5f, 0f);
        dRt.pivot = new Vector2(0.5f, 0f);
        dRt.anchoredPosition = new Vector2(0f, 12f);
        dRt.sizeDelta = new Vector2(220f, 78f);

        Image dFill = dualBox.AddComponent<Image>();
        dFill.sprite = slicedFillSprite;
        dFill.type = Image.Type.Sliced;
        dFill.color = new Color(0.03f, 0.15f, 0.12f, 0.94f);
        dFill.raycastTarget = false;

        GameObject dBorder = new GameObject("Border", typeof(RectTransform));
        dBorder.transform.SetParent(dualBox.transform, false);
        RectTransform dbRt = dBorder.GetComponent<RectTransform>();
        dbRt.anchorMin = Vector2.zero;
        dbRt.anchorMax = Vector2.one;
        dbRt.offsetMin = Vector2.zero;
        dbRt.offsetMax = Vector2.zero;
        Image dBImg = dBorder.AddComponent<Image>();
        dBImg.sprite = slicedBorderSprite;
        dBImg.type = Image.Type.Sliced;
        dBImg.color = new Color(0.22f, 0.78f, 0.96f, 0.75f);
        dBImg.raycastTarget = false;

        GameObject dTxtObj = new GameObject("Text", typeof(RectTransform));
        dTxtObj.transform.SetParent(dualBox.transform, false);
        RectTransform dtRt = dTxtObj.GetComponent<RectTransform>();
        dtRt.anchorMin = Vector2.zero;
        dtRt.anchorMax = Vector2.one;
        dtRt.offsetMin = new Vector2(8f, 6f);
        dtRt.offsetMax = new Vector2(-8f, -6f);

        TextMeshProUGUI dTmp = dTxtObj.AddComponent<TextMeshProUGUI>();
        if (sharedFont != null) dTmp.font = sharedFont;
        dTmp.text = "<color=#38BDF8><b>MODO 2 MANDOS</b></color>\n<size=11><color=#D1FAE5>• <b>1 mando:</b> 1P Teclado/Mouse, 2P Mando 1.\n• <b>2 mandos:</b> 1P usa <b>Mando 1</b> y 2P usa <b>Mando 2</b>.</color></size>";
        dTmp.fontSize = 12;
        dTmp.alignment = TextAlignmentOptions.MidlineLeft;
        dTmp.raycastTarget = false;
    }

    private void CreateGamepadGuideRow(
        Transform parent,
        float posY,
        string btnLabel,
        string actionTitle,
        string actionSubtitle,
        string tooltipName,
        string tooltipDesc,
        Func<bool> isPressedCheck)
    {
        CreateInteractiveKey(parent, new Vector2(-72f, posY), new Vector2(68f, 40f), btnLabel, tooltipName, tooltipDesc, isPressedCheck);

        GameObject txtObj = new GameObject("GuideText_" + btnLabel, typeof(RectTransform));
        txtObj.transform.SetParent(parent, false);
        RectTransform rt = txtObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(-32f, posY);
        rt.sizeDelta = new Vector2(144f, 44f);

        TextMeshProUGUI tmp = txtObj.AddComponent<TextMeshProUGUI>();
        if (sharedFont != null) tmp.font = sharedFont;
        tmp.text = $"<color=#4ADE80><b>{actionTitle}</b></color>\n<size=10><color=#A7F3D0>{actionSubtitle}</color></size>";
        tmp.fontSize = 12;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.raycastTarget = false;
    }

    private void UpdateJoystickStatusText(bool force)
    {
        int count = GameManager.GetConnectedGamepadCount();
        if (!force && count == lastObservedPadCount) return;
        lastObservedPadCount = count;

        if (joyTabTmp != null)
        {
            joyTabTmp.text = count > 0 ? $"JOYSTICK / MANDO ({count})" : "JOYSTICK / MANDO";
        }

        if (joystickStatusTmp == null) return;
        if (count >= 2)
        {
            joystickStatusTmp.text = $"<color=#38BDF8><b>{count} MANDOS DETECTADOS</b></color>\n<size=11><color=#D1FAE5><b>1P:</b> Mando 1 (listo)\n<b>2P:</b> Mando 2 (Start en O1)\n¡Pruébalos en esta arena!</color></size>";
        }
        else if (count == 1)
        {
            joystickStatusTmp.text = "<color=#4ADE80><b>1 MANDO DETECTADO</b></color>\n<size=11><color=#D1FAE5><b>1P:</b> Teclado y Mouse\n<b>2P:</b> Mando 1 (Start en O1)\n¡Pruébalo en esta arena!</color></size>";
        }
        else
        {
            joystickStatusTmp.text = "<color=#94A3B8><b>SIN MANDOS DETECTADOS</b></color>\n<size=11><color=#A7F3D0>Conecta 1 o 2 mandos para ver los botones iluminarse aquí y pilotar la nave.</color></size>";
        }
    }

    private void CreateInteractiveKey(
        Transform parent,
        Vector2 pos,
        Vector2 size,
        string label,
        string keyName,
        string description,
        Func<bool> isPressedCheck)
    {
        GameObject keyObj = new GameObject("Key_" + keyName, typeof(RectTransform));
        keyObj.transform.SetParent(parent, false);
        RectTransform rt = keyObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        // Marco exterior verde para que la tecla siempre conserve contorno nítido incluso al oscurecerse
        Image borderImg = keyObj.AddComponent<Image>();
        borderImg.sprite = slicedBorderSprite;
        borderImg.type = Image.Type.Sliced;
        borderImg.raycastTarget = true;

        // Relleno interior que cambia a verde brillante (hover) o verde oscuro profundo (presionado)
        GameObject fillObj = new GameObject("Fill", typeof(RectTransform));
        fillObj.transform.SetParent(keyObj.transform, false);
        RectTransform fillRt = fillObj.GetComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = new Vector2(2f, 2f);
        fillRt.offsetMax = new Vector2(-2f, -2f);

        Image fillImg = fillObj.AddComponent<Image>();
        fillImg.sprite = slicedFillSprite;
        fillImg.type = Image.Type.Sliced;
        fillImg.raycastTarget = false;

        // Etiqueta de la tecla
        GameObject txtObj = new GameObject("Label", typeof(RectTransform));
        txtObj.transform.SetParent(keyObj.transform, false);
        RectTransform tRt = txtObj.GetComponent<RectTransform>();
        tRt.anchorMin = Vector2.zero;
        tRt.anchorMax = Vector2.one;
        tRt.offsetMin = Vector2.zero;
        tRt.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = txtObj.AddComponent<TextMeshProUGUI>();
        if (sharedFont != null) tmp.font = sharedFont;
        tmp.text = label;
        tmp.fontSize = label.Length > 3 ? 12 : (label.Length == 3 ? 12 : 17);
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;

        TutorialKeyHover hover = keyObj.AddComponent<TutorialKeyHover>();
        hover.Setup(
            this,
            keyName,
            description,
            fillImg,
            borderImg,
            tmp,
            KeyNormalFill,
            KeyHoverFill,
            KeyPressedFill,
            isPressedCheck
        );
    }

    // -------------------------------------------------------------
    // TOOLTIP FLOTANTE EMERGENTE
    // -------------------------------------------------------------
    private void BuildTooltip()
    {
        if (tooltipRoot != null) return;

        tooltipRoot = new GameObject("Tutorial_Tooltip", typeof(RectTransform));
        tooltipRoot.transform.SetParent(transform, false);
        tooltipRect = tooltipRoot.GetComponent<RectTransform>();
        tooltipRect.anchorMin = new Vector2(0.5f, 0.5f);
        tooltipRect.anchorMax = new Vector2(0.5f, 0.5f);
        tooltipRect.pivot = new Vector2(0.5f, 0f);
        tooltipRect.sizeDelta = new Vector2(300f, 140f);

        Image ttFill = tooltipRoot.AddComponent<Image>();
        ttFill.sprite = slicedFillSprite;
        ttFill.type = Image.Type.Sliced;
        ttFill.color = new Color(0.02f, 0.08f, 0.045f, 0.97f);
        ttFill.raycastTarget = false;

        GameObject ttBorderObj = new GameObject("TooltipBorder", typeof(RectTransform));
        ttBorderObj.transform.SetParent(tooltipRoot.transform, false);
        RectTransform ttBrt = ttBorderObj.GetComponent<RectTransform>();
        ttBrt.anchorMin = Vector2.zero;
        ttBrt.anchorMax = Vector2.one;
        ttBrt.offsetMin = Vector2.zero;
        ttBrt.offsetMax = Vector2.zero;
        Image ttBorder = ttBorderObj.AddComponent<Image>();
        ttBorder.sprite = slicedBorderSprite;
        ttBorder.type = Image.Type.Sliced;
        ttBorder.color = new Color(0.30f, 1f, 0.60f, 0.95f);
        ttBorder.raycastTarget = false;

        GameObject titleObj = new GameObject("TooltipTitle", typeof(RectTransform));
        titleObj.transform.SetParent(tooltipRoot.transform, false);
        RectTransform tRt = titleObj.GetComponent<RectTransform>();
        tRt.anchorMin = new Vector2(0f, 1f);
        tRt.anchorMax = new Vector2(1f, 1f);
        tRt.pivot = new Vector2(0.5f, 1f);
        tRt.anchoredPosition = new Vector2(0f, -8f);
        tRt.sizeDelta = new Vector2(280f, 26f);

        tooltipTitleTmp = titleObj.AddComponent<TextMeshProUGUI>();
        if (sharedFont != null) tooltipTitleTmp.font = sharedFont;
        tooltipTitleTmp.fontSize = 15;
        tooltipTitleTmp.fontStyle = FontStyles.Bold;
        tooltipTitleTmp.color = new Color(0.3f, 1f, 0.6f, 1f);
        tooltipTitleTmp.alignment = TextAlignmentOptions.Center;
        tooltipTitleTmp.raycastTarget = false;

        GameObject descObj = new GameObject("TooltipDesc", typeof(RectTransform));
        descObj.transform.SetParent(tooltipRoot.transform, false);
        RectTransform dRt = descObj.GetComponent<RectTransform>();
        dRt.anchorMin = Vector2.zero;
        dRt.anchorMax = new Vector2(1f, 1f);
        dRt.offsetMin = new Vector2(14f, 10f);
        dRt.offsetMax = new Vector2(-14f, -36f);

        tooltipDescTmp = descObj.AddComponent<TextMeshProUGUI>();
        if (sharedFont != null) tooltipDescTmp.font = sharedFont;
        tooltipDescTmp.fontSize = 13;
        tooltipDescTmp.color = new Color(0.92f, 0.97f, 1f, 1f);
        tooltipDescTmp.alignment = TextAlignmentOptions.TopLeft;
        tooltipDescTmp.lineSpacing = -6f;
        tooltipDescTmp.raycastTarget = false;

        tooltipRoot.SetActive(false);
    }

    public void ShowTooltip(string keyName, string description, Vector3 worldPosition)
    {
        if (tooltipRoot == null || panelRect == null) return;

        tooltipTitleTmp.text = keyName;
        tooltipDescTmp.text = description;

        tooltipRoot.transform.position = worldPosition;
        Vector2 pos = tooltipRect.anchoredPosition;
        pos.y += 32f;

        float halfW = panelRect.rect.width * 0.5f;
        float halfH = panelRect.rect.height * 0.5f;
        if (halfW > 160f)
        {
            pos.x = Mathf.Clamp(pos.x, -halfW + 160f, halfW - 160f);
        }
        if (pos.y + 145f > halfH)
        {
            pos.y = tooltipRect.anchoredPosition.y - 175f;
        }

        tooltipRect.anchoredPosition = pos;
        tooltipRoot.SetActive(true);
        tooltipRoot.transform.SetAsLastSibling();
    }

    public void HideTooltip()
    {
        if (tooltipRoot != null)
        {
            tooltipRoot.SetActive(false);
        }
    }

    // -------------------------------------------------------------
    // SPAWN DE ENTIDADES DENTRO DE LA ARENA UI
    // -------------------------------------------------------------
    public float GetArenaHalfWidth()
    {
        return arenaViewportRect != null ? Mathf.Max(140f, arenaViewportRect.rect.width * 0.5f) : 340f;
    }

    public float GetArenaHalfHeight()
    {
        return arenaViewportRect != null ? Mathf.Max(160f, arenaViewportRect.rect.height * 0.5f) : 240f;
    }

    public bool IsPointerOverCloseButton()
    {
        if (closeButtonRect == null || Mouse.current == null) return false;
        Vector2 screenPos = Mouse.current.position.ReadValue();
        Camera cam = (parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay) ? parentCanvas.worldCamera : null;
        return RectTransformUtility.RectangleContainsScreenPoint(closeButtonRect, screenPos, cam);
    }

    private void SpawnPlayerShip()
    {
        if (arenaEntitiesRoot == null) return;
        if (uiPlayer != null) Destroy(uiPlayer.gameObject);

        GameObject shipObj = new GameObject("TutorialUIPlayerShip", typeof(RectTransform));
        shipObj.transform.SetParent(arenaEntitiesRoot, false);

        RectTransform rt = shipObj.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(58f, 58f);
        float startY = -GetArenaHalfHeight() + 62f;
        rt.anchoredPosition = new Vector2(0f, startY);

        Image img = shipObj.AddComponent<Image>();
        img.sprite = playerSprite;
        img.preserveAspect = true;
        img.color = new Color(0.35f, 0.85f, 1.0f, 1f);
        img.raycastTarget = false;

        // Aura visual de carga delante de la nave para modo Bala Cargada
        GameObject chargeAuraObj = new GameObject("ChargeAura", typeof(RectTransform));
        chargeAuraObj.transform.SetParent(shipObj.transform, false);
        RectTransform auraRt = chargeAuraObj.GetComponent<RectTransform>();
        auraRt.anchoredPosition = new Vector2(0f, 32f);
        auraRt.sizeDelta = new Vector2(12f, 12f);
        Image auraImg = chargeAuraObj.AddComponent<Image>();
        auraImg.sprite = ChargeVisualUtils.GetGlowCircleSprite();
        auraImg.color = new Color(0.30f, 0.95f, 1f, 0f);
        auraImg.raycastTarget = false;

        // Barra radial circular arriba a la derecha de la nave (sigue al jugador)
        GameObject radialBarRoot = ChargeVisualUtils.CreateRadialChargeBar(
            shipObj.transform,
            new Vector2(36f, 34f),
            new Vector2(28f, 28f),
            out Image radialFillImg
        );

        // Indicador central de precisión (visible cuando Shift / Modo Lento está activo)
        GameObject focusObj = new GameObject("PrecisionHitboxDot", typeof(RectTransform));
        focusObj.transform.SetParent(shipObj.transform, false);
        RectTransform focusRt = focusObj.GetComponent<RectTransform>();
        focusRt.anchoredPosition = Vector2.zero;
        focusRt.sizeDelta = new Vector2(10f, 10f);
        Image focusImg = focusObj.AddComponent<Image>();
        focusImg.sprite = CreateCircleSprite(16);
        focusImg.color = new Color(0.30f, 1f, 0.65f, 0.95f);
        focusImg.raycastTarget = false;
        focusObj.SetActive(false);

        uiPlayer = shipObj.AddComponent<TutorialUIPlayer>();
        uiPlayer.Setup(this, img, auraRt, auraImg, radialBarRoot, radialFillImg, focusObj);
    }

    private void SpawnGreyBicho()
    {
        if (arenaEntitiesRoot == null) return;

        float halfW = GetArenaHalfWidth();
        float halfH = GetArenaHalfHeight();

        GameObject bichoObj = new GameObject("GreyBichoTier1", typeof(RectTransform));
        bichoObj.transform.SetParent(arenaEntitiesRoot, false);

        RectTransform rt = bichoObj.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(62f, 62f);
        float spawnX = UnityEngine.Random.Range(-halfW + 45f, halfW - 45f);
        rt.anchoredPosition = new Vector2(spawnX, halfH + 45f);

        Image img = bichoObj.AddComponent<Image>();
        img.sprite = greyEnemySprite;
        img.preserveAspect = true;
        img.color = new Color(0.88f, 0.90f, 0.94f, 1f);
        img.raycastTarget = false;

        GameObject flashObj = new GameObject("HitFlashOverlay", typeof(RectTransform));
        flashObj.transform.SetParent(bichoObj.transform, false);
        RectTransform flashRt = flashObj.GetComponent<RectTransform>();
        flashRt.anchorMin = Vector2.zero;
        flashRt.anchorMax = Vector2.one;
        flashRt.offsetMin = Vector2.zero;
        flashRt.offsetMax = Vector2.zero;
        Image flashImg = flashObj.AddComponent<Image>();
        flashImg.sprite = greyEnemyFlashSprite != null ? greyEnemyFlashSprite : greyEnemySprite;
        flashImg.preserveAspect = true;
        flashImg.color = new Color(1f, 1f, 1f, 0f);
        flashImg.raycastTarget = false;

        TutorialUIGreyEnemy enemy = bichoObj.AddComponent<TutorialUIGreyEnemy>();
        enemy.Setup(this, img, flashImg);
        activeEnemies.Add(enemy);
    }

    public void SpawnPlayerBullet(Vector2 pos, Vector2 size, float speed, int damage, bool piercing, Color tint)
    {
        if (arenaEntitiesRoot == null) return;

        GameObject bulletObj = new GameObject("TutorialUIPlayerBullet", typeof(RectTransform));
        bulletObj.transform.SetParent(arenaEntitiesRoot, false);
        RectTransform rt = bulletObj.GetComponent<RectTransform>();
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;

        Image img = bulletObj.AddComponent<Image>();
        img.sprite = bulletSprite;
        img.preserveAspect = false;
        img.color = tint;
        img.raycastTarget = false;

        bool isHeavy = piercing || size.x >= 36f || damage >= 2;
        Color wakeTint = piercing
            ? new Color(1f, 0.90f, 0.34f, 1f)
            : (isHeavy ? new Color(0.35f, 0.96f, 1f, 1f) : new Color(0.40f, 0.90f, 1f, 1f));

        GameObject wakeObj = new GameObject("PlasmaWake", typeof(RectTransform));
        wakeObj.transform.SetParent(bulletObj.transform, false);
        RectTransform wakeRt = wakeObj.GetComponent<RectTransform>();
        wakeRt.anchorMin = new Vector2(0.5f, 0f);
        wakeRt.anchorMax = new Vector2(0.5f, 0f);
        wakeRt.pivot = new Vector2(0.5f, 1f);
        wakeRt.anchoredPosition = new Vector2(0f, size.y * 0.14f);
        wakeRt.sizeDelta = new Vector2(size.x * 0.72f, size.y * 0.85f);
        Image wakeImg = wakeObj.AddComponent<Image>();
        wakeImg.sprite = ChargeVisualUtils.GetPlasmaStreakSprite();
        wakeImg.color = wakeTint;
        wakeImg.raycastTarget = false;

        GameObject glowObj = new GameObject("CoreGlow", typeof(RectTransform));
        glowObj.transform.SetParent(bulletObj.transform, false);
        RectTransform glowRt = glowObj.GetComponent<RectTransform>();
        glowRt.anchorMin = new Vector2(0.5f, 0.5f);
        glowRt.anchorMax = new Vector2(0.5f, 0.5f);
        glowRt.pivot = new Vector2(0.5f, 0.5f);
        glowRt.anchoredPosition = Vector2.zero;
        glowRt.sizeDelta = new Vector2(size.x * 1.45f, size.y * 1.15f);
        Image glowImg = glowObj.AddComponent<Image>();
        glowImg.sprite = ChargeVisualUtils.GetGlowCircleSprite();
        glowImg.color = wakeTint;
        glowImg.raycastTarget = false;

        RectTransform vortexRt = null;
        if (isHeavy)
        {
            GameObject vortexObj = new GameObject("EnergyVortex", typeof(RectTransform));
            vortexObj.transform.SetParent(bulletObj.transform, false);
            vortexRt = vortexObj.GetComponent<RectTransform>();
            vortexRt.anchorMin = new Vector2(0.5f, 0.5f);
            vortexRt.anchorMax = new Vector2(0.5f, 0.5f);
            vortexRt.pivot = new Vector2(0.5f, 0.5f);
            vortexRt.anchoredPosition = Vector2.zero;
            float ringDim = Mathf.Max(size.x * 1.45f, size.y * 0.90f);
            vortexRt.sizeDelta = new Vector2(ringDim, ringDim);
            Image vortexImg = vortexObj.AddComponent<Image>();
            vortexImg.sprite = ChargeVisualUtils.GetEnergyVortexSprite();
            vortexImg.color = piercing ? new Color(0.35f, 0.96f, 1f, 0.85f) : new Color(0.55f, 0.98f, 1f, 0.72f);
            vortexImg.raycastTarget = false;
        }

        TutorialUIPlayerBullet bullet = bulletObj.AddComponent<TutorialUIPlayerBullet>();
        bullet.Setup(this, speed, damage, piercing, isHeavy, img, tint, wakeRt, wakeImg, glowRt, glowImg, vortexRt, wakeTint);
        activePlayerBullets.Add(bullet);
    }

    public void SpawnEnemyBullet(Vector2 pos, Vector2 dir, float speed)
    {
        if (arenaEntitiesRoot == null) return;

        GameObject bulletObj = new GameObject("GreyBichoBullet", typeof(RectTransform));
        bulletObj.transform.SetParent(arenaEntitiesRoot, false);
        RectTransform rt = bulletObj.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(20f, 20f);
        rt.anchoredPosition = pos;

        Image img = bulletObj.AddComponent<Image>();
        img.sprite = greyEnemyBulletSprite;
        img.preserveAspect = true;
        img.color = new Color(0.92f, 0.94f, 0.98f, 1f);
        img.raycastTarget = false;

        TutorialUIEnemyBullet bullet = bulletObj.AddComponent<TutorialUIEnemyBullet>();
        bullet.Setup(this, dir, speed);
        activeEnemyBullets.Add(bullet);
    }

    public Vector2? GetPlayerPosition()
    {
        return uiPlayer != null ? (Vector2?)uiPlayer.Rect.anchoredPosition : null;
    }

    private void SpawnExplosion(Vector2 pos)
    {
        if (arenaEntitiesRoot == null) return;

        GameObject expObj = new GameObject("TutorialExplosion", typeof(RectTransform));
        expObj.transform.SetParent(arenaEntitiesRoot, false);
        RectTransform rt = expObj.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(68f, 68f);
        rt.anchoredPosition = pos;

        Image img = expObj.AddComponent<Image>();
        img.sprite = explosionSprite != null ? explosionSprite : CreateCircleSprite(32);
        img.preserveAspect = true;
        img.color = new Color(0.85f, 0.92f, 0.95f, 0.95f);
        img.raycastTarget = false;

        expObj.AddComponent<TutorialUIExplosion>();
    }

    // -------------------------------------------------------------
    // GENERACIÓN DE SPRITES / TEXTURAS PROCEDURALES
    // -------------------------------------------------------------
    private static Sprite CreateSlicedBorderSprite(int size, int r, int borderWidth)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool outside = IsOutsideRoundedRect(x, y, size, size, r);
                if (outside)
                {
                    pixels[y * size + x] = Color.clear;
                }
                else
                {
                    bool insideInner = !IsOutsideRoundedRect(x - borderWidth, y - borderWidth, size - borderWidth * 2, size - borderWidth * 2, Mathf.Max(1, r - borderWidth))
                                       && x >= borderWidth && x < size - borderWidth && y >= borderWidth && y < size - borderWidth;
                    pixels[y * size + x] = insideInner ? Color.clear : Color.white;
                }
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        Vector4 border = new Vector4(r + 2, r + 2, r + 2, r + 2);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
    }

    private static Sprite CreateSlicedFillSprite(int size, int r)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                pixels[y * size + x] = IsOutsideRoundedRect(x, y, size, size, r) ? Color.clear : Color.white;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        Vector4 border = new Vector4(r + 2, r + 2, r + 2, r + 2);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
    }

    private static bool IsOutsideRoundedRect(int x, int y, int w, int h, int r)
    {
        if (x < 0 || x >= w || y < 0 || y >= h) return true;
        return (x < r && y < r && (x - r) * (x - r) + (y - r) * (y - r) > r * r) ||
               (x >= w - r && y < r && (x - (w - r - 1)) * (x - (w - r - 1)) + (y - r) * (y - r) > r * r) ||
               (x < r && y >= h - r && (x - r) * (x - r) + (y - (h - r - 1)) * (y - (h - r - 1)) > r * r) ||
               (x >= w - r && y >= h - r && (x - (w - r - 1)) * (x - (w - r - 1)) + (y - (h - r - 1)) * (y - (h - r - 1)) > r * r);
    }

    private static Sprite CreateMouseBodySprite(int w, int h)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[w * h];
        float cx = w * 0.5f;
        float cy = h * 0.5f;
        float rx = w * 0.46f;
        float ry = h * 0.47f;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float dx = (x - cx) / rx;
                float dy = (y - cy) / ry;
                float distSq = dx * dx + dy * dy;

                if (distSq <= 1f)
                {
                    bool isBorder = distSq > 0.90f;
                    pixels[y * w + x] = isBorder
                        ? new Color(0.25f, 0.90f, 0.52f, 1f)
                        : new Color(0.05f, 0.16f, 0.10f, 0.95f);
                }
                else
                {
                    pixels[y * w + x] = Color.clear;
                }
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
    }

    private static Sprite CreateGamepadBodySprite(int w, int h)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[w * h];
        Color borderCol = new Color(0.25f, 0.90f, 0.52f, 1f);
        Color fillCol = new Color(0.05f, 0.16f, 0.10f, 0.95f);

        // Cuerpo principal + dos empuñaduras redondeadas inferiores evaluados mediante distancia mínima
        for (int y = 0; y < h; y++)
        {
            float ny = (y / (float)h) * 2f - 1f; // -1 abajo .. +1 arriba
            for (int x = 0; x < w; x++)
            {
                float nx = (x / (float)w) * 2f - 1f; // -1 izq .. +1 der

                // Puente central superior del mando
                float bridgeDist = Mathf.Max(Mathf.Abs(nx) / 0.52f, Mathf.Abs(ny - 0.12f) / 0.42f);
                // Domos laterales (izquierdo y derecho)
                float domeL = Mathf.Sqrt(Mathf.Pow((nx + 0.46f) / 0.46f, 2f) + Mathf.Pow((ny - 0.08f) / 0.52f, 2f));
                float domeR = Mathf.Sqrt(Mathf.Pow((nx - 0.46f) / 0.46f, 2f) + Mathf.Pow((ny - 0.08f) / 0.52f, 2f));
                // Empuñaduras inferiores inclinadas ligeramente hacia afuera
                float gripL = Mathf.Sqrt(Mathf.Pow((nx + 0.58f + Mathf.Max(0f, -ny) * 0.10f) / 0.30f, 2f) + Mathf.Pow((ny + 0.38f) / 0.54f, 2f));
                float gripR = Mathf.Sqrt(Mathf.Pow((nx - 0.58f - Mathf.Max(0f, -ny) * 0.10f) / 0.30f, 2f) + Mathf.Pow((ny + 0.38f) / 0.54f, 2f));

                float minField = Mathf.Min(bridgeDist, Mathf.Min(Mathf.Min(domeL, domeR), Mathf.Min(gripL, gripR)));
                if (minField <= 1f)
                {
                    pixels[y * w + x] = minField > 0.89f ? borderCol : fillCol;
                }
                else
                {
                    pixels[y * w + x] = Color.clear;
                }
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
    }

    private static Texture2D CreateTacticalGridTexture(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[size * size];

        Color clearCol = new Color(0f, 0f, 0f, 0f);
        Color subGrid = new Color(0.35f, 0.90f, 0.55f, 0.22f);
        Color mainGrid = new Color(0.45f, 1.0f, 0.65f, 0.55f);
        Color nodeDot = new Color(0.75f, 1.0f, 0.85f, 0.85f);

        int half = size / 2;
        int quarter = size / 4;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool isMain = (x == 0 || y == 0);
                bool isSub = (x % quarter == 0 || y % quarter == 0);
                bool isCross = (x < 3 || x > size - 4) && (y < 3 || y > size - 4);
                bool isCenterDot = Mathf.Abs(x - half) <= 1 && Mathf.Abs(y - half) <= 1;

                if (isCross || isCenterDot) pixels[y * size + x] = nodeDot;
                else if (isMain) pixels[y * size + x] = mainGrid;
                else if (isSub) pixels[y * size + x] = subGrid;
                else pixels[y * size + x] = clearCol;
            }
        }

        tex.SetPixels(pixels);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Point;
        tex.Apply();
        return tex;
    }

    private static Sprite CreateCircleSprite(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[size * size];
        float c = size * 0.5f;
        float r = size * 0.48f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(c, c));
                float a = Mathf.Clamp01(1f - (dist / r));
                pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Sqrt(a));
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private static Sprite CreateGreyscaleSprite(Sprite source, bool solidWhite = false)
    {
        if (source == null || source.texture == null) return source;
        try
        {
            Texture2D srcTex = source.texture;
            int w = Mathf.Min(srcTex.width, 512);
            int h = Mathf.Min(srcTex.height, 512);
            RenderTexture rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(srcTex, rt);

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            Texture2D copy = new Texture2D(w, h, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            if (RenderTexture.active == rt)
            {
                RenderTexture.active = (prev != rt) ? prev : null;
            }
            RenderTexture.ReleaseTemporary(rt);

            Color[] pixels = copy.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                Color c = pixels[i];
                if (c.a > 0.01f)
                {
                    if (solidWhite)
                    {
                        pixels[i] = new Color(1f, 1f, 1f, c.a);
                    }
                    else
                    {
                        float lum = c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;
                        float grey = Mathf.Clamp01(lum * 1.10f + 0.05f);
                        pixels[i] = new Color(grey, grey, grey, c.a);
                    }
                }
            }

            copy.SetPixels(pixels);
            copy.Apply();
            return Sprite.Create(copy, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
        }
        catch
        {
            return source;
        }
    }
}

/// <summary>
/// Controlador de la nave del jugador dentro de la Arena UI del Tutorial.
/// Replica exactamente los controles reales (WASD/Flechas, Shift modo lento, RMB/Q/E cambio de arma, LMB/Space disparo normal y cargado).
/// </summary>
public class TutorialUIPlayer : MonoBehaviour
{
    public RectTransform Rect { get; private set; }
    public TutorialPanelController.TutorialFireMode CurrentMode { get; private set; } = TutorialPanelController.TutorialFireMode.Normal;
    public bool IsSlowMode { get; private set; } = false;

    private TutorialPanelController panel;
    private Image shipImage;
    private RectTransform chargeAuraRect;
    private Image chargeAuraImage;
    private GameObject chargeBarRoot;
    private Image chargeBarFillImage;
    private GameObject focusDot;

    private float fireRate = 0.217f;
    private float nextFireTime = 0f;

    // Disparo cargado (fiel a PlayerController)
    private float chargeTimeRequired = 2.2f;
    private float chargeSoundThreshold = 0.20f;
    private float maxChargeHoldTime = 5.0f;
    private float currentChargeTime = 0f;
    private bool isFullyCharged = false;
    private float chargeHoldTimer = 0f;
    private bool hasFiredChargedShot = false;
    private float hitFlashTimer = 0f;
    private float bankAmount = 0f;
    private bool prevLeftTriggerDown = false;

    public void Setup(TutorialPanelController ctrl, Image img, RectTransform auraRt, Image auraImg, GameObject barRoot, Image barFillImg, GameObject precisionDot)
    {
        panel = ctrl;
        Rect = GetComponent<RectTransform>();
        shipImage = img;
        chargeAuraRect = auraRt;
        chargeAuraImage = auraImg;
        chargeBarRoot = barRoot;
        chargeBarFillImage = barFillImg;
        focusDot = precisionDot;
    }

    public void TriggerHitFeedback()
    {
        hitFlashTimer = 0.16f;
    }

    void Update()
    {
        if (panel == null || Rect == null) return;

        HandleHitFlash();
        HandleMovement();
        HandleShooting();
    }

    private void HandleHitFlash()
    {
        if (shipImage == null) return;
        if (hitFlashTimer > 0f)
        {
            hitFlashTimer -= Time.deltaTime;
            shipImage.color = new Color(1f, 0.35f, 0.35f, 1f);
        }
        else
        {
            shipImage.color = new Color(0.35f, 0.85f, 1.0f, 1f);
        }
    }

    private void HandleMovement()
    {
        Vector2 move = Vector2.zero;
        bool toggleSlow = false;

        if (Keyboard.current != null)
        {
            var kb = Keyboard.current;
            if (kb.leftShiftKey.wasPressedThisFrame || kb.rightShiftKey.wasPressedThisFrame)
            {
                toggleSlow = true;
            }

            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) move.y += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) move.y -= 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) move.x -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move.x += 1f;
        }

        // Soporte en vivo de mandos conectados dentro de la Arena del Tutorial
        var pads = Gamepad.all;
        for (int i = 0; i < pads.Count; i++)
        {
            Gamepad pad = pads[i];
            if (pad == null) continue;

            if (pad.leftShoulder.wasPressedThisFrame)
            {
                toggleSlow = true;
            }

            Vector2 stick = pad.leftStick.ReadValue();
            if (stick.sqrMagnitude > 0.04f) move += stick;
            if (pad.dpad.up.isPressed) move.y += 1f;
            if (pad.dpad.down.isPressed) move.y -= 1f;
            if (pad.dpad.left.isPressed) move.x -= 1f;
            if (pad.dpad.right.isPressed) move.x += 1f;
        }

        if (toggleSlow)
        {
            IsSlowMode = !IsSlowMode;
            if (focusDot != null) focusDot.SetActive(IsSlowMode);
        }

        if (move.sqrMagnitude > 1f) move.Normalize();

        float baseSpeed = Mathf.Max(260f, panel.GetArenaHalfWidth() * 0.85f);
        float speed = IsSlowMode ? baseSpeed * 0.55f : baseSpeed;

        Vector2 pos = Rect.anchoredPosition + move * speed * Time.deltaTime;
        float limitX = Mathf.Max(20f, panel.GetArenaHalfWidth() - 34f);
        float limitY = Mathf.Max(20f, panel.GetArenaHalfHeight() - 34f);
        pos.x = Mathf.Clamp(pos.x, -limitX, limitX);
        pos.y = Mathf.Clamp(pos.y, -limitY, limitY);
        Rect.anchoredPosition = pos;

        // Solo inclinación lateral (izquierda / derecha); arriba / abajo no rotan la nave
        ChargeVisualUtils.EvalShipBank(ref bankAmount, move.x, Time.deltaTime, 13.5f, 0.19f, out float zAngle, out float scaleXMult);
        Rect.localRotation = Quaternion.Euler(0f, 0f, zAngle);
        Rect.localScale = new Vector3(scaleXMult, 1f, 1f);

        if (chargeBarRoot != null)
        {
            chargeBarRoot.transform.localRotation = Quaternion.Euler(0f, 0f, -zAngle);
            chargeBarRoot.transform.localScale = new Vector3(1f / Mathf.Max(0.25f, scaleXMult), 1f, 1f);
        }
    }

    private void HandleShooting()
    {
        bool toggleMode = false;
        if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame) toggleMode = true;
        // Tecla X para cambiar de modo de arma
        if (Keyboard.current != null && (Keyboard.current.xKey.wasPressedThisFrame || Keyboard.current.qKey.wasPressedThisFrame || Keyboard.current.eKey.wasPressedThisFrame)) toggleMode = true;

        bool isLtDown = TutorialPanelController.AnyGamepadMatches(p => p.leftTrigger.isPressed || p.leftTrigger.ReadValue() > 0.35f);
        if (isLtDown && !prevLeftTriggerDown)
        {
            toggleMode = true;
        }
        prevLeftTriggerDown = isLtDown;

        if (toggleMode)
        {
            CurrentMode = (CurrentMode == TutorialPanelController.TutorialFireMode.Normal)
                ? TutorialPanelController.TutorialFireMode.HeavyCharged
                : TutorialPanelController.TutorialFireMode.Normal;

            currentChargeTime = 0f;
            isFullyCharged = false;
            chargeHoldTimer = 0f;
            hasFiredChargedShot = false;
            UpdateChargeVisual(false, 0f, false);

            if (SoundController.HasInstance)
            {
                SoundController.Instance.StopChargeSound();
                SoundController.Instance.PlaySwitchModeSfx();
            }
        }

        bool isHoldingFire = false;
        if (Mouse.current != null && Mouse.current.leftButton.isPressed && !panel.IsPointerOverCloseButton())
        {
            isHoldingFire = true;
        }
        // Tecla Z (o Barra espaciadora) para disparar ráfagas automáticas en modo normal al mantener presionado
        if (Keyboard.current != null && (Keyboard.current.zKey.isPressed || Keyboard.current.spaceKey.isPressed))
        {
            isHoldingFire = true;
        }
        if (TutorialPanelController.AnyGamepadMatches(p => p.buttonWest.isPressed))
        {
            isHoldingFire = true;
        }

        if (CurrentMode == TutorialPanelController.TutorialFireMode.Normal)
        {
            UpdateChargeVisual(false, 0f, false);
            if (isHoldingFire && Time.time >= nextFireTime)
            {
                nextFireTime = Time.time + fireRate;
                panel.SpawnPlayerBullet(Rect.anchoredPosition + new Vector2(0f, 34f), new Vector2(18f, 36f), 760f, 1, false, new Color(0.45f, 0.92f, 1f, 1f));
                if (SoundController.HasInstance) SoundController.Instance.PlayNormalShootSfx();
            }
        }
        else
        {
            if (isHoldingFire)
            {
                if (!hasFiredChargedShot)
                {
                    if (!isFullyCharged)
                    {
                        currentChargeTime += Time.deltaTime;
                        float progress = Mathf.Clamp01(currentChargeTime / chargeTimeRequired);

                        if (currentChargeTime >= chargeSoundThreshold && SoundController.HasInstance)
                        {
                            float sfxProg = Mathf.Clamp01((currentChargeTime - chargeSoundThreshold) / (chargeTimeRequired - chargeSoundThreshold));
                            SoundController.Instance.UpdateChargeSound(sfxProg);
                        }

                        if (currentChargeTime >= chargeTimeRequired)
                        {
                            isFullyCharged = true;
                            chargeHoldTimer = maxChargeHoldTime;
                            currentChargeTime = chargeTimeRequired;
                            UpdateChargeVisual(true, 1f, true);
                        }
                        else
                        {
                            UpdateChargeVisual(true, progress, false);
                        }
                    }
                    else
                    {
                        chargeHoldTimer -= Time.deltaTime;
                        if (chargeHoldTimer <= 0f)
                        {
                            FireBigChargedShot();
                            hasFiredChargedShot = true;
                            isFullyCharged = false;
                            currentChargeTime = 0f;
                            UpdateChargeVisual(false, 0f, false);
                        }
                        else
                        {
                            UpdateChargeVisual(true, 1f, true);
                        }
                    }
                }
                else
                {
                    UpdateChargeVisual(false, 0f, false);
                }
            }
            else
            {
                if (SoundController.HasInstance) SoundController.Instance.StopChargeSound();

                if (isFullyCharged)
                {
                    FireBigChargedShot();
                }
                else if (!hasFiredChargedShot && currentChargeTime >= 1.0f)
                {
                    panel.SpawnPlayerBullet(Rect.anchoredPosition + new Vector2(0f, 40f), new Vector2(40f, 64f), 740f, 2, false, new Color(0.55f, 0.96f, 1f, 1f));
                    if (SoundController.HasInstance) SoundController.Instance.PlayMediumShootSfx();
                }
                else if (!hasFiredChargedShot && currentChargeTime > 0f && Time.time >= nextFireTime)
                {
                    nextFireTime = Time.time + (fireRate * 0.75f);
                    panel.SpawnPlayerBullet(Rect.anchoredPosition + new Vector2(0f, 32f), new Vector2(14f, 28f), 780f, 1, false, new Color(0.45f, 0.90f, 1f, 0.95f));
                    if (SoundController.HasInstance) SoundController.Instance.PlayMiniShootSfx();
                }

                hasFiredChargedShot = false;
                isFullyCharged = false;
                currentChargeTime = 0f;
                UpdateChargeVisual(false, 0f, false);
            }
        }
    }

    private void FireBigChargedShot()
    {
        if (SoundController.HasInstance)
        {
            SoundController.Instance.StopChargeSound();
            SoundController.Instance.PlayBigShootSfx();
        }
        panel.SpawnPlayerBullet(Rect.anchoredPosition + new Vector2(0f, 52f), new Vector2(110.4f, 161f), 620f, 5, true, new Color(0.92f, 0.96f, 0.45f, 1f));
    }

    private void UpdateChargeVisual(bool visible, float progress, bool full)
    {
        if (chargeBarRoot != null)
        {
            if (chargeBarRoot.activeSelf != visible)
            {
                chargeBarRoot.SetActive(visible);
            }
            if (visible && chargeBarFillImage != null)
            {
                chargeBarFillImage.fillAmount = Mathf.Clamp01(progress);
                Color barColor = ChargeVisualUtils.GetChargeColor(progress, full);
                if (full)
                {
                    float flash = 0.85f + Mathf.Sin(Time.time * 18f) * 0.15f;
                    barColor *= flash;
                    barColor.a = 1f;
                }
                chargeBarFillImage.color = barColor;
            }
        }

        if (chargeAuraRect == null || chargeAuraImage == null) return;
        if (!visible || progress <= 0.02f)
        {
            chargeAuraImage.color = new Color(0.3f, 0.95f, 1f, 0f);
            return;
        }
        float size = Mathf.Lerp(12f, full ? 46f : 34f, progress);
        chargeAuraRect.sizeDelta = new Vector2(size, size);
        Color auraColor = ChargeVisualUtils.GetChargeColor(progress, full);
        auraColor.a = full ? (0.78f + Mathf.Sin(Time.time * 18f) * 0.22f) : Mathf.Lerp(0.28f, 0.82f, progress);
        chargeAuraImage.color = auraColor;
    }

    void OnDisable()
    {
        bankAmount = 0f;
        if (Rect != null)
        {
            Rect.localRotation = Quaternion.identity;
            Rect.localScale = Vector3.one;
        }
        UpdateChargeVisual(false, 0f, false);
        if (SoundController.HasInstance) SoundController.Instance.StopChargeSound();
    }
}

/// <summary>
/// Proyectil del jugador dentro de la Arena UI.
/// </summary>
public class TutorialUIPlayerBullet : MonoBehaviour
{
    public RectTransform Rect { get; private set; }
    public int Damage { get; private set; }
    public bool IsPiercing { get; private set; }

    private TutorialPanelController panel;
    private float speed;
    private bool isHeavyShot;
    private Image bodyImage;
    private Color baseColor = Color.white;
    private RectTransform wakeRect;
    private Image wakeImage;
    private RectTransform glowRect;
    private Image glowImage;
    private RectTransform vortexRect;
    private Color wakeTint = new Color(0.4f, 0.9f, 1f, 1f);
    private float age = 0f;
    private readonly HashSet<TutorialUIGreyEnemy> hitEnemies = new HashSet<TutorialUIGreyEnemy>();

    public void Setup(
        TutorialPanelController ctrl, float spd, int dmg, bool piercing, bool heavy,
        Image img, Color tint,
        RectTransform wakeRt, Image wakeImg,
        RectTransform glowRt, Image glowImg,
        RectTransform vortexRt, Color trailColor)
    {
        panel = ctrl;
        Rect = GetComponent<RectTransform>();
        speed = spd;
        Damage = dmg;
        IsPiercing = piercing;
        isHeavyShot = heavy;
        bodyImage = img;
        baseColor = tint;
        wakeRect = wakeRt;
        wakeImage = wakeImg;
        glowRect = glowRt;
        glowImage = glowImg;
        vortexRect = vortexRt;
        wakeTint = trailColor;
    }

    public bool HasHitEnemy(TutorialUIGreyEnemy enemy) => hitEnemies.Contains(enemy);
    public void MarkEnemyHit(TutorialUIGreyEnemy enemy) => hitEnemies.Add(enemy);

    void Update()
    {
        if (Rect == null || panel == null) return;
        float dt = Time.deltaTime;
        Vector2 pos = Rect.anchoredPosition;
        pos.y += speed * dt;
        Rect.anchoredPosition = pos;

        age += dt;
        ChargeVisualUtils.EvalBulletAnim(
            age, isHeavyShot, IsPiercing,
            out float sx, out float sy,
            out float trailScaleY, out float trailAlpha,
            out float coreGlowAlpha, out float spinDegPerSec
        );
        Rect.localScale = new Vector3(sx, sy, 1f);

        if (bodyImage != null)
        {
            float shimmer = 0.08f + 0.14f * (0.5f + 0.5f * Mathf.Sin(age * (isHeavyShot ? 18f : 26f)));
            bodyImage.color = Color.Lerp(baseColor, Color.white, shimmer);
        }

        if (wakeRect != null && wakeImage != null)
        {
            float w = 0.94f + 0.10f * Mathf.Sin(age * 32f);
            wakeRect.localScale = new Vector3(w, trailScaleY, 1f);
            Color wc = wakeTint;
            wc.a = trailAlpha;
            wakeImage.color = wc;
        }

        if (glowRect != null && glowImage != null)
        {
            float gs = 0.92f + 0.16f * Mathf.Sin(age * 24f);
            glowRect.localScale = new Vector3(gs, gs, 1f);
            Color gc = wakeTint;
            gc.a = coreGlowAlpha;
            glowImage.color = gc;
        }

        if (vortexRect != null)
        {
            vortexRect.Rotate(0f, 0f, spinDegPerSec * dt);
        }

        if (pos.y > panel.GetArenaHalfHeight() + 60f)
        {
            Destroy(gameObject);
        }
    }
}

/// <summary>
/// Bicho Gris de Entrenamiento en la Arena UI:
/// - Misma IA que el Tier 1 (entra desde arriba, se posiciona y patrulla horizontalmente).
/// - 30% menos cadencia de disparo que Tier 1 (Tier 1 dispara cada 2.50s -> 3.57s).
/// </summary>
public class TutorialUIGreyEnemy : MonoBehaviour
{
    public RectTransform Rect { get; private set; }
    public float HitRadius => 26f;
    public bool IsDead { get; private set; } = false;

    private TutorialPanelController panel;
    private Image spriteImage;
    private Image flashOverlayImage;

    private float entrySpeed = 200f;
    private float patrolSpeed = 120f;
    private float settleY;
    private int patrolDir = 1;
    private bool hasSettled = false;
    private bool hasEnteredArena = false;
    private int health = 2;
    private float hitFlashTimer = 0f;
    private const float HitFlashDuration = 0.11f;
    private float animPhase = 0f;
    private float bankAmount = 0f;
    private float recoilTimer = 0f;
    private float prevHoverY = 0f;
    private float prevRecoilY = 0f;

    // 30% menos cadencia de disparo que el Tier 1 normal y -15% de velocidad de generación (4.11s * 1.15 = 4.73s)
    private const float FireCadence = 4.7265f;
    private float nextFireTime = 0f;

    public void Setup(TutorialPanelController ctrl, Image img, Image flashImg)
    {
        panel = ctrl;
        spriteImage = img;
        flashOverlayImage = flashImg;
        Rect = GetComponent<RectTransform>();

        float halfH = panel.GetArenaHalfHeight();
        settleY = UnityEngine.Random.Range(halfH * 0.22f, halfH * 0.72f);
        patrolDir = UnityEngine.Random.value > 0.5f ? 1 : -1;
        nextFireTime = Time.time + UnityEngine.Random.Range(1.0f, 2.2f);
        animPhase = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
    }

    void Update()
    {
        if (IsDead || panel == null || Rect == null) return;

        if (hitFlashTimer > 0f)
        {
            hitFlashTimer = Mathf.Max(0f, hitFlashTimer - Time.deltaTime);
            float alpha = 0.58f * (hitFlashTimer / HitFlashDuration);
            if (flashOverlayImage != null) flashOverlayImage.color = new Color(1f, 1f, 1f, alpha);
            if (spriteImage != null) spriteImage.color = Color.Lerp(new Color(0.88f, 0.90f, 0.94f, 1f), Color.white, hitFlashTimer / HitFlashDuration);
        }
        else
        {
            if (flashOverlayImage != null && flashOverlayImage.color.a > 0f) flashOverlayImage.color = new Color(1f, 1f, 1f, 0f);
            if (spriteImage != null) spriteImage.color = new Color(0.88f, 0.90f, 0.94f, 1f);
        }

        Vector2 pos = Rect.anchoredPosition;

        if (!hasEnteredArena && pos.y <= panel.GetArenaHalfHeight() - 10f)
        {
            hasEnteredArena = true;
            if (SoundController.HasInstance) SoundController.Instance.PlayEnemyEntrySfx(EnemyTier.Tier1);
        }

        if (!hasSettled)
        {
            pos.y -= entrySpeed * Time.deltaTime;
            if (pos.y <= settleY)
            {
                pos.y = settleY;
                hasSettled = true;
            }
            Rect.anchoredPosition = pos;
        }
        else
        {
            pos.x += patrolDir * patrolSpeed * Time.deltaTime;
            float limitX = Mathf.Max(40f, panel.GetArenaHalfWidth() - 42f);
            if (pos.x > limitX)
            {
                pos.x = limitX;
                patrolDir = -1;
            }
            else if (pos.x < -limitX)
            {
                pos.x = -limitX;
                patrolDir = 1;
            }
            Rect.anchoredPosition = pos;

            if (Time.time >= nextFireTime)
            {
                nextFireTime = Time.time + FireCadence;
                FireAtPlayer();
            }
        }

        // Animación mecánica rígida: flotación suave y retroceso vertical al disparar (sin rotación diagonal)
        float bankInput = hasSettled ? patrolDir : 0f;
        ChargeVisualUtils.EvalShipBank(ref bankAmount, bankInput, Time.deltaTime, 10.5f, 0.12f, out _, out float scaleXMult);

        float recoilKickY = 0f;
        if (recoilTimer > 0f)
        {
            recoilTimer = Mathf.Max(0f, recoilTimer - Time.deltaTime);
            recoilKickY = 4.5f * Mathf.Sin((recoilTimer / 0.12f) * Mathf.PI);
        }

        if (hasSettled)
        {
            float hoverY = Mathf.Sin(Time.time * 2.4f + animPhase) * 3.5f;
            float deltaY = (hoverY - prevHoverY) + (recoilKickY - prevRecoilY);
            prevHoverY = hoverY;
            prevRecoilY = recoilKickY;
            if (Mathf.Abs(deltaY) > 0.001f)
            {
                Rect.anchoredPosition += new Vector2(0f, deltaY);
            }
        }
        else
        {
            prevHoverY = 0f;
            prevRecoilY = 0f;
        }

        Rect.localRotation = Quaternion.identity;
        Rect.localScale = new Vector3(scaleXMult, 1f, 1f);
    }

    private void FireAtPlayer()
    {
        Vector2 dir = Vector2.down;
        Vector2? playerPos = panel.GetPlayerPosition();
        if (playerPos.HasValue && UnityEngine.Random.value < 0.60f)
        {
            dir = (playerPos.Value - Rect.anchoredPosition).normalized;
        }

        recoilTimer = 0.12f;
        panel.SpawnEnemyBullet(Rect.anchoredPosition + dir * 24f, dir, 235f);
    }

    public bool TakeDamage(int dmg)
    {
        if (IsDead) return false;
        health -= dmg;
        if (health <= 0)
        {
            IsDead = true;
            if (SoundController.HasInstance) SoundController.Instance.PlayEnemyDeathSfx();
            Destroy(gameObject);
            return true;
        }
        hitFlashTimer = HitFlashDuration;
        if (flashOverlayImage != null) flashOverlayImage.color = new Color(1f, 1f, 1f, 0.58f);
        return false;
    }
}

/// <summary>
/// Proyectil enemigo dentro de la Arena UI.
/// </summary>
public class TutorialUIEnemyBullet : MonoBehaviour
{
    public RectTransform Rect { get; private set; }
    public float HitRadius => 8f;

    private TutorialPanelController panel;
    private Vector2 direction;
    private float speed;

    public void Setup(TutorialPanelController ctrl, Vector2 dir, float spd)
    {
        panel = ctrl;
        Rect = GetComponent<RectTransform>();
        direction = dir.normalized;
        speed = spd;
    }

    void Update()
    {
        if (Rect == null || panel == null) return;
        Vector2 pos = Rect.anchoredPosition + direction * speed * Time.deltaTime;
        Rect.anchoredPosition = pos;

        if (pos.y < -panel.GetArenaHalfHeight() - 30f || Mathf.Abs(pos.x) > panel.GetArenaHalfWidth() + 30f)
        {
            Destroy(gameObject);
        }
    }
}

/// <summary>
/// Efecto visual breve de explosión dentro de la Arena UI.
/// </summary>
public class TutorialUIExplosion : MonoBehaviour
{
    private Image img;
    private float timer = 0f;
    private const float Duration = 0.28f;

    void Awake()
    {
        img = GetComponent<Image>();
    }

    void Update()
    {
        timer += Time.deltaTime;
        float t = Mathf.Clamp01(timer / Duration);
        transform.localScale = Vector3.one * Mathf.Lerp(0.75f, 1.45f, t);
        if (img != null)
        {
            Color c = img.color;
            c.a = 1f - t;
            img.color = c;
        }
        if (t >= 1f)
        {
            Destroy(gameObject);
        }
    }
}
