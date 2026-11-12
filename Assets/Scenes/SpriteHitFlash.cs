using UnityEngine;

/// <summary>
/// Aplica un destello blanco parcial temporal sobre los SpriteRenderers de cualquier enemigo o jefe al recibir daño,
/// mezclando suavemente su sprite hacia blanco sin perder visibilidad ni crear fugas de materiales.
/// </summary>
public class SpriteHitFlash : MonoBehaviour
{
    private static Shader s_FlashShader;
    private static Material s_FlashMaterial;
    private static MaterialPropertyBlock s_Mpb;
    private static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");
    private static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");

    private SpriteRenderer[] renderers;
    private Material[] originalMaterials;
    private float flashDuration = 0.11f;
    private float flashTimer = 0f;
    private float peakStrength = 0.58f;
    private bool isFlashing = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticResources()
    {
        s_FlashShader = null;
        if (s_FlashMaterial != null)
        {
            DestroyImmediate(s_FlashMaterial);
            s_FlashMaterial = null;
        }
        s_Mpb = null;
    }

    public static void TriggerOn(GameObject target, float duration = 0.11f, float peakFlash = 0.58f)
    {
        if (target == null || !target.activeInHierarchy) return;
        SpriteHitFlash flash = target.TryGetComponent<SpriteHitFlash>(out var existing)
            ? existing
            : target.AddComponent<SpriteHitFlash>();
        flash.Trigger(duration, peakFlash);
    }

    private static Material GetSharedFlashMaterial()
    {
        if (s_FlashMaterial != null) return s_FlashMaterial;

        if (s_FlashShader == null)
        {
            s_FlashShader = Resources.Load<Shader>("Shaders/SpriteWhiteFlash");
            if (s_FlashShader == null)
            {
                s_FlashShader = Shader.Find("Custom/URP2D_SpriteWhiteFlash");
            }
        }
        if (s_FlashShader == null) return null;

        s_FlashMaterial = new Material(s_FlashShader)
        {
            name = "Shared_SpriteWhiteFlash_Mat",
            hideFlags = HideFlags.HideAndDontSave
        };
        return s_FlashMaterial;
    }

    private void CacheRenderers()
    {
        SpriteRenderer[] all = GetComponentsInChildren<SpriteRenderer>(true);
        var valid = new System.Collections.Generic.List<SpriteRenderer>(all.Length);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && !all[i].gameObject.name.StartsWith("Vfx_"))
            {
                valid.Add(all[i]);
            }
        }
        renderers = valid.ToArray();
        originalMaterials = new Material[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i].sharedMaterial != s_FlashMaterial)
            {
                originalMaterials[i] = renderers[i].sharedMaterial;
            }
        }
    }

    public void Trigger(float duration = 0.11f, float peakFlash = 0.58f)
    {
        if (GetSharedFlashMaterial() == null) return;
        if (renderers == null || renderers.Length == 0) CacheRenderers();

        flashDuration = Mathf.Max(0.02f, duration);
        peakStrength = Mathf.Clamp01(peakFlash);
        flashTimer = flashDuration;
        isFlashing = true;

        ApplyFlash(peakStrength);
    }

    void Update()
    {
        if (!isFlashing) return;

        flashTimer -= Time.deltaTime;
        if (flashTimer <= 0f)
        {
            RestoreRenderers();
            return;
        }

        float normalized = Mathf.Clamp01(flashTimer / flashDuration);
        ApplyFlash(peakStrength * normalized);
    }

    void OnDisable() => RestoreRenderers();

    private void ApplyFlash(float amount)
    {
        Material flashMat = GetSharedFlashMaterial();
        if (flashMat == null || renderers == null) return;
        if (s_Mpb == null) s_Mpb = new MaterialPropertyBlock();

        s_Mpb.SetColor(FlashColorId, Color.white);
        s_Mpb.SetFloat(FlashAmountId, amount);

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer sr = renderers[i];
            if (sr == null) continue;

            if (sr.sharedMaterial != flashMat)
            {
                originalMaterials[i] = sr.sharedMaterial;
                sr.sharedMaterial = flashMat;
            }
            sr.SetPropertyBlock(s_Mpb);
        }
    }

    private void RestoreRenderers()
    {
        if (!isFlashing || renderers == null) return;
        isFlashing = false;
        flashTimer = 0f;

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer sr = renderers[i];
            if (sr == null) continue;

            sr.SetPropertyBlock(null);
            if (originalMaterials != null && i < originalMaterials.Length && originalMaterials[i] != null)
            {
                sr.sharedMaterial = originalMaterials[i];
            }
        }
    }
}

