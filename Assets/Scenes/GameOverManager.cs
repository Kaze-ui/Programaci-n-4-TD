using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections.Generic;
using System;

[Serializable]
public class LeaderboardEntry
{
    public string playerName;
    public float timeSeconds;
    public int score;
}

[Serializable]
public class LeaderboardData
{
    public List<LeaderboardEntry> entries = new List<LeaderboardEntry>();
}

public class GameOverManager : MonoBehaviour
{
    [Header("Leaderboard UI")]
    [SerializeField] private TextMeshProUGUI[] leaderboardRows; // asignar Row0 a Row9 en orden

    [Header("Ingreso de nombre")]
    [SerializeField] private GameObject nameEntryGroup;
    [SerializeField] private TMP_InputField nameInputField;
    [SerializeField] private Button confirmNameButton;

    [Header("Post registro")]
    [SerializeField] private GameObject postSubmitGroup;
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    private const string SaveKey = "LeaderboardData";
    private const int MaxEntries = 10;

    private LeaderboardData leaderboard;
    private float currentRunTime;
    private int currentRunScore;

    void Start()
    {
        confirmNameButton.onClick.AddListener(ConfirmName);
        LoadLeaderboard();
    }

    void OnEnable()
    {
        nameEntryGroup.SetActive(true);
        postSubmitGroup.SetActive(false);
        RefreshLeaderboardUI();
    }

    // Llamar esto desde el sistema de gameplay cuando termina la partida (victoria o derrota)
    public void SetResults(int score, float timeSeconds)
    {
        currentRunScore = score;
        currentRunTime = timeSeconds;
    }

    private void ConfirmName()
    {
        string name = nameInputField.text.Trim();
        if (string.IsNullOrEmpty(name)) name = "???";

        leaderboard.entries.Add(new LeaderboardEntry
        {
            playerName = name,
            timeSeconds = currentRunTime,
            score = currentRunScore
        });

        // Orden por tiempo de gameplay, de mayor a menor
        leaderboard.entries.Sort((a, b) => b.timeSeconds.CompareTo(a.timeSeconds));

        if (leaderboard.entries.Count > MaxEntries)
            leaderboard.entries.RemoveRange(MaxEntries, leaderboard.entries.Count - MaxEntries);

        SaveLeaderboard();
        RefreshLeaderboardUI();

        nameEntryGroup.SetActive(false);
        postSubmitGroup.SetActive(true);
    }

    private void RefreshLeaderboardUI()
    {
        for (int i = 0; i < leaderboardRows.Length; i++)
        {
            if (i < leaderboard.entries.Count)
            {
                var e = leaderboard.entries[i];
                int minutes = Mathf.FloorToInt(e.timeSeconds / 60f);
                int seconds = Mathf.FloorToInt(e.timeSeconds % 60f);
                leaderboardRows[i].text = $"{i + 1}. {e.playerName} - {minutes:00}:{seconds:00} - {e.score} pts";
            }
            else
            {
                leaderboardRows[i].text = $"{i + 1}. ---";
            }
        }
    }

    private void SaveLeaderboard()
    {
        string json = JsonUtility.ToJson(leaderboard);
        PlayerPrefs.SetString(SaveKey, json);
        PlayerPrefs.Save();
    }

    private void LoadLeaderboard()
    {
        if (PlayerPrefs.HasKey(SaveKey))
        {
            string json = PlayerPrefs.GetString(SaveKey);
            leaderboard = JsonUtility.FromJson<LeaderboardData>(json);
        }
        else
        {
            leaderboard = new LeaderboardData();
        }
    }

    public void RestartGame()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void GoToMainMenu()
    {
        SceneManager.LoadScene(mainMenuSceneName);
    }
}