using UnityEngine;
using TMPro;
using System.Collections;

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
        rt.sizeDelta = new Vector2(420f, 50f);

        p2PromptText = p2Obj.GetComponent<TextMeshProUGUI>();
        if (healthText != null && healthText.font != null) p2PromptText.font = healthText.font;
        p2PromptText.alignment = TextAlignmentOptions.Right;
        p2PromptText.fontSize = 24;
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
                p2PromptText.text = "<color=#FF2222><b>2P CASCO:</b> DERRIBADO</color>";
            }
            else
            {
                string labelColor = currentHealth <= 1 ? "#FF2222" : "#FF5555";
                p2PromptText.text = $"<color={labelColor}><b>2P CASCO:</b></color> <color=#FFFFFF>{currentHealth}</color><color=#FFAAAA>/{maxHealth}</color>";
            }
        }
        else if (isConnected)
        {
            p2PromptText.text = "<color=#FFDF59><b>2P: Presione start para jugar</b></color>";
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
        if (GameManager.Instance != null && (GameManager.Instance.currentState == GameManager.GameState.Won || GameManager.Instance.currentState == GameManager.GameState.Lost))
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
            string prefix = hasPlayer2 ? "1P CASCO:" : "CASCO:";
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
        if (GameManager.Instance != null && (GameManager.Instance.currentState == GameManager.GameState.Won || GameManager.Instance.currentState == GameManager.GameState.Lost)) return;

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