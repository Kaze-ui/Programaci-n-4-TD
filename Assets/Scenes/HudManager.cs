using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class HUDManager : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI healthText;
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI waveText;
    [SerializeField] private TextMeshProUGUI timerText;

    private GameObject bannerObj;
    private RectTransform bannerPanelRt;
    private CanvasGroup bannerCanvasGroup;
    private Image bannerTopAccent;
    private Image bannerBottomAccent;
    private Image bannerLeftBracket;
    private Image bannerRightBracket;
    private Image bannerDividerBar;
    private TextMeshProUGUI bannerEyebrowText;
    private TextMeshProUGUI bannerTitleShadowText;
    private TextMeshProUGUI bannerTitleText;
    private TextMeshProUGUI bannerSubText;
    private Coroutine bannerCoroutine;
    private TextMeshProUGUI p2PromptText;

    // --- TARJETAS MODERNAS DEL HUD ---
    private GameObject healthCardObj;
    private Image healthAccentBar;
    private Image healthMiniFillBar;
    private GameObject waveCardObj;
    private Image waveAccentBar;
    private GameObject scoreCardObj;
    private GameObject timerCardObj;
    private Image timerAccentBar;
    private GameObject p2CardObj;
    private Image p2AccentBar;
    private Image p2MiniFillBar;
    private GameObject p2MiniBarBgObj;
    private static Sprite cachedHudWhiteSprite;

    // --- ALERTA EMERGENTE FASE 2 ("NUEVOS ENEMIGOS DETECTADOS") ---
    private GameObject phase2AlertObj;
    private RectTransform phase2AlertBandRt;
    private CanvasGroup phase2AlertCanvasGroup;
    private Image phase2AlertBgImage;
    private Image phase2AlertTopLine;
    private Image phase2AlertBottomLine;
    private TextMeshProUGUI phase2AlertEyebrowText;
    private TextMeshProUGUI phase2AlertShadowText;
    private TextMeshProUGUI phase2AlertText;
    private TextMeshProUGUI phase2AlertSubText;
    private Coroutine phase2AlertCoroutine;

    // --- CARTELITO EMERGENTE DE CAMBIO DE ARMA (1P abajo-izq / 2P abajo-der) ---
    private sealed class WeaponToastUI
    {
        public GameObject rootObj;
        public RectTransform rt;
        public CanvasGroup canvasGroup;
        public Image accentBar;
        public Image iconFrameBg;
        public Image iconImage;
        public TextMeshProUGUI eyebrowText;
        public TextMeshProUGUI weaponNameText;
        public Coroutine routine;
        public float hiddenY;
        public float shownY;
    }

    private WeaponToastUI p1WeaponToast;
    private WeaponToastUI p2WeaponToast;
    private static Sprite cachedBlastersIconSprite;
    private static Sprite cachedPlasmaIconSprite;

    // --- MÍTICA BARRA DE VIDA DEL JEFE ---
    private bool isBossBarActive = false;
    private bool isBossPhase2 = false;
    private bool isBossInvulnerable = false;
    private string bossName = "Leviatán nodriza";
    private float bossMaxHealth = 500f;
    private float bossCurrentHealth = 500f;
    private float bossDisplayFill = 0f;       // Fill de la barra animada
    private float bossTrailingFill = 0f;      // Barra fantasma amarilla que retiene el daño
    private float bossBarAlpha = 0f;          // Opacidad de entrada/salida
    private float lastDamageTime = 0f;        // Tiempo del último impacto para el delay
    private bool isBossDefeated = false;

    void Awake()
    {
        // Desactiva el Image blanco semitransparente por defecto del panel de HUD
        UnityEngine.UI.Image bgImage = GetComponent<UnityEngine.UI.Image>();
        if (bgImage != null)
        {
            bgImage.enabled = false;
        }

        SetupHudLayout();
        EnsureBannerUI();
    }

    private static Sprite GetHudWhiteSprite()
    {
        if (cachedHudWhiteSprite != null) return cachedHudWhiteSprite;
        Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        for (int y = 0; y < 2; y++)
            for (int x = 0; x < 2; x++)
                tex.SetPixel(x, y, Color.white);
        tex.Apply();
        cachedHudWhiteSprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
        return cachedHudWhiteSprite;
    }

    private GameObject BuildModernHudCard(
        string cardName,
        Transform parent,
        Vector2 anchor,
        Vector2 pivot,
        Vector2 anchoredPos,
        Vector2 size,
        Color accentColor,
        bool accentOnLeft,
        TextMeshProUGUI targetText,
        TextAlignmentOptions alignment,
        float fontSize,
        out Image accentBarImg,
        out Image miniFillBarImg,
        out GameObject miniBarBgObj,
        bool includeMiniHealthBar = false)
    {
        Sprite whiteSp = GetHudWhiteSprite();

        GameObject card = new GameObject(cardName, typeof(RectTransform));
        card.transform.SetParent(parent, false);
        RectTransform cardRt = card.GetComponent<RectTransform>();
        cardRt.anchorMin = anchor;
        cardRt.anchorMax = anchor;
        cardRt.pivot = pivot;
        cardRt.anchoredPosition = anchoredPos;
        cardRt.sizeDelta = size;

        // Fondo oscuro translúcido estilo vidrio táctico
        GameObject bgObj = new GameObject("CardBg", typeof(RectTransform));
        bgObj.transform.SetParent(card.transform, false);
        RectTransform bgRt = bgObj.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;
        Image bgImg = bgObj.AddComponent<Image>();
        bgImg.sprite = whiteSp;
        bgImg.color = new Color(0.03f, 0.06f, 0.13f, 0.78f);
        bgImg.raycastTarget = false;

        // Línea sutil superior (brillo glass)
        GameObject topLine = new GameObject("TopEdge", typeof(RectTransform));
        topLine.transform.SetParent(card.transform, false);
        RectTransform topRt = topLine.GetComponent<RectTransform>();
        topRt.anchorMin = new Vector2(0f, 1f);
        topRt.anchorMax = new Vector2(1f, 1f);
        topRt.pivot = new Vector2(0.5f, 1f);
        topRt.anchoredPosition = Vector2.zero;
        topRt.sizeDelta = new Vector2(0f, 1.5f);
        Image topImg = topLine.AddComponent<Image>();
        topImg.sprite = whiteSp;
        topImg.color = new Color(0.60f, 0.82f, 1f, 0.22f);
        topImg.raycastTarget = false;

        // Línea sutil inferior
        GameObject botLine = new GameObject("BottomEdge", typeof(RectTransform));
        botLine.transform.SetParent(card.transform, false);
        RectTransform botRt = botLine.GetComponent<RectTransform>();
        botRt.anchorMin = new Vector2(0f, 0f);
        botRt.anchorMax = new Vector2(1f, 0f);
        botRt.pivot = new Vector2(0.5f, 0f);
        botRt.anchoredPosition = Vector2.zero;
        botRt.sizeDelta = new Vector2(0f, 1.5f);
        Image botImg = botLine.AddComponent<Image>();
        botImg.sprite = whiteSp;
        botImg.color = new Color(0.25f, 0.45f, 0.75f, 0.28f);
        botImg.raycastTarget = false;

        // Franja de acento neón lateral
        GameObject accentObj = new GameObject("AccentBar", typeof(RectTransform));
        accentObj.transform.SetParent(card.transform, false);
        RectTransform accRt = accentObj.GetComponent<RectTransform>();
        if (accentOnLeft)
        {
            accRt.anchorMin = new Vector2(0f, 0f);
            accRt.anchorMax = new Vector2(0f, 1f);
            accRt.pivot = new Vector2(0f, 0.5f);
            accRt.anchoredPosition = Vector2.zero;
            accRt.sizeDelta = new Vector2(4f, 0f);
        }
        else
        {
            accRt.anchorMin = new Vector2(1f, 0f);
            accRt.anchorMax = new Vector2(1f, 1f);
            accRt.pivot = new Vector2(1f, 0.5f);
            accRt.anchoredPosition = Vector2.zero;
            accRt.sizeDelta = new Vector2(4f, 0f);
        }
        accentBarImg = accentObj.AddComponent<Image>();
        accentBarImg.sprite = whiteSp;
        accentBarImg.color = accentColor;
        accentBarImg.raycastTarget = false;

        miniFillBarImg = null;
        miniBarBgObj = null;
        if (includeMiniHealthBar)
        {
            miniBarBgObj = new GameObject("MiniHpBarBg", typeof(RectTransform));
            miniBarBgObj.transform.SetParent(card.transform, false);
            RectTransform mBgRt = miniBarBgObj.GetComponent<RectTransform>();
            mBgRt.anchorMin = new Vector2(0f, 0f);
            mBgRt.anchorMax = new Vector2(1f, 0f);
            mBgRt.pivot = new Vector2(0.5f, 0f);
            mBgRt.anchoredPosition = new Vector2(0f, 3f);
            mBgRt.sizeDelta = new Vector2(-18f, 4f);
            Image mBgImg = miniBarBgObj.AddComponent<Image>();
            mBgImg.sprite = whiteSp;
            mBgImg.color = new Color(0.15f, 0.20f, 0.30f, 0.90f);
            mBgImg.raycastTarget = false;

            GameObject fillObj = new GameObject("MiniHpBarFill", typeof(RectTransform));
            fillObj.transform.SetParent(miniBarBgObj.transform, false);
            RectTransform fillRt = fillObj.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;
            miniFillBarImg = fillObj.AddComponent<Image>();
            miniFillBarImg.sprite = whiteSp;
            miniFillBarImg.type = Image.Type.Filled;
            miniFillBarImg.fillMethod = Image.FillMethod.Horizontal;
            miniFillBarImg.fillOrigin = accentOnLeft ? (int)Image.OriginHorizontal.Left : (int)Image.OriginHorizontal.Right;
            miniFillBarImg.fillAmount = 1f;
            miniFillBarImg.color = accentColor;
            miniFillBarImg.raycastTarget = false;
        }

        if (targetText != null)
        {
            targetText.transform.SetParent(card.transform, false);
            RectTransform trt = targetText.rectTransform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.pivot = new Vector2(0.5f, 0.5f);
            trt.offsetMin = new Vector2(accentOnLeft ? 14f : 10f, includeMiniHealthBar ? 7f : 2f);
            trt.offsetMax = new Vector2(accentOnLeft ? -10f : -14f, -2f);

            targetText.alignment = alignment;
            targetText.fontSize = fontSize;
            targetText.fontStyle = FontStyles.Bold;
            targetText.characterSpacing = 1.4f;
            targetText.enableAutoSizing = true;
            targetText.fontSizeMin = 14f;
            targetText.fontSizeMax = fontSize;
            targetText.textWrappingMode = TextWrappingModes.NoWrap;
            targetText.raycastTarget = false;
        }

        return card;
    }

    private void SetupHudLayout()
    {
        Transform hudParent = transform;

        // 1. INTEGRIDAD / VIDA (Superior izquierda, fila 1 - deja el centro libre para la barra del jefe)
        if (healthText != null)
        {
            healthCardObj = BuildModernHudCard(
                "HudCard_Health", hudParent,
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(18f, -14f), new Vector2(246f, 44f),
                new Color(0.06f, 0.86f, 0.78f, 1f), true,
                healthText, TextAlignmentOptions.MidlineLeft, 22f,
                out healthAccentBar, out healthMiniFillBar, out _, true);
        }

        // 2. OLEADA / AMENAZA (Superior izquierda, fila 2)
        if (waveText != null)
        {
            waveCardObj = BuildModernHudCard(
                "HudCard_Wave", hudParent,
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(18f, -66f), new Vector2(246f, 38f),
                new Color(0.45f, 0.54f, 0.98f, 1f), true,
                waveText, TextAlignmentOptions.MidlineLeft, 20f,
                out waveAccentBar, out _, out _, false);
        }

        // 3. CRÉDITOS (Superior derecha, fila 1)
        if (scoreText != null)
        {
            scoreCardObj = BuildModernHudCard(
                "HudCard_Credits", hudParent,
                new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-18f, -14f), new Vector2(246f, 44f),
                new Color(0.98f, 0.76f, 0.16f, 1f), false,
                scoreText, TextAlignmentOptions.MidlineRight, 22f,
                out _, out _, out _, false);
        }

        // 4. TIEMPO DE OLEADA (Superior derecha, fila 2 - se oculta en Jefe y Mejoras)
        if (timerText != null)
        {
            timerCardObj = BuildModernHudCard(
                "HudCard_Timer", hudParent,
                new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-18f, -66f), new Vector2(246f, 38f),
                new Color(0.98f, 0.56f, 0.22f, 1f), false,
                timerText, TextAlignmentOptions.MidlineRight, 20f,
                out timerAccentBar, out _, out _, false);
        }

        EnsureP2PromptUI();
    }

    private void EnsureP2PromptUI()
    {
        if (p2PromptText != null) return;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        Transform parent = transform.parent != null ? transform.parent : canvas.transform;
        GameObject p2TextObj = new GameObject("P2PromptText", typeof(RectTransform));
        p2PromptText = p2TextObj.AddComponent<TextMeshProUGUI>();
        if (healthText != null && healthText.font != null) p2PromptText.font = healthText.font;

        p2CardObj = BuildModernHudCard(
            "HudCard_P2", parent,
            new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-18f, 18f), new Vector2(290f, 44f),
            new Color(0.98f, 0.36f, 0.48f, 1f), false,
            p2PromptText, TextAlignmentOptions.MidlineRight, 20f,
            out p2AccentBar, out p2MiniFillBar, out p2MiniBarBgObj, true);

        p2PromptText.text = "";
        if (p2CardObj != null) p2CardObj.SetActive(false);
    }

    public void UpdateP2Status(bool isConnected, bool isJoined, long currentHealth, long maxHealth)
    {
        EnsureP2PromptUI();
        if (p2PromptText == null) return;

        if (isJoined)
        {
            if (p2CardObj != null) p2CardObj.SetActive(true);
            RectTransform cardRt = p2CardObj != null ? p2CardObj.GetComponent<RectTransform>() : null;
            if (cardRt != null) cardRt.sizeDelta = new Vector2(268f, 44f);
            if (p2MiniBarBgObj != null) p2MiniBarBgObj.SetActive(true);

            float pct = maxHealth > 0 ? Mathf.Clamp01((float)currentHealth / maxHealth) : 0f;
            if (p2MiniFillBar != null)
            {
                p2MiniFillBar.fillAmount = pct;
                p2MiniFillBar.color = pct <= 0.25f ? new Color(1f, 0.22f, 0.26f, 1f) : new Color(0.98f, 0.38f, 0.50f, 1f);
            }
            if (p2AccentBar != null)
            {
                p2AccentBar.color = currentHealth <= 0 ? new Color(0.95f, 0.20f, 0.22f, 1f) : new Color(0.98f, 0.38f, 0.50f, 1f);
            }

            if (currentHealth <= 0)
            {
                p2PromptText.text = "<size=62%><color=#F87171><b>2P ESTADO</b></color></size>  <color=#FCA5A5><b>CAÍDO</b></color>";
            }
            else
            {
                string labelColor = currentHealth <= 2 ? "#FF3B30" : "#FB7185";
                p2PromptText.text = $"<size=62%><color={labelColor}><b>2P VIDA</b></color></size>  <color=#F8FAFC><b>{currentHealth}</b></color><color=#64748B>/{maxHealth}</color>";
            }
        }
        else if (isConnected)
        {
            if (p2CardObj != null) p2CardObj.SetActive(true);
            RectTransform cardRt = p2CardObj != null ? p2CardObj.GetComponent<RectTransform>() : null;
            if (p2MiniBarBgObj != null) p2MiniBarBgObj.SetActive(false);
            if (p2AccentBar != null) p2AccentBar.color = new Color(0.98f, 0.82f, 0.20f, 1f);

            if (GameManager.GetConnectedGamepadCount() >= 2)
            {
                if (cardRt != null) cardRt.sizeDelta = new Vector2(430f, 40f);
                p2PromptText.text = "<size=66%><color=#38BDF8><b>1P</b></color></size> <color=#CBD5E1>Mando 1</color>  <color=#475569>|</color>  <size=66%><color=#FACC15><b>2P</b></color></size> <color=#F8FAFC>Pulsa <b>START</b></color>";
            }
            else
            {
                if (cardRt != null) cardRt.sizeDelta = new Vector2(315f, 40f);
                p2PromptText.text = "<size=66%><color=#FACC15><b>CO-OP 2P</b></color></size>  <color=#E2E8F0>Pulsa <b>START</b></color>";
            }
        }
        else
        {
            p2PromptText.text = "";
            if (p2CardObj != null) p2CardObj.SetActive(false);
        }
    }

    public void UpdateP2Health(long current, long max)
    {
        UpdateP2Status(true, true, current, max);
    }

    void Update()
    {
        if (GameManager.Instance != null &&
            (GameManager.Instance.currentState == GameManager.GameState.Won ||
             GameManager.Instance.currentState == GameManager.GameState.Lost ||
             GameManager.Instance.IsAwaitingScreamRevive))
        {
            HideWaveBannerImmediately();
            HidePhase2AlertImmediately();
            HideWeaponToastsImmediately();
        }

        if (isBossBarActive)
        {
            float targetPct = Mathf.Clamp01(bossCurrentHealth / Mathf.Max(1f, bossMaxHealth));

            if (isBossDefeated)
            {
                // Se desvanece suavemente al derrotar al jefe
                bossBarAlpha = Mathf.MoveTowards(bossBarAlpha, 0f, Time.deltaTime * 1.5f);
                if (bossBarAlpha <= 0f) isBossBarActive = false;
            }
            else
            {
                // Aparece de manera suave y fluida
                bossBarAlpha = Mathf.MoveTowards(bossBarAlpha, 1f, Time.deltaTime * 2.5f);

                // La barra principal sube al inicio o baja al recibir daño
                bossDisplayFill = Mathf.MoveTowards(bossDisplayFill, targetPct, Time.deltaTime * 0.9f);

                // La barra fantasma amarilla espera 0.25s y luego persigue a la barra principal
                if (Time.time - lastDamageTime > 0.25f)
                {
                    bossTrailingFill = Mathf.Lerp(bossTrailingFill, bossDisplayFill, Time.deltaTime * 5f);
                }
            }
        }
    }

    public void UpdateHealth(long current, long max, bool hasPlayer2 = false)
    {
        if (healthText != null)
        {
            float pct = max > 0 ? Mathf.Clamp01((float)current / max) : 0f;
            string prefix = hasPlayer2 ? "1P VIDA" : "VIDA";
            string labelColor = pct <= 0.25f ? "#FF4D6D" : (pct <= 0.50f ? "#FBBF24" : "#2DD4BF");
            Color barCol = pct <= 0.25f
                ? new Color(1f, 0.28f, 0.40f, 1f)
                : (pct <= 0.50f ? new Color(0.98f, 0.74f, 0.16f, 1f) : new Color(0.12f, 0.88f, 0.80f, 1f));

            healthText.text = $"<size=62%><color={labelColor}><b>{prefix}</b></color></size>  <color=#F8FAFC><b>{current}</b></color><color=#64748B>/{max}</color>";

            if (healthMiniFillBar != null)
            {
                healthMiniFillBar.fillAmount = pct;
                healthMiniFillBar.color = barCol;
            }
            if (healthAccentBar != null)
            {
                healthAccentBar.color = barCol;
            }
        }
    }

    public void UpdateHealth(long current, long max)
    {
        bool has2P = GameManager.Instance != null && GameManager.Instance.IsPlayer2Joined;
        UpdateHealth(current, max, has2P);
    }

    public void UpdateHealth(int current, int max)
    {
        UpdateHealth((long)current, (long)max);
    }

    public void UpdateScore(int score)
    {
        if (scoreText != null)
            scoreText.text = $"<size=62%><color=#FBBF24><b>CRÉDITOS</b></color></size>  <color=#FFFBEB><b>{score}</b></color> <size=70%><color=#F59E0B><b>CR</b></color></size>";
    }

    public void UpdateWave(int current, int max)
    {
        if (waveText != null)
        {
            waveText.text = $"<size=62%><color=#818CF8><b>OLEADA</b></color></size>  <color=#F8FAFC><b>{current}</b></color><color=#64748B>/{max}</color>";
            if (waveAccentBar != null) waveAccentBar.color = new Color(0.45f, 0.54f, 0.98f, 1f);
        }
    }

    public void SetWaveText(string text)
    {
        if (waveText != null)
        {
            bool isBoss = !string.IsNullOrEmpty(text) && (text.Contains("LEVIAT") || text.Contains("JEFE") || text.Contains("NODRIZA"));
            if (isBoss)
            {
                waveText.text = "<size=60%><color=#FB7185><b>AMENAZA</b></color></size>  <color=#FFF1F2><b>JEFE FINAL</b></color>";
                if (waveAccentBar != null) waveAccentBar.color = new Color(0.98f, 0.25f, 0.36f, 1f);
            }
            else
            {
                waveText.text = $"<size=68%><color=#38BDF8><b>{text}</b></color></size>";
                if (waveAccentBar != null) waveAccentBar.color = new Color(0.22f, 0.74f, 0.97f, 1f);
            }
        }
    }

    private bool isTimerHiddenForUpgrade = false;

    public void HideTimerForUpgrade(bool hide)
    {
        isTimerHiddenForUpgrade = hide;
        bool visible = !hide && (!isBossBarActive || isBossDefeated);
        if (timerCardObj != null) timerCardObj.SetActive(visible);
        if (timerText != null) timerText.gameObject.SetActive(visible);
    }

    public void UpdateTimer(float secondsRemaining)
    {
        if (isTimerHiddenForUpgrade)
        {
            if (timerCardObj != null) timerCardObj.SetActive(false);
            if (timerText != null) timerText.gameObject.SetActive(false);
            return;
        }

        if (timerText != null)
        {
            // Si la barra del jefe está activa, ocultar tarjeta y texto del cronómetro
            if (isBossBarActive && !isBossDefeated)
            {
                timerText.text = "";
                if (timerCardObj != null) timerCardObj.SetActive(false);
                return;
            }

            if (timerCardObj != null && !timerCardObj.activeSelf) timerCardObj.SetActive(true);
            if (!timerText.gameObject.activeSelf) timerText.gameObject.SetActive(true);

            int totalSeconds = Mathf.CeilToInt(Mathf.Max(0f, secondsRemaining));
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            bool urgent = secondsRemaining <= 10f;
            string timerColor = urgent ? "#FF4D6D" : "#FB923C";
            timerText.text = $"<size=62%><color={timerColor}><b>TIEMPO</b></color></size>  <color=#F8FAFC><b>{minutes:00}:{seconds:00}</b></color>";
            if (timerAccentBar != null)
            {
                timerAccentBar.color = urgent ? new Color(1f, 0.26f, 0.36f, 1f) : new Color(0.98f, 0.56f, 0.22f, 1f);
            }
        }
    }

    private void EnsureBannerUI()
    {
        if (bannerObj != null) return;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        Sprite whiteSp = GetHudWhiteSprite();
        TMP_FontAsset hudFont = (healthText != null && healthText.font != null) ? healthText.font : null;

        bannerObj = new GameObject("WaveBannerContainer", typeof(RectTransform), typeof(CanvasGroup));
        bannerObj.transform.SetParent(canvas.transform, false);
        // Colocar al inicio del Canvas para que los paneles modales (UpgradePanel, GameOverPanel) se superpongan encima
        bannerObj.transform.SetSiblingIndex(0);

        RectTransform rt = bannerObj.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        bannerCanvasGroup = bannerObj.GetComponent<CanvasGroup>();
        bannerCanvasGroup.alpha = 0f;
        bannerCanvasGroup.blocksRaycasts = false;
        bannerCanvasGroup.interactable = false;

        // Tarjeta táctica central estilizada
        GameObject panelObj = new GameObject("BannerPanel", typeof(RectTransform));
        panelObj.transform.SetParent(bannerObj.transform, false);
        bannerPanelRt = panelObj.GetComponent<RectTransform>();
        bannerPanelRt.anchorMin = new Vector2(0.5f, 0.5f);
        bannerPanelRt.anchorMax = new Vector2(0.5f, 0.5f);
        bannerPanelRt.pivot = new Vector2(0.5f, 0.5f);
        bannerPanelRt.anchoredPosition = new Vector2(0f, 65f);
        bannerPanelRt.sizeDelta = new Vector2(760f, 174f);

        // Fondo oscuro translúcido tipo vidrio táctico
        GameObject bgObj = new GameObject("BannerBg", typeof(RectTransform));
        bgObj.transform.SetParent(panelObj.transform, false);
        RectTransform bgRt = bgObj.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;
        Image bgImg = bgObj.AddComponent<Image>();
        bgImg.sprite = whiteSp;
        bgImg.color = new Color(0.03f, 0.06f, 0.13f, 0.84f);
        bgImg.raycastTarget = false;

        // Franja superior neón
        GameObject topLineObj = new GameObject("BannerTopAccent", typeof(RectTransform));
        topLineObj.transform.SetParent(panelObj.transform, false);
        RectTransform topRt = topLineObj.GetComponent<RectTransform>();
        topRt.anchorMin = new Vector2(0f, 1f);
        topRt.anchorMax = new Vector2(1f, 1f);
        topRt.pivot = new Vector2(0.5f, 1f);
        topRt.anchoredPosition = Vector2.zero;
        topRt.sizeDelta = new Vector2(0f, 3f);
        bannerTopAccent = topLineObj.AddComponent<Image>();
        bannerTopAccent.sprite = whiteSp;
        bannerTopAccent.color = new Color(0.22f, 0.78f, 1f, 0.95f);
        bannerTopAccent.raycastTarget = false;

        // Franja inferior neón
        GameObject botLineObj = new GameObject("BannerBottomAccent", typeof(RectTransform));
        botLineObj.transform.SetParent(panelObj.transform, false);
        RectTransform botRt = botLineObj.GetComponent<RectTransform>();
        botRt.anchorMin = new Vector2(0f, 0f);
        botRt.anchorMax = new Vector2(1f, 0f);
        botRt.pivot = new Vector2(0.5f, 0f);
        botRt.anchoredPosition = Vector2.zero;
        botRt.sizeDelta = new Vector2(0f, 3f);
        bannerBottomAccent = botLineObj.AddComponent<Image>();
        bannerBottomAccent.sprite = whiteSp;
        bannerBottomAccent.color = new Color(0.22f, 0.78f, 1f, 0.95f);
        bannerBottomAccent.raycastTarget = false;

        // Corchetes laterales luminosos (izquierda y derecha)
        GameObject leftBrObj = new GameObject("BannerLeftBracket", typeof(RectTransform));
        leftBrObj.transform.SetParent(panelObj.transform, false);
        RectTransform lbRt = leftBrObj.GetComponent<RectTransform>();
        lbRt.anchorMin = new Vector2(0f, 0f);
        lbRt.anchorMax = new Vector2(0f, 1f);
        lbRt.pivot = new Vector2(0f, 0.5f);
        lbRt.anchoredPosition = Vector2.zero;
        lbRt.sizeDelta = new Vector2(6f, 0f);
        bannerLeftBracket = leftBrObj.AddComponent<Image>();
        bannerLeftBracket.sprite = whiteSp;
        bannerLeftBracket.color = new Color(0.22f, 0.78f, 1f, 1f);
        bannerLeftBracket.raycastTarget = false;

        GameObject rightBrObj = new GameObject("BannerRightBracket", typeof(RectTransform));
        rightBrObj.transform.SetParent(panelObj.transform, false);
        RectTransform rbRt = rightBrObj.GetComponent<RectTransform>();
        rbRt.anchorMin = new Vector2(1f, 0f);
        rbRt.anchorMax = new Vector2(1f, 1f);
        rbRt.pivot = new Vector2(1f, 0.5f);
        rbRt.anchoredPosition = Vector2.zero;
        rbRt.sizeDelta = new Vector2(6f, 0f);
        bannerRightBracket = rightBrObj.AddComponent<Image>();
        bannerRightBracket.sprite = whiteSp;
        bannerRightBracket.color = new Color(0.22f, 0.78f, 1f, 1f);
        bannerRightBracket.raycastTarget = false;

        // Etiqueta superior táctica (eyebrow)
        GameObject eyebrowObj = new GameObject("BannerEyebrow", typeof(RectTransform));
        eyebrowObj.transform.SetParent(panelObj.transform, false);
        RectTransform eyeRt = eyebrowObj.GetComponent<RectTransform>();
        eyeRt.anchorMin = new Vector2(0f, 1f);
        eyeRt.anchorMax = new Vector2(1f, 1f);
        eyeRt.pivot = new Vector2(0.5f, 1f);
        eyeRt.anchoredPosition = new Vector2(0f, -12f);
        eyeRt.sizeDelta = new Vector2(-40f, 26f);
        bannerEyebrowText = eyebrowObj.AddComponent<TextMeshProUGUI>();
        if (hudFont != null) bannerEyebrowText.font = hudFont;
        bannerEyebrowText.alignment = TextAlignmentOptions.Center;
        bannerEyebrowText.fontSize = 16;
        bannerEyebrowText.fontStyle = FontStyles.Bold;
        bannerEyebrowText.characterSpacing = 5.5f;
        bannerEyebrowText.color = new Color(0.50f, 0.88f, 1f, 0.92f);
        bannerEyebrowText.textWrappingMode = TextWrappingModes.NoWrap;
        bannerEyebrowText.raycastTarget = false;

        // Sombra del título principal
        GameObject shadowObj = new GameObject("BannerTitleShadow", typeof(RectTransform));
        shadowObj.transform.SetParent(panelObj.transform, false);
        RectTransform shRt = shadowObj.GetComponent<RectTransform>();
        shRt.anchorMin = new Vector2(0f, 0.5f);
        shRt.anchorMax = new Vector2(1f, 0.5f);
        shRt.pivot = new Vector2(0.5f, 0.5f);
        shRt.anchoredPosition = new Vector2(3f, 8f);
        shRt.sizeDelta = new Vector2(-40f, 68f);
        bannerTitleShadowText = shadowObj.AddComponent<TextMeshProUGUI>();
        if (hudFont != null) bannerTitleShadowText.font = hudFont;
        bannerTitleShadowText.alignment = TextAlignmentOptions.Center;
        bannerTitleShadowText.fontSize = 52;
        bannerTitleShadowText.fontStyle = FontStyles.Bold;
        bannerTitleShadowText.characterSpacing = 4f;
        bannerTitleShadowText.color = new Color(0f, 0f, 0f, 0.88f);
        bannerTitleShadowText.enableAutoSizing = true;
        bannerTitleShadowText.fontSizeMin = 28;
        bannerTitleShadowText.fontSizeMax = 52;
        bannerTitleShadowText.textWrappingMode = TextWrappingModes.NoWrap;
        bannerTitleShadowText.raycastTarget = false;

        // Título principal
        GameObject titleObj = new GameObject("BannerTitle", typeof(RectTransform));
        titleObj.transform.SetParent(panelObj.transform, false);
        RectTransform titleRt = titleObj.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 0.5f);
        titleRt.anchorMax = new Vector2(1f, 0.5f);
        titleRt.pivot = new Vector2(0.5f, 0.5f);
        titleRt.anchoredPosition = new Vector2(0f, 11f);
        titleRt.sizeDelta = new Vector2(-40f, 68f);
        bannerTitleText = titleObj.AddComponent<TextMeshProUGUI>();
        if (hudFont != null) bannerTitleText.font = hudFont;
        bannerTitleText.alignment = TextAlignmentOptions.Center;
        bannerTitleText.fontSize = 52;
        bannerTitleText.fontStyle = FontStyles.Bold;
        bannerTitleText.characterSpacing = 4f;
        bannerTitleText.color = Color.white;
        bannerTitleText.enableAutoSizing = true;
        bannerTitleText.fontSizeMin = 28;
        bannerTitleText.fontSizeMax = 52;
        bannerTitleText.textWrappingMode = TextWrappingModes.NoWrap;
        bannerTitleText.raycastTarget = false;

        // Barra divisoria fina entre título y subtítulo
        GameObject divObj = new GameObject("BannerDivider", typeof(RectTransform));
        divObj.transform.SetParent(panelObj.transform, false);
        RectTransform divRt = divObj.GetComponent<RectTransform>();
        divRt.anchorMin = new Vector2(0.5f, 0.5f);
        divRt.anchorMax = new Vector2(0.5f, 0.5f);
        divRt.pivot = new Vector2(0.5f, 0.5f);
        divRt.anchoredPosition = new Vector2(0f, -26f);
        divRt.sizeDelta = new Vector2(360f, 2f);
        bannerDividerBar = divObj.AddComponent<Image>();
        bannerDividerBar.sprite = whiteSp;
        bannerDividerBar.color = new Color(0.35f, 0.85f, 1f, 0.55f);
        bannerDividerBar.raycastTarget = false;

        // Subtítulo detallado del banner
        GameObject subObj = new GameObject("BannerSubtitle", typeof(RectTransform));
        subObj.transform.SetParent(panelObj.transform, false);
        RectTransform subRt = subObj.GetComponent<RectTransform>();
        subRt.anchorMin = new Vector2(0f, 0f);
        subRt.anchorMax = new Vector2(1f, 0f);
        subRt.pivot = new Vector2(0.5f, 0f);
        subRt.anchoredPosition = new Vector2(0f, 16f);
        subRt.sizeDelta = new Vector2(-48f, 38f);
        bannerSubText = subObj.AddComponent<TextMeshProUGUI>();
        if (hudFont != null) bannerSubText.font = hudFont;
        bannerSubText.alignment = TextAlignmentOptions.Center;
        bannerSubText.fontSize = 22;
        bannerSubText.fontStyle = FontStyles.Bold;
        bannerSubText.characterSpacing = 2.2f;
        bannerSubText.color = new Color(0.98f, 0.88f, 0.38f, 1f);
        bannerSubText.enableAutoSizing = true;
        bannerSubText.fontSizeMin = 15;
        bannerSubText.fontSizeMax = 22;
        bannerSubText.textWrappingMode = TextWrappingModes.NoWrap;
        bannerSubText.raycastTarget = false;

        bannerObj.SetActive(false);
    }

    public void ShowWaveBanner(string title, string subtitle = null)
    {
        EnsureBannerUI();
        if (bannerObj == null) return;

        string safeTitle = string.IsNullOrEmpty(title) ? "" : title;
        bool isBoss = safeTitle.Contains("JEFE") || safeTitle.Contains("NODRIZA") || safeTitle.Contains("ALERTA");
        bool isCleared = safeTitle.Contains("ASEGURADO") || safeTitle.Contains("CUMPLIDO") || safeTitle.Contains("REINICIADO");
        bool isCoop = safeTitle.Contains("JUGADOR 2");
        bool isSector = safeTitle.Contains("SECTOR") && !isCleared;

        Color accentCol;
        Color titleCol;
        Color subCol;
        string eyebrow;

        if (isBoss)
        {
            accentCol = new Color(1f, 0.20f, 0.28f, 1f);
            titleCol = new Color(1f, 0.30f, 0.34f, 1f);
            subCol = new Color(1f, 0.86f, 0.32f, 1f);
            eyebrow = "//  ALERTA CRÍTICA DE FLOTA  •  AMENAZA CLASE LEVIATÁN  //";
        }
        else if (isCleared)
        {
            accentCol = new Color(0.12f, 0.90f, 0.72f, 1f);
            titleCol = new Color(0.75f, 1f, 0.92f, 1f);
            subCol = new Color(0.56f, 0.94f, 0.84f, 1f);
            eyebrow = safeTitle.Contains("REINICIADO")
                ? "//  PROTOCOLO DE EMERGENCIA  •  REACTIVACIÓN COMPLETA  //"
                : "//  OBJETIVO CUMPLIDO  •  ZONA DESPEJADA  //";
        }
        else if (isCoop)
        {
            accentCol = new Color(0.98f, 0.78f, 0.18f, 1f);
            titleCol = new Color(1f, 0.94f, 0.68f, 1f);
            subCol = new Color(0.65f, 0.90f, 1f, 1f);
            eyebrow = "//  ENLACE TÁCTICO  •  REFUERZO ALIADO EN LÍNEA  //";
        }
        else if (isSector)
        {
            accentCol = new Color(0.24f, 0.78f, 1f, 1f);
            titleCol = new Color(0.96f, 0.99f, 1f, 1f);
            subCol = new Color(1f, 0.86f, 0.34f, 1f);
            eyebrow = "//  DESPLIEGUE TÁCTICO  •  ZONA DE COMBATE ACTIVA  //";
        }
        else
        {
            accentCol = new Color(0.45f, 0.62f, 1f, 1f);
            titleCol = Color.white;
            subCol = new Color(0.98f, 0.88f, 0.38f, 1f);
            eyebrow = "//  TRANSMISIÓN DE COMANDO  //";
        }

        if (bannerTopAccent != null) bannerTopAccent.color = accentCol;
        if (bannerBottomAccent != null) bannerBottomAccent.color = accentCol;
        if (bannerLeftBracket != null) bannerLeftBracket.color = accentCol;
        if (bannerRightBracket != null) bannerRightBracket.color = accentCol;
        if (bannerDividerBar != null) bannerDividerBar.color = new Color(accentCol.r, accentCol.g, accentCol.b, 0.55f);

        if (bannerEyebrowText != null)
        {
            bannerEyebrowText.text = eyebrow;
            bannerEyebrowText.color = Color.Lerp(accentCol, Color.white, 0.35f);
        }

        if (bannerTitleShadowText != null) bannerTitleShadowText.text = safeTitle;
        if (bannerTitleText != null)
        {
            bannerTitleText.text = safeTitle;
            bannerTitleText.color = titleCol;
        }

        if (!string.IsNullOrEmpty(subtitle))
        {
            bannerSubText.text = subtitle.ToUpperInvariant();
            bannerSubText.color = subCol;
            bannerSubText.gameObject.SetActive(true);
            if (bannerDividerBar != null) bannerDividerBar.gameObject.SetActive(true);
        }
        else
        {
            bannerSubText.gameObject.SetActive(false);
            if (bannerDividerBar != null) bannerDividerBar.gameObject.SetActive(false);
        }

        bannerObj.SetActive(true);

        if (bannerCoroutine != null)
        {
            StopCoroutine(bannerCoroutine);
        }
        bannerCoroutine = StartCoroutine(BannerDisplayRoutine());
    }

    private IEnumerator BannerDisplayRoutine()
    {
        if (bannerCanvasGroup == null) yield break;

        // Entrada dinámica rápida con pop de escala (0.18s)
        const float introDur = 0.18f;
        float elapsed = 0f;
        while (elapsed < introDur)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / introDur);
            float easeOut = 1f - (1f - t) * (1f - t);
            bannerCanvasGroup.alpha = easeOut;
            if (bannerPanelRt != null)
            {
                float s = Mathf.Lerp(0.90f, 1.0f, easeOut);
                bannerPanelRt.localScale = new Vector3(s, s, 1f);
            }
            yield return null;
        }
        bannerCanvasGroup.alpha = 1f;
        if (bannerPanelRt != null) bannerPanelRt.localScale = Vector3.one;

        // Permanencia visible completa (2.35s)
        float displayTime = 2.35f;
        elapsed = 0f;
        while (elapsed < displayTime)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // Desvanecimiento suave
        float fadeDuration = 0.85f;
        elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / fadeDuration);
            bannerCanvasGroup.alpha = 1f - t;
            if (bannerPanelRt != null)
            {
                float s = Mathf.Lerp(1.0f, 1.04f, t);
                bannerPanelRt.localScale = new Vector3(s, s, 1f);
            }
            yield return null;
        }

        bannerCanvasGroup.alpha = 0f;
        if (bannerPanelRt != null) bannerPanelRt.localScale = Vector3.one;
        if (bannerObj != null) bannerObj.SetActive(false);
        bannerCoroutine = null;
    }

    public void HideWaveBannerImmediately()
    {
        if (bannerCoroutine != null)
        {
            StopCoroutine(bannerCoroutine);
            bannerCoroutine = null;
        }
        if (bannerCanvasGroup != null)
        {
            bannerCanvasGroup.alpha = 0f;
        }
        if (bannerPanelRt != null)
        {
            bannerPanelRt.localScale = Vector3.one;
        }
        if (bannerObj != null)
        {
            bannerObj.SetActive(false);
        }
    }

    // --- PANTALLA EMERGENTE NEGRA TRANSPARENTE DE FASE 2 ---

    private void EnsurePhase2AlertUI()
    {
        if (phase2AlertObj != null) return;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        Sprite whiteSp = GetHudWhiteSprite();
        TMP_FontAsset hudFont = (healthText != null && healthText.font != null) ? healthText.font : null;

        phase2AlertObj = new GameObject("Phase2EnemyAlertOverlay", typeof(RectTransform), typeof(CanvasGroup));
        phase2AlertObj.transform.SetParent(canvas.transform, false);
        // Encima del banner normal pero debajo de los paneles modales (UpgradePanel, GameOverPanel)
        phase2AlertObj.transform.SetSiblingIndex(bannerObj != null ? bannerObj.transform.GetSiblingIndex() + 1 : 0);

        RectTransform rootRt = phase2AlertObj.GetComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;

        phase2AlertCanvasGroup = phase2AlertObj.GetComponent<CanvasGroup>();
        phase2AlertCanvasGroup.alpha = 0f;
        phase2AlertCanvasGroup.blocksRaycasts = false;
        phase2AlertCanvasGroup.interactable = false;

        // Fondo negro transparente a pantalla completa (deja ver las naves y el láser rojo)
        GameObject bgObj = new GameObject("TransparentBlackBg", typeof(RectTransform));
        bgObj.transform.SetParent(phase2AlertObj.transform, false);
        RectTransform bgRt = bgObj.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;

        phase2AlertBgImage = bgObj.AddComponent<Image>();
        phase2AlertBgImage.sprite = whiteSp;
        phase2AlertBgImage.color = new Color(0f, 0f, 0f, 0.52f);
        phase2AlertBgImage.raycastTarget = false;

        // Franja central de alerta táctica de alta prioridad
        GameObject bandObj = new GameObject("Phase2AlertBand", typeof(RectTransform));
        bandObj.transform.SetParent(phase2AlertObj.transform, false);
        phase2AlertBandRt = bandObj.GetComponent<RectTransform>();
        phase2AlertBandRt.anchorMin = new Vector2(0f, 0.5f);
        phase2AlertBandRt.anchorMax = new Vector2(1f, 0.5f);
        phase2AlertBandRt.pivot = new Vector2(0.5f, 0.5f);
        phase2AlertBandRt.anchoredPosition = Vector2.zero;
        phase2AlertBandRt.sizeDelta = new Vector2(0f, 212f);

        GameObject bandBgObj = new GameObject("BandCoreBg", typeof(RectTransform));
        bandBgObj.transform.SetParent(bandObj.transform, false);
        RectTransform bandBgRt = bandBgObj.GetComponent<RectTransform>();
        bandBgRt.anchorMin = Vector2.zero;
        bandBgRt.anchorMax = Vector2.one;
        bandBgRt.offsetMin = Vector2.zero;
        bandBgRt.offsetMax = Vector2.zero;
        Image bandBgImg = bandBgObj.AddComponent<Image>();
        bandBgImg.sprite = whiteSp;
        bandBgImg.color = new Color(0.06f, 0.02f, 0.03f, 0.78f);
        bandBgImg.raycastTarget = false;

        // Franjas superior e inferior parpadeantes (amarillo/rojo sincronizadas)
        GameObject topHazardObj = new GameObject("BandTopHazard", typeof(RectTransform));
        topHazardObj.transform.SetParent(bandObj.transform, false);
        RectTransform thRt = topHazardObj.GetComponent<RectTransform>();
        thRt.anchorMin = new Vector2(0f, 1f);
        thRt.anchorMax = new Vector2(1f, 1f);
        thRt.pivot = new Vector2(0.5f, 1f);
        thRt.anchoredPosition = Vector2.zero;
        thRt.sizeDelta = new Vector2(0f, 4f);
        phase2AlertTopLine = topHazardObj.AddComponent<Image>();
        phase2AlertTopLine.sprite = whiteSp;
        phase2AlertTopLine.color = new Color(1f, 0.90f, 0.08f, 1f);
        phase2AlertTopLine.raycastTarget = false;

        GameObject botHazardObj = new GameObject("BandBottomHazard", typeof(RectTransform));
        botHazardObj.transform.SetParent(bandObj.transform, false);
        RectTransform bhRt = botHazardObj.GetComponent<RectTransform>();
        bhRt.anchorMin = new Vector2(0f, 0f);
        bhRt.anchorMax = new Vector2(1f, 0f);
        bhRt.pivot = new Vector2(0.5f, 0f);
        bhRt.anchoredPosition = Vector2.zero;
        bhRt.sizeDelta = new Vector2(0f, 4f);
        phase2AlertBottomLine = botHazardObj.AddComponent<Image>();
        phase2AlertBottomLine.sprite = whiteSp;
        phase2AlertBottomLine.color = new Color(1f, 0.90f, 0.08f, 1f);
        phase2AlertBottomLine.raycastTarget = false;

        // Etiqueta táctica superior de radar
        GameObject eyebrowObj = new GameObject("Phase2AlertEyebrow", typeof(RectTransform));
        eyebrowObj.transform.SetParent(bandObj.transform, false);
        RectTransform eyeRt = eyebrowObj.GetComponent<RectTransform>();
        eyeRt.anchorMin = new Vector2(0.05f, 1f);
        eyeRt.anchorMax = new Vector2(0.95f, 1f);
        eyeRt.pivot = new Vector2(0.5f, 1f);
        eyeRt.anchoredPosition = new Vector2(0f, -14f);
        eyeRt.sizeDelta = new Vector2(0f, 30f);
        phase2AlertEyebrowText = eyebrowObj.AddComponent<TextMeshProUGUI>();
        if (hudFont != null) phase2AlertEyebrowText.font = hudFont;
        phase2AlertEyebrowText.alignment = TextAlignmentOptions.Center;
        phase2AlertEyebrowText.fontSize = 18;
        phase2AlertEyebrowText.fontStyle = FontStyles.Bold;
        phase2AlertEyebrowText.characterSpacing = 6f;
        phase2AlertEyebrowText.color = new Color(1f, 0.85f, 0.35f, 0.95f);
        phase2AlertEyebrowText.textWrappingMode = TextWrappingModes.NoWrap;
        phase2AlertEyebrowText.raycastTarget = false;

        // Sombra oscura del texto principal para máxima legibilidad
        GameObject shadowObj = new GameObject("Phase2AlertShadow", typeof(RectTransform));
        shadowObj.transform.SetParent(bandObj.transform, false);
        RectTransform shadowRt = shadowObj.GetComponent<RectTransform>();
        shadowRt.anchorMin = new Vector2(0.04f, 0.5f);
        shadowRt.anchorMax = new Vector2(0.96f, 0.5f);
        shadowRt.pivot = new Vector2(0.5f, 0.5f);
        shadowRt.anchoredPosition = new Vector2(4f, 2f);
        shadowRt.sizeDelta = new Vector2(0f, 84f);

        phase2AlertShadowText = shadowObj.AddComponent<TextMeshProUGUI>();
        if (hudFont != null) phase2AlertShadowText.font = hudFont;
        phase2AlertShadowText.alignment = TextAlignmentOptions.Center;
        phase2AlertShadowText.fontSize = 62;
        phase2AlertShadowText.fontStyle = FontStyles.Bold;
        phase2AlertShadowText.characterSpacing = 3.5f;
        phase2AlertShadowText.color = new Color(0f, 0f, 0f, 0.94f);
        phase2AlertShadowText.enableAutoSizing = true;
        phase2AlertShadowText.fontSizeMin = 30;
        phase2AlertShadowText.fontSizeMax = 64;
        phase2AlertShadowText.textWrappingMode = TextWrappingModes.NoWrap;
        phase2AlertShadowText.raycastTarget = false;

        // Texto frontal "NUEVOS ENEMIGOS DETECTADOS" que intercala entre amarillo y rojo
        GameObject textObj = new GameObject("Phase2AlertText", typeof(RectTransform));
        textObj.transform.SetParent(bandObj.transform, false);
        RectTransform textRt = textObj.GetComponent<RectTransform>();
        textRt.anchorMin = new Vector2(0.04f, 0.5f);
        textRt.anchorMax = new Vector2(0.96f, 0.5f);
        textRt.pivot = new Vector2(0.5f, 0.5f);
        textRt.anchoredPosition = new Vector2(0f, 6f);
        textRt.sizeDelta = new Vector2(0f, 84f);

        phase2AlertText = textObj.AddComponent<TextMeshProUGUI>();
        if (hudFont != null) phase2AlertText.font = hudFont;
        phase2AlertText.alignment = TextAlignmentOptions.Center;
        phase2AlertText.fontSize = 62;
        phase2AlertText.fontStyle = FontStyles.Bold;
        phase2AlertText.characterSpacing = 3.5f;
        phase2AlertText.color = new Color(1f, 0.92f, 0.08f, 1f);
        phase2AlertText.enableAutoSizing = true;
        phase2AlertText.fontSizeMin = 30;
        phase2AlertText.fontSizeMax = 64;
        phase2AlertText.textWrappingMode = TextWrappingModes.NoWrap;
        phase2AlertText.raycastTarget = false;

        // Subtítulo táctico detallado debajo de la alerta
        GameObject subObj = new GameObject("Phase2AlertSubtitle", typeof(RectTransform));
        subObj.transform.SetParent(bandObj.transform, false);
        RectTransform subRt = subObj.GetComponent<RectTransform>();
        subRt.anchorMin = new Vector2(0.05f, 0f);
        subRt.anchorMax = new Vector2(0.95f, 0f);
        subRt.pivot = new Vector2(0.5f, 0f);
        subRt.anchoredPosition = new Vector2(0f, 18f);
        subRt.sizeDelta = new Vector2(0f, 34f);
        phase2AlertSubText = subObj.AddComponent<TextMeshProUGUI>();
        if (hudFont != null) phase2AlertSubText.font = hudFont;
        phase2AlertSubText.alignment = TextAlignmentOptions.Center;
        phase2AlertSubText.fontSize = 20;
        phase2AlertSubText.fontStyle = FontStyles.Bold;
        phase2AlertSubText.characterSpacing = 2.4f;
        phase2AlertSubText.color = new Color(0.98f, 0.94f, 0.84f, 0.96f);
        phase2AlertSubText.enableAutoSizing = true;
        phase2AlertSubText.fontSizeMin = 13;
        phase2AlertSubText.fontSizeMax = 20;
        phase2AlertSubText.textWrappingMode = TextWrappingModes.NoWrap;
        phase2AlertSubText.raycastTarget = false;

        phase2AlertObj.SetActive(false);
    }

    public void ShowPhase2EnemyAlert(string message = "NUEVOS ENEMIGOS DETECTADOS", float duration = 3.0f)
    {
        EnsurePhase2AlertUI();
        if (phase2AlertObj == null) return;

        string msg = string.IsNullOrEmpty(message) ? "NUEVOS ENEMIGOS DETECTADOS" : message;
        if (phase2AlertEyebrowText != null)
            phase2AlertEyebrowText.text = "[  ALERTA DE RADAR  •  FASE 2 DEL LEVIATÁN  ]";
        if (phase2AlertShadowText != null) phase2AlertShadowText.text = msg;
        if (phase2AlertText != null) phase2AlertText.text = msg;
        if (phase2AlertSubText != null)
            phase2AlertSubText.text = "ESCUDO DEL JEFE ACTIVADO  •  ESCUADRÓN TIER 4 EN CAMINO  •  ¡ESQUIVA A LOS COSTADOS!";

        phase2AlertObj.SetActive(true);
        if (phase2AlertCoroutine != null)
        {
            StopCoroutine(phase2AlertCoroutine);
        }
        phase2AlertCoroutine = StartCoroutine(Phase2AlertRoutine(Mathf.Max(0.5f, duration)));
    }

    private IEnumerator Phase2AlertRoutine(float duration)
    {
        if (phase2AlertCanvasGroup == null) yield break;

        Color brightYellow = new Color(1f, 0.90f, 0.08f, 1f);
        Color brightRed = new Color(1f, 0.14f, 0.14f, 1f);

        float elapsed = 0f;
        const float flashInterval = 0.22f; // Intercala claramente entre amarillo y rojo durante los 3 segundos

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            // Suave entrada en los primeros 0.12s y salida limpia en los últimos 0.25s dentro de los 3 segundos totales
            float alpha = 1f;
            if (elapsed < 0.12f)
            {
                alpha = Mathf.Clamp01(elapsed / 0.12f);
            }
            else if (elapsed > duration - 0.25f)
            {
                alpha = Mathf.Clamp01((duration - elapsed) / 0.25f);
            }
            phase2AlertCanvasGroup.alpha = alpha;

            bool isYellowPhase = (Mathf.FloorToInt(elapsed / flashInterval) % 2) == 0;
            Color activeCol = isYellowPhase ? brightYellow : brightRed;
            if (phase2AlertText != null) phase2AlertText.color = activeCol;
            if (phase2AlertTopLine != null) phase2AlertTopLine.color = activeCol;
            if (phase2AlertBottomLine != null) phase2AlertBottomLine.color = activeCol;
            if (phase2AlertEyebrowText != null)
                phase2AlertEyebrowText.color = isYellowPhase ? new Color(1f, 0.92f, 0.45f, 0.96f) : new Color(1f, 0.45f, 0.45f, 0.96f);

            // Ligera respiración de escala para sensación de alarma activa
            if (phase2AlertBandRt != null)
            {
                float pulse = 1.0f + 0.022f * Mathf.Sin(elapsed * 14f);
                phase2AlertBandRt.localScale = new Vector3(1f, pulse, 1f);
            }

            yield return null;
        }

        phase2AlertCanvasGroup.alpha = 0f;
        if (phase2AlertBandRt != null) phase2AlertBandRt.localScale = Vector3.one;
        if (phase2AlertObj != null) phase2AlertObj.SetActive(false);
        phase2AlertCoroutine = null;
    }

    public void HidePhase2AlertImmediately()
    {
        if (phase2AlertCoroutine != null)
        {
            StopCoroutine(phase2AlertCoroutine);
            phase2AlertCoroutine = null;
        }
        if (phase2AlertCanvasGroup != null)
        {
            phase2AlertCanvasGroup.alpha = 0f;
        }
        if (phase2AlertBandRt != null)
        {
            phase2AlertBandRt.localScale = Vector3.one;
        }
        if (phase2AlertObj != null)
        {
            phase2AlertObj.SetActive(false);
        }
    }

    // --- CARTELITO EMERGENTE DE CAMBIO DE ARMA (1P abajo-izq / 2P abajo-der) ---

    private static Sprite GetBlastersIconSprite()
    {
        if (cachedBlastersIconSprite != null) return cachedBlastersIconSprite;

        const int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size; // 0..1
                float v = (y + 0.5f) / size; // 0..1
                Color col = Color.clear;

                // Dos cañones bláster paralelos disparando rayos celestes hacia arriba
                for (int b = 0; b < 2; b++)
                {
                    float cx = (b == 0) ? 0.34f : 0.66f;
                    float dx = Mathf.Abs(u - cx);

                    // Base metálica del cañón (v = 0.14..0.38)
                    if (v >= 0.14f && v <= 0.38f && dx <= 0.085f)
                    {
                        float shade = 1f - (dx / 0.085f) * 0.35f;
                        col = new Color(0.55f * shade, 0.80f * shade, 0.96f * shade, 1f);
                    }

                    // Rayo láser bláster disparando hacia arriba (v = 0.42..0.90)
                    if (v >= 0.40f && v <= 0.92f)
                    {
                        float beamDist = dx / 0.068f;
                        float tipFade = 1f - Mathf.SmoothStep(0.82f, 0.92f, v);
                        float tailFade = Mathf.SmoothStep(0.38f, 0.48f, v);
                        float glow = Mathf.Exp(-beamDist * beamDist * 1.4f) * tipFade * tailFade;
                        float core = Mathf.Exp(-beamDist * beamDist * 6.5f) * tipFade * tailFade;
                        if (glow > 0.02f)
                        {
                            Color boltCol = Color.Lerp(new Color(0.10f, 0.78f, 1f, glow), Color.white, core);
                            boltCol.a = Mathf.Clamp01(Mathf.Max(col.a, glow));
                            col = boltCol;
                        }
                    }

                    // Destello circular en la boca del cañón (v = 0.40)
                    float md = Vector2.Distance(new Vector2(u, v), new Vector2(cx, 0.40f));
                    if (md < 0.12f)
                    {
                        float mGlow = Mathf.Pow(1f - md / 0.12f, 2f);
                        col = Color.Lerp(col, new Color(0.75f, 0.96f, 1f, 1f), mGlow);
                        col.a = Mathf.Clamp01(Mathf.Max(col.a, mGlow));
                    }
                }

                // Puente central conector entre ambos cañones (v = 0.18..0.28)
                if (v >= 0.18f && v <= 0.27f && u >= 0.32f && u <= 0.68f)
                {
                    col = new Color(0.35f, 0.65f, 0.88f, 1f);
                }

                tex.SetPixel(x, y, col);
            }
        }
        tex.Apply();

        cachedBlastersIconSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        return cachedBlastersIconSprite;
    }

    private static Sprite GetPlasmaChargedIconSprite()
    {
        if (cachedPlasmaIconSprite != null) return cachedPlasmaIconSprite;

        const int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        float half = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - half) / half;
                float dy = (y - half) / half;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float angle = Mathf.Atan2(dy, dx);

                // Núcleo brillante de plasma cargado + anillos energéticos + destellos en cruz
                float core = Mathf.Exp(-r * r * 6.5f);
                float midGlow = Mathf.Exp(-r * r * 2.4f) * 0.85f;
                float outerRing = Mathf.Exp(-Mathf.Pow((r - 0.72f) / 0.085f, 2f)) * 0.90f;
                float flare = (Mathf.Pow(Mathf.Abs(Mathf.Cos(angle * 2f)), 10f) * Mathf.Clamp01(1f - r / 0.96f)) * 0.65f;

                float alpha = Mathf.Clamp01(Mathf.Max(core, Mathf.Max(midGlow, Mathf.Max(outerRing, flare))));
                Color outerCol = new Color(0.86f, 0.28f, 1f, alpha); // Magenta/violeta plasma
                Color warmCol = new Color(1f, 0.72f, 0.25f, alpha);  // Halo dorado energético
                Color finalCol = Color.Lerp(outerCol, warmCol, Mathf.Clamp01(outerRing * 0.6f + flare));
                finalCol = Color.Lerp(finalCol, Color.white, Mathf.Clamp01(core * 1.15f));
                finalCol.a = alpha;

                tex.SetPixel(x, y, finalCol);
            }
        }
        tex.Apply();

        cachedPlasmaIconSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        return cachedPlasmaIconSprite;
    }

    private WeaponToastUI EnsureWeaponToast(bool isPlayer2)
    {
        WeaponToastUI existing = isPlayer2 ? p2WeaponToast : p1WeaponToast;
        if (existing != null && existing.rootObj != null) return existing;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return null;

        Sprite whiteSp = GetHudWhiteSprite();
        TMP_FontAsset hudFont = (healthText != null && healthText.font != null) ? healthText.font : null;

        WeaponToastUI ui = new WeaponToastUI();
        ui.shownY = isPlayer2 ? 74f : 20f;
        ui.hiddenY = isPlayer2 ? -8f : -84f;

        GameObject root = new GameObject(isPlayer2 ? "WeaponToast_2P" : "WeaponToast_1P", typeof(RectTransform), typeof(CanvasGroup));
        root.transform.SetParent(transform.parent != null ? transform.parent : canvas.transform, false);
        RectTransform rt = root.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(isPlayer2 ? 1f : 0f, 0f);
        rt.anchorMax = new Vector2(isPlayer2 ? 1f : 0f, 0f);
        rt.pivot = new Vector2(isPlayer2 ? 1f : 0f, 0f);
        rt.anchoredPosition = new Vector2(isPlayer2 ? -18f : 18f, ui.hiddenY);
        rt.sizeDelta = new Vector2(272f, 66f);

        ui.rootObj = root;
        ui.rt = rt;
        ui.canvasGroup = root.GetComponent<CanvasGroup>();
        ui.canvasGroup.alpha = 0f;
        ui.canvasGroup.blocksRaycasts = false;
        ui.canvasGroup.interactable = false;

        // Fondo oscuro translúcido táctico
        GameObject bgObj = new GameObject("ToastBg", typeof(RectTransform));
        bgObj.transform.SetParent(root.transform, false);
        RectTransform bgRt = bgObj.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;
        Image bgImg = bgObj.AddComponent<Image>();
        bgImg.sprite = whiteSp;
        bgImg.color = new Color(0.03f, 0.06f, 0.14f, 0.88f);
        bgImg.raycastTarget = false;

        // Borde superior fino
        GameObject topEdgeObj = new GameObject("TopEdge", typeof(RectTransform));
        topEdgeObj.transform.SetParent(root.transform, false);
        RectTransform teRt = topEdgeObj.GetComponent<RectTransform>();
        teRt.anchorMin = new Vector2(0f, 1f);
        teRt.anchorMax = new Vector2(1f, 1f);
        teRt.pivot = new Vector2(0.5f, 1f);
        teRt.anchoredPosition = Vector2.zero;
        teRt.sizeDelta = new Vector2(0f, 2f);
        Image teImg = topEdgeObj.AddComponent<Image>();
        teImg.sprite = whiteSp;
        teImg.color = new Color(0.60f, 0.85f, 1f, 0.30f);
        teImg.raycastTarget = false;

        // Franja de acento lateral
        GameObject accentObj = new GameObject("AccentBar", typeof(RectTransform));
        accentObj.transform.SetParent(root.transform, false);
        RectTransform accRt = accentObj.GetComponent<RectTransform>();
        accRt.anchorMin = new Vector2(isPlayer2 ? 1f : 0f, 0f);
        accRt.anchorMax = new Vector2(isPlayer2 ? 1f : 0f, 1f);
        accRt.pivot = new Vector2(isPlayer2 ? 1f : 0f, 0.5f);
        accRt.anchoredPosition = Vector2.zero;
        accRt.sizeDelta = new Vector2(5f, 0f);
        ui.accentBar = accentObj.AddComponent<Image>();
        ui.accentBar.sprite = whiteSp;
        ui.accentBar.raycastTarget = false;

        // Recuadro del ícono del arma
        GameObject iconBoxObj = new GameObject("WeaponIconBox", typeof(RectTransform));
        iconBoxObj.transform.SetParent(root.transform, false);
        RectTransform ibRt = iconBoxObj.GetComponent<RectTransform>();
        ibRt.anchorMin = new Vector2(isPlayer2 ? 1f : 0f, 0.5f);
        ibRt.anchorMax = new Vector2(isPlayer2 ? 1f : 0f, 0.5f);
        ibRt.pivot = new Vector2(isPlayer2 ? 1f : 0f, 0.5f);
        ibRt.anchoredPosition = new Vector2(isPlayer2 ? -14f : 14f, 0f);
        ibRt.sizeDelta = new Vector2(46f, 46f);
        ui.iconFrameBg = iconBoxObj.AddComponent<Image>();
        ui.iconFrameBg.sprite = whiteSp;
        ui.iconFrameBg.color = new Color(0.08f, 0.14f, 0.26f, 0.92f);
        ui.iconFrameBg.raycastTarget = false;

        // Dibujo del ícono del arma (Blásters o Plasma Cargado)
        GameObject iconImgObj = new GameObject("WeaponIcon", typeof(RectTransform));
        iconImgObj.transform.SetParent(iconBoxObj.transform, false);
        RectTransform iiRt = iconImgObj.GetComponent<RectTransform>();
        iiRt.anchorMin = Vector2.zero;
        iiRt.anchorMax = Vector2.one;
        iiRt.offsetMin = new Vector2(3f, 3f);
        iiRt.offsetMax = new Vector2(-3f, -3f);
        ui.iconImage = iconImgObj.AddComponent<Image>();
        ui.iconImage.preserveAspect = true;
        ui.iconImage.raycastTarget = false;

        // Etiqueta superior ("1P ARMA ACTIVA" / "2P ARMA ACTIVA")
        GameObject eyeObj = new GameObject("WeaponEyebrow", typeof(RectTransform));
        eyeObj.transform.SetParent(root.transform, false);
        RectTransform eyeRt = eyeObj.GetComponent<RectTransform>();
        eyeRt.anchorMin = new Vector2(0f, 1f);
        eyeRt.anchorMax = new Vector2(1f, 1f);
        eyeRt.pivot = new Vector2(0.5f, 1f);
        eyeRt.offsetMin = new Vector2(isPlayer2 ? 12f : 70f, -25f);
        eyeRt.offsetMax = new Vector2(isPlayer2 ? -70f : -12f, -8f);
        ui.eyebrowText = eyeObj.AddComponent<TextMeshProUGUI>();
        if (hudFont != null) ui.eyebrowText.font = hudFont;
        ui.eyebrowText.alignment = isPlayer2 ? TextAlignmentOptions.Right : TextAlignmentOptions.Left;
        ui.eyebrowText.fontSize = 12;
        ui.eyebrowText.fontStyle = FontStyles.Bold;
        ui.eyebrowText.characterSpacing = 2.4f;
        ui.eyebrowText.textWrappingMode = TextWrappingModes.NoWrap;
        ui.eyebrowText.raycastTarget = false;

        // Nombre principal del arma ("BLÁSTERS" o "PLASMA CARGADO")
        GameObject nameObj = new GameObject("WeaponName", typeof(RectTransform));
        nameObj.transform.SetParent(root.transform, false);
        RectTransform nameRt = nameObj.GetComponent<RectTransform>();
        nameRt.anchorMin = new Vector2(0f, 0f);
        nameRt.anchorMax = new Vector2(1f, 0f);
        nameRt.pivot = new Vector2(0.5f, 0f);
        nameRt.offsetMin = new Vector2(isPlayer2 ? 12f : 70f, 10f);
        nameRt.offsetMax = new Vector2(isPlayer2 ? -70f : -12f, 40f);
        ui.weaponNameText = nameObj.AddComponent<TextMeshProUGUI>();
        if (hudFont != null) ui.weaponNameText.font = hudFont;
        ui.weaponNameText.alignment = isPlayer2 ? TextAlignmentOptions.Right : TextAlignmentOptions.Left;
        ui.weaponNameText.fontSize = 21;
        ui.weaponNameText.fontStyle = FontStyles.Bold;
        ui.weaponNameText.characterSpacing = 1.6f;
        ui.weaponNameText.enableAutoSizing = true;
        ui.weaponNameText.fontSizeMin = 15;
        ui.weaponNameText.fontSizeMax = 21;
        ui.weaponNameText.textWrappingMode = TextWrappingModes.NoWrap;
        ui.weaponNameText.raycastTarget = false;

        root.SetActive(false);

        if (isPlayer2) p2WeaponToast = ui;
        else p1WeaponToast = ui;
        return ui;
    }

    public void ShowWeaponSwitchPopup(bool isPlayer2, bool isChargedMode)
    {
        WeaponToastUI ui = EnsureWeaponToast(isPlayer2);
        if (ui == null || ui.rootObj == null) return;

        Color accent = isChargedMode
            ? new Color(0.86f, 0.34f, 1.0f, 1f)  // Plasma cargado (violeta-magenta)
            : new Color(0.18f, 0.82f, 1.0f, 1f); // Blásters (celeste eléctrico)

        if (ui.accentBar != null) ui.accentBar.color = accent;
        if (ui.iconFrameBg != null) ui.iconFrameBg.color = new Color(accent.r * 0.18f, accent.g * 0.18f, accent.b * 0.24f, 0.92f);
        if (ui.iconImage != null) ui.iconImage.sprite = isChargedMode ? GetPlasmaChargedIconSprite() : GetBlastersIconSprite();

        if (ui.eyebrowText != null)
        {
            ui.eyebrowText.text = isPlayer2 ? "2P  •  ARMA ACTIVA" : "1P  •  ARMA ACTIVA";
            ui.eyebrowText.color = Color.Lerp(accent, Color.white, 0.45f);
        }
        if (ui.weaponNameText != null)
        {
            ui.weaponNameText.text = isChargedMode ? "PLASMA CARGADO" : "BLÁSTERS";
            ui.weaponNameText.color = Color.white;
        }

        ui.rootObj.SetActive(true);
        if (ui.routine != null) StopCoroutine(ui.routine);
        ui.routine = StartCoroutine(WeaponToastRoutine(ui));
    }

    private IEnumerator WeaponToastRoutine(WeaponToastUI ui)
    {
        if (ui == null || ui.rt == null || ui.canvasGroup == null) yield break;

        float xPos = ui.rt.anchoredPosition.x;

        // 1. Sube desde abajo (0.20s con ease-out)
        const float slideInDur = 0.20f;
        float elapsed = 0f;
        while (elapsed < slideInDur)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / slideInDur);
            float ease = 1f - (1f - t) * (1f - t);
            ui.rt.anchoredPosition = new Vector2(xPos, Mathf.Lerp(ui.hiddenY, ui.shownY, ease));
            ui.canvasGroup.alpha = ease;
            yield return null;
        }
        ui.rt.anchoredPosition = new Vector2(xPos, ui.shownY);
        ui.canvasGroup.alpha = 1f;

        // 2. Permanece visible 1.75s
        const float holdDur = 1.75f;
        elapsed = 0f;
        while (elapsed < holdDur)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // 3. Vuelve a bajar suavemente (0.28s)
        const float slideOutDur = 0.28f;
        elapsed = 0f;
        while (elapsed < slideOutDur)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / slideOutDur);
            float ease = t * t;
            ui.rt.anchoredPosition = new Vector2(xPos, Mathf.Lerp(ui.shownY, ui.hiddenY, ease));
            ui.canvasGroup.alpha = 1f - ease;
            yield return null;
        }

        ui.canvasGroup.alpha = 0f;
        ui.rt.anchoredPosition = new Vector2(xPos, ui.hiddenY);
        if (ui.rootObj != null) ui.rootObj.SetActive(false);
        ui.routine = null;
    }

    public void HideWeaponToastsImmediately()
    {
        HideSingleWeaponToast(p1WeaponToast);
        HideSingleWeaponToast(p2WeaponToast);
    }

    private void HideSingleWeaponToast(WeaponToastUI ui)
    {
        if (ui == null) return;
        if (ui.routine != null) { StopCoroutine(ui.routine); ui.routine = null; }
        if (ui.canvasGroup != null) ui.canvasGroup.alpha = 0f;
        if (ui.rootObj != null) ui.rootObj.SetActive(false);
    }

    // --- MÉTODOS DE LA BARRA DE VIDA DEL JEFE ---

    public void ActivateBossHealthBar(string name, float maxHp)
    {
        bossName = string.IsNullOrEmpty(name) ? "Leviatán nodriza" : name;
        bossMaxHealth = Mathf.Max(1f, maxHp);
        bossCurrentHealth = bossMaxHealth;
        bossDisplayFill = 0f; // Comienza vacía para la mítica animación de carga
        bossTrailingFill = 0f;
        bossBarAlpha = 0f;
        isBossDefeated = false;
        isBossPhase2 = false;
        isBossInvulnerable = false;
        isBossBarActive = true;

        if (timerText != null)
        {
            timerText.text = "";
        }
        if (timerCardObj != null)
        {
            timerCardObj.SetActive(false);
        }
    }

    public void SetBossPhase2(bool active)
    {
        isBossPhase2 = active;
    }

    public void SetBossInvulnerable(bool invulnerable)
    {
        isBossInvulnerable = invulnerable;
    }

    public void UpdateBossHealth(float currentHp)
    {
        if (!isBossBarActive) return;

        float clampedHp = Mathf.Max(0f, currentHp);
        if (clampedHp < bossCurrentHealth)
        {
            lastDamageTime = Time.time;
            bossTrailingFill = Mathf.Max(bossTrailingFill, bossDisplayFill);
        }
        else if (clampedHp > bossCurrentHealth)
        {
            // Al curarse (entrada a Fase 2), alinear la barra fantasma con el nuevo porcentaje
            float targetPct = Mathf.Clamp01(clampedHp / Mathf.Max(1f, bossMaxHealth));
            bossTrailingFill = Mathf.Max(bossTrailingFill, targetPct);
        }

        bossCurrentHealth = clampedHp;
    }

    public void OnBossDefeated()
    {
        isBossDefeated = true;
        isBossPhase2 = false;
        isBossInvulnerable = false;
        bossCurrentHealth = 0f;
    }

    void OnGUI()
    {
        if (DualMonitorLeaderboardManager.Instance != null)
        {
            if (DualMonitorLeaderboardManager.Instance.IsLeaderboardCoveringDisplay0 && Event.current.displayIndex == 0)
            {
                return;
            }

            if (DualMonitorLeaderboardManager.Instance.HasMultipleDisplays && Event.current.displayIndex != DualMonitorLeaderboardManager.Instance.CurrentGameDisplayIndex)
            {
                return;
            }
        }

        DrawBossHealthBar();
    }

    private void DrawBossHealthBar()
    {
        if (!isBossBarActive || bossBarAlpha <= 0.001f) return;
        if (GameManager.Instance != null &&
            (GameManager.Instance.currentState == GameManager.GameState.Won ||
             GameManager.Instance.currentState == GameManager.GameState.Lost ||
             GameManager.Instance.IsAwaitingScreamRevive))
        {
            return;
        }

        int screenW = Screen.width;
        float barWidth = Mathf.Clamp(screenW * 0.58f, 540f, 780f);
        float barHeight = 28f;
        float x = (screenW - barWidth) * 0.5f;
        float y = 30f;

        Color prevColor = GUI.color;
        Texture2D white = Texture2D.whiteTexture;

        // 1. TÍTULO ÉPICO DEL JEFE (Encima de la barra)
        GUIStyle nameStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 17,
            fontStyle = FontStyle.Bold
        };

        float hpPct = Mathf.Clamp01(bossCurrentHealth / Mathf.Max(1f, bossMaxHealth));

        string titleText = "Leviatán nodriza";

        // Sombra del título
        nameStyle.normal.textColor = new Color(0f, 0f, 0f, bossBarAlpha * 0.95f);
        GUI.Label(new Rect(x + 2, y - 26f + 2, barWidth, 24f), titleText, nameStyle);

        // Título frontal vibrante centrado (gris si es invencible, azul blindado en Fase 1 con escudo, rojo furia en Fase 2)
        Color nameColor;
        if (isBossInvulnerable && !isBossDefeated)
        {
            nameColor = new Color(0.76f, 0.80f, 0.85f, bossBarAlpha);
        }
        else if (!isBossPhase2 && !isBossDefeated)
        {
            float shieldPulse = Mathf.Sin(Time.time * 6f) * 0.5f + 0.5f;
            nameColor = Color.Lerp(new Color(0.42f, 0.85f, 1f, bossBarAlpha), new Color(0.80f, 0.96f, 1f, bossBarAlpha), shieldPulse);
        }
        else if (hpPct < 0.25f && !isBossDefeated)
        {
            nameColor = new Color(1f, 0.25f, 0.25f, bossBarAlpha);
        }
        else
        {
            nameColor = new Color(1f, 0.55f, 0.25f, bossBarAlpha);
        }
        nameStyle.normal.textColor = nameColor;
        GUI.Label(new Rect(x, y - 26f, barWidth, 24f), titleText, nameStyle);

        // 2. FONDO Y MARCO DEL CONTENEDOR (Cyber Obsidian & Neon Frame)
        // Borde sombra exterior negro
        GUI.color = new Color(0f, 0f, 0f, bossBarAlpha * 0.90f);
        GUI.DrawTexture(new Rect(x - 3, y - 3, barWidth + 6, barHeight + 6), white);

        // Fondo oscuro de la barra
        GUI.color = new Color(0.04f, 0.07f, 0.14f, bossBarAlpha * 0.96f);
        GUI.DrawTexture(new Rect(x, y, barWidth, barHeight), white);

        // Borde Neón: gris metálico cuando es invencible, azul en Fase 1 con escudo, rojo/naranja en Fase 2
        Color borderColor;
        if (isBossInvulnerable && !isBossDefeated)
        {
            borderColor = new Color(0.70f, 0.74f, 0.80f, 0.95f);
        }
        else if (!isBossPhase2 && !isBossDefeated)
        {
            float pulse = Mathf.Sin(Time.time * 8f) * 0.5f + 0.5f;
            borderColor = Color.Lerp(new Color(0.18f, 0.72f, 1f, 1f), new Color(0.70f, 0.95f, 1f, 1f), pulse);
        }
        else if (hpPct < 0.25f && !isBossDefeated)
        {
            float pulse = Mathf.Sin(Time.time * 14f) * 0.5f + 0.5f;
            borderColor = Color.Lerp(new Color(1f, 0.15f, 0.25f, 1f), new Color(1f, 0.75f, 0.75f, 1f), pulse);
        }
        else
        {
            borderColor = new Color(1f, 0.45f, 0.15f, 0.95f);
        }
        borderColor.a *= bossBarAlpha;

        GUI.color = borderColor;
        GUI.DrawTexture(new Rect(x - 2, y - 2, barWidth + 4, 2), white);          // Arriba
        GUI.DrawTexture(new Rect(x - 2, y + barHeight, barWidth + 4, 2), white);  // Abajo
        GUI.DrawTexture(new Rect(x - 2, y, 2, barHeight), white);                  // Izquierda
        GUI.DrawTexture(new Rect(x + barWidth, y, 2, barHeight), white);          // Derecha

        // 3. BARRA FANTASMA DE DAÑO RESIDUAL (Efecto Mítico de Daño)
        float innerW = barWidth - 4;
        float innerH = barHeight - 4;
        float innerX = x + 2;
        float innerY = y + 2;

        if (bossTrailingFill > bossDisplayFill)
        {
            float trailW = innerW * Mathf.Clamp01(bossTrailingFill);
            if (isBossInvulnerable && !isBossDefeated)
                GUI.color = new Color(0.68f, 0.71f, 0.76f, bossBarAlpha * 0.80f);
            else if (!isBossPhase2)
                GUI.color = new Color(0.65f, 0.92f, 1f, bossBarAlpha * 0.85f);
            else
                GUI.color = new Color(1f, 0.88f, 0.35f, bossBarAlpha * 0.88f);
            GUI.DrawTexture(new Rect(innerX, innerY, trailW, innerH), white);
        }

        // 4. BARRA DE VIDA PRINCIPAL (Gris si es invencible; Azul blindado en Fase 1 con escudo; Carmesí/Fuego en Fase 2)
        if (bossDisplayFill > 0.001f)
        {
            float fillW = innerW * Mathf.Clamp01(bossDisplayFill);

            Color barColor;
            if (isBossInvulnerable && !isBossDefeated)
            {
                float invulnPulse = Mathf.Sin(Time.time * 5f) * 0.5f + 0.5f;
                barColor = Color.Lerp(
                    new Color(0.46f, 0.49f, 0.54f, 1f),
                    new Color(0.60f, 0.64f, 0.69f, 1f),
                    invulnPulse * 0.45f);
            }
            else if (!isBossPhase2 && !isBossDefeated)
            {
                // Fase 1: Escudo protector azul cibernético activo
                float shieldShimmer = Mathf.Sin(Time.time * 7f) * 0.5f + 0.5f;
                barColor = Color.Lerp(
                    new Color(0.10f, 0.50f, 0.96f, 1f),
                    new Color(0.24f, 0.76f, 1.00f, 1f),
                    shieldShimmer * 0.45f);
            }
            else if (hpPct > 0.50f)
            {
                barColor = new Color(0.95f, 0.18f, 0.25f, 1f); // Carmesí intenso en Fase 2 (sin escudo)
            }
            else if (hpPct > 0.25f)
            {
                barColor = new Color(1.0f, 0.52f, 0.12f, 1f); // Naranja caliente
            }
            else
            {
                float criticalFlash = Mathf.Sin(Time.time * 16f) * 0.20f + 0.80f;
                barColor = new Color(1.0f, 0.15f * criticalFlash, 0.15f * criticalFlash, 1f);
            }
            barColor.a *= bossBarAlpha;

            GUI.color = barColor;
            GUI.DrawTexture(new Rect(innerX, innerY, fillW, innerH), white);

            // Reflejo superior estilo cristal de energía
            if (isBossInvulnerable && !isBossDefeated)
                GUI.color = new Color(0.86f, 0.89f, 0.93f, bossBarAlpha * 0.30f);
            else if (isBossPhase2)
                GUI.color = new Color(0.80f, 0.96f, 1f, bossBarAlpha * 0.42f);
            else
                GUI.color = new Color(1f, 1f, 1f, bossBarAlpha * 0.30f);
            GUI.DrawTexture(new Rect(innerX, innerY, fillW, innerH * 0.36f), white);
        }

        // 5. MARCAS DE FASE / SEGMENTOS (25%, 50%, 75%)
        GUI.color = new Color(0.02f, 0.04f, 0.08f, bossBarAlpha * 0.85f);
        for (int i = 1; i <= 3; i++)
        {
            float segX = innerX + (innerW * (i * 0.25f));
            GUI.DrawTexture(new Rect(segX - 1, innerY, 2, innerH), white);
        }

        // 6. NÚMEROS DE HP Y PORCENTAJE CENTRADOS
        // Restablecer GUI.color a blanco para que el tinte de los divisores oscuros no oscurezca el texto a negro
        GUI.color = Color.white;

        GUIStyle hpStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 14,
            fontStyle = FontStyle.Bold
        };

        string hpText = $"{Mathf.CeilToInt(bossCurrentHealth)} / {Mathf.CeilToInt(bossMaxHealth)} HP   ({Mathf.RoundToInt(hpPct * 100f)}%)";

        // Sombra de contorno oscura en 4 direcciones para máxima legibilidad sobre cualquier fondo
        hpStyle.normal.textColor = new Color(0f, 0f, 0f, bossBarAlpha * 0.98f);
        GUI.Label(new Rect(x + 1, y + 1, barWidth, barHeight), hpText, hpStyle);
        GUI.Label(new Rect(x - 1, y - 1, barWidth, barHeight), hpText, hpStyle);
        GUI.Label(new Rect(x + 1, y - 1, barWidth, barHeight), hpText, hpStyle);
        GUI.Label(new Rect(x - 1, y + 1, barWidth, barHeight), hpText, hpStyle);

        // Texto frontal blanco puro brillante
        hpStyle.normal.textColor = new Color(1f, 1f, 1f, bossBarAlpha);
        GUI.Label(new Rect(x, y, barWidth, barHeight), hpText, hpStyle);

        GUI.color = prevColor;
    }
}

