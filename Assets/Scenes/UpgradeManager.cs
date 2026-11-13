using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System;

public class UpgradeManager : MonoBehaviour
{
    [Header("Daño")]
    [SerializeField] private Button damageButton;
    [SerializeField] private TextMeshProUGUI damageCostText;
    [SerializeField] private float damageIncreasePerLevel = 0.5f;
    private int damageLevel = 0;

    [Header("Velocidad")]
    [SerializeField] private Button speedButton;
    [SerializeField] private TextMeshProUGUI speedCostText;
    [SerializeField] private float speedIncreasePerLevel = 350f; // Incremento notable (+39% por nivel)
    private int speedLevel = 0;

    [Header("Salud")]
    [SerializeField] private Button healthButton;
    [SerializeField] private TextMeshProUGUI healthCostText;
    [SerializeField] private int healthIncreasePerLevel = 2;
    private int healthLevel = 0;

    [Header("Continuar a la siguiente oleada")]
    [SerializeField] private Button continueButton;

    [Header("Imágenes de Mejoras (Opcional - se usarán automáticamente al asignarlas)")]
    public Sprite damageImage;
    public Sprite healthImage;
    public Sprite speedImage;

    // WaveController se suscribe a esto para saber cuándo el jugador
    // terminó de comprar mejoras y hay que arrancar la siguiente oleada.
    public Action OnContinue;

    [Header("Configuración de costo")]
    [SerializeField] private int baseCost = 25;
    [SerializeField] private int costIncreasePerLevel = 20;

    private bool hasContinued = false;

    [Header("Referencias")]
    [SerializeField] private PlayerController playerController;

    // Referencias internas a los componentes visuales de las tarjetas
    private Image damageArtImage;
    private Image healthArtImage;
    private Image speedArtImage;
    private TextMeshProUGUI damageTitleText;
    private TextMeshProUGUI healthTitleText;
    private TextMeshProUGUI speedTitleText;

    // Medidor de audio compartido para activar el botón Continuar
    private readonly MicrophoneScreamMeterSession screamSession = new MicrophoneScreamMeterSession();
    private bool screamUnlocked = false;
    private bool wasCoopOnOpen = false;
    private RectTransform continueRect;
    private TextMeshProUGUI continueText;
    private RectTransform screamFillRect;
    private Image screamFillImg;
    private Image[] continueBorderEdges;
    private RectTransform micFillRect;
    private Image micFillImg;
    private RectTransform micThresholdRect;
    private TextMeshProUGUI micPromptTmp;
    private TextMeshProUGUI micDeviceTmp;

    // Código de atajo (Cheat) para saltar la barra de audio y continuar: 7, 8, 9, 4, 5, 6, 1, 2, 3
    private static readonly int[] CheatSkipSequence = { 7, 8, 9, 4, 5, 6, 1, 2, 3 };
    private int currentCheatIndex = 0;

    private static Sprite s_CardBgSprite;
    private static Sprite s_DamageDefaultIcon;
    private static Sprite s_HealthDefaultIcon;
    private static Sprite s_SpeedDefaultIcon;

    void Awake()
    {
        if (speedIncreasePerLevel < 350f)
        {
            speedIncreasePerLevel = 350f; // Asegura aumento sustancial de velocidad (+39% sobre velocidad base)
        }
        SetupLayoutAndCards();
    }

    void OnEnable()
    {
        transform.SetAsLastSibling();
        hasContinued = false;
        screamUnlocked = false;
        currentCheatIndex = 0;
        wasCoopOnOpen = GameManager.Instance != null && GameManager.Instance.IsPlayer2Joined;

        // Ocultar cualquier texto o cronómetro residual del HUD durante la pantalla de mejoras
        if (GameManager.Instance != null && GameManager.Instance.hudManager != null)
        {
            GameManager.Instance.hudManager.HideTimerForUpgrade(true);
        }

        if (continueButton != null)
        {
            continueButton.interactable = false;
        }
        if (continueRect != null)
        {
            continueRect.anchoredPosition = new Vector2(0f, -262f);
        }

        screamSession.Begin();
        ScreamMeterUIUtils.UpdateThresholdMarker(micThresholdRect, wasCoopOnOpen);
        RefreshUI();
        RefreshScreamGateUI();

        if (SoundController.Instance != null)
        {
            SoundController.Instance.PlayUpgradePanelOpenSfx();
        }
    }

