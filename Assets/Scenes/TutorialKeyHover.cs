using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Controla un botón o tecla interactiva en los diagramas del tutorial:
/// - Muestra tooltip al pasar el cursor encima y lo oculta al salir.
/// - Resalta en un verde más oscuro y se presiona visualmente cuando la tecla o botón físico está pulsado.
/// </summary>
public class TutorialKeyHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private string tooltipKey;
    private string tooltipDesc;
    private TutorialPanelController controller;

    private Image fillImage;
    private Image borderImage;
    private TextMeshProUGUI labelTmp;

    private Color normalFillColor;
    private Color hoverFillColor;
    private Color pressedFillColor;

    private Color normalBorderColor = new Color(0.25f, 0.90f, 0.50f, 1f);
    private Color hoverBorderColor = new Color(0.45f, 1f, 0.70f, 1f);
    private Color pressedBorderColor = new Color(0.15f, 0.70f, 0.38f, 1f);

    private Color normalTextColor = new Color(0.75f, 1f, 0.85f, 1f);
    private Color pressedTextColor = Color.white;

    private Func<bool> pressedPredicate;
    private bool isHovered = false;
    private bool isVisuallyPressed = false;
    private Vector3 targetScale = Vector3.one;

    public void Setup(
        TutorialPanelController ctrl,
        string keyText,
        string descText,
        Image fillImg,
        Image borderImg,
        TextMeshProUGUI lbl,
        Color normalCol,
        Color hovCol,
        Color pressCol,
        Func<bool> isPressedCheck = null)
    {
        controller = ctrl;
        tooltipKey = keyText;
        tooltipDesc = descText;
        fillImage = fillImg;
        borderImage = borderImg;
        labelTmp = lbl;

        normalFillColor = normalCol;
        hoverFillColor = hovCol;
        pressedFillColor = pressCol;
        pressedPredicate = isPressedCheck;

        ApplyVisualState(false, false, true);
    }

    // Sobrecarga de compatibilidad simple
    public void Setup(TutorialPanelController ctrl, string keyText, string descText, Color normalCol, Color hovCol)
    {
        Image img = GetComponent<Image>();
        Setup(
            ctrl,
            keyText,
            descText,
            img,
            null,
            GetComponentInChildren<TextMeshProUGUI>(),
            normalCol,
            hovCol,
            new Color(0.02f, 0.16f, 0.08f, 1f),
            null
        );
    }

    void Update()
    {
        bool pressedNow = pressedPredicate != null && pressedPredicate();
        if (pressedNow != isVisuallyPressed)
        {
            ApplyVisualState(isHovered, pressedNow, false);
        }

        if (transform.localScale != targetScale)
        {
            transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.unscaledDeltaTime * 22f);
        }
    }

    private void ApplyVisualState(bool hovered, bool pressed, bool immediate)
    {
        isHovered = hovered;
        isVisuallyPressed = pressed;

        if (pressed)
        {
            if (fillImage != null) fillImage.color = pressedFillColor;
            if (borderImage != null) borderImage.color = pressedBorderColor;
            if (labelTmp != null) labelTmp.color = pressedTextColor;
            targetScale = new Vector3(0.92f, 0.92f, 1f);
        }
        else if (hovered)
        {
            if (fillImage != null) fillImage.color = hoverFillColor;
            if (borderImage != null) borderImage.color = hoverBorderColor;
            if (labelTmp != null) labelTmp.color = Color.white;
            targetScale = new Vector3(1.04f, 1.04f, 1f);
        }
        else
        {
            if (fillImage != null) fillImage.color = normalFillColor;
            if (borderImage != null) borderImage.color = normalBorderColor;
            if (labelTmp != null) labelTmp.color = normalTextColor;
            targetScale = Vector3.one;
        }

        if (immediate)
        {
            transform.localScale = targetScale;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        ApplyVisualState(true, isVisuallyPressed, false);

        if (controller != null)
        {
            controller.ShowTooltip(tooltipKey, tooltipDesc, transform.position);
        }

        if (SoundController.HasInstance)
        {
            SoundController.Instance.PlayUpgradeHoverSfx();
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        ApplyVisualState(false, isVisuallyPressed, false);

        if (controller != null)
        {
            controller.HideTooltip();
        }
    }

    void OnDisable()
    {
        ApplyVisualState(false, false, true);
        if (controller != null)
        {
            controller.HideTooltip();
        }
    }
}
