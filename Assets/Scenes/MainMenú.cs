using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using TMPro;

public class MainMenu : MonoBehaviour
{
    private static Sprite s_PlayBtnSprite;
    private static Sprite s_TutorialBtnSprite;
    private static Sprite s_QuitBtnSprite;
    private TutorialPanelController tutorialController;

    private Canvas GetMenuCanvas()
    {
        Canvas myCanvas = GetComponentInParent<Canvas>();
        if (myCanvas != null && myCanvas.name != "LeaderboardSecondaryCanvas" && myCanvas.GetComponentInParent<DualMonitorLeaderboardManager>() == null)
        {
            return myCanvas;
        }

        // Buscar el canvas que contiene el PlayButton original del menú principal
        Button[] allButtons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var b in allButtons)
        {
            if (b != null && b.name == "PlayButton")
            {
                Canvas c = b.GetComponentInParent<Canvas>();
                if (c != null && c.name != "LeaderboardSecondaryCanvas" && c.GetComponentInParent<DualMonitorLeaderboardManager>() == null)
                {
                    return c;
                }
            }
        }

        Canvas[] allCanvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var c in allCanvases)
        {
            if (c != null && c.name != "LeaderboardSecondaryCanvas" && c.GetComponentInParent<DualMonitorLeaderboardManager>() == null)
            {
                return c;
            }
        }
        return null;
    }

    void Awake()
    {
        EnsureEventSystem();
        EnsureMenuBackground();
        SetupTitle();
        SetupButtons();
        EnsureTutorialPanel();
        if (SoundController.Instance != null)
        {
            SoundController.Instance.PlayMenuMusic();
        }
    }

    void Start()
    {
        // Re-verificar en Start para garantizar orden y presencia tras inicialización de escena
        EnsureEventSystem();
        EnsureMenuBackground();
        SetupButtons();
        EnsureTutorialPanel();
    }

    void Update()
    {
        bool toggle = false;
        if (UnityEngine.InputSystem.Keyboard.current != null)
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            toggle = kb.f10Key.wasPressedThisFrame
                  || kb.f9Key.wasPressedThisFrame
                  || kb.f8Key.wasPressedThisFrame
                  || kb.lKey.wasPressedThisFrame
                  || kb.backquoteKey.wasPressedThisFrame
                  || (kb.tabKey.wasPressedThisFrame && kb.ctrlKey.isPressed);
        }

        if (toggle && DualMonitorLeaderboardManager.Instance != null)
        {
            if (DualMonitorLeaderboardManager.Instance.HasMultipleDisplays)
            {
                DualMonitorLeaderboardManager.Instance.ToggleSwapDisplays();
            }
            else
            {
                DualMonitorLeaderboardManager.Instance.ToggleLeaderboardSingleDisplay();
            }
        }
    }

    private void EnsureEventSystem()
    {
        EventSystem es = FindAnyObjectByType<EventSystem>();
        if (es == null)
        {
            GameObject esObj = new GameObject("EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
        }
    }

    private void EnsureMenuBackground()
    {
        Canvas canvas = GetMenuCanvas();
        if (canvas == null) return;

        // Asegurar que el Canvas tenga GraphicRaycaster activo
        if (canvas.GetComponent<GraphicRaycaster>() == null)
        {
            canvas.gameObject.AddComponent<GraphicRaycaster>();
        }

        Transform existingBg = canvas.transform.Find("MenuBackground_Interactive");
        GameObject bgObj;
        if (existingBg != null)
        {
            bgObj = existingBg.gameObject;
        }
        else
        {
            bgObj = new GameObject("MenuBackground_Interactive", typeof(RectTransform));
            bgObj.transform.SetParent(canvas.transform, false);
            bgObj.transform.SetAsFirstSibling();
        }

        if (bgObj.GetComponent<MenuInteractiveBackground>() == null)
        {
            bgObj.AddComponent<MenuInteractiveBackground>();
        }

        // Eliminar fondo estático viejo si existía
        Transform oldBg = canvas.transform.Find("MenuBackground_Image");
        if (oldBg != null)
        {
            Destroy(oldBg.gameObject);
        }
    }

    private void SetupTitle()
    {
        Canvas canvas = GetMenuCanvas();
        if (canvas == null) return;

        TMP_FontAsset sharedFont = null;
        TextMeshProUGUI anyTmp = canvas.GetComponentInChildren<TextMeshProUGUI>();
        if (anyTmp != null) sharedFont = anyTmp.font;

        // 1. Título Principal "SPACE WARS"
        Transform titleTrans = canvas.transform.Find("Title_SpaceWars");
        GameObject titleObj = titleTrans != null ? titleTrans.gameObject : new GameObject("Title_SpaceWars", typeof(RectTransform));
        titleObj.transform.SetParent(canvas.transform, false);

        RectTransform titleRt = titleObj.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0.5f, 0.5f);
        titleRt.anchorMax = new Vector2(0.5f, 0.5f);
        titleRt.pivot = new Vector2(0.5f, 0.5f);
        titleRt.anchoredPosition = new Vector2(0f, 175f);
        titleRt.sizeDelta = new Vector2(750f, 110f);

        TextMeshProUGUI titleTmp = titleObj.GetComponent<TextMeshProUGUI>();
        if (titleTmp == null) titleTmp = titleObj.AddComponent<TextMeshProUGUI>();
        if (sharedFont != null) titleTmp.font = sharedFont;
        titleTmp.text = "<color=#38BDF8>SPACE</color> <color=#FBBF24>WARS</color>";
        titleTmp.fontSize = 76;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.alignment = TextAlignmentOptions.Center;
        titleTmp.characterSpacing = 6f;
        titleTmp.raycastTarget = false;

        // 2. Subtítulo temático con alto contraste y placa de respaldo para legibilidad total
        Transform subTrans = canvas.transform.Find("Subtitle_SpaceWars");
        GameObject subObj = subTrans != null ? subTrans.gameObject : new GameObject("Subtitle_SpaceWars", typeof(RectTransform));
        subObj.transform.SetParent(canvas.transform, false);

        RectTransform subRt = subObj.GetComponent<RectTransform>();
        subRt.anchorMin = new Vector2(0.5f, 0.5f);
        subRt.anchorMax = new Vector2(0.5f, 0.5f);
        subRt.pivot = new Vector2(0.5f, 0.5f);
        subRt.anchoredPosition = new Vector2(0f, 118f);
        subRt.sizeDelta = new Vector2(580f, 42f);

        // Si subObj tiene TextMeshProUGUI huérfano, lo eliminamos para evitar conflicto con Graphic (Image)
        TextMeshProUGUI oldTmp = subObj.GetComponent<TextMeshProUGUI>();
        if (oldTmp != null)
        {
            DestroyImmediate(oldTmp);
        }

        // Placa de respaldo oscura translúcida con sutil borde cian para contraste cristalino
        Image subBg = subObj.GetComponent<Image>();
        if (subBg == null) subBg = subObj.AddComponent<Image>();
        subBg.sprite = GenerateButtonSprite(new Color(0.22f, 0.74f, 0.97f, 0.55f), new Color(0.02f, 0.05f, 0.12f, 0.90f), new Color(0.01f, 0.02f, 0.06f, 0.90f));
        subBg.type = Image.Type.Simple;
        subBg.raycastTarget = false;

        // Texto hijo separado para que Image y TextMeshProUGUI no compartan el mismo GameObject
        Transform textChildTrans = subObj.transform.Find("SubText");
        GameObject textChildObj = textChildTrans != null ? textChildTrans.gameObject : new GameObject("SubText", typeof(RectTransform));
        textChildObj.transform.SetParent(subObj.transform, false);

        RectTransform textRt = textChildObj.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        TextMeshProUGUI subTmp = textChildObj.GetComponent<TextMeshProUGUI>();
        if (subTmp == null) subTmp = textChildObj.AddComponent<TextMeshProUGUI>();
        if (sharedFont != null) subTmp.font = sharedFont;
        // Texto con brillo blanco/plateado de máximo contraste
        subTmp.text = "<color=#F8FAFC><b>PROTOCOLO DE DEFENSA GALÁCTICA</b></color>";
        subTmp.fontSize = 17;
        subTmp.fontStyle = FontStyles.Bold;
        subTmp.alignment = TextAlignmentOptions.Center;
        subTmp.characterSpacing = 8f;
        subTmp.raycastTarget = false;
    }

    private void SetupButtons()
    {
        Canvas canvas = GetMenuCanvas();
        if (canvas == null) return;

        // Limpiar cualquier botón huérfano en la leaderboard
        DualMonitorLeaderboardManager dmlm = DualMonitorLeaderboardManager.Instance;
        if (dmlm != null)
        {
            dmlm.CleanupStrayObjects();
        }
        else
        {
            GameObject lbCanvas = GameObject.Find("LeaderboardSecondaryCanvas");
            if (lbCanvas != null)
            {
                Button[] lbButtons = lbCanvas.GetComponentsInChildren<Button>(true);
                foreach (var b in lbButtons)
                {
                    if (b != null) Destroy(b.gameObject);
                }
            }
        }

        if (s_PlayBtnSprite == null)
        {
            s_PlayBtnSprite = GenerateButtonSprite(
                new Color(0.22f, 0.74f, 0.97f, 1f),       // Borde cian neón
                new Color(0.08f, 0.16f, 0.28f, 0.94f),     // Fondo superior
                new Color(0.03f, 0.06f, 0.14f, 0.94f)      // Fondo inferior
            );
        }

        if (s_TutorialBtnSprite == null)
        {
            s_TutorialBtnSprite = GenerateButtonSprite(
                new Color(0.22f, 0.88f, 0.45f, 1f),       // Borde verde esmeralda
                new Color(0.05f, 0.22f, 0.11f, 0.94f),     // Fondo superior
                new Color(0.02f, 0.09f, 0.04f, 0.94f)      // Fondo inferior
            );
        }

        if (s_QuitBtnSprite == null)
        {
            s_QuitBtnSprite = GenerateButtonSprite(
                new Color(1f, 0.32f, 0.38f, 1f),          // Borde rojo neón
                new Color(0.22f, 0.08f, 0.12f, 0.94f),     // Fondo superior
                new Color(0.12f, 0.03f, 0.05f, 0.94f)      // Fondo inferior
            );
        }

        // Configurar Botón PLAY (arriba)
        Button playBtn = FindButtonByName(canvas, "PlayButton");

        // Configurar Botón TUTORIAL (centro, verde esmeralda con idéntico diseño que Play y Quit)
        Button tutorialBtn = FindButtonByName(canvas, "TutorialButton");
        if (tutorialBtn == null)
        {
            if (playBtn != null)
            {
                // Duplicar el GameObject del PlayButton para copiar exactamente estructura y componentes
                GameObject tutObj = Instantiate(playBtn.gameObject, canvas.transform, false);
                tutObj.name = "TutorialButton";

                // Remover el Button viejo clonado (que traía la llamada persistente a PlayGame) y agregar uno limpio
                Button oldBtn = tutObj.GetComponent<Button>();
                if (oldBtn != null)
                {
                    DestroyImmediate(oldBtn);
                }
                tutorialBtn = tutObj.AddComponent<Button>();
            }
            else
            {
                GameObject tutObj = new GameObject("TutorialButton", typeof(RectTransform));
                tutObj.transform.SetParent(canvas.transform, false);
                tutorialBtn = tutObj.AddComponent<Button>();

                GameObject textObj = new GameObject("Text", typeof(RectTransform));
                textObj.transform.SetParent(tutObj.transform, false);
                TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
                TMP_FontAsset sharedFont = null;
                TextMeshProUGUI anyTmp = canvas.GetComponentInChildren<TextMeshProUGUI>();
                if (anyTmp != null) sharedFont = anyTmp.font;
                if (sharedFont != null) tmp.font = sharedFont;
            }
        }

        if (tutorialBtn.transform.parent != canvas.transform)
        {
            tutorialBtn.transform.SetParent(canvas.transform, false);
        }

        tutorialBtn.onClick.RemoveAllListeners();
        tutorialBtn.onClick.AddListener(OpenTutorial);

        // Configurar Botón QUIT (abajo)
        Button quitBtn = FindButtonByName(canvas, "QuitButton");

        if (playBtn != null)
        {
            StyleButton(playBtn, new Vector2(0f, 35f), s_PlayBtnSprite, "<color=#38BDF8><b>PLAY</b></color>", new Color(0.22f, 0.74f, 0.97f));
        }

        StyleButton(tutorialBtn, new Vector2(0f, -35f), s_TutorialBtnSprite, "<color=#4ADE80><b>TUTORIAL</b></color>", new Color(0.22f, 0.88f, 0.45f));

        if (quitBtn != null)
        {
            StyleButton(quitBtn, new Vector2(0f, -105f), s_QuitBtnSprite, "<color=#FF6B6B><b>QUIT</b></color>", new Color(1f, 0.42f, 0.42f));
        }
    }

    private Button FindButtonByName(Canvas canvas, string name)
    {
        Button[] allButtons = canvas.GetComponentsInChildren<Button>(true);
        foreach (var b in allButtons)
        {
            if (b.name == name) return b;
        }
        return null;
    }

    private void StyleButton(Button btn, Vector2 anchoredPos, Sprite bgSprite, string labelText, Color accentColor)
    {
        RectTransform rt = btn.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(290f, 58f);

        Image img = btn.GetComponent<Image>();
        if (img == null)
        {
            img = btn.gameObject.AddComponent<Image>();
        }
        if (bgSprite != null)
        {
            img.sprite = bgSprite;
            img.type = Image.Type.Sliced;
        }
        img.color = Color.white;
        img.raycastTarget = true; // Asegurar respuesta de clics

        btn.targetGraphic = img;

        // Configuración de transiciones de color limpias
        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
        cb.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        cb.selectedColor = Color.white;
        cb.fadeDuration = 0.08f;
        btn.colors = cb;
        btn.interactable = true;

        // Texto del botón
        TextMeshProUGUI tmp = btn.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null)
        {
            RectTransform textRt = tmp.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            tmp.text = labelText;
            tmp.fontSize = 24;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.characterSpacing = 4f;
            tmp.raycastTarget = false; // El texto nunca debe bloquear el clic al botón
        }

        // Componente interactivo de animación de escala y sonidos al pasar el mouse
        MenuButtonHover hover = btn.gameObject.GetComponent<MenuButtonHover>();
        if (hover == null) hover = btn.gameObject.AddComponent<MenuButtonHover>();
        hover.Setup(btn);
    }

    private static Sprite GenerateButtonSprite(Color borderColor, Color bodyTop, Color bodyBot)
    {
        return GeneratePanelSprite(290, 58, 8, 2, borderColor, bodyTop, bodyBot);
    }

    public static Sprite GeneratePanelSprite(int w, int h, int r, int borderWidth, Color borderColor, Color bodyTop, Color bodyBot)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[w * h];

        for (int y = 0; y < h; y++)
        {
            float vNorm = (float)y / h;
            for (int x = 0; x < w; x++)
            {
                bool isCorner = (x < r && y < r && (x - r) * (x - r) + (y - r) * (y - r) > r * r) ||
                                (x >= w - r && y < r && (x - (w - r)) * (x - (w - r)) + (y - r) * (y - r) > r * r) ||
                                (x < r && y >= h - r && (x - r) * (x - r) + (y - (h - r)) * (y - (h - r)) > r * r) ||
                                (x >= w - r && y >= h - r && (x - (w - r)) * (x - (w - r)) + (y - (h - r)) * (y - (h - r)) > r * r);

                if (isCorner)
                {
                    pixels[y * w + x] = Color.clear;
                }
                else
                {
                    bool isBorder = (x < borderWidth || x >= w - borderWidth || y < borderWidth || y >= h - borderWidth);
                    // Línea acentuada en los bordes para estilo sci-fi
                    if (isBorder)
                    {
                        pixels[y * w + x] = borderColor;
                    }
                    else
                    {
                        Color baseCol = Color.Lerp(bodyBot, bodyTop, vNorm);
                        // Sutil brillo interior horizontal en el borde superior
                        if (y >= h - borderWidth - 3)
                        {
                            baseCol = Color.Lerp(baseCol, borderColor, 0.40f);
                        }
                        pixels[y * w + x] = baseCol;
                    }
                }
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
    }

    public void OpenTutorial()
    {
        if (SoundController.Instance != null)
        {
            SoundController.Instance.PlayUpgradePurchasedSfx();
        }

        EnsureTutorialPanel();
        if (tutorialController != null)
        {
            tutorialController.OpenTutorial();
        }
    }

    private void EnsureTutorialPanel()
    {
        if (tutorialController != null) return;

        Canvas canvas = GetMenuCanvas();
        if (canvas == null) return;

        Transform existingPanel = canvas.transform.Find("TutorialPanel");
        GameObject panelObj;
        if (existingPanel != null)
        {
            panelObj = existingPanel.gameObject;
        }
        else
        {
            panelObj = new GameObject("TutorialPanel", typeof(RectTransform));
            panelObj.transform.SetParent(canvas.transform, false);
            panelObj.transform.SetAsLastSibling();
        }

        tutorialController = panelObj.GetComponent<TutorialPanelController>();
        if (tutorialController == null)
        {
            tutorialController = panelObj.AddComponent<TutorialPanelController>();
        }
        panelObj.SetActive(false);
    }

    public void PlayGame()
    {
        if (SoundController.Instance != null)
        {
            SoundController.Instance.PlayUpgradeContinueSfx();
        }

        if (SceneController.Instance != null)
        {
            SceneController.Instance.LoadGameScene();
        }
        else
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("InGame");
        }
    }

    public void QuitGame()
    {
        if (SoundController.Instance != null)
        {
            SoundController.Instance.PlayUpgradeFailedSfx();
        }

        if (SceneController.Instance != null)
        {
            SceneController.Instance.QuitGame();
        }
        else
        {
            Debug.Log("Cerrando el juego...");
            Application.Quit();

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }
}

public class MenuButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    private Vector3 originalScale = Vector3.one;
    private Vector3 targetScale = Vector3.one;
    private Button targetBtn;

    void Awake()
    {
        originalScale = transform.localScale;
        targetScale = originalScale;
    }

    public void Setup(Button btn)
    {
        targetBtn = btn;
        originalScale = transform.localScale;
        targetScale = originalScale;
    }

    void Update()
    {
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.unscaledDeltaTime * 14f);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        targetScale = originalScale * 1.05f;

        if (SoundController.Instance != null)
        {
            SoundController.Instance.PlayUpgradeHoverSfx();
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        targetScale = originalScale;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        targetScale = originalScale * 0.97f;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        targetScale = originalScale * 1.05f;
    }

    void OnDisable()
    {
        targetScale = originalScale;
        transform.localScale = originalScale;
    }
}