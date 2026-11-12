using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Fondo espacial animado e interactivo para el Menú Principal:
/// - Mueve suavemente una capa de estrellas profundas y nebulosas.
/// - Genera un campo de partículas estelares/polvo cósmico en movimiento hacia la pantalla.
/// - Reacciona interactivamente a la posición del cursor (efecto de paralaje / inclinación sutil).
/// </summary>
public class MenuInteractiveBackground : MonoBehaviour
{
    private RectTransform rectTransform;
    private RawImage nebulaLayer1;
    private RawImage nebulaLayer2;
    private Image glowVignette;

    // Partículas estelares
    private const int STAR_COUNT = 45;
    private RectTransform[] starRects;
    private Image[] starImages;
    private Vector2[] starVelocities;
    private float[] starBaseAlpha;

    private Vector2 targetParallax = Vector2.zero;
    private Vector2 currentParallax = Vector2.zero;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        if (rectTransform == null) rectTransform = gameObject.AddComponent<RectTransform>();

        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        BuildBackgroundLayers();
        BuildInteractiveStars();
    }

    private void BuildBackgroundLayers()
    {
        // 1. Capa base oscura con gradiente cósmico
        GameObject baseObj = new GameObject("CosmicBase", typeof(RectTransform));
        baseObj.transform.SetParent(transform, false);
        RectTransform baseRt = baseObj.GetComponent<RectTransform>();
        baseRt.anchorMin = Vector2.zero;
        baseRt.anchorMax = Vector2.one;
        baseRt.offsetMin = Vector2.zero;
        baseRt.offsetMax = Vector2.zero;

        Image baseImg = baseObj.AddComponent<Image>();
        baseImg.color = new Color(0.02f, 0.04f, 0.09f, 1f); // Azul espacial muy profundo
        baseImg.raycastTarget = false;

        // 2. Capa de textura de espacio en movimiento lento continuo
        Sprite spaceSprite = Resources.Load<Sprite>("Sprites/SpaceBackground");
        if (spaceSprite != null && spaceSprite.texture != null)
        {
            GameObject nebObj = new GameObject("NebulaMovingLayer", typeof(RectTransform));
            nebObj.transform.SetParent(transform, false);
            RectTransform nebRt = nebObj.GetComponent<RectTransform>();
            nebRt.anchorMin = Vector2.zero;
            nebRt.anchorMax = Vector2.one;
            nebRt.offsetMin = Vector2.zero;
            nebRt.offsetMax = Vector2.zero;

            nebulaLayer1 = nebObj.AddComponent<RawImage>();
            nebulaLayer1.texture = spaceSprite.texture;
            nebulaLayer1.color = new Color(0.35f, 0.50f, 0.85f, 0.45f); // Tinte azul cian interestelar
            nebulaLayer1.raycastTarget = false;
        }

        // 3. Viñeta radial oscura para dar profundidad y contraste absoluto a los textos y botones
        GameObject vigObj = new GameObject("MenuVignette", typeof(RectTransform));
        vigObj.transform.SetParent(transform, false);
        RectTransform vigRt = vigObj.GetComponent<RectTransform>();
        vigRt.anchorMin = Vector2.zero;
        vigRt.anchorMax = Vector2.one;
        vigRt.offsetMin = Vector2.zero;
        vigRt.offsetMax = Vector2.zero;

        glowVignette = vigObj.AddComponent<Image>();
        glowVignette.sprite = CreateVignetteSprite(128, 128);
        glowVignette.color = new Color(0f, 0f, 0f, 0.70f);
        glowVignette.raycastTarget = false;
    }

    private void BuildInteractiveStars()
    {
        starRects = new RectTransform[STAR_COUNT];
        starImages = new Image[STAR_COUNT];
        starVelocities = new Vector2[STAR_COUNT];
        starBaseAlpha = new float[STAR_COUNT];

        Sprite starSprite = CreateCircularStarSprite(16);

        for (int i = 0; i < STAR_COUNT; i++)
        {
            GameObject sObj = new GameObject("Star_" + i, typeof(RectTransform));
            sObj.transform.SetParent(transform, false);

            RectTransform sRt = sObj.GetComponent<RectTransform>();
            starRects[i] = sRt;

            float size = Random.Range(3f, 9f);
            sRt.sizeDelta = new Vector2(size, size);
            sRt.anchorMin = new Vector2(0.5f, 0.5f);
            sRt.anchorMax = new Vector2(0.5f, 0.5f);
            sRt.anchoredPosition = new Vector2(Random.Range(-550f, 550f), Random.Range(-350f, 350f));

            Image sImg = sObj.AddComponent<Image>();
            sImg.sprite = starSprite;
            sImg.raycastTarget = false;

            // Variedad de estrellas: azules, doradas y blancas
            float starType = Random.value;
            Color col = starType < 0.4f ? new Color(0.4f, 0.85f, 1f, 1f) :
                        (starType < 0.7f ? new Color(1f, 0.85f, 0.4f, 1f) : Color.white);

            float alpha = Random.Range(0.4f, 0.95f);
            starBaseAlpha[i] = alpha;
            col.a = alpha;
            sImg.color = col;
            starImages[i] = sImg;

            // Velocidad de desplazamiento flotante
            float speedY = Random.Range(-25f, -8f);
            float speedX = Random.Range(-6f, 6f);
            starVelocities[i] = new Vector2(speedX, speedY);
        }
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;

        // 1. Desplazamiento sutil continuo de la nebulosa cósmica
        if (nebulaLayer1 != null)
        {
            Rect uv = nebulaLayer1.uvRect;
            uv.y += dt * 0.015f; // Flujo hacia abajo
            uv.x += dt * 0.005f;
            nebulaLayer1.uvRect = uv;
        }

        // 2. Interacción con el Mouse (Paralaje interactivo)
        Vector2 mousePos = Vector2.zero;
        if (UnityEngine.InputSystem.Mouse.current != null)
        {
            mousePos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
        }
        float screenW = Mathf.Max(1f, Screen.width);
        float screenH = Mathf.Max(1f, Screen.height);
        float normX = (mousePos.x / screenW - 0.5f) * 2f;
        float normY = (mousePos.y / screenH - 0.5f) * 2f;

        targetParallax = new Vector2(normX * 18f, normY * 12f);
        currentParallax = Vector2.Lerp(currentParallax, targetParallax, dt * 4f);

        // 3. Actualización de las estrellas flotantes con centelleo y movimiento interactivo
        float time = Time.unscaledTime;
        for (int i = 0; i < STAR_COUNT; i++)
        {
            if (starRects[i] == null) continue;

            Vector2 pos = starRects[i].anchoredPosition;
            pos += starVelocities[i] * dt;
            // Desplazamiento adicional por el movimiento del ratón
            pos += currentParallax * (dt * 0.4f * (i % 3 + 1));

            // Reciclar estrellas al salir de pantalla
            if (pos.y < -380f)
            {
                pos.y = 380f;
                pos.x = Random.Range(-550f, 550f);
            }
            if (pos.x < -600f) pos.x = 600f;
            if (pos.x > 600f) pos.x = -600f;

            starRects[i].anchoredPosition = pos;

            // Centelleo (twinkle) suave
            if (starImages[i] != null)
            {
                Color c = starImages[i].color;
                float twinkle = Mathf.Sin(time * 3f + i * 1.5f) * 0.25f;
                c.a = Mathf.Clamp01(starBaseAlpha[i] + twinkle);
                starImages[i].color = c;
            }
        }
    }

    private static Sprite CreateCircularStarSprite(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[size * size];
        float center = size * 0.5f;
        float radius = size * 0.48f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(center, center));
                float t = Mathf.Clamp01(1f - (dist / radius));
                t = t * t; // Curva suave
                pixels[y * size + x] = new Color(1f, 1f, 1f, t);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private static Sprite CreateVignetteSprite(int w, int h)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[w * h];
        float cx = w * 0.5f;
        float cy = h * 0.5f;
        float maxDist = Mathf.Sqrt(cx * cx + cy * cy);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                float norm = dist / maxDist;
                // Más transparente en el centro, muy oscuro hacia los bordes
                float alpha = Mathf.SmoothStep(0.25f, 0.95f, norm);
                pixels[y * w + x] = new Color(0f, 0f, 0f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
    }
}

