using UnityEngine;
using UnityEngine.UI;
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
    [SerializeField] private float speedIncreasePerLevel = 150f;
    private int speedLevel = 0;

    [Header("Salud")]
    [SerializeField] private Button healthButton;
    [SerializeField] private TextMeshProUGUI healthCostText;
    [SerializeField] private int healthIncreasePerLevel = 1;
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
    [SerializeField] private int baseCost = 10;
    [SerializeField] private int costIncreasePerLevel = 5;

    [Header("Temporizador de Pantalla de Mejoras")]
    public float maxWaitTime = 20f;
    private float countdownTimer = 20f;
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

    private static Sprite s_CardBgSprite;
    private static Sprite s_DamageDefaultIcon;
    private static Sprite s_HealthDefaultIcon;
    private static Sprite s_SpeedDefaultIcon;

    void Awake()
    {
        SetupLayoutAndCards();
    }

    void OnEnable()
    {
        countdownTimer = maxWaitTime;
        hasContinued = false;
        RefreshUI();

        if (SoundController.Instance != null)
        {
            SoundController.Instance.PlayUpgradePanelOpenSfx();
        }
    }

    void Update()
    {
        if (hasContinued) return;

        // Se usa Time.unscaledDeltaTime porque Time.timeScale se encuentra en 0f durante el panel de mejoras
        countdownTimer -= Time.unscaledDeltaTime;

        if (countdownTimer <= 0f)
        {
            countdownTimer = 0f;
            TriggerContinue();
        }
    }

    void Start()
    {
        if (damageButton != null) damageButton.onClick.AddListener(PurchaseDamage);
        if (speedButton != null) speedButton.onClick.AddListener(PurchaseSpeed);
        if (healthButton != null) healthButton.onClick.AddListener(PurchaseHealth);
        if (continueButton != null) continueButton.onClick.AddListener(TriggerContinue);
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
            panelBg.color = new Color(0.02f, 0.05f, 0.10f, 0.82f);
        }

        // Crear sprites procedurales elegantes por defecto
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

        // 5. Configurar botón de Continuar abajo en el centro
        if (continueButton != null)
        {
            RectTransform contRt = continueButton.GetComponent<RectTransform>();
            contRt.anchorMin = new Vector2(0.5f, 0.5f);
            contRt.anchorMax = new Vector2(0.5f, 0.5f);
            contRt.pivot = new Vector2(0.5f, 0.5f);
            contRt.anchoredPosition = new Vector2(0f, -250f);
            contRt.sizeDelta = new Vector2(250f, 50f);

            Image contImg = continueButton.GetComponent<Image>();
            if (contImg != null)
            {
                contImg.color = new Color(0.12f, 0.22f, 0.35f, 0.95f);
            }

            TextMeshProUGUI contText = continueButton.GetComponentInChildren<TextMeshProUGUI>();
            if (contText != null)
            {
                contText.text = "CONTINUAR";
                contText.fontSize = 22;
                contText.fontStyle = FontStyles.Bold;
                contText.alignment = TextAlignmentOptions.Center;
                contText.color = Color.white;
            }
        }
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

    private void TriggerContinue()
    {
        if (hasContinued) return;
        hasContinued = true;

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
        if (playerController != null) playerController.IncreaseDamage(damageIncreasePerLevel);
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
        if (playerController != null) playerController.IncreaseSpeed(speedIncreasePerLevel);
        RefreshUI();

        if (SoundController.Instance != null)
        {
            SoundController.Instance.PlayUpgradePurchasedSfx();
        }
    }

    private void PurchaseHealth()
    {
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
        if (playerController != null) playerController.IncreaseMaxHealth(healthIncreasePerLevel);
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
        if (healthCostText != null) healthCostText.text = $"{GetCost(healthLevel)} PTS";

        // Actualizar sprites si se asignaron externamente
        if (damageArtImage != null && damageImage != null) damageArtImage.sprite = damageImage;
        if (healthArtImage != null && healthImage != null) healthArtImage.sprite = healthImage;
        if (speedArtImage != null && speedImage != null) speedArtImage.sprite = speedImage;
    }

    void OnGUI()
    {
        // Contador visual arcade en la parte superior central de la pantalla
        int secondsLeft = Mathf.CeilToInt(countdownTimer);
        string timerStr = $"Siguiente oleada en: {secondsLeft}s";

        float width = 360f;
        float height = 46f;
        float x = (Screen.width - width) * 0.5f;
        float y = 30f;

        Texture2D bgTex = Texture2D.whiteTexture;
        Color prevColor = GUI.color;

        // Fondo oscuro semitransparente
        GUI.color = new Color(0.04f, 0.08f, 0.16f, 0.90f);
        GUI.DrawTexture(new Rect(x, y, width, height), bgTex);

        // Borde estilizado (azul neón normal, rojo brillante cuando faltan 5 segundos o menos)
        Color borderColor = (secondsLeft <= 5) ? new Color(1f, 0.22f, 0.22f, 0.95f) : new Color(0.2f, 0.75f, 1f, 0.85f);
        GUI.color = borderColor;
        GUI.DrawTexture(new Rect(x, y, width, 2), bgTex);
        GUI.DrawTexture(new Rect(x, y + height - 2, width, 2), bgTex);
        GUI.DrawTexture(new Rect(x, y, 2, height), bgTex);
        GUI.DrawTexture(new Rect(x + width - 2, y, 2, height), bgTex);

        GUI.color = prevColor;

        GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.alignment = TextAnchor.MiddleCenter;
        labelStyle.fontSize = 20;
        labelStyle.fontStyle = FontStyle.Bold;
        labelStyle.normal.textColor = (secondsLeft <= 5) ? new Color(1f, 0.35f, 0.35f) : new Color(1f, 0.88f, 0.25f);

        GUI.Label(new Rect(x, y, width, height), timerStr, labelStyle);
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

    private static Sprite GenerateDamageIcon()
    {
        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[size * size];
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 pos = new Vector2(x, y);
                float dist = Vector2.Distance(pos, center);

                // Círculo de brillo rojo/naranja
                float glow = Mathf.Clamp01(1f - (dist / (size * 0.45f)));
                glow = glow * glow;

                // Hoja / espada diagonal
                float diag1 = Mathf.Abs((x - y));
                float diag2 = Mathf.Abs((x - (size - y)));
                bool isBlade = (diag1 <= 4f && dist < 45f) || (diag2 <= 4f && dist < 45f);

                if (isBlade)
                {
                    pixels[y * size + x] = new Color(1f, 0.95f, 0.85f, 1f);
                }
                else if (glow > 0.05f)
                {
                    pixels[y * size + x] = new Color(1f, 0.35f, 0.15f, glow * 0.85f);
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

    private static Sprite GenerateHealthIcon()
    {
        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[size * size];
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 pos = new Vector2(x, y);
                float dist = Vector2.Distance(pos, center);

                // Cruz médica verde/esmeralda brillante
                float dx = Mathf.Abs(x - center.x);
                float dy = Mathf.Abs(y - center.y);
                bool inCross = (dx <= 12f && dy <= 38f) || (dy <= 12f && dx <= 38f);

                float glow = Mathf.Clamp01(1f - (dist / (size * 0.45f)));
                glow = glow * glow;

                if (inCross)
                {
                    pixels[y * size + x] = new Color(0.2f, 1f, 0.5f, 1f);
                }
                else if (glow > 0.05f)
                {
                    pixels[y * size + x] = new Color(0.1f, 0.85f, 0.35f, glow * 0.65f);
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

    private static Sprite GenerateSpeedIcon()
    {
        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[size * size];
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 pos = new Vector2(x, y);
                float dist = Vector2.Distance(pos, center);

                // Rayo / chevron aerodinámico cian
                float relY = y - center.y;
                float arrowX = Mathf.Abs(x - center.x);
                bool inArrow1 = Mathf.Abs(relY - (arrowX * 0.7f - 15f)) <= 7f && arrowX <= 38f;
                bool inArrow2 = Mathf.Abs(relY - (arrowX * 0.7f + 15f)) <= 7f && arrowX <= 38f;

                float glow = Mathf.Clamp01(1f - (dist / (size * 0.45f)));
                glow = glow * glow;

                if (inArrow1 || inArrow2)
                {
                    pixels[y * size + x] = new Color(0.7f, 0.95f, 1f, 1f);
                }
                else if (glow > 0.05f)
                {
                    pixels[y * size + x] = new Color(0.15f, 0.65f, 1f, glow * 0.75f);
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

    public int DamageLevel => damageLevel;
    public int SpeedLevel => speedLevel;
    public int HealthLevel => healthLevel;
}