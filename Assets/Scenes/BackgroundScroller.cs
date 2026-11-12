using UnityEngine;
using System.Collections.Generic;

public class BackgroundScroller : MonoBehaviour
{
    [Header("Sprite de Fondo")]
    public Sprite backgroundSprite;
    public Sprite bossBackgroundSprite;

    [Header("Velocidad de desplazamiento")]
    public float scrollSpeed = 58f;
    public float bossScrollSpeed = 98f;

    private Camera mainCam;
    private SpriteRenderer sr1;
    private SpriteRenderer sr2;
    private float bgHeight;

    private SpriteRenderer bossSr1;
    private SpriteRenderer bossSr2;
    private float bossBgHeight;

    private SpriteRenderer abyssVignetteSr;
    private float viewWidth;
    private float viewHeight;

    private float bossBlend = 0f;
    private bool wasInBossFight = false;
    private float bossEntrySurge = 0f;
    private BossController cachedBoss;
    private float nextBossLookupTime = 0f;

    private struct ParallaxMote
    {
        public Transform tr;
        public SpriteRenderer sr;
        public float speedMult;
        public float baseAlpha;
        public float twinkleFreq;
        public float twinklePhase;
        public float driftX;
        public float swayAmp;
        public float swayFreq;
        public Color normalColor;
        public Color bossColor;
        public bool bossOnly;
    }

    private readonly List<ParallaxMote> parallaxMotes = new List<ParallaxMote>();
    private Sprite runtimeStarSprite;
    private Sprite runtimeStreakSprite;
    private Sprite runtimeAbyssSprite;

    void Awake()
    {
        mainCam = Camera.main;
        SetupBackground();
    }

    void OnDestroy()
    {
        DestroyRuntimeSprite(ref runtimeStarSprite);
        DestroyRuntimeSprite(ref runtimeStreakSprite);
        DestroyRuntimeSprite(ref runtimeAbyssSprite);
    }

    private static void DestroyRuntimeSprite(ref Sprite sp)
    {
        if (sp != null)
        {
            if (sp.texture != null) Destroy(sp.texture);
            Destroy(sp);
            sp = null;
        }
    }

    void SetupBackground()
    {
        if (backgroundSprite == null)
        {
            backgroundSprite = Resources.Load<Sprite>("Sprites/SpaceBackground");
        }
        if (bossBackgroundSprite == null)
        {
            bossBackgroundSprite = Resources.Load<Sprite>("Sprites/BossBackground");
        }

        Vector3 camPos = (mainCam != null) ? mainCam.transform.position : new Vector3(957f, 542f, 0f);
        transform.position = new Vector3(camPos.x, camPos.y, 0f);

        viewHeight = (mainCam != null) ? mainCam.orthographicSize * 2f : 2000f;
        viewWidth = viewHeight * ((mainCam != null) ? mainCam.aspect : (16f / 9f));

        SetupLayerPair("SpaceBG", backgroundSprite, -100, 1f, out sr1, out sr2, out bgHeight);
        SetupLayerPair("BossBG", bossBackgroundSprite, -99, 0f, out bossSr1, out bossSr2, out bossBgHeight);
        SetupBossAbyssOverlay();
        BuildParallaxLayers();
    }

    private void SetupLayerPair(
        string prefix,
        Sprite sprite,
        int sortingOrder,
        float initialAlpha,
        out SpriteRenderer layer1,
        out SpriteRenderer layer2,
        out float pairHeight)
    {
        GameObject obj1 = new GameObject($"{prefix}_Layer1");
        obj1.transform.SetParent(transform, false);
        obj1.transform.localPosition = Vector3.zero;
        layer1 = obj1.AddComponent<SpriteRenderer>();
        layer1.sprite = sprite;
        layer1.sortingOrder = sortingOrder;
        layer1.color = new Color(1f, 1f, 1f, initialAlpha);

        GameObject obj2 = new GameObject($"{prefix}_Layer2");
        obj2.transform.SetParent(transform, false);
        layer2 = obj2.AddComponent<SpriteRenderer>();
        layer2.sprite = sprite;
        layer2.flipY = true;
        layer2.sortingOrder = sortingOrder;
        layer2.color = new Color(1f, 1f, 1f, initialAlpha);

        if (sprite != null && sprite.rect.height > 0)
        {
            float spriteW = sprite.bounds.size.x;
            float spriteH = sprite.bounds.size.y;

            float scaleX = (viewWidth * 1.18f) / spriteW;
            float scaleY = (viewHeight * 1.18f) / spriteH;
            float scale = Mathf.Max(scaleX, scaleY);

            obj1.transform.localScale = new Vector3(scale, scale, 1f);
            obj2.transform.localScale = new Vector3(scale, scale, 1f);

            pairHeight = spriteH * scale;
            obj2.transform.localPosition = new Vector3(0f, pairHeight, 0f);
        }
        else
        {
            obj1.transform.localScale = new Vector3(4f, 4f, 1f);
            obj2.transform.localScale = new Vector3(4f, 4f, 1f);
            pairHeight = 2500f;
            obj2.transform.localPosition = new Vector3(0f, pairHeight, 0f);
        }
    }