/// <summary>
/// Resultado de un cuadro de análisis acústico del micrófono.
/// </summary>
public struct ScreamAnalysisResult
{
    public float VocalLevel;
    public float WindLevel;
    public float Harmonicity;
    public bool IsBlowing;
}

/// <summary>
/// Sesión reutilizable de captura y medición de gritos por micrófono con rechazo de soplidos,
/// retardo de caída de 1.5s y soporte diferenciado para 1P / 2P.
/// </summary>
public sealed class MicrophoneScreamMeterSession
{
    public const float StartupSettleSeconds = 0.25f;
    public const float ScreamDecayDelaySeconds = 1.5f;
    private const int SampleWindow = 1024;

    private readonly float[] sampleBuffer = new float[SampleWindow];
    private readonly float[] vocalBuffer = new float[SampleWindow];

    private AudioClip micClip;
    private string activeMicDevice;
    private int micSampleRate = 44100;
    private bool isRecording = false;
    private float elapsedOpen = 0f;
    private float lastLoudTime = -10f;
    private float nextMicRetryTime = 0f;
    private float consecutiveBlowTime = 0f;
    private float blowWarningTimer = 0f;

    public float Progress { get; private set; }
    public float SmoothedMicDisplay { get; private set; }
    public bool ValidScream { get; private set; }
    public bool IsBlowing => blowWarningTimer > 0f && !ValidScream;
    public bool IsRecording => isRecording;
    public string ActiveDeviceName => activeMicDevice;
    public bool IsComplete => Progress >= 1f;

