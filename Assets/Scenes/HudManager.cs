using UnityEngine;
using TMPro;

public class HUDManager : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI healthText;
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI waveText;
    [SerializeField] private TextMeshProUGUI timerText;

    public void UpdateHealth(int current, int max)
    {
        healthText.text = $"Vida: {current}/{max}";
    }

    public void UpdateScore(int score)
    {
        scoreText.text = $"Puntos: {score}";
    }

    public void UpdateWave(int current, int max)
    {
        waveText.text = $"Oleada {current}/{max}";
    }

    public void UpdateTimer(float elapsedSeconds)
    {
        int minutes = Mathf.FloorToInt(elapsedSeconds / 60f);
        int seconds = Mathf.FloorToInt(elapsedSeconds % 60f);
        timerText.text = $"{minutes:00}:{seconds:00}";
    }
}