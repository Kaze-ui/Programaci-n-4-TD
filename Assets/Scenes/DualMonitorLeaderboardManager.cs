using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Administrador Inteligente de Visualización (1 Monitor o 2 Monitores).
/// - CON 1 SOLO MONITOR:
///     * El juego se ejecuta normalmente en la pantalla principal.
///     * Presionando [F10] o [L]: Alternas al instante entre ver el Juego y ver el Salón de la Fama (Leaderboard).
///     * Presionando [Esc] mientras ves el Leaderboard: Vuelves al juego de inmediato.
/// - CON 2 MONITORES CONECTADOS (Laptop + Monitor Grande):
///     * Activa automáticamente ambas pantallas.
///     * Por defecto: Monitor Grande = Videojuego | Laptop = Salón de la Fama en vivo.
///     * Presionando [F10]: Intercambias qué se ve en cada pantalla al instante.
/// </summary>
public class DualMonitorLeaderboardManager : MonoBehaviour
{
    public static DualMonitorLeaderboardManager Instance { get; private set; }

    private const string SaveKey1P = "LeaderboardData";
    private const string SaveKey2P = "LeaderboardData_2P";
    private const string PrefKey_GameOnDisplay2 = "DualMonitor_GameOnDisplay2";

    // En modo 2 monitores: True = Juego en Monitor Grande (Display 2) y Leaderboard en Laptop (Display 1)
    public bool isGameOnDisplay2 = true;

    // En modo 1 solo monitor: True = Se sobrepone el Leaderboard en pantalla completa; False = Se ve el juego normalmente
    private bool showLeaderboardOnSingleDisplay = false;

    private Camera secondaryCam;
    private Canvas secondaryCanvas;
    private CanvasScaler secondaryScaler;

    private TextMeshProUGUI[] rows1P = new TextMeshProUGUI[10];
    private Image[] rowBgs1P = new Image[10];

    private TextMeshProUGUI[] rows2P = new TextMeshProUGUI[10];
    private TextMeshProUGUI[] rows2PSub = new TextMeshProUGUI[10];
    private Image[] rowBgs2P = new Image[10];

    private TextMeshProUGUI liveBadgeText;
    private TextMeshProUGUI footerStatusText;

    private string lastData1PJson = "";
    private string lastData2PJson = "";
    private float refreshTimer = 0f;
    private const float RefreshInterval = 0.5f;
    private int lastDisplayCount = 1;

    public bool HasMultipleDisplays => Display.displays != null && Display.displays.Length > 1;

    /// <summary>
    /// Retorna true si el Salón de la Fama está cubriendo la pantalla principal (Display 0 / Laptop).
    /// </summary>
    public bool IsLeaderboardCoveringDisplay0
    {
        get
        {
            if (!HasMultipleDisplays) return showLeaderboardOnSingleDisplay;
            return isGameOnDisplay2;
        }
    }

    /// <summary>
    /// Retorna el índice de pantalla donde debe mostrarse el videojuego (0 o 1).
    /// </summary>
    public int CurrentGameDisplayIndex
    {
        get
        {
            if (!HasMultipleDisplays) return 0;
            return isGameOnDisplay2 ? 1 : 0;
        }
    }

    /// <summary>
    /// Indica si el Salón de la Fama está activo en un display específico (0 o 1).
    /// </summary>
    public bool IsLeaderboardActiveOnDisplay(int displayIndex)
    {
        if (!HasMultipleDisplays)
        {
            return displayIndex == 0 && showLeaderboardOnSingleDisplay;
        }
        else
        {
            int leaderboardDisplay = isGameOnDisplay2 ? 0 : 1;
            return displayIndex == leaderboardDisplay;
        }
    }