    public static void EvalThresholds(bool isCoop, out float noiseGate, out float peakRef, out float fillSpeed, out float decaySpeed)
    {
        if (isCoop)
        {
            noiseGate = 0.21f;
            peakRef = 0.68f;
            fillSpeed = 0.24f;
            decaySpeed = 0.26f;
        }
        else
        {
            noiseGate = 0.14f;
            peakRef = 0.50f;
            fillSpeed = 0.34f;
            decaySpeed = 0.22f;
        }
    }

    public void Begin()
    {
        elapsedOpen = 0f;
        Progress = 0f;
        SmoothedMicDisplay = 0f;
        ValidScream = false;
        lastLoudTime = -10f;
        nextMicRetryTime = 0f;
        consecutiveBlowTime = 0f;
        blowWarningTimer = 0f;
        TryStartMicrophone();
    }

    public void Step(float unscaledDt, bool isCoop, bool holdAtComplete)
    {
        elapsedOpen += unscaledDt;
        blowWarningTimer = Mathf.Max(0f, blowWarningTimer - unscaledDt);

        if (!isRecording && Time.unscaledTime >= nextMicRetryTime)
        {
            nextMicRetryTime = Time.unscaledTime + 0.5f;
            TryStartMicrophone();
        }

        EvalThresholds(isCoop, out float noiseGate, out float peakRef, out float fillSpeed, out float decaySpeed);
        ScreamAnalysisResult analysis = elapsedOpen >= StartupSettleSeconds ? AnalyzeMicrophone(isCoop, noiseGate) : default;

        float displaySignal = analysis.IsBlowing
            ? Mathf.Max(analysis.VocalLevel, analysis.WindLevel)
            : analysis.VocalLevel;
        float targetDisplay = Mathf.Clamp01(displaySignal / peakRef);
        float smoothRate = targetDisplay > SmoothedMicDisplay ? 15f : 5.5f;
        SmoothedMicDisplay = Mathf.MoveTowards(SmoothedMicDisplay, targetDisplay, unscaledDt * smoothRate);

        if (analysis.IsBlowing)
        {
            consecutiveBlowTime += unscaledDt;
            if (consecutiveBlowTime >= 0.14f)
            {
                blowWarningTimer = 0.55f;
            }
        }
        else
        {
            consecutiveBlowTime = Mathf.Max(0f, consecutiveBlowTime - unscaledDt * 2.5f);
        }

        ValidScream = !analysis.IsBlowing && analysis.VocalLevel > noiseGate;
        if (holdAtComplete && Progress >= 1f)
        {
            Progress = 1f;
            return;
        }

        if (ValidScream)
        {
            consecutiveBlowTime = 0f;
            blowWarningTimer = 0f;
            float strength = Mathf.Clamp01((analysis.VocalLevel - noiseGate) / Mathf.Max(0.05f, peakRef - noiseGate));
            float gain = Mathf.Pow(strength, 1.35f) * fillSpeed * unscaledDt;
            Progress = Mathf.Clamp01(Progress + gain);
            lastLoudTime = Time.unscaledTime;
        }
        else if (Time.unscaledTime - lastLoudTime > ScreamDecayDelaySeconds)
        {
            Progress = Mathf.Max(0f, Progress - decaySpeed * unscaledDt);
        }
    }