    private void SetupBossAbyssOverlay()
    {
        runtimeAbyssSprite = CreateAbyssVignetteSprite(128);
        GameObject vigObj = new GameObject("BossAbyssVignette");
        vigObj.transform.SetParent(transform, false);
        vigObj.transform.localPosition = Vector3.zero;
        vigObj.transform.localScale = new Vector3(viewWidth * 1.06f, viewHeight * 1.06f, 1f);

        abyssVignetteSr = vigObj.AddComponent<SpriteRenderer>();
        abyssVignetteSr.sprite = runtimeAbyssSprite;
        abyssVignetteSr.sortingOrder = -91;
        abyssVignetteSr.color = new Color(0.55f, 0.04f, 0.14f, 0f);
    }

    private void BuildParallaxLayers()
    {
        runtimeStarSprite = CreateSoftCircleSprite(24);
        runtimeStreakSprite = CreateDustStreakSprite(16, 48);

        // Capa lejana de cúmulos estelares con centelleo lento (paralaje profundo)
        SpawnMoteLayer("ParallaxStars_Far", 70, 0.36f, 0.55f, 11f, 22f, -97, runtimeStarSprite, false, false);

        // Capa media de estrellas nítidas y polvo interestelar
        SpawnMoteLayer("ParallaxStars_Mid", 42, 0.72f, 0.98f, 16f, 30f, -95, runtimeStarSprite, false, false);

        // Capa cercana de micro-partículas y estelas cósmicas rápidas (sensación de velocidad)
        SpawnMoteLayer("ParallaxDust_Near", 26, 1.40f, 1.85f, 22f, 46f, -92, runtimeStreakSprite, true, false);

        // Brasas carmesí y cenizas cósmicas exclusivas de la batalla contra el Jefe Final
        SpawnMoteLayer("ParallaxEmbers_Boss", 42, 1.15f, 2.05f, 14f, 32f, -93, runtimeStarSprite, false, true);
    }

    private void SpawnMoteLayer(
        string prefix, int count, float minSpeedMult, float maxSpeedMult,
        float minSize, float maxSize, int sortingOrder, Sprite sprite, bool isStreak, bool bossOnly)
    {
        float halfW = viewWidth * 0.54f;
        float halfH = viewHeight * 0.56f;

        for (int i = 0; i < count; i++)
        {
            GameObject go = new GameObject($"{prefix}_{i}");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(Random.Range(-halfW, halfW), Random.Range(-halfH, halfH), 0f);

            float size = Random.Range(minSize, maxSize);
            if (isStreak)
                go.transform.localScale = new Vector3(size * 0.42f, size * 2.1f, 1f);
            else if (bossOnly)
                go.transform.localScale = new Vector3(size * 0.85f, size * 1.15f, 1f);
            else
                go.transform.localScale = new Vector3(size, size, 1f);

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = sortingOrder;

            float roll = Random.value;
            Color normalCol = roll < 0.46f
                ? new Color(0.55f, 0.88f, 1.0f, 1f)
                : (roll < 0.76f ? new Color(1.0f, 0.82f, 0.52f, 1f) : new Color(0.88f, 0.94f, 1.0f, 1f));

            Color bossCol = roll < 0.42f
                ? new Color(1.0f, 0.22f, 0.26f, 1f)
                : (roll < 0.78f ? new Color(1.0f, 0.52f, 0.16f, 1f) : new Color(0.82f, 0.28f, 1.0f, 1f));

            float baseAlpha = bossOnly
                ? Random.Range(0.28f, 0.72f)
                : (isStreak ? Random.Range(0.14f, 0.30f) : Random.Range(0.32f, 0.82f));

            Color initCol = bossOnly ? bossCol : normalCol;
            initCol.a = bossOnly ? 0f : baseAlpha;
            sr.color = initCol;

            parallaxMotes.Add(new ParallaxMote
            {
                tr = go.transform,
                sr = sr,
                speedMult = Random.Range(minSpeedMult, maxSpeedMult),
                baseAlpha = baseAlpha,
                twinkleFreq = Random.Range(bossOnly ? 3.5f : 1.2f, bossOnly ? 7.5f : 4.2f),
                twinklePhase = Random.Range(0f, Mathf.PI * 2f),
                driftX = isStreak ? 0f : Random.Range(-3.5f, 3.5f),
                swayAmp = bossOnly ? Random.Range(14f, 36f) : 0f,
                swayFreq = bossOnly ? Random.Range(1.4f, 3.2f) : 0f,
                normalColor = normalCol,
                bossColor = bossCol,
                bossOnly = bossOnly
            });
        }
    }

