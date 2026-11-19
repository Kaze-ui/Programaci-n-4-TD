using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TMPro;
using System.Collections.Generic;
using System;

[Serializable]
public class LeaderboardEntry
{
    public string playerName;
    public string player2Name;
    public string teamName;
    public float timeSeconds;
    public int score;
    public int waveReached = 1;
    public bool reachedBoss = false;
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
    private const string CoopSaveKey = "LeaderboardData_2P";
    private const int MaxEntries = 10;

    private LeaderboardData leaderboard;
    private LeaderboardData coopLeaderboard;
    private float currentRunTime;
    private int currentRunScore;
    private bool isVictoryRun = false;
    private bool isCoopRun = false;
    private int currentRunWaveReached = 1;
    private bool reachedBossInRun = false;
    private LeaderboardEntry lastSubmittedEntry;
    private bool hasBuiltModernUI = false;

    // Fuentes dinámicas modernas cacheadas para estética sci-fi
    private static TMP_FontAsset s_DisplayFont;
    private static TMP_FontAsset s_MonoFont;

    // Sprites procedurales cacheados para tarjetas y botones
    private static Sprite s_LeaderboardCardSprite;
    private static Sprite s_ActionCardSpriteDefeat;
    private static Sprite s_ActionCardSpriteVictory;
    private static Sprite s_StatTileSprite;
    private static Sprite s_PlaqueDefeatSprite;
    private static Sprite s_PlaqueVictorySprite;
    private static Sprite s_InputSprite;
    private static Sprite s_TeamInputSprite;
    private static Sprite s_Pilot1InputSprite;
    private static Sprite s_Pilot2InputSprite;
    private static Sprite s_ConfirmBtnSprite;
    private static Sprite s_RestartBtnSprite;
    private static Sprite s_MenuBtnSprite;
    private static Sprite s_RowDarkA;
    private static Sprite s_RowDarkB;
    private static Sprite s_RowGold;
    private static Sprite s_RowSilver;
    private static Sprite s_RowBronze;
    private static Sprite s_RowHighlight;

    // Referencias creadas dinámicamente para actualizar métricas y temas
    private TextMeshProUGUI titleText;
    private Image subtitlePlaqueImg;
    private TextMeshProUGUI subtitleText;
    private TextMeshProUGUI leaderboardHeaderText;
    private TextMeshProUGUI leaderboardLegendText;
    private Image actionCardImg;
    private Image actionDividerImg;
    private TextMeshProUGUI actionHeaderText;
    private TextMeshProUGUI scoreValueText;
    private TextMeshProUGUI timeValueText;
    private TextMeshProUGUI entryPromptText;
    private TMP_InputField coopTeamInputField;
    private TMP_InputField coopP2InputField;
    private GameObject[] coopOnlyObjects = Array.Empty<GameObject>();
    private readonly Image[] rowBgImages = new Image[MaxEntries];
    private readonly TextMeshProUGUI[] rowCoopSubTexts = new TextMeshProUGUI[MaxEntries];

    void Awake()
    {
        // Se carga acá (no en Start) porque OnEnable puede dispararse antes que Start
        // la primera vez que el panel se activa, y necesitamos "leaderboard" ya listo.
        LoadLeaderboard();
        EnsureModernUI();
    }

    void Start()
    {
        if (confirmNameButton != null)
        {
            confirmNameButton.onClick.RemoveListener(ConfirmName);
            confirmNameButton.onClick.AddListener(ConfirmName);
        }

        WireInputNavigation();
    }

    void Update()
    {
        if (!isCoopRun || nameEntryGroup == null || !nameEntryGroup.activeInHierarchy) return;
        if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
        {
            CycleCoopInputFocus();
        }
    }

    void OnEnable()
    {
        transform.SetAsLastSibling();
        lastSubmittedEntry = null;
        if (GameManager.Instance != null) isCoopRun = GameManager.Instance.IsPlayer2Joined;
        LoadLeaderboard();

        EnsureModernUI();

        if (nameEntryGroup != null) nameEntryGroup.SetActive(true);
        if (postSubmitGroup != null) postSubmitGroup.SetActive(false);
        ClearInputs();

        ApplyThemeAndSummary();
        RefreshLeaderboardUI();
    }

    // Llamar esto desde el sistema de gameplay cuando termina la partida (victoria o derrota)
    public void SetResults(int score, float timeSeconds)
    {
        bool coop = GameManager.Instance != null && GameManager.Instance.IsPlayer2Joined;
        SetResults(score, timeSeconds, false, coop, 1, false);
    }

    public void SetResults(int score, float timeSeconds, bool isVictory)
    {
        bool coop = GameManager.Instance != null && GameManager.Instance.IsPlayer2Joined;
        SetResults(score, timeSeconds, isVictory, coop, 1, isVictory);
    }

    public void SetResults(int score, float timeSeconds, bool isVictory, bool isCoop)
    {
        SetResults(score, timeSeconds, isVictory, isCoop, 1, isVictory);
    }

    public void SetResults(int score, float timeSeconds, bool isVictory, bool isCoop, int waveReached, bool reachedBoss)
    {
        isVictoryRun = isVictory;
        isCoopRun = isCoop;
        currentRunScore = score;
        currentRunTime = timeSeconds;
        currentRunWaveReached = Mathf.Max(1, waveReached);
        reachedBossInRun = reachedBoss || isVictory;
        lastSubmittedEntry = null;
        LoadLeaderboard();

        gameObject.SetActive(true);
        EnsureModernUI();
        ClearInputs();
        ApplyThemeAndSummary();
        RefreshLeaderboardUI();
    }

    private void EnsureModernUI()
    {
        EnsureModernFonts();
        EnsureSprites();

        // Fondo oscuro profundo tipo cabina espacial para contraste total
        Image panelBg = GetComponent<Image>();
        if (panelBg != null)
        {
            panelBg.sprite = null;
            panelBg.color = new Color(0.015f, 0.028f, 0.072f, 0.94f);
            panelBg.raycastTarget = true;
        }

        if (!hasBuiltModernUI)
        {
            hasBuiltModernUI = true;
            BuildHeader();
            RectTransform lbCard = BuildLeaderboardCard();
            ArrangeLeaderboardRows(lbCard);
            RectTransform actionCard = BuildActionMissionCard();
            StyleActionGroups(actionCard);
        }
    }

