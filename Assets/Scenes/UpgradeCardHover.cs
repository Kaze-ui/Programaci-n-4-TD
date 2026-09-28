using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class UpgradeCardHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image artworkImage;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Vector3 hoverScale = new Vector3(1.035f, 1.035f, 1f);

    private Vector3 originalScale = Vector3.one;
    private Color originalArtColor = Color.white;
    private Color originalBgColor = Color.white;
    private bool isHovered = false;

    void Awake()
    {
        originalScale = transform.localScale;
        if (backgroundImage == null) backgroundImage = GetComponent<Image>();
        if (backgroundImage != null) originalBgColor = backgroundImage.color;
        if (artworkImage != null) originalArtColor = artworkImage.color;
    }

    public void Setup(Image artImg, Image bgImg)
    {
        artworkImage = artImg;
        backgroundImage = bgImg;
        if (artworkImage != null) originalArtColor = artworkImage.color;
        if (backgroundImage != null) originalBgColor = backgroundImage.color;
        originalScale = transform.localScale;
    }

    public void SetArtwork(Image img)
    {
        artworkImage = img;
        if (img != null) originalArtColor = img.color;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;

        if (SoundController.Instance != null)
        {
            SoundController.Instance.PlayUpgradeHoverSfx();
        }

        // Oscurecer ligeramente la imagen y la tarjeta para dar feedback táctil de selección
        if (artworkImage != null)
        {
            artworkImage.color = new Color(originalArtColor.r * 0.70f, originalArtColor.g * 0.70f, originalArtColor.b * 0.70f, originalArtColor.a);
        }

        if (backgroundImage != null)
        {
            backgroundImage.color = new Color(originalBgColor.r * 0.75f, originalBgColor.g * 0.75f, originalBgColor.b * 0.75f, originalBgColor.a);
        }

        transform.localScale = Vector3.Scale(originalScale, hoverScale);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;

        if (artworkImage != null)
        {
            artworkImage.color = originalArtColor;
        }

        if (backgroundImage != null)
        {
            backgroundImage.color = originalBgColor;
        }

        transform.localScale = originalScale;
    }

    void OnDisable()
    {
        if (isHovered)
        {
            OnPointerExit(null);
        }
    }
}
