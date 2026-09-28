using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using TMPro;

public class MainMenu : MonoBehaviour
{
    private static Sprite s_PlayBtnSprite;
    private static Sprite s_QuitBtnSprite;

    void Awake()
    {
        EnsureMenuBackground();
        SetupTitle();
        SetupButtons();
        if (SoundController.Instance != null)
        {
            SoundController.Instance.PlayMenuMusic();
        }
    }

    void Start()
    {
        EnsureMenuBackground();
        SetupTitle();
        SetupButtons();
        if (SoundController.Instance != null)
        {
            SoundController.Instance.PlayMenuMusic();
        }
    }

    private void EnsureMenuBackground()
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        Transform existingBg = canvas.transform.Find("MenuBackground_Image");
        GameObject bgObj;
        if (existingBg != null)
        {
            bgObj = existingBg.gameObject;
        }
        else
        {
            bgObj = new GameObject("MenuBackground_Image", typeof(RectTransform));
            bgObj.transform.SetParent(canvas.transform, false);
            bgObj.transform.SetAsFirstSibling();
        }

        Image img = bgObj.GetComponent<Image>() ?? bgObj.AddComponent<Image>();
        Sprite bgSprite = Resources.Load<Sprite>("Sprites/MenuBackground");
        if (bgSprite != null)
        {
            img.sprite = bgSprite;
        }
        img.color = new Color(0.92f, 0.94f, 1f, 1f);

        RectTransform rt = bgObj.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private void SetupTitle()
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();
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

        TextMeshProUGUI titleTmp = titleObj.GetComponent<TextMeshProUGUI>() ?? titleObj.AddComponent<TextMeshProUGUI>();
        if (sharedFont != null) titleTmp.font = sharedFont;
        titleTmp.text = "<color=#38BDF8>SPACE</color> <color=#FBBF24>WARS</color>";
        titleTmp.fontSize = 76;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.alignment = TextAlignmentOptions.Center;
        titleTmp.characterSpacing = 6f;
        titleTmp.raycastTarget = false;

        // 2. Subtítulo temático
        Transform subTrans = canvas.transform.Find("Subtitle_SpaceWars");
        GameObject subObj = subTrans != null ? subTrans.gameObject : new GameObject("Subtitle_SpaceWars", typeof(RectTransform));
        subObj.transform.SetParent(canvas.transform, false);

        RectTransform subRt = subObj.GetComponent<RectTransform>();
        subRt.anchorMin = new Vector2(0.5f, 0.5f);
        subRt.anchorMax = new Vector2(0.5f, 0.5f);
        subRt.pivot = new Vector2(0.5f, 0.5f);
        subRt.anchoredPosition = new Vector2(0f, 118f);
        subRt.sizeDelta = new Vector2(600f, 40f);

        TextMeshProUGUI subTmp = subObj.GetComponent<TextMeshProUGUI>() ?? subObj.AddComponent<TextMeshProUGUI>();
        if (sharedFont != null) subTmp.font = sharedFont;
        subTmp.text = "<color=#94A3B8>PROTOCOLO DE DEFENSA GALÁCTICA</color>";
        subTmp.fontSize = 17;
        subTmp.fontStyle = FontStyles.Bold;
        subTmp.alignment = TextAlignmentOptions.Center;
        subTmp.characterSpacing = 8f;
        subTmp.raycastTarget = false;
    }

    private void SetupButtons()
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        if (s_PlayBtnSprite == null)
        {
            s_PlayBtnSprite = GenerateButtonSprite(
                new Color(0.22f, 0.74f, 0.97f, 1f),       // Borde cian neón
                new Color(0.08f, 0.16f, 0.28f, 0.94f),     // Fondo superior
                new Color(0.03f, 0.06f, 0.14f, 0.94f)      // Fondo inferior
            );
        }

        if (s_QuitBtnSprite == null)
        {
            s_QuitBtnSprite = GenerateButtonSprite(
                new Color(1f, 0.32f, 0.38f, 1f),          // Borde rojo neón
                new Color(0.22f, 0.08f, 0.12f, 0.94f),     // Fondo superior
                new Color(0.07f, 0.03f, 0.06f, 0.94f)      // Fondo inferior
            );
        }

        // Configurar Botón PLAY
        Button playBtn = FindButtonByName(canvas, "PlayButton");
        if (playBtn != null)
        {
            StyleButton(playBtn, new Vector2(0f, 12f), s_PlayBtnSprite, "<color=#38BDF8><b>PLAY</b></color>", new Color(0.22f, 0.74f, 0.97f));
        }

        // Configurar Botón QUIT
        Button quitBtn = FindButtonByName(canvas, "QuitButton");
        if (quitBtn != null)
        {
            StyleButton(quitBtn, new Vector2(0f, -70f), s_QuitBtnSprite, "<color=#FF6B6B><b>QUIT</b></color>", new Color(1f, 0.42f, 0.42f));
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
        if (img != null)
        {
            img.sprite = bgSprite;
            img.type = Image.Type.Simple;
            img.color = Color.white;
        }

        // Configuración de transiciones de color limpias
        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
        cb.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        cb.selectedColor = Color.white;
        cb.fadeDuration = 0.08f;
        btn.colors = cb;

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
            tmp.raycastTarget = false;
        }

        // Componente interactivo de animación de escala y sonidos al pasar el mouse
        MenuButtonHover hover = btn.gameObject.GetComponent<MenuButtonHover>() ?? btn.gameObject.AddComponent<MenuButtonHover>();
        hover.Setup(btn);
    }

    private static Sprite GenerateButtonSprite(Color borderColor, Color bodyTop, Color bodyBot)
    {
        int w = 290;
        int h = 58;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[w * h];
        int r = 8;
        int borderWidth = 2;

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
}