    void Update()
    {
        if (Time.timeScale == 0f) return;

        float dt = Time.deltaTime;
        bool inBossFight = GameManager.Instance != null && GameManager.Instance.IsInBossFight;

        if (inBossFight && !wasInBossFight)
        {
            bossEntrySurge = 1f;
            cachedBoss = null;
            nextBossLookupTime = 0f;
        }
        else if (!inBossFight && wasInBossFight)
        {
            cachedBoss = null;
            bossEntrySurge = 0f;
        }
        wasInBossFight = inBossFight;

        if (inBossFight)
        {
            if (cachedBoss == null && Time.time >= nextBossLookupTime)
            {
                cachedBoss = FindAnyObjectByType<BossController>();
                nextBossLookupTime = Time.time + 0.5f;
            }
            bossBlend = Mathf.MoveTowards(bossBlend, 1f, dt * 0.55f);
            bossEntrySurge = Mathf.Max(0f, bossEntrySurge - dt * 0.65f);
        }
        else
        {
            bossBlend = Mathf.MoveTowards(bossBlend, 0f, dt * 2.5f);
            bossEntrySurge = 0f;
        }

        bool bossEnraged = inBossFight && cachedBoss != null && cachedBoss.IsEnraged;
        bool bossLaser = inBossFight && cachedBoss != null && cachedBoss.IsLaserActive;

        float targetBossSpeed = bossEnraged ? bossScrollSpeed * 1.25f : bossScrollSpeed;
        float currentSpeed = Mathf.Lerp(scrollSpeed, targetBossSpeed, bossBlend)
            + bossEntrySurge * 125f
            + (bossLaser ? 32f : 0f);
        float delta = currentSpeed * dt;

        ScrollLayerPair(sr1, sr2, bgHeight, delta);
        ScrollLayerPair(bossSr1, bossSr2, bossBgHeight, delta * 1.12f);

        bool hasBossMap = bossBackgroundSprite != null;
        float normalAlpha = hasBossMap ? Mathf.Clamp01(1f - bossBlend * 0.92f) : 1f;
        Color normalTint = Color.Lerp(Color.white, new Color(0.55f, 0.14f, 0.22f, normalAlpha), bossBlend);
        normalTint.a = normalAlpha;
        if (sr1 != null) sr1.color = normalTint;
        if (sr2 != null) sr2.color = normalTint;

        float bossPulse = 0.5f + 0.5f * Mathf.Sin(Time.time * (bossEnraged ? 5.2f : 2.6f));
        Color bossTint = bossEnraged
            ? Color.Lerp(new Color(1f, 0.84f, 0.86f, 1f), new Color(1f, 0.68f, 0.72f, 1f), bossPulse * 0.55f)
            : Color.Lerp(Color.white, new Color(1f, 0.88f, 0.90f, 1f), bossPulse * 0.35f);
        bossTint.a = hasBossMap ? bossBlend : 0f;
        if (bossSr1 != null) bossSr1.color = bossTint;
        if (bossSr2 != null) bossSr2.color = bossTint;

        if (abyssVignetteSr != null)
        {
            float vigAlpha = bossBlend * (0.58f + bossPulse * (bossEnraged ? 0.24f : 0.14f) + bossEntrySurge * 0.25f + (bossLaser ? 0.14f : 0f));
            Color vigCol = bossEnraged
                ? Color.Lerp(new Color(0.75f, 0.05f, 0.14f, 1f), new Color(0.95f, 0.16f, 0.10f, 1f), bossPulse)
                : Color.Lerp(new Color(0.45f, 0.04f, 0.18f, 1f), new Color(0.78f, 0.06f, 0.18f, 1f), bossPulse);
            vigCol.a = Mathf.Clamp01(vigAlpha);
            abyssVignetteSr.color = vigCol;
        }

        float wrapHalfW = viewWidth * 0.54f;
        float wrapHalfH = viewHeight * 0.56f;
        float moteSpeedBoost = Mathf.Lerp(1f, bossEnraged ? 1.72f : 1.42f, bossBlend) + bossEntrySurge * 0.85f;

        for (int i = 0; i < parallaxMotes.Count; i++)
        {
            ParallaxMote m = parallaxMotes[i];
            if (m.tr == null) continue;

            if (m.bossOnly && bossBlend <= 0.005f)
            {
                if (m.sr != null && m.sr.color.a > 0f)
                {
                    Color hidden = m.sr.color;
                    hidden.a = 0f;
                    m.sr.color = hidden;
                }
                continue;
            }

            Vector3 lp = m.tr.localPosition;
            lp.y -= delta * m.speedMult * moteSpeedBoost;
            float sway = m.swayAmp > 0f ? Mathf.Cos(Time.time * m.swayFreq + m.twinklePhase) * m.swayAmp : 0f;
            lp.x += (m.driftX + sway) * dt;

            if (lp.y < -wrapHalfH)
            {
                lp.y = wrapHalfH;
                lp.x = Random.Range(-wrapHalfW, wrapHalfW);
            }
            else if (lp.x > wrapHalfW) lp.x = -wrapHalfW;
            else if (lp.x < -wrapHalfW) lp.x = wrapHalfW;

            m.tr.localPosition = lp;

            if (m.sr != null)
            {
                Color c = Color.Lerp(m.normalColor, m.bossColor, bossBlend);
                float twinkle = 0.72f + 0.28f * Mathf.Sin(Time.time * m.twinkleFreq + m.twinklePhase);
                float visMult = m.bossOnly ? bossBlend : 1f;
                c.a = m.baseAlpha * twinkle * visMult;
                m.sr.color = c;
            }
        }
    }