    public bool TryStartMicrophone()
    {
        Stop();

        string[] devices = Microphone.devices;
        if (devices == null || devices.Length == 0)
        {
            activeMicDevice = null;
            isRecording = false;
            return false;
        }

        activeMicDevice = devices[0];
        try
        {
            Microphone.GetDeviceCaps(activeMicDevice, out int minFreq, out int maxFreq);
            int freq = (minFreq == 0 && maxFreq == 0) ? 44100 : Mathf.Clamp(44100, minFreq, maxFreq);
            micClip = Microphone.Start(activeMicDevice, true, 1, freq);
            isRecording = (micClip != null);
            micSampleRate = (micClip != null && micClip.frequency > 0) ? micClip.frequency : freq;
        }
        catch
        {
            activeMicDevice = null;
            micClip = null;
            isRecording = false;
        }

        return isRecording;
    }

    public void Stop()
    {
        if (!string.IsNullOrEmpty(activeMicDevice))
        {
            try
            {
                if (Microphone.IsRecording(activeMicDevice))
                {
                    Microphone.End(activeMicDevice);
                }
            }
            catch { }
        }
        micClip = null;
        isRecording = false;
    }

    private ScreamAnalysisResult AnalyzeMicrophone(bool isCoop, float noiseGate)
    {
        ScreamAnalysisResult result = default;
        if (!isRecording || micClip == null || string.IsNullOrEmpty(activeMicDevice)) return result;
        if (!Microphone.IsRecording(activeMicDevice)) return result;

        int micPos = Microphone.GetPosition(activeMicDevice);
        int clipSamples = micClip.samples;
        if (micPos <= 0 || clipSamples < SampleWindow) return result;

        int startSample = micPos - SampleWindow;
        if (startSample < 0) startSample += clipSamples;

        try
        {
            micClip.GetData(sampleBuffer, startSample);
        }
        catch
        {
            return result;
        }

        int n = sampleBuffer.Length;
        double sum = 0.0;
        for (int i = 0; i < n; i++)
        {
            sum += sampleBuffer[i];
        }
        float mean = (float)(sum / n);

        // Separar energía subsónica de viento (<120 Hz) vs banda vocal (140-2400 Hz)
        float sr = Mathf.Max(8000, micSampleRate);
        float dtSample = 1f / sr;
        float rcWind = 1f / (2f * Mathf.PI * 120f);
        float alphaWind = dtSample / (rcWind + dtSample);
        float rcHp = 1f / (2f * Mathf.PI * 140f);
        float alphaHp = rcHp / (rcHp + dtSample);
        float rcLp = 1f / (2f * Mathf.PI * 2400f);
        float alphaLp = dtSample / (rcLp + dtSample);

        float windLp = 0f;
        float hp = 0f;
        float bp = 0f;
        float prevX = 0f;

        double sumSqWind = 0.0;
        double sumSqVocal = 0.0;
        float vocalPeak = 0f;
        float windPeak = 0f;

        for (int i = 0; i < n; i++)
        {
            float x = sampleBuffer[i] - mean;

            windLp += alphaWind * (x - windLp);
            float aw = Mathf.Abs(windLp);
            sumSqWind += windLp * windLp;
            if (aw > windPeak) windPeak = aw;

            hp = alphaHp * (hp + x - prevX);
            prevX = x;
            bp += alphaLp * (hp - bp);
            vocalBuffer[i] = bp;

            float av = Mathf.Abs(bp);
            sumSqVocal += bp * bp;
            if (av > vocalPeak) vocalPeak = av;
        }

        float windRms = Mathf.Sqrt((float)(sumSqWind / n));
        float vocalRms = Mathf.Sqrt((float)(sumSqVocal / n));

        result.WindLevel = Mathf.Clamp01(windRms * 0.72f + windPeak * 0.28f);
        result.VocalLevel = Mathf.Clamp01(vocalRms * 0.72f + vocalPeak * 0.28f);

        float activityFloor = Mathf.Max(0.03f, noiseGate * 0.5f);
        if (result.VocalLevel <= activityFloor && result.WindLevel <= 0.04f) return result;

        // Medir periodicidad vocal mediante autocorrelación normalizada entre 110 Hz y 780 Hz
        int minLag = Mathf.Clamp(Mathf.RoundToInt(sr / 780f), 4, n / 4);
        int maxLag = Mathf.Clamp(Mathf.RoundToInt(sr / 110f), minLag + 4, n / 2);

        float bestHarmonicity = 0f;
        int bestLag = minLag;
        for (int lag = minLag; lag <= maxLag; lag += 2)
        {
            float r = ComputeNormalizedAutocorrelation(vocalBuffer, n, lag);
            if (r > bestHarmonicity)
            {
                bestHarmonicity = r;
                bestLag = lag;
            }
        }

        int refineMin = Mathf.Max(minLag, bestLag - 1);
        int refineMax = Mathf.Min(maxLag, bestLag + 1);
        for (int lag = refineMin; lag <= refineMax; lag++)
        {
            float r = ComputeNormalizedAutocorrelation(vocalBuffer, n, lag);
            if (r > bestHarmonicity)
            {
                bestHarmonicity = r;
            }
        }

        result.Harmonicity = bestHarmonicity;

        float windRatio = windRms / Mathf.Max(0.0005f, vocalRms);
        bool heavyWindThump = windRms > 0.03f && windRatio > 1.35f;
        float minHarmonicity = isCoop ? 0.24f : 0.30f;
        bool aperiodicAirNoise = result.VocalLevel > noiseGate * 0.65f && bestHarmonicity < minHarmonicity;

        result.IsBlowing = heavyWindThump || aperiodicAirNoise;
        return result;
    }