    void OnDisable()
    {
        currentCheatIndex = 0;
        screamSession.Stop();
        if (continueRect != null) continueRect.anchoredPosition = new Vector2(0f, -262f);

        if (GameManager.Instance != null && GameManager.Instance.hudManager != null)
        {
            GameManager.Instance.hudManager.HideTimerForUpgrade(false);
        }
    }

    void OnDestroy()
    {
        screamSession.Stop();
    }

    void Update()
    {
        if (hasContinued) return;

        CheckSkipCheatCode();
        if (hasContinued) return;

        float rawDt = Time.unscaledDeltaTime;
        screamSession.Step(Mathf.Min(0.05f, rawDt), wasCoopOnOpen, true);

        if (!screamUnlocked && screamSession.IsComplete)
        {
            screamUnlocked = true;
            if (continueButton != null)
            {
                continueButton.interactable = true;
                if (EventSystem.current != null)
                {
                    EventSystem.current.SetSelectedGameObject(continueButton.gameObject);
                }
            }
            if (SoundController.Instance != null)
            {
                SoundController.Instance.PlayUpgradeContinueSfx();
            }
        }

        RefreshScreamGateUI();
    }

    private void CheckSkipCheatCode()
    {
        int digit = GetPressedDigit();
        if (digit == -1) return;

        if (digit == CheatSkipSequence[currentCheatIndex])
        {
            currentCheatIndex++;
            if (currentCheatIndex >= CheatSkipSequence.Length)
            {
                currentCheatIndex = 0;
                Debug.Log("<color=#00FF88><b>[CHEAT]</b> Secuencia 7-8-9-4-5-6-1-2-3 detectada. Ignorando barra de audio y avanzando a la siguiente oleada...</color>");
                screamUnlocked = true;
                TriggerContinue();
            }
        }
        else
        {
            currentCheatIndex = (digit == CheatSkipSequence[0]) ? 1 : 0;
        }
    }