    private void EnsureModernFonts()
    {
        TMP_FontAsset baseFont = null;
        if (leaderboardRows != null && leaderboardRows.Length > 0 && leaderboardRows[0] != null)
        {
            baseFont = leaderboardRows[0].font;
        }
        if (baseFont == null && nameInputField != null)
        {
            baseFont = nameInputField.fontAsset;
        }

        if (s_DisplayFont == null)
        {
            s_DisplayFont = TryCreateDynamicFont("Segoe UI", "Bold", 72, baseFont)
                ?? TryCreateDynamicFont("Bahnschrift", "SemiBold", 72, baseFont)
                ?? baseFont;
        }

        if (s_MonoFont == null)
        {
            s_MonoFont = TryCreateDynamicFont("Consolas", "Bold", 64, s_DisplayFont)
                ?? s_DisplayFont;
        }
    }

    private static TMP_FontAsset TryCreateDynamicFont(string family, string style, int size, TMP_FontAsset fallback)
    {
        try
        {
            TMP_FontAsset created = TMP_FontAsset.CreateFontAsset(family, style, size);
            if (created != null)
            {
                if (fallback != null)
                {
                    if (created.fallbackFontAssetTable == null) created.fallbackFontAssetTable = new List<TMP_FontAsset>();
                    if (!created.fallbackFontAssetTable.Contains(fallback)) created.fallbackFontAssetTable.Add(fallback);
                }
                return created;
            }
        }
        catch
        {
            // En caso de plataforma sin fuentes de escritorio, usa fallback limpio
        }
        return null;
    }

    private static void EnsureSprites()
    {
        if (s_LeaderboardCardSprite == null)
        {
            s_LeaderboardCardSprite = MainMenu.GeneratePanelSprite(
                680, 610, 14, 2,
                new Color(0.22f, 0.74f, 0.97f, 0.70f),
                new Color(0.035f, 0.075f, 0.16f, 0.95f),
                new Color(0.015f, 0.030f, 0.075f, 0.97f));
        }

        if (s_ActionCardSpriteDefeat == null)
        {
            s_ActionCardSpriteDefeat = MainMenu.GeneratePanelSprite(
                560, 610, 14, 2,
                new Color(0.96f, 0.30f, 0.36f, 0.72f),
                new Color(0.055f, 0.045f, 0.12f, 0.95f),
                new Color(0.020f, 0.025f, 0.065f, 0.97f));
        }

        if (s_ActionCardSpriteVictory == null)
        {
            s_ActionCardSpriteVictory = MainMenu.GeneratePanelSprite(
                560, 610, 14, 2,
                new Color(0.28f, 0.85f, 0.98f, 0.78f),
                new Color(0.035f, 0.085f, 0.16f, 0.95f),
                new Color(0.015f, 0.035f, 0.080f, 0.97f));
        }

        if (s_StatTileSprite == null)
        {
            s_StatTileSprite = MainMenu.GeneratePanelSprite(
                238, 114, 10, 2,
                new Color(0.28f, 0.62f, 0.88f, 0.48f),
                new Color(0.04f, 0.09f, 0.19f, 0.96f),
                new Color(0.02f, 0.045f, 0.11f, 0.98f));
        }

        if (s_PlaqueDefeatSprite == null)
        {
            s_PlaqueDefeatSprite = MainMenu.GeneratePanelSprite(
                780, 48, 10, 2,
                new Color(0.96f, 0.32f, 0.36f, 0.82f),
                new Color(0.11f, 0.035f, 0.075f, 0.96f),
                new Color(0.04f, 0.015f, 0.04f, 0.98f));
        }

        if (s_PlaqueVictorySprite == null)
        {
            s_PlaqueVictorySprite = MainMenu.GeneratePanelSprite(
                780, 48, 10, 2,
                new Color(0.22f, 0.78f, 0.98f, 0.85f),
                new Color(0.035f, 0.09f, 0.18f, 0.96f),
                new Color(0.015f, 0.04f, 0.09f, 0.98f));
        }

        if (s_InputSprite == null)
        {
            s_InputSprite = MainMenu.GeneratePanelSprite(
                410, 56, 10, 2,
                new Color(0.22f, 0.74f, 0.97f, 0.85f),
                new Color(0.03f, 0.08f, 0.16f, 0.98f),
                new Color(0.015f, 0.04f, 0.09f, 0.98f));
        }

        if (s_TeamInputSprite == null)
        {
            s_TeamInputSprite = MainMenu.GeneratePanelSprite(
                440, 46, 9, 2,
                new Color(0.98f, 0.75f, 0.18f, 0.88f),
                new Color(0.05f, 0.08f, 0.16f, 0.98f),
                new Color(0.02f, 0.04f, 0.09f, 0.98f));
        }

        if (s_Pilot1InputSprite == null)
        {
            s_Pilot1InputSprite = MainMenu.GeneratePanelSprite(
                212, 46, 9, 2,
                new Color(0.22f, 0.74f, 0.97f, 0.88f),
                new Color(0.03f, 0.08f, 0.16f, 0.98f),
                new Color(0.015f, 0.04f, 0.09f, 0.98f));
        }

        if (s_Pilot2InputSprite == null)
        {
            s_Pilot2InputSprite = MainMenu.GeneratePanelSprite(
                212, 46, 9, 2,
                new Color(0.22f, 0.88f, 0.48f, 0.88f),
                new Color(0.03f, 0.10f, 0.14f, 0.98f),
                new Color(0.015f, 0.05f, 0.08f, 0.98f));
        }

        if (s_ConfirmBtnSprite == null)
        {
            s_ConfirmBtnSprite = MainMenu.GeneratePanelSprite(
                360, 58, 9, 2,
                new Color(0.22f, 0.88f, 0.45f, 0.95f),
                new Color(0.04f, 0.18f, 0.14f, 0.96f),
                new Color(0.02f, 0.09f, 0.08f, 0.98f));
        }

        if (s_RestartBtnSprite == null)
        {
            s_RestartBtnSprite = MainMenu.GeneratePanelSprite(
                360, 58, 9, 2,
                new Color(0.22f, 0.74f, 0.97f, 0.95f),
                new Color(0.04f, 0.15f, 0.28f, 0.96f),
                new Color(0.02f, 0.07f, 0.15f, 0.98f));
        }

        if (s_MenuBtnSprite == null)
        {
            s_MenuBtnSprite = MainMenu.GeneratePanelSprite(
                360, 58, 9, 2,
                new Color(0.98f, 0.75f, 0.14f, 0.90f),
                new Color(0.18f, 0.12f, 0.04f, 0.96f),
                new Color(0.09f, 0.06f, 0.02f, 0.98f));
        }

        if (s_RowDarkA == null)
        {
            s_RowDarkA = MakeRowSprite(new Color(0.16f, 0.30f, 0.48f, 0.28f), new Color(0.045f, 0.085f, 0.165f, 0.88f));
            s_RowDarkB = MakeRowSprite(new Color(0.14f, 0.25f, 0.40f, 0.22f), new Color(0.030f, 0.060f, 0.125f, 0.88f));
            s_RowGold = MakeRowSprite(new Color(0.98f, 0.75f, 0.18f, 0.65f), new Color(0.14f, 0.10f, 0.03f, 0.92f));
            s_RowSilver = MakeRowSprite(new Color(0.75f, 0.84f, 0.95f, 0.50f), new Color(0.08f, 0.11f, 0.18f, 0.90f));
            s_RowBronze = MakeRowSprite(new Color(0.90f, 0.52f, 0.24f, 0.50f), new Color(0.12f, 0.07f, 0.04f, 0.90f));
            s_RowHighlight = MakeRowSprite(new Color(0.22f, 0.88f, 0.58f, 0.90f), new Color(0.04f, 0.20f, 0.16f, 0.95f));
        }
    }