    private static float ComputeNormalizedAutocorrelation(float[] buffer, int length, int lag)
    {
        int count = length - lag;
        if (count <= 0) return 0f;

        double cross = 0.0;
        double normA = 0.0;
        double normB = 0.0;
        for (int i = 0; i < count; i++)
        {
            float a = buffer[i];
            float b = buffer[i + lag];
            cross += a * b;
            normA += a * a;
            normB += b * b;
        }

        double denom = Math.Sqrt(normA * normB);
        return denom > 1e-7 ? (float)(cross / denom) : 0f;
    }
}

/// <summary>
/// Utilidades compartidas de UI para las barras de medidor de grito en Reactivación y Mejoras.
/// </summary>
public static class ScreamMeterUIUtils
{
    public static GameObject CreateBarTrack(
        Transform parent,
        string name,
        Vector2 pos,
        Vector2 size,
        Color bgColor,
        Color borderColor,
        out RectTransform fillRect,
        out Image fillImg,
        out Image[] borderEdges)
    {
        GameObject track = new GameObject(name, typeof(RectTransform));
        track.transform.SetParent(parent, false);
        RectTransform rt = track.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        Image bg = track.AddComponent<Image>();
        bg.color = bgColor;
        bg.raycastTarget = false;

        fillImg = CreateBarFill(track.transform, out fillRect);
        borderEdges = CreateBorderFrame(track.transform, borderColor, 2f);
        return track;
    }