    // Sprites procedurales cacheados
    private static Sprite s_PanelBg;
    private static Sprite s_RowBgDarkA;
    private static Sprite s_RowBgDarkB;
    private static Sprite s_RowBgGold;
    private static Sprite s_RowBgSilver;
    private static Sprite s_RowBgBronze;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitialize()
    {
        if (Instance != null) return;

        GameObject managerObj = new GameObject("[DualMonitorLeaderboardManager]");
        managerObj.AddComponent<DualMonitorLeaderboardManager>();
        DontDestroyOnLoad(managerObj);
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        isGameOnDisplay2 = PlayerPrefs.GetInt(PrefKey_GameOnDisplay2, 1) == 1;

        EnsureSprites();
        BuildSecondaryDisplayHierarchy();
        ApplyDisplayConfiguration();
        RefreshLeaderboards();

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void Start()
    {
        ApplyDisplayConfiguration();
        StartCoroutine(ApplyDisplayConfigurationDeferred());
        RefreshLeaderboards();
    }

    void OnEnable()
    {
        Display.onDisplaysUpdated += OnDisplaysUpdated;
    }

    void OnDisable()
    {
        Display.onDisplaysUpdated -= OnDisplaysUpdated;
    }

    private void OnDisplaysUpdated()
    {
        Debug.Log($"[DualMonitor] Cambio en monitores detectado: {Display.displays.Length} pantallas.");
        ActivateSecondaryDisplay();
        ApplyDisplayConfiguration();
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Display.onDisplaysUpdated -= OnDisplaysUpdated;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyDisplayConfiguration();
        StartCoroutine(ApplyDisplayConfigurationDeferred());
    }

    /// <summary>
    /// Activa el segundo monitor en Unity si Windows detecta múltiples pantallas extendidas.
    /// </summary>
    public void ActivateSecondaryDisplay()
    {
        if (HasMultipleDisplays)
        {
            if (!Display.displays[1].active)
            {
                Display.displays[1].Activate();
                Debug.Log($"[DualMonitor] Segundo monitor detectado y activado: {Display.displays[1].systemWidth}x{Display.displays[1].systemHeight}");
            }
        }
    }

    void Update()
    {
        // Detección dinámica de monitores conectados o cambios de modo de pantalla
        int currentDisplayCount = Display.displays != null ? Display.displays.Length : 1;
        if (currentDisplayCount != lastDisplayCount)
        {
            lastDisplayCount = currentDisplayCount;
            if (currentDisplayCount > 1)
            {
                showLeaderboardOnSingleDisplay = false;
                ActivateSecondaryDisplay();
            }
            ApplyDisplayConfiguration();
        }

        bool hasMultiple = HasMultipleDisplays;

        // F10, F9 o L para alternar / intercambiar pantallas
        bool togglePressed = false;
        if (UnityEngine.InputSystem.Keyboard.current != null)
        {
            togglePressed = UnityEngine.InputSystem.Keyboard.current.f10Key.wasPressedThisFrame
                         || UnityEngine.InputSystem.Keyboard.current.f9Key.wasPressedThisFrame
                         || UnityEngine.InputSystem.Keyboard.current.lKey.wasPressedThisFrame;
        }

        if (!togglePressed)
        {
            try
            {
                togglePressed = Input.GetKeyDown(KeyCode.F10) || Input.GetKeyDown(KeyCode.F9) || Input.GetKeyDown(KeyCode.L);
            }
            catch { }
        }

        if (togglePressed)
        {
            if (HasMultipleDisplays)
            {
                showLeaderboardOnSingleDisplay = false;
                ToggleSwapDisplays();
            }
            else
            {
                ToggleLeaderboardSingleDisplay();
            }
        }

        // Si estamos en 1 solo monitor y se presiona Escape con el leaderboard abierto, regresar al juego
        if (!hasMultiple && showLeaderboardOnSingleDisplay)
        {
            if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                showLeaderboardOnSingleDisplay = false;
                ApplyDisplayConfiguration();
            }
        }

        // Pulso animado del indicador "EN VIVO"
        if (liveBadgeText != null && liveBadgeText.gameObject.activeInHierarchy)
        {
            float pulse = Mathf.Sin(Time.unscaledTime * 5f) * 0.5f + 0.5f;
            liveBadgeText.color = Color.Lerp(new Color(0.2f, 0.95f, 0.4f, 1f), new Color(0.85f, 1f, 0.85f, 1f), pulse);
        }

        // Refresco de datos en tiempo real
        refreshTimer += Time.unscaledDeltaTime;
        if (refreshTimer >= RefreshInterval)
        {
            refreshTimer = 0f;
            CheckAndRefreshData();
            UpdateFooterStatus();
            CleanupStrayObjects();
        }
    }

    /// <summary>
    /// Limpia cualquier botón u objeto que scripts externos hayan intentado emparentar en el canvas del Salón de la Fama.
    /// </summary>
    public void CleanupStrayObjects()
    {
        if (secondaryCanvas == null) return;
        Transform stray = secondaryCanvas.transform.Find("TutorialButton");
        if (stray != null)
        {
            Destroy(stray.gameObject);
        }

        // Asegurar que no haya botones interactivos huérfanos en la pantalla del Salón de la Fama
        Button[] buttons = secondaryCanvas.GetComponentsInChildren<Button>(true);
        foreach (var b in buttons)
        {
            if (b != null)
            {
                Destroy(b.gameObject);
            }
        }
    }

    /// <summary>
    /// CON 1 SOLO MONITOR: Alterna entre ver el juego o ver el Salón de la Fama a pantalla completa.
    /// </summary>
    public void ToggleLeaderboardSingleDisplay()
    {
        showLeaderboardOnSingleDisplay = !showLeaderboardOnSingleDisplay;
        if (showLeaderboardOnSingleDisplay)
        {
            RefreshLeaderboards();
        }
        ApplyDisplayConfiguration();
        Debug.Log($"[DualMonitor] Modo 1 monitor: Leaderboard {(showLeaderboardOnSingleDisplay ? "ABIERTO" : "CERRADO")}");
    }