    private static Sprite MakeRowSprite(Color border, Color body)
    {
        return MainMenu.GeneratePanelSprite(636, 43, 7, 1, border, body, body);
    }

    private RectTransform GetOrCreateUIGameObject(string objName, Transform parent = null)
    {
        Transform targetParent = parent != null ? parent : transform;
        Transform existing = targetParent.Find(objName);
        if (existing != null)
        {
            RectTransform existingRt = existing.GetComponent<RectTransform>();
            if (existingRt != null) return existingRt;
            Destroy(existing.gameObject);
        }

        GameObject go = new GameObject(objName, typeof(RectTransform));
        go.transform.SetParent(targetParent, false);
        return go.GetComponent<RectTransform>();
    }

    private void BuildHeader()
    {
        RectTransform rt = GetOrCreateUIGameObject("Title_GameOver");
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -44f);
        rt.sizeDelta = new Vector2(960f, 96f);

        titleText = rt.GetComponent<TextMeshProUGUI>() ?? rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (s_DisplayFont != null) titleText.font = s_DisplayFont;
        titleText.fontSize = 78;
        titleText.fontStyle = FontStyles.Bold;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.characterSpacing = 10f;
        titleText.raycastTarget = false;

        // Placa oscura contrastante para que el subtítulo ("NAVE DESTRUIDA...") siempre sea nítido
        RectTransform plaqueRt = GetOrCreateUIGameObject("SubtitlePlaque_GameOver");
        plaqueRt.anchorMin = new Vector2(0.5f, 1f);
        plaqueRt.anchorMax = new Vector2(0.5f, 1f);
        plaqueRt.pivot = new Vector2(0.5f, 1f);
        plaqueRt.anchoredPosition = new Vector2(0f, -148f);
        plaqueRt.sizeDelta = new Vector2(780f, 48f);

        subtitlePlaqueImg = plaqueRt.GetComponent<Image>() ?? plaqueRt.gameObject.AddComponent<Image>();
        subtitlePlaqueImg.type = Image.Type.Simple;
        subtitlePlaqueImg.raycastTarget = false;

        // Mover Subtitle_GameOver dentro de la placa para evitar conflicto Graphic/Text en el mismo objeto
        Transform legacySub = transform.Find("Subtitle_GameOver");
        RectTransform subRt = legacySub != null
            ? legacySub.GetComponent<RectTransform>()
            : GetOrCreateUIGameObject("Subtitle_GameOver", plaqueRt);
        if (subRt.parent != plaqueRt) subRt.SetParent(plaqueRt, false);

        subRt.anchorMin = Vector2.zero;
        subRt.anchorMax = Vector2.one;
        subRt.offsetMin = Vector2.zero;
        subRt.offsetMax = Vector2.zero;