    public static Image CreateBarFill(Transform parent, out RectTransform fillRect)
    {
        GameObject fillObj = new GameObject("Fill", typeof(RectTransform));
        fillObj.transform.SetParent(parent, false);
        fillRect = fillObj.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(0f, 1f);
        fillRect.offsetMin = new Vector2(2f, 2f);
        fillRect.offsetMax = new Vector2(-2f, -2f);
        Image fillImg = fillObj.AddComponent<Image>();
        fillImg.color = Color.cyan;
        fillImg.raycastTarget = false;
        return fillImg;
    }

    public static RectTransform CreateThresholdMarker(Transform micTrackTransform)
    {
        GameObject threshObj = new GameObject("ThresholdMarker", typeof(RectTransform), typeof(Image));
        threshObj.transform.SetParent(micTrackTransform, false);
        RectTransform rt = threshObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.23f, 0f);
        rt.anchorMax = new Vector2(0.23f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(3f, 6f);
        Image thImg = threshObj.GetComponent<Image>();
        thImg.color = new Color(1f, 0.92f, 0.25f, 1f);
        thImg.raycastTarget = false;
        return rt;
    }

    public static Image[] CreateBorderFrame(Transform parent, Color borderColor, float thickness)
    {
        Image[] edges = new Image[4];
        for (int i = 0; i < 4; i++)
        {
            GameObject edge = new GameObject("BorderEdge_" + i, typeof(RectTransform), typeof(Image));
            edge.transform.SetParent(parent, false);
            RectTransform rt = edge.GetComponent<RectTransform>();
            Image img = edge.GetComponent<Image>();
            img.color = borderColor;
            img.raycastTarget = false;
            edges[i] = img;

            if (i == 0)
            {
                rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0.5f, 1f); rt.sizeDelta = new Vector2(0f, thickness);
            }
            else if (i == 1)
            {
                rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(1f, 0f);
                rt.pivot = new Vector2(0.5f, 0f); rt.sizeDelta = new Vector2(0f, thickness);
            }
            else if (i == 2)
            {
                rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 0.5f); rt.sizeDelta = new Vector2(thickness, 0f);
            }
            else
            {
                rt.anchorMin = new Vector2(1f, 0f); rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(1f, 0.5f); rt.sizeDelta = new Vector2(thickness, 0f);
            }
        }
        return edges;
    }

    public static TextMeshProUGUI CreateLabel(
        Transform parent,
        string name,
        Vector2 pos,
        Vector2 size,
        string text,
        float fontSize,
        TextAlignmentOptions alignment,
        TMP_FontAsset sharedFont)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
        if (sharedFont != null) tmp.font = sharedFont;
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.raycastTarget = false;
        return tmp;
    }

    public static Color EvaluateProgressColor(float progress)
    {
        if (progress < 0.45f)
        {
            return Color.Lerp(new Color(0.15f, 0.78f, 1f, 1f), new Color(1f, 0.76f, 0.15f, 1f), progress / 0.45f);
        }
        return Color.Lerp(new Color(1f, 0.76f, 0.15f, 1f), new Color(0.22f, 0.98f, 0.56f, 1f), (progress - 0.45f) / 0.55f);
    }

    public static void UpdateThresholdMarker(RectTransform thresholdRect, bool isCoop)
    {
        if (thresholdRect == null) return;
        MicrophoneScreamMeterSession.EvalThresholds(isCoop, out float noiseGate, out float peakRef, out _, out _);
        float gateX = Mathf.Clamp(noiseGate / peakRef, 0.1f, 0.75f);
        thresholdRect.anchorMin = new Vector2(gateX, 0f);
        thresholdRect.anchorMax = new Vector2(gateX, 1f);
        thresholdRect.anchoredPosition = Vector2.zero;
    }

    public static void ApplyMicStatusUI(
        MicrophoneScreamMeterSession session,
        bool isCoop,
        bool completedReady,
        RectTransform micFillRect,
        Image micFillImg,
        TextMeshProUGUI micPromptTmp,
        TextMeshProUGUI micDeviceTmp)
    {
        if (micFillRect != null)
        {
            micFillRect.anchorMax = new Vector2(Mathf.Clamp01(session.SmoothedMicDisplay), 1f);
        }

        if (micFillImg != null)
        {
            if (session.IsBlowing)
            {
                micFillImg.color = new Color(1f, 0.18f, 0.52f, 1f);
            }
            else if (completedReady || session.ValidScream)
            {
                micFillImg.color = new Color(0.22f, 0.98f, 0.56f, 1f);
            }
            else
            {
                micFillImg.color = new Color(0.95f, 0.42f, 0.25f, 0.92f);
            }
        }

        if (micPromptTmp != null)
        {
            if (completedReady)
            {
                micPromptTmp.text = "<color=#4ADE80><b>¡REQUISITO COMPLETADO!</b></color>";
            }
            else if (!session.IsRecording)
            {
                micPromptTmp.text = "<color=#FF5555><b>SIN MICRÓFONO</b></color>";
            }
            else if (session.IsBlowing)
            {
                micPromptTmp.text = isCoop
                    ? "<color=#FF2E79><b>¡NO SOPLEN! ¡GRITEN!</b></color>"
                    : "<color=#FF2E79><b>¡NO SOPLES! ¡GRITÁ!</b></color>";
            }
            else if (session.ValidScream)
            {
                micPromptTmp.text = "<color=#4ADE80><b>¡SEGUÍ ASÍ!</b></color>";
            }
            else
            {
                micPromptTmp.text = isCoop
                    ? "<color=#FBBF24><b>¡MÁS FUERTE LOS 2!</b></color>"
                    : "<color=#FBBF24><b>¡MÁS FUERTE!</b></color>";
            }
        }

        if (micDeviceTmp != null)
        {
            if (session.IsRecording && !string.IsNullOrEmpty(session.ActiveDeviceName))
            {
                string shortName = session.ActiveDeviceName.Length > 38
                    ? session.ActiveDeviceName.Substring(0, 38) + "..."
                    : session.ActiveDeviceName;
                micDeviceTmp.text = $"<color=#93C5FD>Mic:</color> <color=#E2E8F0>{shortName}</color>";
            }
            else
            {
                micDeviceTmp.text = "<color=#FF6B6B><b>Sin micrófono detectado</b></color>";
            }
        }
    }
}

