using UnityEngine;
using TMPro;
using System.Collections;

public class HUDManager : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI healthText;
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI waveText;
    [SerializeField] private TextMeshProUGUI timerText;

    private float bannerAlpha = 0f;
    private string bannerTitle = "";
    private string bannerSubtitle = "";
    private Coroutine bannerCoroutine;

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
    }

    void Update()
    {
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

    public void UpdateHealth(long current, long max)
    {
        if (healthText != null)
        {
            string labelColor = current <= 1 ? "#FF4444" : "#00F5D4";
            healthText.text = $"<color={labelColor}><b>CASCO:</b></color> <color=#FFFFFF>{current}</color><color=#7FE7D9>/{max}</color>";
        }
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

    public void ShowWaveBanner(string title, string subtitle = null)
    {
        bannerTitle = title;
        bannerSubtitle = subtitle;

        if (bannerCoroutine != null)
        {
            StopCoroutine(bannerCoroutine);
        }
        bannerCoroutine = StartCoroutine(BannerDisplayRoutine());
    }

    private IEnumerator BannerDisplayRoutine()
    {
        bannerAlpha = 1f;

        // Se muestra completo durante 3 segundos
        yield return new WaitForSeconds(3.0f);

        // Luego se desvanece lentamente
        float fadeDuration = 1.2f;
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            bannerAlpha = Mathf.Clamp01(1f - (elapsed / fadeDuration));
            yield return null;
        }

        bannerAlpha = 0f;
        bannerCoroutine = null;
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
        DrawWaveBanner();
        DrawBossHealthBar();
    }

    private void DrawWaveBanner()
    {
        if (bannerAlpha <= 0.001f || string.IsNullOrEmpty(bannerTitle)) return;
        if (GameManager.Instance != null && (GameManager.Instance.currentState == GameManager.GameState.Won || GameManager.Instance.currentState == GameManager.GameState.Lost)) return;

        int screenW = Screen.width;
        int screenH = Screen.height;

        int titleSize = Mathf.Clamp(Mathf.RoundToInt(screenH * 0.085f), 36, 120);
        int subtitleSize = Mathf.Clamp(Mathf.RoundToInt(screenH * 0.045f), 20, 65);

        bool isBoss = bannerTitle.Contains("JEFE") || bannerTitle.Contains("NODRIZA");

        GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = titleSize,
            fontStyle = FontStyle.Bold
        };

        Color titleColor = isBoss ? new Color(1f, 0.22f, 0.22f, bannerAlpha) : new Color(1f, 1f, 1f, bannerAlpha);
        Color shadowColor = new Color(0f, 0f, 0f, bannerAlpha * 0.85f);

        float centerY = screenH * 0.40f;
        float titleHeight = titleSize * 1.35f;
        Rect titleRect = new Rect(0, centerY - (titleHeight * 0.5f), screenW, titleHeight);

        // Sombra de contraste para legibilidad sobre el fondo
        titleStyle.normal.textColor = shadowColor;
        GUI.Label(new Rect(titleRect.x + 3, titleRect.y + 3, titleRect.width, titleRect.height), bannerTitle, titleStyle);

        // Título principal frontal
        titleStyle.normal.textColor = titleColor;
        GUI.Label(titleRect, bannerTitle, titleStyle);

        // Subtítulo ("¡Prepárate!") si existe
        if (!string.IsNullOrEmpty(bannerSubtitle))
        {
            GUIStyle subStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = subtitleSize,
                fontStyle = FontStyle.Bold
            };

            float subY = titleRect.y + titleHeight + 2f;
            float subHeight = subtitleSize * 1.35f;
            Rect subRect = new Rect(0, subY, screenW, subHeight);

            subStyle.normal.textColor = shadowColor;
            GUI.Label(new Rect(subRect.x + 2, subRect.y + 2, subRect.width, subRect.height), bannerSubtitle, subStyle);

            subStyle.normal.textColor = new Color(1f, 0.88f, 0.35f, bannerAlpha);
            GUI.Label(subRect, bannerSubtitle, subStyle);
        }
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