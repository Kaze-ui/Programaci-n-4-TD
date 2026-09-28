using UnityEngine;
using System.Collections;

public class EnemyExplosion : MonoBehaviour
{
    private static Sprite s_ExplosionT1;
    private static Sprite s_ExplosionT2;
    private static Sprite s_ExplosionT3;
    private static Sprite s_ExplosionT4;
    private static Sprite s_ExplosionBoss;

    private SpriteRenderer mainRenderer;
    private SpriteRenderer shockwaveRenderer;

    public static void PreloadSprites()
    {
        if (s_ExplosionT1 == null) s_ExplosionT1 = Resources.Load<Sprite>("Sprites/ExplosionTier1");
        if (s_ExplosionT2 == null) s_ExplosionT2 = Resources.Load<Sprite>("Sprites/ExplosionTier2");
        if (s_ExplosionT3 == null) s_ExplosionT3 = Resources.Load<Sprite>("Sprites/ExplosionTier3");
        if (s_ExplosionT4 == null) s_ExplosionT4 = Resources.Load<Sprite>("Sprites/ExplosionTier4");
        if (s_ExplosionBoss == null) s_ExplosionBoss = Resources.Load<Sprite>("Sprites/ExplosionBoss");
    }

    public static Sprite GetSpriteForTier(EnemyTier tier)
    {
        PreloadSprites();
        switch (tier)
        {
            case EnemyTier.Tier1: return s_ExplosionT1;
            case EnemyTier.Tier2: return s_ExplosionT2;
            case EnemyTier.Tier3: return s_ExplosionT3;
            case EnemyTier.Tier4: return s_ExplosionT4;
            default: return s_ExplosionT1;
        }
    }

    public static void Spawn(Vector3 position, EnemyTier tier, Vector3 enemyScale, Material material = null, Sprite customSprite = null)
    {
        Sprite spriteToUse = customSprite != null ? customSprite : GetSpriteForTier(tier);
        if (spriteToUse == null) return;

        GameObject expObj = new GameObject($"Explosion_{tier}");
        expObj.transform.position = position;

        // Rotación inicial aleatoria para dar variedad visual en cada baja
        float randomAngle = Random.Range(0f, 360f);
        expObj.transform.rotation = Quaternion.Euler(0f, 0f, randomAngle);

        EnemyExplosion expComponent = expObj.AddComponent<EnemyExplosion>();
        expComponent.Initialize(spriteToUse, tier, enemyScale, material);
    }

    public static void SpawnBossSequence(Vector3 bossPos, Vector3 bossScale, Material material = null)
    {
        PreloadSprites();
        GameObject controller = new GameObject("BossExplosionSequence");
        controller.transform.position = bossPos;
        EnemyExplosion exp = controller.AddComponent<EnemyExplosion>();
        exp.StartCoroutine(exp.PlayBossSequenceRoutine(bossPos, bossScale, material));
    }