/// <summary>
/// Pantalla previa al Game Over: captura audio del micrófono en vivo para que el jugador
/// (o ambos jugadores con requisito mayor) grite a todo pulmón y reviva con 5s de invulnerabilidad.
/// </summary>
public class ScreamReviveController : MonoBehaviour
{
    private const float CountdownDuration = 22.0f;

    private static ScreamReviveController instance;

    private readonly MicrophoneScreamMeterSession micSession = new MicrophoneScreamMeterSession();
    private Action onSuccessCallback;
    private Action onFailCallback;
    private bool isCoopMode = false;
    private int attemptNumber = 1;
    private int maxAttempts = 3;
    private long targetReviveHealth = 10L;
    private bool isFinished = false;
    private bool hasBuiltUI = false;
    private float timeRemaining = CountdownDuration;

    // Referencias UI
    private RectTransform cardRect;
    private Image[] cardBorderEdges;
    private TextMeshProUGUI titleTmp;
    private TextMeshProUGUI subtitleTmp;
    private TextMeshProUGUI timerTmp;
    private TextMeshProUGUI requirementTmp;
    private RectTransform chargeFillRect;
    private Image chargeFillImg;
    private TextMeshProUGUI chargePercentTmp;
    private RectTransform micFillRect;
    private Image micFillImg;
    private RectTransform micThresholdRect;
    private TextMeshProUGUI micPromptTmp;
    private TextMeshProUGUI micDeviceTmp;
    private TMP_FontAsset sharedFont;

