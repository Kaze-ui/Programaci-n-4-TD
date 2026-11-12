using System.Collections.Generic;
using UnityEngine;

public class Bullet : MonoBehaviour
{
    [Header("Movimiento")]
    public float speed = 2000f;

    [Header("Daño")]
    public float damage = 1f;

    [Header("Límite de vida")]
    public float lifeTime = 3f; // se autodestruye después de este tiempo por las dudas

    [Header("Propiedades de Bala Grande")]
    public bool isBigBullet = false;

    // Evita que la misma bala dañe repetidamente al mismo enemigo si lo atraviesa
    private HashSet<GameObject> hitEnemies = new HashSet<GameObject>();

    // Animación visual del proyectil
    private Vector3 baseScale;
    private Color baseColor = Color.white;
    private Color wakeTint = new Color(0.38f, 0.90f, 1f, 1f);
    private SpriteRenderer rootRenderer;
    private SpriteRenderer wakeRenderer;
    private Transform wakeTransform;
    private SpriteRenderer glowRenderer;
    private Transform glowTransform;
    private Transform outerVortexTransform;
    private Transform innerVortexTransform;
    private bool isHeavyShot = false;
    private float age = 0f;

    void Start()
    {
        Destroy(gameObject, lifeTime);
        InitVisualAnimation();
    }

    private void InitVisualAnimation()
    {
        baseScale = transform.localScale;
        isHeavyShot = isBigBullet || baseScale.x >= 120f || damage >= 2.5f;

        rootRenderer = GetComponent<SpriteRenderer>();
        int baseOrder = 0;
        Material sharedMat = null;
        if (rootRenderer != null)
        {
            baseColor = rootRenderer.color;
            baseOrder = rootRenderer.sortingOrder;
            sharedMat = rootRenderer.sharedMaterial;
        }

        wakeTint = isBigBullet
            ? new Color(1f, 0.90f, 0.34f, 1f)
            : (isHeavyShot ? new Color(0.35f, 0.96f, 1f, 1f) : new Color(0.40f, 0.90f, 1f, 1f));

        wakeRenderer = CreateVisualChild(
            "PlasmaWake",
            ChargeVisualUtils.GetPlasmaStreakSprite(),
            new Vector3(0f, -0.34f, 0f),
            new Vector3(isBigBullet ? 2.1f : 1.75f, 1.9f, 1f),
            baseOrder - 1,
            sharedMat,
            wakeTint
        );
        wakeTransform = wakeRenderer.transform;

        glowRenderer = CreateVisualChild(
            "CoreGlow",
            ChargeVisualUtils.GetGlowCircleSprite(),
            Vector3.zero,
            new Vector3(1.65f, 1.35f, 1f),
            baseOrder - 1,
            sharedMat,
            wakeTint
        );
        glowTransform = glowRenderer.transform;

        if (isHeavyShot)
        {
            SpriteRenderer outerSr = CreateVisualChild(
                "EnergyVortex",
                ChargeVisualUtils.GetEnergyVortexSprite(),
                Vector3.zero,
                isBigBullet ? new Vector3(1.85f, 1.85f, 1f) : new Vector3(1.55f, 1.55f, 1f),
                baseOrder + 1,
                sharedMat,
                isBigBullet ? new Color(0.35f, 0.96f, 1f, 0.85f) : new Color(0.55f, 0.98f, 1f, 0.72f)
            );
            outerVortexTransform = outerSr.transform;

            if (isBigBullet)
            {
                SpriteRenderer innerSr = CreateVisualChild(
                    "InnerEnergyVortex",
                    ChargeVisualUtils.GetEnergyVortexSprite(),
                    Vector3.zero,
                    new Vector3(1.25f, 1.25f, 1f),
                    baseOrder + 2,
                    sharedMat,
                    new Color(1f, 0.90f, 0.32f, 0.90f)
                );
                innerVortexTransform = innerSr.transform;
            }
        }
    }

    private SpriteRenderer CreateVisualChild(
        string childName, Sprite sprite, Vector3 localPos, Vector3 localScale,
        int sortingOrder, Material mat, Color tint)
    {
        GameObject obj = new GameObject(childName);
        obj.transform.SetParent(transform, false);
        obj.transform.localPosition = localPos;
        obj.transform.localScale = localScale;

        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        if (mat != null) sr.sharedMaterial = mat;
        sr.sortingOrder = sortingOrder;
        sr.color = tint;
        return sr;
    }

    void Update()
    {
        if (Time.timeScale == 0f) return;

        float dt = Time.deltaTime;
        transform.position += Vector3.up * speed * dt;
        UpdateVisualAnimation(dt);
    }

    private void UpdateVisualAnimation(float dt)
    {
        age += dt;
        ChargeVisualUtils.EvalBulletAnim(
            age, isHeavyShot, isBigBullet,
            out float sx, out float sy,
            out float trailScaleY, out float trailAlpha,
            out float coreGlowAlpha, out float spinDegPerSec
        );

        transform.localScale = new Vector3(baseScale.x * sx, baseScale.y * sy, baseScale.z);

        if (rootRenderer != null)
        {
            float shimmer = 0.08f + 0.14f * (0.5f + 0.5f * Mathf.Sin(age * (isHeavyShot ? 18f : 26f)));
            rootRenderer.color = Color.Lerp(baseColor, Color.white, shimmer);
        }

        if (wakeTransform != null && wakeRenderer != null)
        {
            float wakeWidth = (isBigBullet ? 2.1f : 1.75f) * (0.94f + 0.10f * Mathf.Sin(age * 32f));
            wakeTransform.localScale = new Vector3(wakeWidth, 1.9f * trailScaleY, 1f);
            Color c = wakeTint;
            c.a = trailAlpha;
            wakeRenderer.color = c;
        }

        if (glowTransform != null && glowRenderer != null)
        {
            float gs = 1.45f + 0.25f * Mathf.Sin(age * 24f);
            glowTransform.localScale = new Vector3(gs, gs * 1.15f, 1f);
            Color gc = wakeTint;
            gc.a = coreGlowAlpha;
            glowRenderer.color = gc;
        }

        if (outerVortexTransform != null)
        {
            outerVortexTransform.Rotate(0f, 0f, spinDegPerSec * dt);
        }
        if (innerVortexTransform != null)
        {
            innerVortexTransform.Rotate(0f, 0f, -spinDegPerSec * 1.35f * dt);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        HandleHit(other);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        // En balas grandes, permite detectar enemigos adicionales que toquen el colisionador durante el vuelo
        if (isBigBullet)
        {
            HandleHit(other);
        }
    }

    private void HandleHit(Collider2D other)
    {
        if (Time.timeScale == 0f || other == null) return;

        if (other.CompareTag("Enemy"))
        {
            GameObject enemyObj = other.gameObject;
            if (hitEnemies.Contains(enemyObj)) return;
            hitEnemies.Add(enemyObj);

            IDamageable damageable = enemyObj.GetComponent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(damage);
            }

            if (isBigBullet)
            {
                // Si la bala gigante derrotó al enemigo (murió o fue destruido),
                // la bala NO se destruye: atraviesa y sigue su curso pudiendo golpear y derrotar a otros enemigos.
                // Si el enemigo sobrevive (como el Jefe o un enemigo con mucha vida),
                // la bala gigante se consume y se destruye en el impacto.
                bool isDefeated = (damageable == null) || damageable.IsDead() || (enemyObj == null);
                if (!isDefeated)
                {
                    Destroy(gameObject);
                }
            }
            else
            {
                // Balas normales y mini balas se destruyen en el primer impacto
                Destroy(gameObject);
            }
        }
    }
}