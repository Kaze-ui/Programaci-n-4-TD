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
    private CanvasGroup bannerCanvasGroup;
    private TextMeshProUGUI bannerTitleText;
    private TextMeshProUGUI bannerSubText;
    private Coroutine bannerCoroutine;
    private TextMeshProUGUI p2PromptText;

    // --- MÍTICA BARRA DE VIDA DEL JEFE ---
    private bool isBossBarActive = false;
    private string bossName = "Leviatán nodriza";
    private float bossMaxHealth = 250f;
    private float bossCurrentHealth = 250f;
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

    private void SetupHudLayout()
    {
        // 1. Créditos a la derecha (esquina superior derecha)
        if (scoreText != null)
        {
            RectTransform rtScore = scoreText.rectTransform;
            rtScore.anchorMin = new Vector2(1f, 1f);
            rtScore.anchorMax = new Vector2(1f, 1f);
            rtScore.pivot = new Vector2(1f, 1f);
            rtScore.anchoredPosition = new Vector2(-25f, -15f);
            rtScore.sizeDelta = new Vector2(240f, 50f);
            scoreText.alignment = TextAlignmentOptions.Right;
        }

        // 2. Cronómetro de Defensa a la izquierda de Créditos (sin solaparse)
        if (timerText != null)
        {
            RectTransform rtTimer = timerText.rectTransform;
            rtTimer.anchorMin = new Vector2(1f, 1f);
            rtTimer.anchorMax = new Vector2(1f, 1f);
            rtTimer.pivot = new Vector2(1f, 1f);
            rtTimer.anchoredPosition = new Vector2(-280f, -15f);
            rtTimer.sizeDelta = new Vector2(250f, 50f);
            timerText.alignment = TextAlignmentOptions.Right;
        }

        EnsureP2PromptUI();
    }

    private void EnsureP2PromptUI()
    {
        if (p2PromptText != null) return;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        GameObject p2Obj = new GameObject("P2PromptText", typeof(RectTransform), typeof(TextMeshProUGUI));
        p2Obj.transform.SetParent(transform.parent != null ? transform.parent : canvas.transform, false);

        RectTransform rt = p2Obj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(-25f, 20f);
        rt.sizeDelta = new Vector2(560f, 50f);

        p2PromptText = p2Obj.GetComponent<TextMeshProUGUI>();
        if (healthText != null && healthText.font != null) p2PromptText.font = healthText.font;
        p2PromptText.alignment = TextAlignmentOptions.Right;
        p2PromptText.fontSize = 22;
        p2PromptText.fontStyle = FontStyles.Bold;
        p2PromptText.enableWordWrapping = false;
        p2PromptText.raycastTarget = false;
        p2PromptText.text = "";
    }

    public void UpdateP2Status(bool isConnected, bool isJoined, long currentHealth, long maxHealth)
    {
        EnsureP2PromptUI();
        if (p2PromptText == null) return;

        if (isJoined)
        {
            if (currentHealth <= 0)
            {
                p2PromptText.text = "<color=#FF2222><b>2P VIDA:</b> DERRIBADO</color>";
            }
            else
            {
                string labelColor = currentHealth <= 1 ? "#FF2222" : "#FF5555";
                p2PromptText.text = $"<color={labelColor}><b>2P VIDA:</b></color> <color=#FFFFFF>{currentHealth}</color><color=#FFAAAA>/{maxHealth}</color>";
            }
        }
        else if (isConnected)
        {
            if (GameManager.GetConnectedGamepadCount() >= 2)
            {
                p2PromptText.text = "<color=#7DD3FC><b>1P: Mando 1</b></color> <color=#94A3B8>|</color> <color=#FFDF59><b>2P (Mando 2): Presione Start</b></color>";
            }
            else
            {
                p2PromptText.text = "<color=#FFDF59><b>2P: Presione start para jugar</b></color>";
            }
        }
        else
        {
            p2PromptText.text = "";
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
            string prefix = hasPlayer2 ? "1P VIDA:" : "VIDA:";
            string labelColor = current <= 1 ? "#FF4444" : "#00F5D4";
            healthText.text = $"<color={labelColor}><b>{prefix}</b></color> <color=#FFFFFF>{current}</color><color=#7FE7D9>/{max}</color>";
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
            scoreText.text = $"<color=#FFD700><b>CRÉDITOS:</b></color> <color=#FFFFFF>{score}</color>";
    }

    public void UpdateWave(int current, int max)
    {
        if (waveText != null)
            waveText.text = $"<color=#38BDF8><b>SECTOR:</b></color> <color=#FFFFFF>{current}</color><color=#93C5FD>/{max}</color>";
    }

    public void SetWaveText(string text)
    {
        if (waveText != null)
            waveText.text = $"<color=#38BDF8><b>{text}</b></color>";
    }

    public void UpdateTimer(float secondsRemaining)
    {
        if (timerText != null)
        {
            // Si la barra del jefe está activa, despejar el texto del cronómetro para evitar solapamientos
            if (isBossBarActive && !isBossDefeated)
            {
                timerText.text = "";
                return;
            }

            int totalSeconds = Mathf.CeilToInt(Mathf.Max(0f, secondsRemaining));
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            string timerColor = secondsRemaining <= 10f ? "#FF3B30" : "#FF9F43";
            timerText.text = $"<color={timerColor}><b>DEFENSA:</b></color> <color=#FFFFFF>{minutes:00}:{seconds:00}</color>";
        }
    }

    private void EnsureBannerUI()
    {
        if (bannerObj != null) return;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

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

        // Título del banner
        GameObject titleObj = new GameObject("BannerTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleObj.transform.SetParent(bannerObj.transform, false);
        RectTransform titleRt = titleObj.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 0.5f);
        titleRt.anchorMax = new Vector2(1f, 0.5f);
        titleRt.pivot = new Vector2(0.5f, 0.5f);
        titleRt.anchoredPosition = new Vector2(0f, 90f);
        titleRt.sizeDelta = new Vector2(0f, 110f);

        bannerTitleText = titleObj.GetComponent<TextMeshProUGUI>();
        if (healthText != null && healthText.font != null) bannerTitleText.font = healthText.font;
        bannerTitleText.alignment = TextAlignmentOptions.Center;
        bannerTitleText.fontSize = 54;
        bannerTitleText.fontStyle = FontStyles.Bold;
        bannerTitleText.color = Color.white;
        bannerTitleText.enableWordWrapping = false;
        bannerTitleText.raycastTarget = false;

        // Subtítulo del banner
        GameObject subObj = new GameObject("BannerSubtitle", typeof(RectTransform), typeof(TextMeshProUGUI));
        subObj.transform.SetParent(bannerObj.transform, false);
        RectTransform subRt = subObj.GetComponent<RectTransform>();
        subRt.anchorMin = new Vector2(0f, 0.5f);
        subRt.anchorMax = new Vector2(1f, 0.5f);
        subRt.pivot = new Vector2(0.5f, 0.5f);
        subRt.anchoredPosition = new Vector2(0f, 30f);
        subRt.sizeDelta = new Vector2(0f, 60f);

        bannerSubText = subObj.GetComponent<TextMeshProUGUI>();
        if (healthText != null && healthText.font != null) bannerSubText.font = healthText.font;
        bannerSubText.alignment = TextAlignmentOptions.Center;
        bannerSubText.fontSize = 28;
        bannerSubText.fontStyle = FontStyles.Bold;
        bannerSubText.color = new Color(1f, 0.88f, 0.35f, 1f);
        bannerSubText.enableWordWrapping = false;
        bannerSubText.raycastTarget = false;

        bannerObj.SetActive(false);
    }

    public void ShowWaveBanner(string title, string subtitle = null)
    {
        EnsureBannerUI();
        if (bannerObj == null) return;

        bool isBoss = title.Contains("JEFE") || title.Contains("NODRIZA") || title.Contains("ALERTA");
        bannerTitleText.text = title;
        bannerTitleText.color = isBoss ? new Color(1f, 0.25f, 0.25f, 1f) : Color.white;

        if (!string.IsNullOrEmpty(subtitle))
        {
            bannerSubText.text = subtitle;
            bannerSubText.gameObject.SetActive(true);
        }
        else
        {
            bannerSubText.gameObject.SetActive(false);
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

        bannerCanvasGroup.alpha = 1f;

        // Se muestra completo durante 2.5s usando tiempo sin escalar para que no se quede congelado
        float displayTime = 2.5f;
        float elapsed = 0f;
        while (elapsed < displayTime)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // Desvanecimiento suave
        float fadeDuration = 1.0f;
        elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            bannerCanvasGroup.alpha = Mathf.Clamp01(1f - (elapsed / fadeDuration));
            yield return null;
        }

        bannerCanvasGroup.alpha = 0f;
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
        if (bannerObj != null)
        {
            bannerObj.SetActive(false);
        }
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
        isBossBarActive = true;

        if (timerText != null)
        {
            timerText.text = "";
        }
    }

    public void UpdateBossHealth(float currentHp)
    {
        if (!isBossBarActive) return;

        if (currentHp < bossCurrentHealth)
        {
            lastDamageTime = Time.time;
            bossTrailingFill = Mathf.Max(bossTrailingFill, bossDisplayFill);
        }

        bossCurrentHealth = Mathf.Max(0f, currentHp);
    }

    public void OnBossDefeated()
    {
        isBossDefeated = true;
        bossCurrentHealth = 0f;
    }

    void OnGUI()
    {
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

        // Título frontal vibrante centrado
        Color nameColor = (hpPct < 0.25f && !isBossDefeated)
            ? new Color(1f, 0.25f, 0.25f, bossBarAlpha)
            : new Color(1f, 0.88f, 0.35f, bossBarAlpha);
        nameStyle.normal.textColor = nameColor;
        GUI.Label(new Rect(x, y - 26f, barWidth, 24f), titleText, nameStyle);

        // 2. FONDO Y MARCO DEL CONTENEDOR (Cyber Obsidian & Neon Frame)
        // Borde sombra exterior negro
        GUI.color = new Color(0f, 0f, 0f, bossBarAlpha * 0.90f);
        GUI.DrawTexture(new Rect(x - 3, y - 3, barWidth + 6, barHeight + 6), white);

        // Fondo oscuro de la barra
        GUI.color = new Color(0.04f, 0.07f, 0.14f, bossBarAlpha * 0.96f);
        GUI.DrawTexture(new Rect(x, y, barWidth, barHeight), white);

        // Borde Neón: azul cian normal, rojo furia parpadeante en fase crítica (< 25%)
        Color borderColor;
        if (hpPct < 0.25f && !isBossDefeated)
        {
            float pulse = Mathf.Sin(Time.time * 14f) * 0.5f + 0.5f;
            borderColor = Color.Lerp(new Color(1f, 0.15f, 0.25f, 1f), new Color(1f, 0.75f, 0.75f, 1f), pulse);
        }
        else
        {
            borderColor = new Color(0.22f, 0.74f, 0.97f, 0.92f);
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
            GUI.color = new Color(1f, 0.88f, 0.35f, bossBarAlpha * 0.88f);
            GUI.DrawTexture(new Rect(innerX, innerY, trailW, innerH), white);
        }

        // 4. BARRA DE VIDA PRINCIPAL (Gradiente de Energía Carmesí / Fuego)
        if (bossDisplayFill > 0.001f)
        {
            float fillW = innerW * Mathf.Clamp01(bossDisplayFill);

            Color barColor;
            if (hpPct > 0.50f)
            {
                barColor = new Color(0.95f, 0.18f, 0.25f, 1f); // Carmesí intenso
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
        GUIStyle hpStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 14,
            fontStyle = FontStyle.Bold
        };

        string hpText = $"{Mathf.CeilToInt(bossCurrentHealth)} / {Mathf.CeilToInt(bossMaxHealth)} HP   ({Mathf.RoundToInt(hpPct * 100f)}%)";

        // Sombra del texto
        hpStyle.normal.textColor = new Color(0f, 0f, 0f, bossBarAlpha * 0.95f);
        GUI.Label(new Rect(x + 1, y + 1, barWidth, barHeight), hpText, hpStyle);

        // Texto frontal blanco limpio
        hpStyle.normal.textColor = new Color(1f, 1f, 1f, bossBarAlpha * 0.98f);
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