        subtitleText = subRt.GetComponent<TextMeshProUGUI>() ?? subRt.gameObject.AddComponent<TextMeshProUGUI>();
        if (s_DisplayFont != null) subtitleText.font = s_DisplayFont;
        subtitleText.fontSize = 19;
        subtitleText.fontStyle = FontStyles.Bold;
        subtitleText.alignment = TextAlignmentOptions.Center;
        subtitleText.characterSpacing = 4.5f;
        subtitleText.textWrappingMode = TextWrappingModes.NoWrap;
        subtitleText.raycastTarget = false;
    }

    private RectTransform BuildLeaderboardCard()
    {
        RectTransform cardRt = GetOrCreateUIGameObject("LeaderboardCard");
        cardRt.anchorMin = new Vector2(0.5f, 0.5f);
        cardRt.anchorMax = new Vector2(0.5f, 0.5f);
        cardRt.pivot = new Vector2(0.5f, 0.5f);
        cardRt.anchoredPosition = new Vector2(-335f, -72f);
        cardRt.sizeDelta = new Vector2(680f, 610f);

        Image cardImg = cardRt.GetComponent<Image>() ?? cardRt.gameObject.AddComponent<Image>();
        cardImg.sprite = s_LeaderboardCardSprite;
        cardImg.type = Image.Type.Simple;
        cardImg.color = Color.white;
        cardImg.raycastTarget = false;

        leaderboardHeaderText = CreateLabel("CardHeader", cardRt, new Vector2(0f, 268f), new Vector2(636f, 34f),
            "<color=#38BDF8><b>CLASIFICACIÓN DE PILOTOS</b></color>  <color=#64748B>// TOP 10</color>",
            s_DisplayFont, 21, TextAlignmentOptions.Left, 3f);

        CreateBar("HeaderDivider", cardRt, new Vector2(0f, 242f), new Vector2(636f, 2f), new Color(0.22f, 0.74f, 0.97f, 0.55f));

        leaderboardLegendText = CreateLabel("ColumnLegend", cardRt, new Vector2(0f, 218f), new Vector2(608f, 24f),
            "<color=#7DD3FC><pos=4>POS<pos=50>PILOTO<pos=230>OLEADA<pos=355>TIEMPO<pos=465>PUNTOS</color>",
            s_MonoFont, 15, TextAlignmentOptions.MidlineLeft, 0.5f);

        return cardRt;
    }

    private void ArrangeLeaderboardRows(RectTransform lbCard)
    {
        if (leaderboardRows == null || leaderboardRows.Length == 0) return;

        Transform containerTrans = leaderboardRows[0] != null ? leaderboardRows[0].transform.parent : null;
        if (containerTrans == null) return;

        // Evitar re-anidar si LeaderboardContainer ya está dentro de LeaderboardCard
        if (containerTrans.parent != lbCard) containerTrans.SetParent(lbCard, false);

        RectTransform contRt = containerTrans.GetComponent<RectTransform>();
        if (contRt != null)
        {
            contRt.anchorMin = new Vector2(0.5f, 0.5f);
            contRt.anchorMax = new Vector2(0.5f, 0.5f);
            contRt.pivot = new Vector2(0.5f, 0.5f);
            contRt.anchoredPosition = new Vector2(0f, -46f);
            contRt.sizeDelta = new Vector2(636f, 480f);
        }

        VerticalLayoutGroup vlg = containerTrans.GetComponent<VerticalLayoutGroup>();
        if (vlg != null)
        {
            vlg.padding = new RectOffset(0, 0, 0, 0);
            vlg.spacing = 5f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
        }

        for (int i = 0; i < leaderboardRows.Length; i++)
        {
            TextMeshProUGUI rowTmp = leaderboardRows[i];
            if (rowTmp == null) continue;

            // Crear slot contenedor con Image propio para tener fondo de fila sin chocar con TextMeshProUGUI
            RectTransform slotRt = GetOrCreateUIGameObject($"RowSlot_{i}", containerTrans);
            slotRt.sizeDelta = new Vector2(636f, 43f);
            slotRt.SetSiblingIndex(i);

            Image slotBg = slotRt.GetComponent<Image>() ?? slotRt.gameObject.AddComponent<Image>();
            slotBg.type = Image.Type.Simple;
            slotBg.color = Color.white;
            slotBg.raycastTarget = false;
            if (i < rowBgImages.Length) rowBgImages[i] = slotBg;

            RectTransform rowRt = rowTmp.rectTransform;
            if (rowRt.parent != slotRt) rowRt.SetParent(slotRt, false);
            rowRt.anchorMin = Vector2.zero;
            rowRt.anchorMax = Vector2.one;
            rowRt.offsetMin = new Vector2(14f, 0f);
            rowRt.offsetMax = new Vector2(-14f, 0f);

            if (s_MonoFont != null) rowTmp.font = s_MonoFont;
            rowTmp.fontSize = 19;
            rowTmp.fontStyle = FontStyles.Bold;
            rowTmp.color = Color.white;
            rowTmp.alignment = TextAlignmentOptions.MidlineLeft;
            rowTmp.characterSpacing = 0.5f;
            rowTmp.textWrappingMode = TextWrappingModes.NoWrap;
            rowTmp.overflowMode = TextOverflowModes.Ellipsis;
            rowTmp.raycastTarget = false;

            // Subtítulo de fila (Piloto 1 y Piloto 2) visible solo en modo 2 jugadores
            RectTransform coopSubRt = GetOrCreateUIGameObject("CoopPilotsText", slotRt);
            coopSubRt.anchorMin = new Vector2(0f, 0f);
            coopSubRt.anchorMax = new Vector2(1f, 0f);
            coopSubRt.pivot = new Vector2(0.5f, 0f);
            coopSubRt.offsetMin = new Vector2(72f, 2f);
            coopSubRt.offsetMax = new Vector2(-240f, 20f);

            TextMeshProUGUI coopSubTmp = coopSubRt.GetComponent<TextMeshProUGUI>() ?? coopSubRt.gameObject.AddComponent<TextMeshProUGUI>();
            if (s_MonoFont != null) coopSubTmp.font = s_MonoFont;
            coopSubTmp.fontSize = 12.5f;
            coopSubTmp.fontStyle = FontStyles.Bold;
            coopSubTmp.color = new Color(0.80f, 0.86f, 0.94f, 1f);
            coopSubTmp.alignment = TextAlignmentOptions.MidlineLeft;
            coopSubTmp.characterSpacing = 0.2f;
            coopSubTmp.textWrappingMode = TextWrappingModes.NoWrap;
            coopSubTmp.overflowMode = TextOverflowModes.Ellipsis;
            coopSubTmp.raycastTarget = false;
            coopSubTmp.gameObject.SetActive(false);
            if (i < rowCoopSubTexts.Length) rowCoopSubTexts[i] = coopSubTmp;
        }
    }

    private RectTransform BuildActionMissionCard()
    {
        RectTransform cardRt = GetOrCreateUIGameObject("MissionActionCard");
        cardRt.anchorMin = new Vector2(0.5f, 0.5f);
        cardRt.anchorMax = new Vector2(0.5f, 0.5f);
        cardRt.pivot = new Vector2(0.5f, 0.5f);
        cardRt.anchoredPosition = new Vector2(355f, -72f);
        cardRt.sizeDelta = new Vector2(560f, 610f);

        actionCardImg = cardRt.GetComponent<Image>() ?? cardRt.gameObject.AddComponent<Image>();
        actionCardImg.sprite = s_ActionCardSpriteDefeat;
        actionCardImg.type = Image.Type.Simple;
        actionCardImg.color = Color.white;
        actionCardImg.raycastTarget = false;

        actionHeaderText = CreateLabel("MissionHeader", cardRt, new Vector2(0f, 268f), new Vector2(510f, 34f),
            "INFORME DE MISIÓN", s_DisplayFont, 21, TextAlignmentOptions.Left, 3.5f);

        actionDividerImg = CreateBar("MissionDivider", cardRt, new Vector2(0f, 242f), new Vector2(510f, 2f), new Color(0.96f, 0.30f, 0.36f, 0.65f));

        // Dos tarjetas métricas lado a lado (Puntuación y Tiempo)
        BuildStatTile("StatTile_Score", cardRt, new Vector2(-128f, 152f), "PUNTUACIÓN", out scoreValueText, new Color(0.98f, 0.75f, 0.14f, 1f));
        BuildStatTile("StatTile_Time", cardRt, new Vector2(128f, 152f), "SUPERVIVENCIA", out timeValueText, new Color(0.22f, 0.74f, 0.97f, 1f));

        CreateBar("ActionSectionDivider", cardRt, new Vector2(0f, 64f), new Vector2(510f, 1.5f), new Color(0.58f, 0.64f, 0.72f, 0.25f));

        return cardRt;
    }

    private void BuildStatTile(string name, RectTransform parent, Vector2 pos, string label, out TextMeshProUGUI valueText, Color valueColor)
    {
        RectTransform tileRt = GetOrCreateUIGameObject(name, parent);
        tileRt.anchorMin = new Vector2(0.5f, 0.5f);
        tileRt.anchorMax = new Vector2(0.5f, 0.5f);
        tileRt.pivot = new Vector2(0.5f, 0.5f);
        tileRt.anchoredPosition = pos;
        tileRt.sizeDelta = new Vector2(238f, 114f);

        Image tileImg = tileRt.GetComponent<Image>() ?? tileRt.gameObject.AddComponent<Image>();
        tileImg.sprite = s_StatTileSprite;
        tileImg.type = Image.Type.Simple;
        tileImg.color = Color.white;
        tileImg.raycastTarget = false;

        CreateLabel("Label", tileRt, new Vector2(0f, 30f), new Vector2(214f, 24f),
            $"<color=#94A3B8><b>{label}</b></color>", s_DisplayFont, 14, TextAlignmentOptions.Center, 3f);

        valueText = CreateLabel("Value", tileRt, new Vector2(0f, -14f), new Vector2(214f, 54f),
            "0", s_MonoFont, 34, TextAlignmentOptions.Center, 1f);
        valueText.color = valueColor;
    }

    private void StyleActionGroups(RectTransform actionCard)
    {
        // 1. Grupo de ingreso de nombre (1 Jugador o Escuadrón 2 Jugadores)
        if (nameEntryGroup != null)
        {
            RectTransform groupRt = nameEntryGroup.GetComponent<RectTransform>();
            if (groupRt.parent != actionCard) groupRt.SetParent(actionCard, false);
            groupRt.anchorMin = new Vector2(0.5f, 0.5f);
            groupRt.anchorMax = new Vector2(0.5f, 0.5f);
            groupRt.pivot = new Vector2(0.5f, 0.5f);
            groupRt.anchoredPosition = new Vector2(0f, -118f);
            groupRt.sizeDelta = new Vector2(500f, 340f);

            entryPromptText = CreateLabel("PromptLabel", groupRt, new Vector2(0f, 108f), new Vector2(460f, 30f),
                "<color=#E2E8F0><b>REGISTRA TU INDICATIVO DE PILOTO</b></color>",
                s_DisplayFont, 18, TextAlignmentOptions.Center, 2.5f);

            if (nameInputField != null)
            {
                // Clonar antes de alterar textos/tamaños para crear los campos de Equipo y Jugador 2
                coopTeamInputField = CloneInputField(groupRt, "TeamNameInputField");
                coopP2InputField = CloneInputField(groupRt, "Player2NameInputField");

                ConfigureInputField(nameInputField, new Vector2(0f, 38f), new Vector2(410f, 56f), s_InputSprite,
                    14, 24f, "ESCRIBE TU NOMBRE...", new Color(0.22f, 0.78f, 0.98f, 1f));

                ConfigureInputField(coopTeamInputField, new Vector2(0f, 74f), new Vector2(440f, 46f), s_TeamInputSprite,
                    18, 20f, "NOMBRE DEL EQUIPO...", new Color(0.98f, 0.75f, 0.18f, 1f));

                ConfigureInputField(coopP2InputField, new Vector2(114f, -6f), new Vector2(212f, 46f), s_Pilot2InputSprite,
                    12, 19f, "PILOTO 2...", new Color(0.22f, 0.88f, 0.48f, 1f));

                TextMeshProUGUI teamLbl = CreateLabel("TeamMiniLabel", groupRt, new Vector2(0f, 108f), new Vector2(440f, 20f),
                    "<color=#FBBF24><b>NOMBRE DEL EQUIPO</b></color>", s_DisplayFont, 13, TextAlignmentOptions.Left, 2f);
                TextMeshProUGUI p1Lbl = CreateLabel("P1MiniLabel", groupRt, new Vector2(-114f, 28f), new Vector2(212f, 20f),
                    "<color=#38BDF8><b>JUGADOR 1</b></color>", s_DisplayFont, 13, TextAlignmentOptions.Left, 2f);
                TextMeshProUGUI p2Lbl = CreateLabel("P2MiniLabel", groupRt, new Vector2(114f, 28f), new Vector2(212f, 20f),
                    "<color=#4ADE80><b>JUGADOR 2</b></color>", s_DisplayFont, 13, TextAlignmentOptions.Left, 2f);

                coopOnlyObjects = new GameObject[]
                {
                    coopTeamInputField.gameObject,
                    coopP2InputField.gameObject,
                    teamLbl.gameObject,
                    p1Lbl.gameObject,
                    p2Lbl.gameObject
                };

                WireInputNavigation();
            }

            if (confirmNameButton != null)
            {
                StyleSciFiButton(
                    confirmNameButton,
                    new Vector2(0f, -64f),
                    new Vector2(360f, 58f),
                    s_ConfirmBtnSprite,
                    "<color=#4ADE80><b>CONFIRMAR REGISTRO</b></color>");
            }
        }

        // 2. Grupo posterior al registro (Reintentar / Menú Principal)
        if (postSubmitGroup != null)
        {
            RectTransform postRt = postSubmitGroup.GetComponent<RectTransform>();
            if (postRt.parent != actionCard) postRt.SetParent(actionCard, false);
            postRt.anchorMin = new Vector2(0.5f, 0.5f);
            postRt.anchorMax = new Vector2(0.5f, 0.5f);
            postRt.pivot = new Vector2(0.5f, 0.5f);
            postRt.anchoredPosition = new Vector2(0f, -118f);
            postRt.sizeDelta = new Vector2(500f, 340f);

            CreateLabel("SavedBadge", postRt, new Vector2(0f, 92f), new Vector2(460f, 32f),
                "<color=#4ADE80><b>REGISTRO GUARDADO EN LA BASE</b></color>",
                s_DisplayFont, 18, TextAlignmentOptions.Center, 3f);

            Button[] postButtons = postSubmitGroup.GetComponentsInChildren<Button>(true);
            foreach (Button btn in postButtons)
            {
                if (btn == null) continue;
                if (btn.name.IndexOf("Restart", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    StyleSciFiButton(
                        btn,
                        new Vector2(0f, 18f),
                        new Vector2(360f, 58f),
                        s_RestartBtnSprite,
                        "<color=#38BDF8><b>REINTENTAR MISIÓN</b></color>");
                }
                else
                {
                    StyleSciFiButton(
                        btn,
                        new Vector2(0f, -62f),
                        new Vector2(360f, 58f),
                        s_MenuBtnSprite,
                        "<color=#FBBF24><b>MENÚ PRINCIPAL</b></color>");
                }
            }
        }
    }

    private TMP_InputField CloneInputField(Transform parent, string objName)
    {
        Transform existing = parent.Find(objName);
        if (existing != null && existing.TryGetComponent<TMP_InputField>(out var existingField))
        {
            return existingField;
        }

        GameObject cloneObj = Instantiate(nameInputField.gameObject, parent, false);
        cloneObj.name = objName;
        TMP_InputField field = cloneObj.GetComponent<TMP_InputField>();
        field.onSubmit.RemoveAllListeners();
        field.onEndEdit.RemoveAllListeners();
        field.onValueChanged.RemoveAllListeners();
        return field;
    }

    private void ConfigureInputField(TMP_InputField field, Vector2 pos, Vector2 size, Sprite bgSprite, int charLimit, float fontSize, string placeholderText, Color caretColor)
    {
        if (field == null) return;

        RectTransform inputRt = field.GetComponent<RectTransform>();
        inputRt.anchorMin = new Vector2(0.5f, 0.5f);
        inputRt.anchorMax = new Vector2(0.5f, 0.5f);
        inputRt.pivot = new Vector2(0.5f, 0.5f);
        inputRt.anchoredPosition = pos;
        inputRt.sizeDelta = size;

        Image inputBg = field.GetComponent<Image>();
        if (inputBg != null)
        {
            inputBg.sprite = bgSprite;
            inputBg.type = Image.Type.Simple;
            inputBg.color = Color.white;
        }

        field.characterLimit = charLimit;
        field.customCaretColor = true;
        field.caretColor = caretColor;
        field.caretWidth = 2;
        field.selectionColor = new Color(caretColor.r, caretColor.g, caretColor.b, 0.32f);

        if (field.textViewport != null)
        {
            field.textViewport.anchorMin = Vector2.zero;
            field.textViewport.anchorMax = Vector2.one;
            field.textViewport.offsetMin = new Vector2(14f, 4f);
            field.textViewport.offsetMax = new Vector2(-14f, -4f);
        }

        if (field.textComponent != null)
        {
            RectTransform txtRt = field.textComponent.rectTransform;
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = Vector2.zero;
            txtRt.offsetMax = Vector2.zero;
            if (s_MonoFont != null) field.textComponent.font = s_MonoFont;
            field.textComponent.fontSize = fontSize;
            field.textComponent.fontStyle = FontStyles.Bold;
            field.textComponent.color = new Color(0.97f, 0.98f, 1f, 1f);
            field.textComponent.alignment = TextAlignmentOptions.Center;
        }

        if (field.placeholder is TextMeshProUGUI phTmp)
        {
            RectTransform phRt = phTmp.rectTransform;
            phRt.anchorMin = Vector2.zero;
            phRt.anchorMax = Vector2.one;
            phRt.offsetMin = Vector2.zero;
            phRt.offsetMax = Vector2.zero;
            if (s_DisplayFont != null) phTmp.font = s_DisplayFont;
            phTmp.text = placeholderText;
            phTmp.fontSize = Mathf.Max(14f, fontSize - 5f);
            phTmp.fontStyle = FontStyles.Bold;
            phTmp.color = new Color(0.42f, 0.52f, 0.66f, 0.85f);
            phTmp.characterSpacing = 1.8f;
            phTmp.alignment = TextAlignmentOptions.Center;
        }
    }

    private void WireInputNavigation()
    {
        if (coopTeamInputField != null)
        {
            coopTeamInputField.onSubmit.RemoveAllListeners();
            coopTeamInputField.onSubmit.AddListener(_ => FocusInput(nameInputField));
        }

        if (nameInputField != null)
        {
            nameInputField.onSubmit.RemoveAllListeners();
            nameInputField.onSubmit.AddListener(_ =>
            {
                if (nameEntryGroup == null || !nameEntryGroup.activeSelf) return;
                if (isCoopRun && coopP2InputField != null) FocusInput(coopP2InputField);
                else ConfirmName();
            });
        }

        if (coopP2InputField != null)
        {
            coopP2InputField.onSubmit.RemoveAllListeners();
            coopP2InputField.onSubmit.AddListener(_ =>
            {
                if (nameEntryGroup != null && nameEntryGroup.activeSelf) ConfirmName();
            });
        }
    }

    private void CycleCoopInputFocus()
    {
        GameObject current = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (coopTeamInputField != null && current == coopTeamInputField.gameObject) FocusInput(nameInputField);
        else if (nameInputField != null && current == nameInputField.gameObject) FocusInput(coopP2InputField);
        else FocusInput(coopTeamInputField);
    }

    private static void FocusInput(TMP_InputField field)
    {
        if (field == null || !field.gameObject.activeInHierarchy) return;
        field.Select();
        field.ActivateInputField();
    }

    private void ClearInputs()
    {
        if (nameInputField != null) nameInputField.text = string.Empty;
        if (coopTeamInputField != null) coopTeamInputField.text = string.Empty;
        if (coopP2InputField != null) coopP2InputField.text = string.Empty;
    }

    private void StyleSciFiButton(Button btn, Vector2 pos, Vector2 size, Sprite sprite, string labelMarkup)
    {
        RectTransform rt = btn.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        Image img = btn.GetComponent<Image>();
        if (img != null)
        {
            img.sprite = sprite;
            img.type = Image.Type.Simple;
            img.color = Color.white;
            img.raycastTarget = true;
            btn.targetGraphic = img;
        }

        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1.16f, 1.16f, 1.16f, 1f);
        cb.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        cb.selectedColor = Color.white;
        cb.fadeDuration = 0.08f;
        btn.colors = cb;

        TextMeshProUGUI tmp = btn.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmp != null)
        {
            if (s_DisplayFont != null) tmp.font = s_DisplayFont;
            tmp.text = labelMarkup;
            tmp.fontSize = 20;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.characterSpacing = 3.5f;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.raycastTarget = false;
        }

        MenuButtonHover hover = btn.gameObject.GetComponent<MenuButtonHover>() ?? btn.gameObject.AddComponent<MenuButtonHover>();
        hover.Setup(btn);
    }

    private TextMeshProUGUI CreateLabel(string objName, Transform parent, Vector2 pos, Vector2 size, string text, TMP_FontAsset font, float fontSize, TextAlignmentOptions align, float charSpacing)
    {
        RectTransform rt = GetOrCreateUIGameObject(objName, parent);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        TextMeshProUGUI tmp = rt.GetComponent<TextMeshProUGUI>() ?? rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = align;
        tmp.characterSpacing = charSpacing;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.raycastTarget = false;
        return tmp;
    }

    private Image CreateBar(string objName, Transform parent, Vector2 pos, Vector2 size, Color color)
    {
        RectTransform rt = GetOrCreateUIGameObject(objName, parent);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        Image img = rt.GetComponent<Image>() ?? rt.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private void ApplyThemeAndSummary()
    {
        if (titleText != null)
        {
            titleText.text = isVictoryRun
                ? "<color=#38BDF8><b>MISIÓN CUMPLIDA</b></color>"
                : "<color=#FF4D5A><b>GAME OVER</b></color>";
        }

        if (subtitlePlaqueImg != null)
        {
            subtitlePlaqueImg.sprite = isVictoryRun ? s_PlaqueVictorySprite : s_PlaqueDefeatSprite;
        }

        if (subtitleText != null)
        {
            if (isVictoryRun)
            {
                subtitleText.text = "<color=#FBBF24><b>JEFE DERROTADO</b></color>";
            }
            else if (reachedBossInRun)
            {
                subtitleText.text = "<color=#A855F7><b>DERROTADO CONTRA EL JEFE</b></color>";
            }
            else
            {
                string playerStr = isCoopRun ? "LLEGARON HASTA LA" : "LLEGASTE HASTA LA";
                subtitleText.text = $"<color=#F87171><b>{playerStr} OLEADA {currentRunWaveReached}</b></color>";
            }
        }

        if (leaderboardHeaderText != null)
        {
            leaderboardHeaderText.text = isCoopRun
                ? "<color=#38BDF8><b>CLASIFICACIÓN 2 JUGADORES</b></color>  <color=#FBBF24>// CO-OP TOP 10</color>"
                : "<color=#38BDF8><b>CLASIFICACIÓN 1 JUGADOR</b></color>  <color=#94A3B8>// SOLO TOP 10</color>";
        }

        if (leaderboardLegendText != null)
        {
            leaderboardLegendText.text = isCoopRun
                ? "<color=#7DD3FC><pos=4>POS<pos=50>EQUIPO  /  PILOTOS<pos=230>OLEADA<pos=355>TIEMPO<pos=465>PUNTOS</color>"
                : "<color=#7DD3FC><pos=4>POS<pos=50>PILOTO<pos=230>OLEADA<pos=355>TIEMPO<pos=465>PUNTOS</color>";
        }

        if (actionCardImg != null)
        {
            actionCardImg.sprite = isVictoryRun ? s_ActionCardSpriteVictory : s_ActionCardSpriteDefeat;
        }

        if (actionHeaderText != null)
        {
            actionHeaderText.text = isVictoryRun
                ? "<color=#38BDF8><b>INFORME DE VICTORIA</b></color>"
                : "<color=#FB7185><b>INFORME DE MISIÓN</b></color>";
        }

        if (actionDividerImg != null)
        {
            actionDividerImg.color = isVictoryRun
                ? new Color(0.22f, 0.74f, 0.97f, 0.60f)
                : new Color(0.96f, 0.30f, 0.36f, 0.65f);
        }

        if (scoreValueText != null)
        {
            scoreValueText.text = currentRunScore.ToString("N0");
        }

        if (timeValueText != null)
        {
            int mins = Mathf.FloorToInt(currentRunTime / 60f);
            int secs = Mathf.FloorToInt(currentRunTime % 60f);
            timeValueText.text = $"{mins:00}:{secs:00}";
        }

        ApplyCoopEntryLayout();
    }

    private void ApplyCoopEntryLayout()
    {
        for (int i = 0; i < coopOnlyObjects.Length; i++)
        {
            if (coopOnlyObjects[i] != null) coopOnlyObjects[i].SetActive(isCoopRun);
        }

        if (entryPromptText != null)
        {
            RectTransform prt = entryPromptText.rectTransform;
            prt.anchoredPosition = isCoopRun ? new Vector2(0f, 142f) : new Vector2(0f, 108f);
            entryPromptText.fontSize = isCoopRun ? 16.5f : 18f;
            entryPromptText.text = isCoopRun
                ? "<color=#E2E8F0><b>REGISTRO DE ESCUADRÓN  //  2 JUGADORES</b></color>"
                : "<color=#E2E8F0><b>REGISTRA TU INDICATIVO DE PILOTO</b></color>";
        }

        if (nameInputField != null)
        {
            if (isCoopRun)
            {
                ConfigureInputField(nameInputField, new Vector2(-114f, -6f), new Vector2(212f, 46f), s_Pilot1InputSprite,
                    12, 19f, "PILOTO 1...", new Color(0.22f, 0.78f, 0.98f, 1f));
            }
            else
            {
                ConfigureInputField(nameInputField, new Vector2(0f, 38f), new Vector2(410f, 56f), s_InputSprite,
                    14, 24f, "ESCRIBE TU NOMBRE...", new Color(0.22f, 0.78f, 0.98f, 1f));
            }
        }

        if (confirmNameButton != null)
        {
            RectTransform btnRt = confirmNameButton.GetComponent<RectTransform>();
            btnRt.anchoredPosition = isCoopRun ? new Vector2(0f, -98f) : new Vector2(0f, -64f);
            btnRt.sizeDelta = isCoopRun ? new Vector2(360f, 56f) : new Vector2(360f, 58f);
        }
    }

    private static string SanitizeFieldText(TMP_InputField field, string fallback, int maxLen)
    {
        string raw = field != null ? field.text.Trim() : string.Empty;
        if (string.IsNullOrEmpty(raw)) return fallback;
        return raw.Length > maxLen ? raw.Substring(0, maxLen) : raw;
    }

    private void ConfirmName()
    {
        if (nameInputField == null) return;

        LeaderboardEntry newEntry;
        if (isCoopRun)
        {
            newEntry = new LeaderboardEntry
            {
                teamName = SanitizeFieldText(coopTeamInputField, "ESCUADRÓN", 18),
                playerName = SanitizeFieldText(nameInputField, "PILOTO 1", 12),
                player2Name = SanitizeFieldText(coopP2InputField, "PILOTO 2", 12),
                timeSeconds = currentRunTime,
                score = currentRunScore,
                waveReached = currentRunWaveReached,
                reachedBoss = reachedBossInRun
            };
        }
        else
        {
            newEntry = new LeaderboardEntry
            {
                playerName = SanitizeFieldText(nameInputField, "PILOTO", 14),
                timeSeconds = currentRunTime,
                score = currentRunScore,
                waveReached = currentRunWaveReached,
                reachedBoss = reachedBossInRun
            };
        }

        LeaderboardData targetBoard;
        if (isCoopRun)
        {
            if (coopLeaderboard == null) coopLeaderboard = new LeaderboardData();
            targetBoard = coopLeaderboard;
        }
        else
        {
            if (leaderboard == null) leaderboard = new LeaderboardData();
            targetBoard = leaderboard;
        }

        lastSubmittedEntry = newEntry;
        targetBoard.entries.Add(newEntry);

        // Orden por tiempo de gameplay, de mayor a menor (desempate por puntuación)
        targetBoard.entries.Sort((a, b) =>
        {
            int cmpTime = b.timeSeconds.CompareTo(a.timeSeconds);
            return cmpTime != 0 ? cmpTime : b.score.CompareTo(a.score);
        });

        if (targetBoard.entries.Count > MaxEntries)
            targetBoard.entries.RemoveRange(MaxEntries, targetBoard.entries.Count - MaxEntries);

        SaveLeaderboard(isCoopRun);
        RefreshLeaderboardUI();

        if (nameEntryGroup != null) nameEntryGroup.SetActive(false);
        if (postSubmitGroup != null) postSubmitGroup.SetActive(true);
    }

    private void RefreshLeaderboardUI()
    {
        LeaderboardData activeBoard = isCoopRun ? coopLeaderboard : leaderboard;
        if (leaderboardRows == null || activeBoard == null) return;

        for (int i = 0; i < leaderboardRows.Length; i++)
        {
            if (leaderboardRows[i] == null) continue;

            Image bg = i < rowBgImages.Length ? rowBgImages[i] : null;
            TextMeshProUGUI coopSub = i < rowCoopSubTexts.Length ? rowCoopSubTexts[i] : null;
            if (coopSub != null) coopSub.gameObject.SetActive(isCoopRun);

            string rankStr = $"{i + 1:00}.";

            if (i < activeBoard.entries.Count)
            {
                LeaderboardEntry e = activeBoard.entries[i];
                bool isNew = lastSubmittedEntry != null && ReferenceEquals(e, lastSubmittedEntry);
                int minutes = Mathf.FloorToInt(e.timeSeconds / 60f);
                int seconds = Mathf.FloorToInt(e.timeSeconds % 60f);

                string rankColor = i switch
                {
                    0 => "#FBBF24",
                    1 => "#E2E8F0",
                    2 => "#FB923C",
                    _ => "#7DD3FC"
                };

                string nameColor = isNew ? "#4ADE80" : (i == 0 ? "#FEF3C7" : "#F1F5F9");
                string timeColor = isNew ? "#4ADE80" : "#38BDF8";
                string ptsColor = isNew ? "#86EFAC" : "#FBBF24";

                string waveStr;
                string waveColor;
                if (e.reachedBoss)
                {
                    waveStr = "JEFE";
                    waveColor = isNew ? "#D8B4FE" : "#C084FC";
                }
                else
                {
                    int w = e.waveReached > 0 ? e.waveReached : 1;
                    waveStr = $"OLEADA {w}";
                    waveColor = isNew ? "#86EFAC" : "#38BDF8";
                }

                if (isCoopRun)
                {
                    string safeTeam = string.IsNullOrWhiteSpace(e.teamName) ? "ESCUADRÓN" : e.teamName.Trim();
                    if (safeTeam.Length > 18) safeTeam = safeTeam.Substring(0, 18);

                    string p1 = string.IsNullOrWhiteSpace(e.playerName) ? "PILOTO 1" : e.playerName.Trim();
                    if (p1.Length > 12) p1 = p1.Substring(0, 12);

                    string p2 = string.IsNullOrWhiteSpace(e.player2Name) ? "PILOTO 2" : e.player2Name.Trim();
                    if (p2.Length > 12) p2 = p2.Substring(0, 12);

                    leaderboardRows[i].text =
                        $"<color={rankColor}><pos=4>{rankStr}</color><pos=50><voffset=9><size=15><color={nameColor}>{safeTeam.ToUpperInvariant()}</color></size></voffset><pos=230><voffset=9><size=14><color={waveColor}>{waveStr}</color></size></voffset><pos=355><voffset=9><color={timeColor}>{minutes:00}:{seconds:00}</color></voffset><pos=465><voffset=9><color={ptsColor}>{e.score:N0} PTS</color></voffset>";

                    if (coopSub != null)
                    {
                        string j1Col = isNew ? "#86EFAC" : "#38BDF8";
                        string j2Col = isNew ? "#86EFAC" : "#4ADE80";
                        coopSub.text = $"<color={j1Col}>J1:</color> {p1.ToUpperInvariant()}  <color=#64748B>·</color>  <color={j2Col}>J2:</color> {p2.ToUpperInvariant()}";
                    }
                }
                else
                {
                    string safeName = string.IsNullOrWhiteSpace(e.playerName) ? "PILOTO" : e.playerName.Trim();
                    if (safeName.Length > 14) safeName = safeName.Substring(0, 14);

                    leaderboardRows[i].text =
                        $"<color={rankColor}><pos=4>{rankStr}</color><pos=50><color={nameColor}>{safeName.ToUpperInvariant()}</color><pos=230><color={waveColor}>{waveStr}</color><pos=355><color={timeColor}>{minutes:00}:{seconds:00}</color><pos=465><color={ptsColor}>{e.score:N0} PTS</color>";
                }

                if (bg != null)
                {
                    if (isNew) bg.sprite = s_RowHighlight;
                    else if (i == 0) bg.sprite = s_RowGold;
                    else if (i == 1) bg.sprite = s_RowSilver;
                    else if (i == 2) bg.sprite = s_RowBronze;
                    else bg.sprite = (i % 2 == 0) ? s_RowDarkA : s_RowDarkB;
                }
            }
            else
            {
                if (isCoopRun)
                {
                    leaderboardRows[i].text = $"<color=#475569><pos=4>{rankStr}<pos=50>---<pos=230>---<pos=355>--:--<pos=465>---</color>";
                    if (coopSub != null) coopSub.text = string.Empty;
                }
                else
                {
                    leaderboardRows[i].text = $"<color=#475569><pos=4>{rankStr}<pos=50>---<pos=230>---<pos=355>--:--<pos=465>---</color>";
                }
                if (bg != null) bg.sprite = (i % 2 == 0) ? s_RowDarkA : s_RowDarkB;
            }
        }
    }

    private void SaveLeaderboard(bool coop)
    {
        string key = coop ? CoopSaveKey : SaveKey;
        LeaderboardData data = coop ? coopLeaderboard : leaderboard;
        string json = JsonUtility.ToJson(data);
        PlayerPrefs.SetString(key, json);
        PlayerPrefs.Save();
    }

    private void LoadLeaderboard()
    {
        leaderboard = LoadLeaderboardByKey(SaveKey);
        coopLeaderboard = LoadLeaderboardByKey(CoopSaveKey);
    }

    private static LeaderboardData LoadLeaderboardByKey(string key)
    {
        if (PlayerPrefs.HasKey(key))
        {
            string json = PlayerPrefs.GetString(key);
            LeaderboardData data = JsonUtility.FromJson<LeaderboardData>(json);
            if (data != null) return data;
        }
        return new LeaderboardData();
    }

    public void RestartGame()
    {
        SceneController.Instance.RestartCurrentScene();
    }

    public void GoToMainMenu()
    {
        SceneController.Instance.LoadMainMenuScene();
    }

    [ContextMenu("Borrar Todas las Clasificaciones")]
    public static void ClearAllLeaderboards()
    {
        PlayerPrefs.DeleteKey(SaveKey);
        PlayerPrefs.DeleteKey(CoopSaveKey);
        PlayerPrefs.Save();
        Debug.Log("[Leaderboard] Todas las clasificaciones han sido borradas de PlayerPrefs.");
    }

    public void ClearCurrentModeLeaderboard()
    {
        if (isCoopRun)
        {
            PlayerPrefs.DeleteKey(CoopSaveKey);
            coopLeaderboard = new LeaderboardData();
        }
        else
        {
            PlayerPrefs.DeleteKey(SaveKey);
            leaderboard = new LeaderboardData();
        }
        PlayerPrefs.Save();
        RefreshLeaderboardUI();
        Debug.Log($"[Leaderboard] Clasificación reseteada.");
    }
}
