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

    public void UpdateHealth(int current, int max)
    {
        if (healthText != null)
            healthText.text = $"CASCO: {current}/{max}";
    }

    public void UpdateScore(int score)
    {
        if (scoreText != null)
            scoreText.text = $"CRÉDITOS: {score}";
    }

    public void UpdateWave(int current, int max)
    {
        if (waveText != null)
            waveText.text = $"SECTOR {current}/{max}";
    }

    public void SetWaveText(string text)
    {
        if (waveText != null)
            waveText.text = text;
    }

    public void UpdateTimer(float secondsRemaining)
    {
        if (timerText != null)
        {
            int totalSeconds = Mathf.CeilToInt(Mathf.Max(0f, secondsRemaining));
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            timerText.text = $"DEFENSA: {minutes:00}:{seconds:00}";
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

    void OnGUI()
    {
        if (bannerAlpha <= 0.001f || string.IsNullOrEmpty(bannerTitle)) return;
        if (GameManager.Instance != null && (GameManager.Instance.currentState == GameManager.GameState.Won || GameManager.Instance.currentState == GameManager.GameState.Lost)) return;

        int screenW = Screen.width;
        int screenH = Screen.height;

        int titleSize = Mathf.Clamp(Mathf.RoundToInt(screenH * 0.085f), 36, 120);
        int subtitleSize = Mathf.Clamp(Mathf.RoundToInt(screenH * 0.045f), 20, 65);

        bool isBoss = bannerTitle.Contains("JEFE");

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

            // Sombra del subtítulo
            subStyle.normal.textColor = shadowColor;
            GUI.Label(new Rect(subRect.x + 2, subRect.y + 2, subRect.width, subRect.height), bannerSubtitle, subStyle);

            // Subtítulo frontal en color dorado enérgico
            subStyle.normal.textColor = new Color(1f, 0.88f, 0.35f, bannerAlpha);
            GUI.Label(subRect, bannerSubtitle, subStyle);
        }
    }
}