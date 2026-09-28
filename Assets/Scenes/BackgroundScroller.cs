using UnityEngine;

public class BackgroundScroller : MonoBehaviour
{
    [Header("Sprite de Fondo")]
    public Sprite backgroundSprite;

    [Header("Velocidad de desplazamiento")]
    public float scrollSpeed = 60f;

    private Camera mainCam;
    private SpriteRenderer sr1;
    private SpriteRenderer sr2;
    private float bgHeight;

    void Awake()
    {
        mainCam = Camera.main;
        SetupBackground();
    }

    void SetupBackground()
    {
        if (backgroundSprite == null)
        {
            backgroundSprite = Resources.Load<Sprite>("Sprites/SpaceBackground");
        }

        Vector3 camPos = (mainCam != null) ? mainCam.transform.position : new Vector3(957f, 542f, 0f);
        transform.position = new Vector3(camPos.x, camPos.y, 0f);

        // Primer plano de fondo
        GameObject bgObj1 = new GameObject("SpaceBG_Layer1");
        bgObj1.transform.SetParent(transform);
        bgObj1.transform.localPosition = Vector3.zero;
        sr1 = bgObj1.AddComponent<SpriteRenderer>();
        sr1.sprite = backgroundSprite;
        sr1.sortingOrder = -100;

        // Segundo plano de fondo para desplazamiento continuo sin cortes
        GameObject bgObj2 = new GameObject("SpaceBG_Layer2");
        bgObj2.transform.SetParent(transform);
        sr2 = bgObj2.AddComponent<SpriteRenderer>();
        sr2.sprite = backgroundSprite;
        sr2.sortingOrder = -100;

        // Calcular escala para cubrir todo el ancho y alto visible de la cámara ortográfica
        float camHeight = (mainCam != null) ? mainCam.orthographicSize * 2f : 2000f;
        float camWidth = camHeight * ((mainCam != null) ? mainCam.aspect : (16f / 9f));

        if (backgroundSprite != null && backgroundSprite.rect.height > 0)
        {
            float spriteW = backgroundSprite.bounds.size.x;
            float spriteH = backgroundSprite.bounds.size.y;

            float scaleX = (camWidth * 1.25f) / spriteW;
            float scaleY = (camHeight * 1.25f) / spriteH;
            float scale = Mathf.Max(scaleX, scaleY);

            bgObj1.transform.localScale = new Vector3(scale, scale, 1f);
            bgObj2.transform.localScale = new Vector3(scale, scale, 1f);

            bgHeight = spriteH * scale;
            bgObj2.transform.localPosition = new Vector3(0f, bgHeight, 0f);
        }
        else
        {
            bgObj1.transform.localScale = new Vector3(4f, 4f, 1f);
            bgObj2.transform.localScale = new Vector3(4f, 4f, 1f);
            bgHeight = 2500f;
            bgObj2.transform.localPosition = new Vector3(0f, bgHeight, 0f);
        }
    }

    void Update()
    {
        if (Time.timeScale == 0f) return;

        float delta = scrollSpeed * Time.deltaTime;

        if (sr1 != null)
        {
            sr1.transform.localPosition += Vector3.down * delta;
        }
        if (sr2 != null)
        {
            sr2.transform.localPosition += Vector3.down * delta;
        }

        // Reposicionar en ciclo continuo
        if (sr1 != null && sr1.transform.localPosition.y <= -bgHeight)
        {
            sr1.transform.localPosition = new Vector3(0f, sr2.transform.localPosition.y + bgHeight - 2f, 0f);
        }

        if (sr2 != null && sr2.transform.localPosition.y <= -bgHeight)
        {
            sr2.transform.localPosition = new Vector3(0f, sr1.transform.localPosition.y + bgHeight - 2f, 0f);
        }
    }
}