    /// <summary>
    /// CON 2 MONITORES: Intercambia qué monitor muestra el juego y cuál muestra las tablas.
    /// </summary>
    public void ToggleSwapDisplays()
    {
        isGameOnDisplay2 = !isGameOnDisplay2;
        PlayerPrefs.SetInt(PrefKey_GameOnDisplay2, isGameOnDisplay2 ? 1 : 0);
        PlayerPrefs.Save();

        ApplyDisplayConfiguration();

        Debug.Log($"[DualMonitor] Pantallas intercambiadas con F10: Juego en Pantalla {(isGameOnDisplay2 ? 2 : 1)}, Leaderboard en Pantalla {(isGameOnDisplay2 ? 1 : 2)}");
    }

    public void ApplyDisplayConfiguration()
    {
        bool hasMultiple = HasMultipleDisplays;

        if (hasMultiple)
        {
            // === CASO 2 MONITORES CONECTADOS ===
            ActivateSecondaryDisplay();

            int gameDisplay = isGameOnDisplay2 ? 1 : 0;
            int leaderboardDisplay = isGameOnDisplay2 ? 0 : 1;

            if (secondaryCam != null)
            {
                secondaryCam.targetDisplay = leaderboardDisplay;
                secondaryCam.enabled = true;
                secondaryCam.gameObject.SetActive(true);
            }
            if (secondaryCanvas != null)
            {
                secondaryCanvas.targetDisplay = leaderboardDisplay;
                secondaryCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                secondaryCanvas.sortingOrder = 9999;
                secondaryCanvas.enabled = true;
                secondaryCanvas.gameObject.SetActive(true);
            }

            // Asignar todas las cámaras del juego al display de juego
            Camera[] cams = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var c in cams)
            {
                if (c != null && c != secondaryCam)
                {
                    c.targetDisplay = gameDisplay;
                }
            }

            // Asignar todos los Canvases del juego al display de juego
            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var cv in canvases)
            {
                if (cv != null && cv != secondaryCanvas && cv.isRootCanvas)
                {
                    cv.targetDisplay = gameDisplay;
                }
            }
        }
        else
        {
            // === CASO 1 SOLO MONITOR CONECTADO ===
            // 1. El juego SIEMPRE va al Display 1 (targetDisplay = 0), jamás a una pantalla inexistente
            Camera[] cams = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var c in cams)
            {
                if (c != null && c != secondaryCam)
                {
                    c.targetDisplay = 0;
                }
            }

            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var cv in canvases)
            {
                if (cv != null && cv != secondaryCanvas && cv.isRootCanvas)
                {
                    cv.targetDisplay = 0;
                }
            }

            // 2. Apagar la cámara secundaria en 1 solo monitor para que NO bloquee el juego
            if (secondaryCam != null)
            {
                secondaryCam.enabled = false;
                secondaryCam.gameObject.SetActive(false);
            }

            // 3. El Canvas se muestra directamente por encima del juego en ScreenSpaceOverlay
            if (secondaryCanvas != null)
            {
                secondaryCanvas.targetDisplay = 0;
                secondaryCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                secondaryCanvas.sortingOrder = 9999;
                secondaryCanvas.enabled = showLeaderboardOnSingleDisplay;
                secondaryCanvas.gameObject.SetActive(showLeaderboardOnSingleDisplay);
            }
        }
    }

    private IEnumerator ApplyDisplayConfigurationDeferred()
    {
        yield return null;
        ApplyDisplayConfiguration();
    }

    private void CheckAndRefreshData()
    {
        string cur1P = PlayerPrefs.GetString(SaveKey1P, "");
        string cur2P = PlayerPrefs.GetString(SaveKey2P, "");

        if (cur1P != lastData1PJson || cur2P != lastData2PJson)
        {
            lastData1PJson = cur1P;
            lastData2PJson = cur2P;
            RefreshLeaderboards();
        }
    }

    private void UpdateFooterStatus()
    {
        if (footerStatusText == null) return;

        bool hasMultiple = HasMultipleDisplays;

        string controlsTip = hasMultiple
            ? "<color=#FBBF24>[F10] Intercambiar pantallas entre Laptop y Monitor Grande</color>"
            : "<color=#FBBF24>[F10 / Esc] Volver al juego</color>";

        string locationBadge = hasMultiple
            ? (isGameOnDisplay2
                ? "<color=#38BDF8>● SALÓN DE LA FAMA (LAPTOP)</color>  <color=#64748B>·</color>  JUEGO EN PANTALLA GRANDE"
                : "<color=#38BDF8>● SALÓN DE LA FAMA (PANTALLA 2)</color>  <color=#64748B>·</color>  JUEGO EN PANTALLA PRINCIPAL")
            : "<color=#38BDF8>● SALÓN DE LA FAMA (VISTA EN PANTALLA ÚNICA)</color>";

        if (GameManager.Instance != null && GameManager.Instance.currentState == GameManager.GameState.Playing)
        {
            int wave = WaveController.Instance != null ? WaveController.Instance.CurrentWaveIndex + 1 : 1;
            int score = GameManager.Instance.CurrentScore;
            footerStatusText.text = $"{locationBadge}  <color=#64748B>·</color>  <color=#FBBF24>PARTIDA EN VIVO:</color> OL.{wave} ({score:N0} PTS)  <color=#64748B>·</color>  {controlsTip}";
        }
        else if (GameManager.Instance != null && GameManager.Instance.currentState == GameManager.GameState.BossFight)
        {
            footerStatusText.text = $"{locationBadge}  <color=#64748B>·</color>  <color=#EF4444>¡BATALLA CONTRA EL JEFE!</color>  <color=#64748B>·</color>  {controlsTip}";
        }
        else
        {
            footerStatusText.text = $"{locationBadge}  <color=#64748B>·</color>  <color=#4ADE80>SISTEMA LISTO PARA JUGAR</color>  <color=#64748B>·</color>  {controlsTip}";
        }
    }

    public void RefreshLeaderboards()
    {
        LeaderboardData data1P = LoadData(SaveKey1P);
        LeaderboardData data2P = LoadData(SaveKey2P);

        // Actualizar 1P
        for (int i = 0; i < 10; i++)
        {
            if (rows1P[i] == null) continue;

            string rankBadge = GetRankPrefix(i);
            if (i < data1P.entries.Count)
            {
                LeaderboardEntry e = data1P.entries[i];
                string name = string.IsNullOrWhiteSpace(e.playerName) ? "PILOTO" : e.playerName.Trim();
                if (name.Length > 13) name = name.Substring(0, 13);

                string waveStr = e.reachedBoss ? "<color=#EF4444>JEFE</color>" : $"<color=#38BDF8>OL.{e.waveReached}</color>";
                int m = (int)(e.timeSeconds / 60f);
                int s = (int)(e.timeSeconds % 60f);
                string timeStr = $"{m:00}:{s:00}";

                string nameCol = (i == 0) ? "#FEF08A" : (i == 1 ? "#F1F5F9" : (i == 2 ? "#FED7AA" : "#FFFFFF"));
                string ptsCol = (i == 0) ? "#FBBF24" : "#38BDF8";

                rows1P[i].text = $"{rankBadge}<pos=65><color={nameCol}>{name.ToUpperInvariant()}</color><pos=260>{waveStr}<pos=360><color=#94A3B8>{timeStr}</color><pos=455><color={ptsCol}>{e.score:N0} PTS</color>";
                if (rowBgs1P[i] != null) rowBgs1P[i].sprite = GetRowSprite(i);
            }
            else
            {
                rows1P[i].text = $"{rankBadge}<pos=65><color=#475569>---<pos=260>---<pos=360>--:--<pos=455>---</color>";
                if (rowBgs1P[i] != null) rowBgs1P[i].sprite = (i % 2 == 0) ? s_RowBgDarkA : s_RowBgDarkB;
            }
        }

        // Actualizar 2P
        for (int i = 0; i < 10; i++)
        {
            if (rows2P[i] == null) continue;

            string rankBadge = GetRankPrefix(i);
            if (i < data2P.entries.Count)
            {
                LeaderboardEntry e = data2P.entries[i];
                string team = string.IsNullOrWhiteSpace(e.teamName) ? "EQUIPO ALFA" : e.teamName.Trim();
                if (team.Length > 16) team = team.Substring(0, 16);

                string p1 = string.IsNullOrWhiteSpace(e.playerName) ? "J1" : e.playerName.Trim();
                if (p1.Length > 10) p1 = p1.Substring(0, 10);
                string p2 = string.IsNullOrWhiteSpace(e.player2Name) ? "J2" : e.player2Name.Trim();
                if (p2.Length > 10) p2 = p2.Substring(0, 10);

                string waveStr = e.reachedBoss ? "<color=#EF4444>JEFE</color>" : $"<color=#38BDF8>OL.{e.waveReached}</color>";
                int m = (int)(e.timeSeconds / 60f);
                int s = (int)(e.timeSeconds % 60f);
                string timeStr = $"{m:00}:{s:00}";

                string teamCol = (i == 0) ? "#FEF08A" : (i == 1 ? "#F1F5F9" : (i == 2 ? "#FED7AA" : "#FFFFFF"));
                string ptsCol = (i == 0) ? "#FBBF24" : "#4ADE80";

                rows2P[i].text = $"{rankBadge}<pos=65><voffset=8><size=16><color={teamCol}>{team.ToUpperInvariant()}</color></size></voffset><pos=260><voffset=8>{waveStr}</voffset><pos=360><voffset=8><color=#94A3B8>{timeStr}</color></voffset><pos=455><voffset=8><color={ptsCol}>{e.score:N0} PTS</color></voffset>";

                if (rows2PSub[i] != null)
                {
                    rows2PSub[i].text = $"<color=#38BDF8>J1:</color> {p1.ToUpperInvariant()}  <color=#64748B>·</color>  <color=#4ADE80>J2:</color> {p2.ToUpperInvariant()}";
                }

                if (rowBgs2P[i] != null) rowBgs2P[i].sprite = GetRowSprite(i);
            }
            else
            {
                rows2P[i].text = $"{rankBadge}<pos=65><color=#475569>---<pos=260>---<pos=360>--:--<pos=455>---</color>";
                if (rows2PSub[i] != null) rows2PSub[i].text = string.Empty;
                if (rowBgs2P[i] != null) rowBgs2P[i].sprite = (i % 2 == 0) ? s_RowBgDarkA : s_RowBgDarkB;
            }
        }
    }

    private string GetRankPrefix(int index)
    {
        switch (index)
        {
            case 0: return "<color=#FBBF24>#1</color>";
            case 1: return "<color=#E2E8F0>#2</color>";
            case 2: return "<color=#F59E0B>#3</color>";
            default: return $"<color=#64748B>#{index + 1}</color>";
        }
    }

    private Sprite GetRowSprite(int index)
    {
        switch (index)
        {
            case 0: return s_RowBgGold;
            case 1: return s_RowBgSilver;
            case 2: return s_RowBgBronze;
            default: return (index % 2 == 0) ? s_RowBgDarkA : s_RowBgDarkB;
        }
    }

    private LeaderboardData LoadData(string key)
    {
        if (PlayerPrefs.HasKey(key))
        {
            string json = PlayerPrefs.GetString(key);
            try
            {
                LeaderboardData d = JsonUtility.FromJson<LeaderboardData>(json);
                if (d != null && d.entries != null)
                {
                    d.entries.Sort((a, b) => b.score.CompareTo(a.score));
                    return d;
                }
            }
            catch { }
        }
        return new LeaderboardData();
    }

    // ==========================================
    // CONSTRUCCIÓN PROCEDURAL DE LA UI DEL SALÓN DE LA FAMA
    // ==========================================

    private void BuildSecondaryDisplayHierarchy()
    {
        int uiLayer = LayerMask.NameToLayer("UI");
        if (uiLayer < 0) uiLayer = 5;

        // 1. Cámara dedicada para Display 2 (solo activa en modo multi-monitor)
        GameObject camObj = new GameObject("LeaderboardSecondaryCamera");
        camObj.transform.SetParent(transform, false);
        secondaryCam = camObj.AddComponent<Camera>();
        secondaryCam.targetDisplay = 1;
        secondaryCam.clearFlags = CameraClearFlags.SolidColor;
        secondaryCam.backgroundColor = new Color(0.015f, 0.030f, 0.075f, 1f);
        secondaryCam.cullingMask = (1 << uiLayer) | (1 << 0);
        secondaryCam.depth = 50;
        secondaryCam.enabled = false;
        secondaryCam.gameObject.SetActive(false);

        // 2. Canvas escalable configurado en ScreenSpaceOverlay para garantizar renderizado perfecto
        GameObject canvasObj = new GameObject("LeaderboardSecondaryCanvas");
        canvasObj.transform.SetParent(transform, false);
        secondaryCanvas = canvasObj.AddComponent<Canvas>();
        secondaryCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        secondaryCanvas.sortingOrder = 9999;
        secondaryCanvas.targetDisplay = 0;
        secondaryCanvas.enabled = false;
        secondaryCanvas.gameObject.SetActive(false);

        secondaryScaler = canvasObj.AddComponent<CanvasScaler>();
        secondaryScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        secondaryScaler.referenceResolution = new Vector2(1920, 1080);
        secondaryScaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();

        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (font == null) font = TMP_Settings.defaultFontAsset;

        // 3. Fondo cibernético
        GameObject bgObj = new GameObject("BackgroundOverlay", typeof(RectTransform), typeof(Image));
        bgObj.transform.SetParent(canvasObj.transform, false);
        RectTransform bgRt = bgObj.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;
        Image bgImg = bgObj.GetComponent<Image>();
        bgImg.color = new Color(0.02f, 0.035f, 0.08f, 0.98f);

        // 4. Encabezado principal superior (Y: +470)
        GameObject headerObj = new GameObject("HeaderRoot", typeof(RectTransform));
        headerObj.transform.SetParent(canvasObj.transform, false);
        RectTransform headerRt = headerObj.GetComponent<RectTransform>();
        headerRt.anchorMin = new Vector2(0.5f, 1f);
        headerRt.anchorMax = new Vector2(0.5f, 1f);
        headerRt.pivot = new Vector2(0.5f, 1f);
        headerRt.anchoredPosition = new Vector2(0f, -22f);
        headerRt.sizeDelta = new Vector2(1840f, 110f);

        // Título del juego
        GameObject titleObj = new GameObject("TitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleObj.transform.SetParent(headerObj.transform, false);
        TextMeshProUGUI titleText = titleObj.GetComponent<TextMeshProUGUI>();
        if (font != null) titleText.font = font;
        titleText.text = "<color=#38BDF8>SPACE</color> <color=#FBBF24>WARS</color> <color=#94A3B8>//</color> <color=#F8FAFC>SALÓN DE LA FAMA</color>";
        titleText.fontSize = 42f;
        titleText.fontStyle = FontStyles.Bold;
        titleText.alignment = TextAlignmentOptions.Center;
        RectTransform titleRt = titleObj.GetComponent<RectTransform>();
        titleRt.anchoredPosition = new Vector2(0f, -12f);
        titleRt.sizeDelta = new Vector2(1400f, 50f);

        // Subtítulo con insignia EN VIVO
        GameObject subObj = new GameObject("SubtitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
        subObj.transform.SetParent(headerObj.transform, false);
        TextMeshProUGUI subText = subObj.GetComponent<TextMeshProUGUI>();
        if (font != null) subText.font = font;
        subText.text = "<color=#38BDF8>TABLA DE CLASIFICACIONES EN TIEMPO REAL</color>  <color=#64748B>·</color>  COMPITE POR EL MÁXIMO PUNTAJE";
        subText.fontSize = 18f;
        subText.fontStyle = FontStyles.Bold;
        subText.alignment = TextAlignmentOptions.Center;
        RectTransform subRt = subObj.GetComponent<RectTransform>();
        subRt.anchoredPosition = new Vector2(0f, -60f);
        subRt.sizeDelta = new Vector2(1400f, 30f);

        // Insignia "● EN VIVO" a la derecha
        GameObject liveObj = new GameObject("LiveBadgeText", typeof(RectTransform), typeof(TextMeshProUGUI));
        liveObj.transform.SetParent(headerObj.transform, false);
        liveBadgeText = liveObj.GetComponent<TextMeshProUGUI>();
        if (font != null) liveBadgeText.font = font;
        liveBadgeText.text = "● EN VIVO";
        liveBadgeText.fontSize = 22f;
        liveBadgeText.fontStyle = FontStyles.Bold;
        liveBadgeText.alignment = TextAlignmentOptions.Right;
        RectTransform liveRt = liveObj.GetComponent<RectTransform>();
        liveRt.anchorMin = new Vector2(1f, 0.5f);
        liveRt.anchorMax = new Vector2(1f, 0.5f);
        liveRt.pivot = new Vector2(1f, 0.5f);
        liveRt.anchoredPosition = new Vector2(-30f, 0f);
        liveRt.sizeDelta = new Vector2(250f, 40f);

        // 5. Dos columnas de tablas (Lado Izquierdo: 1 Jugador | Lado Derecho: 2 Jugadores)
        float cardWidth = 880f;
        float cardHeight = 840f;
        float cardY = -70f;

        // Panel 1P (Izquierda: X = -460)
        BuildLeaderboardPanel(
            canvasObj.transform, font,
            new Vector2(-460f, cardY), new Vector2(cardWidth, cardHeight),
            "MODO SOLITARIO (1 JUGADOR)",
            new Color(0.22f, 0.74f, 0.97f, 1f),
            "POS   PILOTO                      OLEADA    TIEMPO    PUNTOS",
            false,
            rows1P, rowBgs1P, null);

        // Panel 2P (Derecha: X = +460)
        BuildLeaderboardPanel(
            canvasObj.transform, font,
            new Vector2(460f, cardY), new Vector2(cardWidth, cardHeight),
            "MODO COOPERATIVO (2 JUGADORES)",
            new Color(0.29f, 0.87f, 0.50f, 1f),
            "POS   EQUIPO / TRIPULACIÓN        OLEADA    TIEMPO    PUNTOS",
            true,
            rows2P, rowBgs2P, rows2PSub);

        // 6. Pie de página con estado en vivo (Y: -510)
        GameObject footerObj = new GameObject("FooterBar", typeof(RectTransform), typeof(Image));
        footerObj.transform.SetParent(canvasObj.transform, false);
        RectTransform footerRt = footerObj.GetComponent<RectTransform>();
        footerRt.anchorMin = new Vector2(0.5f, 0f);
        footerRt.anchorMax = new Vector2(0.5f, 0f);
        footerRt.pivot = new Vector2(0.5f, 0f);
        footerRt.anchoredPosition = new Vector2(0f, 14f);
        footerRt.sizeDelta = new Vector2(1840f, 44f);
        Image footerImg = footerObj.GetComponent<Image>();
        footerImg.sprite = s_PanelBg;
        footerImg.color = new Color(0.04f, 0.08f, 0.18f, 0.92f);

        GameObject statusTextObj = new GameObject("StatusText", typeof(RectTransform), typeof(TextMeshProUGUI));
        statusTextObj.transform.SetParent(footerObj.transform, false);
        footerStatusText = statusTextObj.GetComponent<TextMeshProUGUI>();
        if (font != null) footerStatusText.font = font;
        footerStatusText.text = "<color=#4ADE80>● SISTEMA EN LÍNEA:</color>  ESPERANDO NUEVOS RETADORES  <color=#64748B>·</color>  ¡ACÉRCATE A JUGAR!";
        footerStatusText.fontSize = 17f;
        footerStatusText.fontStyle = FontStyles.Bold;
        footerStatusText.alignment = TextAlignmentOptions.Center;
        RectTransform statusRt = statusTextObj.GetComponent<RectTransform>();
        statusRt.anchorMin = Vector2.zero;
        statusRt.anchorMax = Vector2.one;
        statusRt.offsetMin = new Vector2(20f, 0f);
        statusRt.offsetMax = new Vector2(-20f, 0f);

        SetLayerRecursively(canvasObj, uiLayer);
    }

    private static void SetLayerRecursively(GameObject obj, int layer)
    {
        if (obj == null) return;
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            if (child != null) SetLayerRecursively(child.gameObject, layer);
        }
    }

    private void BuildLeaderboardPanel(
        Transform parent, TMP_FontAsset font,
        Vector2 pos, Vector2 size,
        string panelTitle, Color accentColor,
        string legendText, bool isCoop,
        TextMeshProUGUI[] rowTexts, Image[] rowBgs, TextMeshProUGUI[] rowSubs)
    {
        int uiLayer = LayerMask.NameToLayer("UI");
        if (uiLayer < 0) uiLayer = 5;

        // Contenedor principal de la tarjeta
        GameObject panelObj = new GameObject("Panel_" + (isCoop ? "2P" : "1P"), typeof(RectTransform), typeof(Image));
        panelObj.transform.SetParent(parent, false);
        panelObj.layer = uiLayer;
        RectTransform panelRt = panelObj.GetComponent<RectTransform>();
        panelRt.anchoredPosition = pos;
        panelRt.sizeDelta = size;
        Image panelImg = panelObj.GetComponent<Image>();
        panelImg.sprite = s_PanelBg;
        panelImg.type = Image.Type.Sliced;
        panelImg.color = new Color(0.035f, 0.075f, 0.16f, 0.96f);

        // Barra de título del panel
        GameObject barObj = new GameObject("TitleBar", typeof(RectTransform), typeof(Image));
        barObj.transform.SetParent(panelObj.transform, false);
        barObj.layer = uiLayer;
        RectTransform barRt = barObj.GetComponent<RectTransform>();
        barRt.anchorMin = new Vector2(0f, 1f);
        barRt.anchorMax = new Vector2(1f, 1f);
        barRt.pivot = new Vector2(0.5f, 1f);
        barRt.anchoredPosition = new Vector2(0f, -12f);
        barRt.sizeDelta = new Vector2(-24f, 50f);
        Image barImg = barObj.GetComponent<Image>();
        barImg.sprite = s_RowBgDarkB;
        barImg.color = new Color(accentColor.r * 0.18f, accentColor.g * 0.18f, accentColor.b * 0.18f, 0.95f);

        GameObject titleTextObj = new GameObject("TitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleTextObj.transform.SetParent(barObj.transform, false);
        titleTextObj.layer = uiLayer;
        TextMeshProUGUI titleTxt = titleTextObj.GetComponent<TextMeshProUGUI>();
        if (font != null) titleTxt.font = font;
        titleTxt.text = panelTitle;
        titleTxt.fontSize = 24f;
        titleTxt.fontStyle = FontStyles.Bold;
        titleTxt.color = accentColor;
        titleTxt.alignment = TextAlignmentOptions.Center;
        RectTransform titleRt = titleTextObj.GetComponent<RectTransform>();
        titleRt.anchorMin = Vector2.zero;
        titleRt.anchorMax = Vector2.one;
        titleRt.offsetMin = Vector2.zero;
        titleRt.offsetMax = Vector2.zero;

        // Leyenda de columnas (POS | PILOTO | OLEADA | TIEMPO | PUNTOS)
        GameObject legObj = new GameObject("LegendText", typeof(RectTransform), typeof(TextMeshProUGUI));
        legObj.transform.SetParent(panelObj.transform, false);
        legObj.layer = uiLayer;
        TextMeshProUGUI legTxt = legObj.GetComponent<TextMeshProUGUI>();
        if (font != null) legTxt.font = font;
        legTxt.text = $"<color=#64748B><pos=12>POS<pos=65>{(isCoop ? "EQUIPO / PILOTOS" : "PILOTO")}<pos=260>OLEADA<pos=360>TIEMPO<pos=455>PUNTOS</color>";
        legTxt.fontSize = 15f;
        legTxt.fontStyle = FontStyles.Bold;
        RectTransform legRt = legObj.GetComponent<RectTransform>();
        legRt.anchorMin = new Vector2(0f, 1f);
        legRt.anchorMax = new Vector2(1f, 1f);
        legRt.pivot = new Vector2(0.5f, 1f);
        legRt.anchoredPosition = new Vector2(0f, -70f);
        legRt.sizeDelta = new Vector2(-36f, 28f);

        // Contenedor de las 10 filas
        float startY = -106f;
        float rowHeight = isCoop ? 68f : 66f;
        float spacing = isCoop ? 4f : 5f;

        for (int i = 0; i < 10; i++)
        {
            GameObject rowObj = new GameObject($"Row_{i}", typeof(RectTransform), typeof(Image));
            rowObj.transform.SetParent(panelObj.transform, false);
            rowObj.layer = uiLayer;
            RectTransform rowRt = rowObj.GetComponent<RectTransform>();
            rowRt.anchorMin = new Vector2(0f, 1f);
            rowRt.anchorMax = new Vector2(1f, 1f);
            rowRt.pivot = new Vector2(0.5f, 1f);
            rowRt.anchoredPosition = new Vector2(0f, startY - (i * (rowHeight + spacing)));
            rowRt.sizeDelta = new Vector2(-28f, rowHeight);

            Image rowImg = rowObj.GetComponent<Image>();
            rowImg.sprite = (i % 2 == 0) ? s_RowBgDarkA : s_RowBgDarkB;
            rowImg.type = Image.Type.Sliced;
            rowBgs[i] = rowImg;

            // Texto principal de la fila
            GameObject rowTextObj = new GameObject("RowText", typeof(RectTransform), typeof(TextMeshProUGUI));
            rowTextObj.transform.SetParent(rowObj.transform, false);
            rowTextObj.layer = uiLayer;
            TextMeshProUGUI rText = rowTextObj.GetComponent<TextMeshProUGUI>();
            if (font != null) rText.font = font;
            rText.fontSize = 19f;
            rText.fontStyle = FontStyles.Bold;
            rText.alignment = TextAlignmentOptions.Left;
            rText.raycastTarget = false;
            RectTransform rTextRt = rowTextObj.GetComponent<RectTransform>();
            rTextRt.anchorMin = Vector2.zero;
            rTextRt.anchorMax = Vector2.one;
            rTextRt.offsetMin = new Vector2(12f, isCoop ? 16f : 0f);
            rTextRt.offsetMax = new Vector2(-12f, 0f);
            rowTexts[i] = rText;

            // En modo cooperativo: subtítulo para mostrar J1 y J2 debajo del nombre del equipo
            if (isCoop && rowSubs != null)
            {
                GameObject subTextObj = new GameObject("CoopSubText", typeof(RectTransform), typeof(TextMeshProUGUI));
                subTextObj.transform.SetParent(rowObj.transform, false);
                subTextObj.layer = uiLayer;
                TextMeshProUGUI sText = subTextObj.GetComponent<TextMeshProUGUI>();
                if (font != null) sText.font = font;
                sText.fontSize = 13.5f;
                sText.fontStyle = FontStyles.Normal;
                sText.alignment = TextAlignmentOptions.Left;
                sText.raycastTarget = false;
                RectTransform sTextRt = subTextObj.GetComponent<RectTransform>();
                sTextRt.anchorMin = Vector2.zero;
                sTextRt.anchorMax = Vector2.one;
                sTextRt.offsetMin = new Vector2(77f, 4f);
                sTextRt.offsetMax = new Vector2(-12f, -38f);
                rowSubs[i] = sText;
            }
        }
    }

    private static void EnsureSprites()
    {
        if (s_PanelBg == null)
        {
            s_PanelBg = MainMenu.GeneratePanelSprite(
                680, 610, 14, 2,
                new Color(0.22f, 0.74f, 0.97f, 0.85f),
                new Color(0.035f, 0.075f, 0.16f, 0.96f),
                new Color(0.015f, 0.030f, 0.075f, 0.98f));
        }

        if (s_RowBgDarkA == null)
        {
            s_RowBgDarkA = MainMenu.GeneratePanelSprite(
                640, 56, 8, 1,
                new Color(0.25f, 0.35f, 0.50f, 0.40f),
                new Color(0.06f, 0.10f, 0.18f, 0.85f),
                new Color(0.04f, 0.07f, 0.13f, 0.88f));
        }

        if (s_RowBgDarkB == null)
        {
            s_RowBgDarkB = MainMenu.GeneratePanelSprite(
                640, 56, 8, 1,
                new Color(0.25f, 0.35f, 0.50f, 0.25f),
                new Color(0.04f, 0.07f, 0.14f, 0.85f),
                new Color(0.03f, 0.05f, 0.10f, 0.88f));
        }

        if (s_RowBgGold == null)
        {
            s_RowBgGold = MainMenu.GeneratePanelSprite(
                640, 56, 8, 2,
                new Color(1f, 0.84f, 0.20f, 0.95f),
                new Color(0.22f, 0.17f, 0.03f, 0.90f),
                new Color(0.12f, 0.09f, 0.01f, 0.92f));
        }

        if (s_RowBgSilver == null)
        {
            s_RowBgSilver = MainMenu.GeneratePanelSprite(
                640, 56, 8, 2,
                new Color(0.85f, 0.92f, 1f, 0.90f),
                new Color(0.12f, 0.16f, 0.22f, 0.88f),
                new Color(0.07f, 0.10f, 0.15f, 0.92f));
        }

        if (s_RowBgBronze == null)
        {
            s_RowBgBronze = MainMenu.GeneratePanelSprite(
                640, 56, 8, 2,
                new Color(0.92f, 0.58f, 0.28f, 0.90f),
                new Color(0.20f, 0.11f, 0.04f, 0.88f),
                new Color(0.11f, 0.06f, 0.02f, 0.92f));
        }
    }
}