    private static void ScrollLayerPair(SpriteRenderer layer1, SpriteRenderer layer2, float height, float delta)
    {
        if (layer1 == null || layer2 == null || height <= 0f) return;

        layer1.transform.localPosition += Vector3.down * delta;
        layer2.transform.localPosition += Vector3.down * delta;

        if (layer1.transform.localPosition.y <= -height)
        {
            layer1.transform.localPosition = new Vector3(0f, layer2.transform.localPosition.y + height, 0f);
        }

        if (layer2.transform.localPosition.y <= -height)
        {
            layer2.transform.localPosition = new Vector3(0f, layer1.transform.localPosition.y + height, 0f);
        }
    }

    private static Sprite CreateAbyssVignetteSprite(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[size * size];
        float invMax = 1f / Mathf.Max(1f, (size - 1) * 0.5f);

        for (int y = 0; y < size; y++)
        {
            float ny = (y - (size - 1) * 0.5f) * invMax; // -1 abajo, +1 arriba
            for (int x = 0; x < size; x++)
            {
                float nx = (x - (size - 1) * 0.5f) * invMax;
                float edgeDist = Mathf.Sqrt(nx * nx * 1.15f + ny * ny * 0.92f);
                float rim = Mathf.Pow(Mathf.Clamp01((edgeDist - 0.44f) / 0.68f), 1.65f);
                float topOmen = Mathf.Clamp01((ny - 0.30f) / 0.70f) * 0.34f;
                float a = Mathf.Clamp01(rim * 0.85f + topOmen);
                pixels[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private static Sprite CreateSoftCircleSprite(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[size * size];
        float center = (size - 1) * 0.5f;
        float invR = 1f / Mathf.Max(1f, center);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - center) * invR;
                float dy = (y - center) * invR;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float core = Mathf.Clamp01(1f - d * 1.45f);
                float halo = Mathf.Pow(Mathf.Clamp01(1f - d), 2.8f);
                float a = Mathf.Clamp01(core * 0.75f + halo * 0.55f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private static Sprite CreateDustStreakSprite(int width, int height)
    {
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[width * height];
        float cx = (width - 1) * 0.5f;
        float cy = (height - 1) * 0.5f;

        for (int y = 0; y < height; y++)
        {
            float ny = Mathf.Abs(y - cy) / Mathf.Max(1f, cy);
            float fy = Mathf.Pow(Mathf.Clamp01(1f - ny), 1.8f);
            for (int x = 0; x < width; x++)
            {
                float nx = Mathf.Abs(x - cx) / Mathf.Max(1f, cx);
                float fx = Mathf.Pow(Mathf.Clamp01(1f - nx), 2.5f);
                pixels[y * width + x] = new Color(1f, 1f, 1f, fx * fy);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), Mathf.Max(width, height));
    }
}