    private int GetPressedDigit()
    {
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null)
        {
            if (kb.digit7Key.wasPressedThisFrame || kb.numpad7Key.wasPressedThisFrame) return 7;
            if (kb.digit8Key.wasPressedThisFrame || kb.numpad8Key.wasPressedThisFrame) return 8;
            if (kb.digit9Key.wasPressedThisFrame || kb.numpad9Key.wasPressedThisFrame) return 9;
            if (kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame) return 4;
            if (kb.digit5Key.wasPressedThisFrame || kb.numpad5Key.wasPressedThisFrame) return 5;
            if (kb.digit6Key.wasPressedThisFrame || kb.numpad6Key.wasPressedThisFrame) return 6;
            if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) return 1;
            if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) return 2;
            if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) return 3;
            if (kb.digit0Key.wasPressedThisFrame || kb.numpad0Key.wasPressedThisFrame) return 0;
        }

        try
        {
            if (Input.GetKeyDown(KeyCode.Alpha7) || Input.GetKeyDown(KeyCode.Keypad7)) return 7;
            if (Input.GetKeyDown(KeyCode.Alpha8) || Input.GetKeyDown(KeyCode.Keypad8)) return 8;
            if (Input.GetKeyDown(KeyCode.Alpha9) || Input.GetKeyDown(KeyCode.Keypad9)) return 9;
            if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4)) return 4;
            if (Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.Keypad5)) return 5;
            if (Input.GetKeyDown(KeyCode.Alpha6) || Input.GetKeyDown(KeyCode.Keypad6)) return 6;
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) return 1;
            if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) return 2;
            if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) return 3;
            if (Input.GetKeyDown(KeyCode.Alpha0) || Input.GetKeyDown(KeyCode.Keypad0)) return 0;
        }
        catch (System.InvalidOperationException) { }

        return -1;
    }

    void Start()
    {
        if (damageButton != null) damageButton.onClick.AddListener(PurchaseDamage);
        if (speedButton != null) speedButton.onClick.AddListener(PurchaseSpeed);
        if (healthButton != null) healthButton.onClick.AddListener(PurchaseHealth);
        if (continueButton != null) continueButton.onClick.AddListener(HandleContinueButtonClicked);
    }

    private void SetupLayoutAndCards()
    {
        // 1. Deshabilitar cualquier HorizontalLayoutGroup que interfiera con el posicionamiento libre
        HorizontalLayoutGroup hlg = GetComponent<HorizontalLayoutGroup>();
        if (hlg != null) hlg.enabled = false;

        // Fondo oscuro del panel completo
        Image panelBg = GetComponent<Image>();
        if (panelBg != null)
        {
            panelBg.color = new Color(0.02f, 0.05f, 0.10f, 0.88f);
            panelBg.raycastTarget = true;
        }

        // Crear sprites procedurales elegantes por defecto con sus símbolos correspondientes
        if (s_CardBgSprite == null) s_CardBgSprite = GenerateCardBgSprite();
        if (s_DamageDefaultIcon == null) s_DamageDefaultIcon = GenerateDamageIcon();
        if (s_HealthDefaultIcon == null) s_HealthDefaultIcon = GenerateHealthIcon();
        if (s_SpeedDefaultIcon == null) s_SpeedDefaultIcon = GenerateSpeedIcon();

        // 2. Configurar tarjeta de DAÑO (Columna izquierda: X = -300)
        SetupCard(damageButton, damageCostText, "DAÑO", new Vector2(-300f, 25f),
            damageImage != null ? damageImage : s_DamageDefaultIcon,
            new Color(1f, 0.35f, 0.25f, 1f),
            out damageArtImage, out damageTitleText);

        // 3. Configurar tarjeta de SALUD (Columna central: X = 0)
        SetupCard(healthButton, healthCostText, "VIDA", new Vector2(0f, 25f),
            healthImage != null ? healthImage : s_HealthDefaultIcon,
            new Color(0.25f, 0.95f, 0.45f, 1f),
            out healthArtImage, out healthTitleText);

        // 4. Configurar tarjeta de VELOCIDAD (Columna derecha: X = 300)
        SetupCard(speedButton, speedCostText, "VELOCIDAD", new Vector2(300f, 25f),
            speedImage != null ? speedImage : s_SpeedDefaultIcon,
            new Color(0.25f, 0.75f, 1f, 1f),
            out speedArtImage, out speedTitleText);

        // 5. Reemplazar el botón simple de Continuar por un medidor de audio integrado que lo activa al 100%
        SetupScreamContinueGate();
    }

    private void SetupScreamContinueGate()
    {
        TMP_FontAsset sharedFont = null;
        if (damageCostText != null) sharedFont = damageCostText.font;
        else
        {
            TextMeshProUGUI anyTmp = FindAnyObjectByType<TextMeshProUGUI>();
            if (anyTmp != null) sharedFont = anyTmp.font;
        }

        if (continueButton == null)
        {
            GameObject btnObj = new GameObject("ContinueButton", typeof(RectTransform), typeof(Image), typeof(Button));
            btnObj.transform.SetParent(transform, false);
            continueButton = btnObj.GetComponent<Button>();
        }

        continueRect = continueButton.GetComponent<RectTransform>();
        continueRect.anchorMin = new Vector2(0.5f, 0.5f);
        continueRect.anchorMax = new Vector2(0.5f, 0.5f);
        continueRect.pivot = new Vector2(0.5f, 0.5f);
        continueRect.anchoredPosition = new Vector2(0f, -262f);
        continueRect.sizeDelta = new Vector2(540f, 58f);

        Image contImg = continueButton.GetComponent<Image>();
        if (contImg != null)
        {
            contImg.sprite = null;
            contImg.color = new Color(0.02f, 0.04f, 0.09f, 0.96f);
        }

        ColorBlock cb = continueButton.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(0.86f, 1f, 0.92f, 1f);
        cb.pressedColor = new Color(0.68f, 0.90f, 0.78f, 1f);
        cb.selectedColor = Color.white;
        cb.disabledColor = Color.white;
        cb.fadeDuration = 0.08f;
        continueButton.colors = cb;
        continueButton.interactable = false;

        screamFillImg = ScreamMeterUIUtils.CreateBarFill(continueButton.transform, out screamFillRect);
        screamFillImg.transform.SetAsFirstSibling();

        for (int i = 1; i <= 3; i++)
        {
            GameObject tick = new GameObject("Tick_" + i, typeof(RectTransform), typeof(Image));
            tick.transform.SetParent(continueButton.transform, false);
            tick.transform.SetSiblingIndex(i);
            RectTransform tRt = tick.GetComponent<RectTransform>();
            float tx = i * 0.25f;
            tRt.anchorMin = new Vector2(tx, 0f);
            tRt.anchorMax = new Vector2(tx, 1f);
            tRt.sizeDelta = new Vector2(2f, 0f);
            Image tImg = tick.GetComponent<Image>();
            tImg.color = new Color(0.02f, 0.04f, 0.08f, 0.7f);
            tImg.raycastTarget = false;
        }

        continueBorderEdges = ScreamMeterUIUtils.CreateBorderFrame(continueButton.transform, new Color(0.28f, 0.65f, 0.92f, 0.85f), 2.5f);

        continueText = continueButton.GetComponentInChildren<TextMeshProUGUI>();
        if (continueText == null)
        {
            continueText = ScreamMeterUIUtils.CreateLabel(continueButton.transform, "Text", Vector2.zero, new Vector2(520f, 46f),
                "", 20, TextAlignmentOptions.Center, sharedFont);
        }
        continueText.transform.SetAsLastSibling();
        continueText.fontSize = 20;
        continueText.fontStyle = FontStyles.Bold;
        continueText.alignment = TextAlignmentOptions.Center;
        continueText.color = Color.white;
        continueText.raycastTarget = false;
        RectTransform ctRt = continueText.GetComponent<RectTransform>();
        ctRt.anchorMin = Vector2.zero;
        ctRt.anchorMax = Vector2.one;
        ctRt.offsetMin = Vector2.zero;
        ctRt.offsetMax = Vector2.zero;

        // Medidor en vivo de potencia del micrófono y dispositivo debajo del botón
        ScreamMeterUIUtils.CreateLabel(transform, "UpgradeMicCaption", new Vector2(-135f, -306f), new Vector2(270f, 22f),
            "<color=#94A3B8><b>NIVEL DEL MICRÓFONO</b></color>", 12, TextAlignmentOptions.MidlineLeft, sharedFont);
        micPromptTmp = ScreamMeterUIUtils.CreateLabel(transform, "UpgradeMicPrompt", new Vector2(135f, -306f), new Vector2(270f, 22f),
            "", 12, TextAlignmentOptions.MidlineRight, sharedFont);

        GameObject micTrack = ScreamMeterUIUtils.CreateBarTrack(transform, "UpgradeMicMeter", new Vector2(0f, -328f), new Vector2(540f, 18f),
            new Color(0.015f, 0.035f, 0.08f, 1f), new Color(0.45f, 0.65f, 0.85f, 0.65f), out micFillRect, out micFillImg, out _);

        micThresholdRect = ScreamMeterUIUtils.CreateThresholdMarker(micTrack.transform);

        micDeviceTmp = ScreamMeterUIUtils.CreateLabel(transform, "UpgradeMicDevice", new Vector2(0f, -352f), new Vector2(540f, 22f),
            "", 12, TextAlignmentOptions.Center, sharedFont);
    }

    private void RefreshScreamGateUI()
    {
        if (screamFillRect != null)
        {
            screamFillRect.anchorMax = new Vector2(Mathf.Clamp01(screamSession.Progress), 1f);
        }

        if (screamFillImg != null)
        {
            screamFillImg.color = screamUnlocked
                ? new Color(0.18f, 0.88f, 0.48f, 1f)
                : ScreamMeterUIUtils.EvaluateProgressColor(screamSession.Progress);
        }

        if (continueText != null)
        {
            if (screamUnlocked)
            {
                continueText.text = "¡ACTIVADO! TOCÁ PARA CONTINUAR ►";
                continueText.color = Color.white;
            }
            else
            {
                int pct = Mathf.RoundToInt(screamSession.Progress * 100f);
                continueText.text = wasCoopOnOpen
                    ? $"¡GRITEN PARA ACTIVAR CONTINUAR! ({pct}%)"
                    : $"¡GRITÁ PARA ACTIVAR CONTINUAR! ({pct}%)";
                continueText.color = new Color(0.95f, 0.98f, 1f, 0.96f);
            }
        }

        if (continueBorderEdges != null)
        {
            Color borderCol;
            if (screamUnlocked)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 7f);
                borderCol = Color.Lerp(new Color(0.22f, 0.98f, 0.56f, 1f), new Color(0.65f, 1f, 0.82f, 1f), pulse);
            }
            else if (screamSession.IsBlowing)
            {
                borderCol = new Color(1f, 0.18f, 0.52f, 1f);
            }
            else if (screamSession.ValidScream)
            {
                borderCol = new Color(0.28f, 0.98f, 0.64f, 1f);
            }
            else
            {
                borderCol = new Color(0.28f, 0.65f, 0.92f, 0.85f);
            }

            for (int i = 0; i < continueBorderEdges.Length; i++)
            {
                if (continueBorderEdges[i] != null) continueBorderEdges[i].color = borderCol;
            }
        }

        if (continueRect != null)
        {
            if (!screamUnlocked && screamSession.ValidScream)
            {
                float amp = Mathf.Lerp(1.2f, 4.2f, screamSession.SmoothedMicDisplay);
                continueRect.anchoredPosition = new Vector2(
                    Mathf.Sin(Time.unscaledTime * 68f) * amp,
                    -262f + Mathf.Cos(Time.unscaledTime * 54f) * amp * 0.6f
                );
            }
            else
            {
                continueRect.anchoredPosition = new Vector2(0f, -262f);
            }
        }

        ScreamMeterUIUtils.ApplyMicStatusUI(
            screamSession,
            wasCoopOnOpen,
            screamUnlocked,
            micFillRect,
            micFillImg,
            micPromptTmp,
            micDeviceTmp);
    }

    private void SetupCard(Button btn, TextMeshProUGUI costText, string title, Vector2 containerPos,
        Sprite artworkSprite, Color accentColor, out Image artImgRef, out TextMeshProUGUI titleTextRef)
    {
        artImgRef = null;
        titleTextRef = null;
        if (btn == null) return;

        // Contenedor padre de la tarjeta
        RectTransform containerRt = btn.transform.parent as RectTransform;
        if (containerRt != null && containerRt != transform)
        {
            containerRt.anchorMin = new Vector2(0.5f, 0.5f);
            containerRt.anchorMax = new Vector2(0.5f, 0.5f);
            containerRt.pivot = new Vector2(0.5f, 0.5f);
            containerRt.anchoredPosition = containerPos;
            containerRt.sizeDelta = new Vector2(240f, 380f);
        }

        // Rectángulo parado verticalmente (el botón mismo)
        RectTransform btnRt = btn.GetComponent<RectTransform>();
        btnRt.anchorMin = new Vector2(0.5f, 0.5f);
        btnRt.anchorMax = new Vector2(0.5f, 0.5f);
        btnRt.pivot = new Vector2(0.5f, 0.5f);
        btnRt.anchoredPosition = new Vector2(0f, 30f);
        btnRt.sizeDelta = new Vector2(230f, 320f);

        Image btnImg = btn.GetComponent<Image>();
        if (btnImg != null)
        {
            btnImg.sprite = s_CardBgSprite;
            btnImg.type = Image.Type.Simple;
            btnImg.color = Color.white;
        }

        // Configuración de tintes del botón
        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(0.70f, 0.70f, 0.70f, 1f); // Se oscurece al posar el mouse
        cb.pressedColor = new Color(0.48f, 0.48f, 0.48f, 1f);
        cb.selectedColor = Color.white;
        cb.fadeDuration = 0.08f;
        btn.colors = cb;

        // Crear/obtener área para el título en la parte superior del rectángulo
        Transform titleTransform = btn.transform.Find("TitleText");
        GameObject titleGo;
        if (titleTransform != null)
        {
            titleGo = titleTransform.gameObject;
        }
        else
        {
            titleGo = new GameObject("TitleText", typeof(RectTransform), typeof(CanvasRenderer));
            titleGo.transform.SetParent(btn.transform, false);
        }

        RectTransform titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0.5f, 0.5f);
        titleRt.anchorMax = new Vector2(0.5f, 0.5f);
        titleRt.pivot = new Vector2(0.5f, 0.5f);
        titleRt.anchoredPosition = new Vector2(0f, 122f);
        titleRt.sizeDelta = new Vector2(210f, 35f);

        TextMeshProUGUI tmpTitle = titleGo.GetComponent<TextMeshProUGUI>() ?? titleGo.AddComponent<TextMeshProUGUI>();
        if (costText != null)
        {
            tmpTitle.font = costText.font;
            tmpTitle.fontSharedMaterial = costText.fontSharedMaterial;
        }
        tmpTitle.text = title;
        tmpTitle.fontSize = 22;
        tmpTitle.fontStyle = FontStyles.Bold;
        tmpTitle.alignment = TextAlignmentOptions.Center;
        tmpTitle.color = accentColor;
        tmpTitle.raycastTarget = false;
        titleTextRef = tmpTitle;

        // Crear/obtener área de imagen dentro del rectángulo (donde irá la imagen futura)
        Transform artTransform = btn.transform.Find("ArtworkImage");
        GameObject artGo;
        if (artTransform != null)
        {
            artGo = artTransform.gameObject;
        }
        else
        {
            artGo = new GameObject("ArtworkImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            artGo.transform.SetParent(btn.transform, false);
        }

        RectTransform artRt = artGo.GetComponent<RectTransform>();
        artRt.anchorMin = new Vector2(0.5f, 0.5f);
        artRt.anchorMax = new Vector2(0.5f, 0.5f);
        artRt.pivot = new Vector2(0.5f, 0.5f);
        artRt.anchoredPosition = new Vector2(0f, -8f);
        artRt.sizeDelta = new Vector2(185f, 185f);

        Image artImg = artGo.GetComponent<Image>();
        artImg.sprite = artworkSprite;
        artImg.preserveAspect = true;
        artImg.raycastTarget = false; // Permite que los clics atraviesen hacia el botón
        artImgRef = artImg;

        // Componente de Hover para sonido y oscurecimiento
        UpgradeCardHover hover = btn.gameObject.GetComponent<UpgradeCardHover>() ?? btn.gameObject.AddComponent<UpgradeCardHover>();
        hover.Setup(artImg, btnImg);

        // Texto del precio debajo del rectángulo
        if (costText != null)
        {
            RectTransform costRt = costText.GetComponent<RectTransform>();
            costRt.anchorMin = new Vector2(0.5f, 0.5f);
            costRt.anchorMax = new Vector2(0.5f, 0.5f);
            costRt.pivot = new Vector2(0.5f, 0.5f);
            costRt.anchoredPosition = new Vector2(0f, -155f);
            costRt.sizeDelta = new Vector2(220f, 42f);

            costText.alignment = TextAlignmentOptions.Center;
            costText.fontSize = 26;
            costText.fontStyle = FontStyles.Bold;
            costText.color = new Color(1f, 0.88f, 0.25f, 1f);
            costText.raycastTarget = false;
        }
    }

    private void HandleContinueButtonClicked()
    {
        if (!screamUnlocked) return;
        TriggerContinue();
    }

    private void TriggerContinue()
    {
        if (hasContinued) return;
        hasContinued = true;

        screamSession.Stop();

        if (SoundController.Instance != null)
        {
            SoundController.Instance.PlayUpgradeContinueSfx();
        }

        OnContinue?.Invoke();
    }

    private int GetCost(int level)
    {
        return baseCost + (level * costIncreasePerLevel);
    }

    private void PurchaseDamage()
    {
        int cost = GetCost(damageLevel);
        if (GameManager.Instance == null) return;

        if (!GameManager.Instance.TrySpendPoints(cost))
        {
            if (SoundController.Instance != null)
            {
                SoundController.Instance.PlayUpgradeFailedSfx();
            }
            StartCoroutine(FlashPriceText(damageCostText));
            return;
        }

        damageLevel++;
        PlayerController p1 = playerController != null ? playerController : GameManager.Instance.player;
        if (p1 == null) p1 = FindAnyObjectByType<PlayerController>();
        if (p1 != null) p1.IncreaseDamage(damageIncreasePerLevel);

        if (GameManager.Instance.player2 != null && GameManager.Instance.player2 != p1)
        {
            GameManager.Instance.player2.IncreaseDamage(damageIncreasePerLevel);
        }
        RefreshUI();

        if (SoundController.Instance != null)
        {
            SoundController.Instance.PlayUpgradePurchasedSfx();
        }
    }

    private void PurchaseSpeed()
    {
        int cost = GetCost(speedLevel);
        if (GameManager.Instance == null) return;

        if (!GameManager.Instance.TrySpendPoints(cost))
        {
            if (SoundController.Instance != null)
            {
                SoundController.Instance.PlayUpgradeFailedSfx();
            }
            StartCoroutine(FlashPriceText(speedCostText));
            return;
        }

        speedLevel++;
        PlayerController p1 = playerController != null ? playerController : GameManager.Instance.player;
        if (p1 == null) p1 = FindAnyObjectByType<PlayerController>();
        if (p1 != null)
        {
            p1.IncreaseSpeed(speedIncreasePerLevel);
        }
        if (GameManager.Instance.player2 != null && GameManager.Instance.player2 != p1)
        {
            GameManager.Instance.player2.IncreaseSpeed(speedIncreasePerLevel);
        }
        RefreshUI();

        if (SoundController.Instance != null)
        {
            SoundController.Instance.PlayUpgradePurchasedSfx();
        }
    }

    private void PurchaseHealth()
    {
        PlayerController p1 = playerController != null ? playerController : GameManager.Instance?.player;
        if (p1 == null) p1 = FindAnyObjectByType<PlayerController>();

        // El máximo de vidas es 10. Si ya tiene 10 o más vidas, no se permite comprar
        bool isHealthMaxed = (p1 != null && p1.GetCurrentHealth() >= 10) &&
                             (GameManager.Instance == null || GameManager.Instance.player2 == null || GameManager.Instance.player2.GetCurrentHealth() >= 10);

        if (isHealthMaxed)
        {
            if (SoundController.Instance != null)
            {
                SoundController.Instance.PlayUpgradeFailedSfx();
            }
            StartCoroutine(FlashPriceText(healthCostText));
            return;
        }

        int cost = GetCost(healthLevel);
        if (GameManager.Instance == null) return;

        if (!GameManager.Instance.TrySpendPoints(cost))
        {
            if (SoundController.Instance != null)
            {
                SoundController.Instance.PlayUpgradeFailedSfx();
            }
            StartCoroutine(FlashPriceText(healthCostText));
            return;
        }

        healthLevel++;
        if (p1 != null) p1.IncreaseMaxHealth(healthIncreasePerLevel);
        if (GameManager.Instance.player2 != null && GameManager.Instance.player2 != p1)
        {
            GameManager.Instance.player2.IncreaseMaxHealth(healthIncreasePerLevel);
        }
        RefreshUI();

        if (SoundController.Instance != null)
        {
            SoundController.Instance.PlayUpgradePurchasedSfx();
        }
    }

    private System.Collections.IEnumerator FlashPriceText(TextMeshProUGUI text)
    {
        if (text == null) yield break;
        Color normalCol = new Color(1f, 0.88f, 0.25f, 1f);
        text.color = new Color(1f, 0.25f, 0.25f, 1f);

        float elapsed = 0f;
        while (elapsed < 0.28f)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (text != null)
        {
            text.color = normalCol;
        }
    }

    public void RefreshUI()
    {
        // Actualizar números de precio abajo de cada rectángulo
        if (damageCostText != null) damageCostText.text = $"{GetCost(damageLevel)} PTS";
        if (speedCostText != null) speedCostText.text = $"{GetCost(speedLevel)} PTS";

        bool isHealthMaxed = (playerController != null && playerController.GetCurrentHealth() >= 10) &&
                             (GameManager.Instance == null || GameManager.Instance.player2 == null || GameManager.Instance.player2.GetCurrentHealth() >= 10);
        if (healthButton != null)
        {
            healthButton.interactable = !isHealthMaxed;
        }

        if (healthCostText != null)
        {
            if (isHealthMaxed)
            {
                healthCostText.text = "<color=#94A3B8>MÁXIMO (10/10)</color>";
            }
            else
            {
                healthCostText.text = $"{GetCost(healthLevel)} PTS";
            }
        }

        // Actualizar sprites si se asignaron externamente
        if (damageArtImage != null && damageImage != null) damageArtImage.sprite = damageImage;
        if (healthArtImage != null && healthImage != null) healthArtImage.sprite = healthImage;
        if (speedArtImage != null && speedImage != null) speedArtImage.sprite = speedImage;
    }

    // --- GENERADORES PROCEDURALES DE TEXTURAS PARA TARJETAS ---

    private static Sprite GenerateCardBgSprite()
    {
        int w = 120;
        int h = 168;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[w * h];
        Color borderColor = new Color(0.22f, 0.45f, 0.75f, 0.95f);
        Color bodyColorTop = new Color(0.10f, 0.15f, 0.26f, 0.95f);
        Color bodyColorBot = new Color(0.05f, 0.08f, 0.15f, 0.95f);

        int r = 10;
        for (int y = 0; y < h; y++)
        {
            float vNorm = (float)y / h;
            for (int x = 0; x < w; x++)
            {
                bool isCorner = (x < r && y < r && (x - r) * (x - r) + (y - r) * (y - r) > r * r) ||
                                (x >= w - r && y < r && (x - (w - r)) * (x - (w - r)) + (y - r) * (y - r) > r * r) ||
                                (x < r && y >= h - r && (x - r) * (x - r) + (y - (h - r)) * (y - (h - r)) > r * r) ||
                                (x >= w - r && y >= h - r && (x - (w - r)) * (x - (w - r)) + (y - (h - r)) * (y - (h - r)) > r * r);

                if (isCorner)
                {
                    pixels[y * w + x] = Color.clear;
                }
                else
                {
                    bool isBorder = (x < 3 || x >= w - 3 || y < 3 || y >= h - 3);
                    if (isBorder)
                    {
                        pixels[y * w + x] = borderColor;
                    }
                    else
                    {
                        pixels[y * w + x] = Color.Lerp(bodyColorBot, bodyColorTop, vNorm);
                    }
                }
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
    }

    private static Sprite CreateIconSprite(Color glowColor, Color iconColor, System.Func<int, int, int, Vector2, bool> isPixelInShape)
    {
        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[size * size];
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                float glow = Mathf.Clamp01(1f - (dist / (size * 0.45f)));

                if (isPixelInShape(x, y, size, center))
                {
                    pixels[y * size + x] = iconColor;
                }
                else if (glow > 0.05f)
                {
                    pixels[y * size + x] = new Color(glowColor.r, glowColor.g, glowColor.b, glow * glow * 0.75f);
                }
                else
                {
                    pixels[y * size + x] = Color.clear;
                }
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    // Dibujo 1: Espadas cruzadas de Daño (naranja/rojo de combate)
    private static Sprite GenerateDamageIcon() => CreateIconSprite(
        new Color(1f, 0.35f, 0.15f),
        new Color(1f, 0.95f, 0.85f),
        (x, y, size, center) =>
        {
            float d = Vector2.Distance(new Vector2(x, y), center);
            if (d > 44f) return false;
            float diag1 = Mathf.Abs(x - y);
            float diag2 = Mathf.Abs(x - (size - y));
            return diag1 <= 4.5f || diag2 <= 4.5f;
        });

    // Dibujo 2: Cruz médica de Salud / Vida (verde esmeralda brillante)
    private static Sprite GenerateHealthIcon() => CreateIconSprite(
        new Color(0.1f, 0.85f, 0.35f),
        new Color(0.3f, 1f, 0.6f),
        (x, y, size, center) =>
        {
            float dx = Mathf.Abs(x - center.x);
            float dy = Mathf.Abs(y - center.y);
            return (dx <= 12f && dy <= 38f) || (dy <= 12f && dx <= 38f);
        });

    // Dibujo 3: Doble chevron aerodinámico de Velocidad (cian eléctrico)
    private static Sprite GenerateSpeedIcon() => CreateIconSprite(
        new Color(0.15f, 0.65f, 1f),
        new Color(0.7f, 0.95f, 1f),
        (x, y, size, center) =>
        {
            float relY = y - center.y;
            float ax = Mathf.Abs(x - center.x);
            return (Mathf.Abs(relY - (ax * 0.7f - 15f)) <= 6.5f && ax <= 36f) ||
                   (Mathf.Abs(relY - (ax * 0.7f + 15f)) <= 6.5f && ax <= 36f);
        });

    public int DamageLevel => damageLevel;
    public int SpeedLevel => speedLevel;
    public int HealthLevel => healthLevel;
}