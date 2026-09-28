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

    private const string SaveKey = "LeaderboardData";
    private const int MaxEntries = 10;

    private LeaderboardData leaderboard;
    private float currentRunTime;
    private int currentRunScore;
    private bool isVictoryRun = false;

    void Awake()
    {
        // Se carga acá (no en Start) porque OnEnable puede dispararse antes que Start
        // la primera vez que el panel se activa, y necesitamos "leaderboard" ya listo.
        LoadLeaderboard();
    }

    void Start()
    {
        if (confirmNameButton != null) confirmNameButton.onClick.AddListener(ConfirmName);
    }

    void OnEnable()
    {
        if (nameEntryGroup != null) nameEntryGroup.SetActive(true);
        if (postSubmitGroup != null) postSubmitGroup.SetActive(false);
        RefreshLeaderboardUI();
        EnsureGameOverTitle();
    }

    // Llamar esto desde el sistema de gameplay cuando termina la partida (victoria o derrota)
    public void SetResults(int score, float timeSeconds)
    {
        SetResults(score, timeSeconds, false);
    }

    public void SetResults(int score, float timeSeconds, bool isVictory)
    {
        isVictoryRun = isVictory;
        gameObject.SetActive(true);
        currentRunScore = score;
        currentRunTime = timeSeconds;
        EnsureGameOverTitle();
    }

    private RectTransform GetOrCreateUIGameObject(string name)
    {
        Transform existing = transform.Find(name);
        if (existing != null)
        {
            RectTransform existingRt = existing.GetComponent<RectTransform>();
            if (existingRt != null)
            {
                return existingRt;
            }
            Destroy(existing.gameObject);
        }

        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(transform, false);
        return go.GetComponent<RectTransform>();
    }

    private void EnsureGameOverTitle()
    {
        // 1. TÍTULO PRINCIPAL: "GAME OVER" (En mayúsculas)
        RectTransform rt = GetOrCreateUIGameObject("Title_GameOver");
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -65f);
        rt.sizeDelta = new Vector2(900f, 110f);

        TextMeshProUGUI tmp = rt.GetComponent<TextMeshProUGUI>() ?? rt.gameObject.AddComponent<TextMeshProUGUI>();

        if (leaderboardRows != null && leaderboardRows.Length > 0 && leaderboardRows[0] != null)
        {
            tmp.font = leaderboardRows[0].font;
        }

        string colorHex = isVictoryRun ? "#38BDF8" : "#EF4444";
        tmp.text = $"<color={colorHex}><b>GAME OVER</b></color>";
        tmp.fontSize = 86;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.characterSpacing = 8f;
        tmp.raycastTarget = false;

        // 2. SUBTÍTULO
        RectTransform subRt = GetOrCreateUIGameObject("Subtitle_GameOver");
        subRt.anchorMin = new Vector2(0.5f, 1f);
        subRt.anchorMax = new Vector2(0.5f, 1f);
        subRt.pivot = new Vector2(0.5f, 1f);
        subRt.anchoredPosition = new Vector2(0f, -155f);
        subRt.sizeDelta = new Vector2(800f, 40f);

        TextMeshProUGUI subTmp = subRt.GetComponent<TextMeshProUGUI>() ?? subRt.gameObject.AddComponent<TextMeshProUGUI>();
        if (tmp.font != null) subTmp.font = tmp.font;

        subTmp.text = isVictoryRun
            ? "<color=#FBBF24>¡MISIÓN CUMPLIDA - LEVIATÁN NODRIZA DERROTADO!</color>"
            : "<color=#94A3B8>NAVE DESTRUIDA - FIN DE LA TRANSMISIÓN</color>";
        subTmp.fontSize = 20;
        subTmp.fontStyle = FontStyles.Bold;
        subTmp.alignment = TextAlignmentOptions.Center;
        subTmp.characterSpacing = 6f;
        subTmp.raycastTarget = false;
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
        SceneController.Instance.RestartCurrentScene();
    }

    public void GoToMainMenu()
    {
        SceneController.Instance.LoadMainMenuScene();
    }
}