    public void Initialize(Sprite sprite, EnemyTier tier, Vector3 enemyScale, Material material)
    {
        mainRenderer = gameObject.AddComponent<SpriteRenderer>();
        mainRenderer.sprite = sprite;
        if (material != null) mainRenderer.material = material;
        mainRenderer.sortingOrder = 15; // Por encima de enemigos y balas normales

        // Determinar escala base adecuada
        float targetBase = 100f;
        switch (tier)
        {
            case EnemyTier.Tier1: targetBase = Mathf.Max(95f, Mathf.Abs(enemyScale.x)); break;
            case EnemyTier.Tier2: targetBase = Mathf.Max(105f, Mathf.Abs(enemyScale.x)); break;
            case EnemyTier.Tier3: targetBase = Mathf.Max(145f, Mathf.Abs(enemyScale.x)); break;
            case EnemyTier.Tier4: targetBase = Mathf.Max(165f, Mathf.Abs(enemyScale.x)); break;
        }

        Vector3 baseScale = new Vector3(targetBase, targetBase, 1f);

        // Crear onda expansiva secundaria como objeto hijo
        GameObject shockwaveObj = new GameObject("Shockwave");
        shockwaveObj.transform.SetParent(transform, false);
        shockwaveObj.transform.localPosition = Vector3.zero;
        shockwaveObj.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 180f));

        shockwaveRenderer = shockwaveObj.AddComponent<SpriteRenderer>();
        shockwaveRenderer.sprite = sprite;
        if (material != null) shockwaveRenderer.material = material;
        shockwaveRenderer.sortingOrder = 16;

        StartCoroutine(AnimateExplosion(baseScale));
    }

    private IEnumerator AnimateExplosion(Vector3 baseScale)
    {
        float duration = 0.36f;
        float elapsed = 0f;

        float rotSpeed = Random.Range(-45f, 45f);

        while (elapsed < duration)
        {
            float dt = Time.deltaTime;
            elapsed += dt;
            float t = Mathf.Clamp01(elapsed / duration);

            // Giro sutil durante la explosión
            transform.Rotate(0f, 0f, rotSpeed * dt);

            // Fase 1 (0.0 a 0.08s): Destello e impacto inmediato (pop de escala)
            // Fase 2 (0.08 a 0.36s): Expansión y desvanecimiento
            float scaleFactor;
            float alpha;

            if (t < 0.22f)
            {
                float popT = t / 0.22f;
                scaleFactor = Mathf.Lerp(0.55f, 1.15f, Mathf.Sin(popT * Mathf.PI * 0.5f));
                alpha = 1f;
            }
            else
            {
                float fadeT = (t - 0.22f) / 0.78f;
                scaleFactor = Mathf.Lerp(1.15f, 1.45f, fadeT);
                alpha = Mathf.SmoothStep(1f, 0f, fadeT);
            }

            transform.localScale = baseScale * scaleFactor;
            if (mainRenderer != null)
            {
                mainRenderer.color = new Color(1f, 1f, 1f, alpha);
            }

            // Animación de la onda expansiva secundaria (se expande más rápido y desaparece antes)
            if (shockwaveRenderer != null)
            {
                float shockT = Mathf.Clamp01(elapsed / 0.22f);
                float shockScale = Mathf.Lerp(0.8f, 1.85f, shockT);
                float shockAlpha = Mathf.SmoothStep(0.7f, 0f, shockT);

                shockwaveRenderer.transform.localScale = Vector3.one * shockScale;
                shockwaveRenderer.color = new Color(1f, 1f, 1f, shockAlpha);
            }

            yield return null;
        }

        Destroy(gameObject);
    }

    private IEnumerator PlayBossSequenceRoutine(Vector3 bossPos, Vector3 bossScale, Material material)
    {
        Sprite bossSpr = s_ExplosionBoss != null ? s_ExplosionBoss : (s_ExplosionT4 != null ? s_ExplosionT4 : s_ExplosionT1);
        float baseWidth = Mathf.Max(300f, Mathf.Abs(bossScale.x));

        // 6 explosiones periféricas con estallidos en distintos puntos del chasis
        for (int i = 0; i < 6; i++)
        {
            Vector3 offset = new Vector3(
                Random.Range(-baseWidth * 0.45f, baseWidth * 0.45f),
                Random.Range(-baseWidth * 0.25f, baseWidth * 0.25f),
                0f
            );

            Spawn(bossPos + offset, (EnemyTier)(i % 4), new Vector3(130f, 130f, 1f), material);
            yield return new WaitForSeconds(0.12f);
        }

        // Gran explosión central final del Jefe
        if (bossSpr != null)
        {
            GameObject bigExp = new GameObject("BossFinalExplosion");
            bigExp.transform.position = bossPos;
            EnemyExplosion exp = bigExp.AddComponent<EnemyExplosion>();
            exp.Initialize(bossSpr, EnemyTier.Tier4, new Vector3(baseWidth * 1.6f, baseWidth * 1.6f, 1f), material);
        }

        yield return new WaitForSeconds(0.6f);
        Destroy(gameObject);
    }
}