    public static void Show(bool isCoop, int attempt, int maxAttemptsCount, long reviveHp, Action onSuccess, Action onFail)
    {
        EnsureInstance();
        if (instance == null)
        {
            onFail?.Invoke();
            return;
        }
        instance.BeginSession(isCoop, attempt, maxAttemptsCount, reviveHp, onSuccess, onFail);
    }

    private static void EnsureInstance()
    {
        if (instance != null) return;

        Canvas targetCanvas = null;
        if (GameManager.Instance != null && GameManager.Instance.hudManager != null)
        {
            targetCanvas = GameManager.Instance.hudManager.GetComponentInParent<Canvas>();
        }
        if (targetCanvas == null) targetCanvas = FindAnyObjectByType<Canvas>();
        if (targetCanvas == null) return;

        if (FindAnyObjectByType<EventSystem>() == null)
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        GameObject root = new GameObject("ScreamReviveOverlay", typeof(RectTransform));
        root.transform.SetParent(targetCanvas.transform, false);
        RectTransform rt = root.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        instance = root.AddComponent<ScreamReviveController>();
    }

    private void BeginSession(bool isCoop, int attempt, int maxAttemptsCount, long reviveHp, Action onSuccess, Action onFail)
    {
        EnsureUI();

        isCoopMode = isCoop;
        attemptNumber = Mathf.Max(1, attempt);
        maxAttempts = Mathf.Max(1, maxAttemptsCount);
        targetReviveHealth = Math.Max(1L, reviveHp);
        onSuccessCallback = onSuccess;
        onFailCallback = onFail;
        isFinished = false;
        timeRemaining = CountdownDuration;

        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        UpdateModeTexts();
        micSession.Begin();
        RefreshUI();
    }

    void Update()
    {
        if (isFinished || !gameObject.activeInHierarchy) return;

        float dt = Mathf.Min(0.05f, Time.unscaledDeltaTime);
        timeRemaining = Mathf.Max(0f, timeRemaining - dt);
        micSession.Step(dt, isCoopMode, true);

        RefreshUI();

        if (micSession.IsComplete)
        {
            FinishSession(true);
        }
        else if (timeRemaining <= 0f)
        {
            FinishSession(false);
        }
    }

    private void FinishSession(bool succeeded)
    {
        if (isFinished) return;
        isFinished = true;

        micSession.Stop();
        if (cardRect != null) cardRect.anchoredPosition = Vector2.zero;
        gameObject.SetActive(false);

        if (succeeded)
        {
            onSuccessCallback?.Invoke();
        }
        else
        {
            onFailCallback?.Invoke();
        }
    }

    private void UpdateModeTexts()
    {
        if (titleTmp != null)
        {
            titleTmp.text = isCoopMode
                ? "<color=#F8FAFC><b>¡NAVES SIN ENERGÍA! ¡GRITEN!</b></color>"
                : "<color=#F8FAFC><b>¡NAVE SIN ENERGÍA! ¡GRITÁ!</b></color>";
        }

        if (subtitleTmp != null)
        {
            subtitleTmp.text = isCoopMode
                ? "¡Griten juntos al micrófono para encenderlas! (Meta más alta)"
                : "¡Gritá fuerte al micrófono para encenderla y revivir!";
        }

        if (requirementTmp != null)
        {
            requirementTmp.text = isCoopMode
                ? $"<color=#F59E0B><b>2P ({attemptNumber}/{maxAttempts}) • {targetReviveHealth} VIDA</b></color>"
                : $"<color=#38BDF8><b>INTENTO {attemptNumber}/{maxAttempts} • {targetReviveHealth} VIDA</b></color>";
        }

        ScreamMeterUIUtils.UpdateThresholdMarker(micThresholdRect, isCoopMode);
    }

    private void RefreshUI()
    {
        if (timerTmp != null)
        {
            string tCol = timeRemaining <= 5.0f ? "#FF4757" : "#FBBF24";
            timerTmp.text = $"<color={tCol}><b>TIEMPO: {timeRemaining:0.0}s</b></color>";
        }

        if (chargeFillRect != null)
        {
            chargeFillRect.anchorMax = new Vector2(Mathf.Clamp01(micSession.Progress), 1f);
        }

        if (chargeFillImg != null)
        {
            chargeFillImg.color = ScreamMeterUIUtils.EvaluateProgressColor(micSession.Progress);
        }

        if (chargePercentTmp != null)
        {
            chargePercentTmp.text = $"ENERGÍA: {Mathf.RoundToInt(micSession.Progress * 100f)}%";
        }

        ScreamMeterUIUtils.ApplyMicStatusUI(
            micSession,
            isCoopMode,
            micSession.IsComplete,
            micFillRect,
            micFillImg,
            micPromptTmp,
            micDeviceTmp);

        if (cardRect != null)
        {
            if (micSession.ValidScream)
            {
                float amp = Mathf.Lerp(1.5f, 5.5f, micSession.SmoothedMicDisplay);
                cardRect.anchoredPosition = new Vector2(
                    Mathf.Sin(Time.unscaledTime * 68f) * amp,
                    Mathf.Cos(Time.unscaledTime * 54f) * amp * 0.7f
                );
            }
            else
            {
                cardRect.anchoredPosition = Vector2.zero;
            }
        }

        if (cardBorderEdges != null)
        {
            Color borderCol;
            if (micSession.IsBlowing)
            {
                borderCol = new Color(1f, 0.18f, 0.52f, 1f);
            }
            else if (micSession.ValidScream)
            {
                borderCol = new Color(0.28f, 0.98f, 0.64f, 1f);
            }
            else
            {
                borderCol = new Color(0.96f, 0.30f, 0.38f, 0.92f);
            }
            for (int i = 0; i < cardBorderEdges.Length; i++)
            {
                if (cardBorderEdges[i] != null) cardBorderEdges[i].color = borderCol;
            }
        }
    }

    private void EnsureUI()
    {
        if (hasBuiltUI) return;
        hasBuiltUI = true;

        TextMeshProUGUI anyTmp = FindAnyObjectByType<TextMeshProUGUI>();
        if (anyTmp != null) sharedFont = anyTmp.font;

        Image backdrop = gameObject.AddComponent<Image>();
        backdrop.color = new Color(0.01f, 0.02f, 0.06f, 0.88f);
        backdrop.raycastTarget = true;

        GameObject card = new GameObject("ScreamCard", typeof(RectTransform));
        card.transform.SetParent(transform, false);
        cardRect = card.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.anchoredPosition = Vector2.zero;
        cardRect.sizeDelta = new Vector2(620f, 430f);

        Image cardBg = card.AddComponent<Image>();
        cardBg.color = new Color(0.03f, 0.06f, 0.14f, 0.97f);
        cardBg.raycastTarget = true;

        cardBorderEdges = ScreamMeterUIUtils.CreateBorderFrame(card.transform, new Color(0.96f, 0.30f, 0.38f, 0.92f), 3f);

        titleTmp = ScreamMeterUIUtils.CreateLabel(card.transform, "Title", new Vector2(0f, 152f), new Vector2(580f, 48f),
            "<color=#F8FAFC><b>¡NAVE SIN ENERGÍA! ¡GRITÁ!</b></color>", 29, TextAlignmentOptions.Center, sharedFont);

        subtitleTmp = ScreamMeterUIUtils.CreateLabel(card.transform, "Subtitle", new Vector2(0f, 102f), new Vector2(560f, 36f),
            "", 16, TextAlignmentOptions.Center, sharedFont);

        timerTmp = ScreamMeterUIUtils.CreateLabel(card.transform, "Timer", new Vector2(-135f, 34f), new Vector2(270f, 28f),
            "", 16, TextAlignmentOptions.MidlineLeft, sharedFont);
        requirementTmp = ScreamMeterUIUtils.CreateLabel(card.transform, "Requirement", new Vector2(135f, 34f), new Vector2(270f, 28f),
            "", 13, TextAlignmentOptions.MidlineRight, sharedFont);

        GameObject chargeTrack = ScreamMeterUIUtils.CreateBarTrack(card.transform, "ChargeMeter", new Vector2(0f, -14f), new Vector2(540f, 48f),
            new Color(0.01f, 0.03f, 0.07f, 1f), new Color(0.22f, 0.85f, 0.96f, 0.85f), out chargeFillRect, out chargeFillImg, out _);

        for (int i = 1; i <= 3; i++)
        {
            GameObject tick = new GameObject("Tick_" + i, typeof(RectTransform), typeof(Image));
            tick.transform.SetParent(chargeTrack.transform, false);
            RectTransform tRt = tick.GetComponent<RectTransform>();
            float tx = i * 0.25f;
            tRt.anchorMin = new Vector2(tx, 0f);
            tRt.anchorMax = new Vector2(tx, 1f);
            tRt.sizeDelta = new Vector2(2f, 0f);
            Image tImg = tick.GetComponent<Image>();
            tImg.color = new Color(0.02f, 0.04f, 0.08f, 0.7f);
            tImg.raycastTarget = false;
        }

        chargePercentTmp = ScreamMeterUIUtils.CreateLabel(chargeTrack.transform, "Percent", Vector2.zero, new Vector2(520f, 40f),
            "REACTIVACIÓN DEL NÚCLEO: 0%", 17, TextAlignmentOptions.Center, sharedFont);

        ScreamMeterUIUtils.CreateLabel(card.transform, "MicCaption", new Vector2(-135f, -54f), new Vector2(270f, 22f),
            "<color=#94A3B8><b>NIVEL DEL MICRÓFONO</b></color>", 12, TextAlignmentOptions.MidlineLeft, sharedFont);
        micPromptTmp = ScreamMeterUIUtils.CreateLabel(card.transform, "MicPrompt", new Vector2(135f, -54f), new Vector2(270f, 22f),
            "", 12, TextAlignmentOptions.MidlineRight, sharedFont);

        GameObject micTrack = ScreamMeterUIUtils.CreateBarTrack(card.transform, "MicMeter", new Vector2(0f, -80f), new Vector2(540f, 20f),
            new Color(0.015f, 0.035f, 0.08f, 1f), new Color(0.45f, 0.65f, 0.85f, 0.65f), out micFillRect, out micFillImg, out _);

        micThresholdRect = ScreamMeterUIUtils.CreateThresholdMarker(micTrack.transform);

        micDeviceTmp = ScreamMeterUIUtils.CreateLabel(card.transform, "MicDevice", new Vector2(0f, -114f), new Vector2(540f, 28f),
            "", 12, TextAlignmentOptions.Center, sharedFont);

        CreateGiveUpButton(card.transform, new Vector2(0f, -168f), new Vector2(260f, 42f));
    }

    private void CreateGiveUpButton(Transform parent, Vector2 pos, Vector2 size)
    {
        GameObject btnObj = new GameObject("Btn_IrAGameOver", typeof(RectTransform));
        btnObj.transform.SetParent(parent, false);
        RectTransform rt = btnObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        Image bg = btnObj.AddComponent<Image>();
        bg.color = new Color(0.24f, 0.05f, 0.09f, 0.95f);

        ScreamMeterUIUtils.CreateBorderFrame(btnObj.transform, new Color(0.95f, 0.35f, 0.42f, 0.85f), 2f);

        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = bg;
        ColorBlock cb = btn.colors;
        cb.normalColor = new Color(0.24f, 0.05f, 0.09f, 0.95f);
        cb.highlightedColor = new Color(0.45f, 0.10f, 0.16f, 1f);
        cb.pressedColor = new Color(0.16f, 0.03f, 0.06f, 1f);
        btn.colors = cb;
        btn.onClick.AddListener(() => FinishSession(false));

        ScreamMeterUIUtils.CreateLabel(btnObj.transform, "Text", Vector2.zero, size,
            "<color=#FFE4E6><b>RENDIRSE / GAME OVER</b></color>", 15, TextAlignmentOptions.Center, sharedFont);
    }

    void OnDisable()
    {
        micSession.Stop();
    }

    void OnDestroy()
    {
        micSession.Stop();
        if (instance == this) instance = null;
    }
}