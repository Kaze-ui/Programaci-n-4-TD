using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UpgradeManager : MonoBehaviour
{
    [Header("Daño")]
    [SerializeField] private Button damageButton;
    [SerializeField] private TextMeshProUGUI damageCostText;
    private int damageLevel = 0;

    [Header("Velocidad")]
    [SerializeField] private Button speedButton;
    [SerializeField] private TextMeshProUGUI speedCostText;
    private int speedLevel = 0;

    [Header("Salud")]
    [SerializeField] private Button healthButton;
    [SerializeField] private TextMeshProUGUI healthCostText;
    private int healthLevel = 0;

    [Header("Configuración de costo")]
    [SerializeField] private int baseCost = 10;
    [SerializeField] private int costIncreasePerLevel = 5;

    [SerializeField] private PlayerStats playerStats; // referencia a tus stats/puntos

    void OnEnable()
    {
        RefreshUI();
    }

    void Start()
    {
        if (damageButton != null) damageButton.onClick.AddListener(() => TryPurchase(ref damageLevel, damageCostText));
        if (speedButton != null) speedButton.onClick.AddListener(() => TryPurchase(ref speedLevel, speedCostText));
        if (healthButton != null) healthButton.onClick.AddListener(() => TryPurchase(ref healthLevel, healthCostText));
    }

    private int GetCost(int level)
    {
        return baseCost + (level * costIncreasePerLevel);
    }

    private void TryPurchase(ref int level, TextMeshProUGUI costText)
    {
        int cost = GetCost(level);

        if (playerStats != null && playerStats.Points >= cost)
        {
            playerStats.SpendPoints(cost);
            level++;
            if (costText != null) costText.text = GetCost(level).ToString();
        }
        // si no alcanza, no pasa nada (opcionalmente después le sumamos un feedback visual/sonoro de "no alcanza")
    }

    public void RefreshUI()
    {
        if (damageCostText != null) damageCostText.text = GetCost(damageLevel).ToString();
        if (speedCostText != null) speedCostText.text = GetCost(speedLevel).ToString();
        if (healthCostText != null) healthCostText.text = GetCost(healthLevel).ToString();
    }

    public int DamageLevel => damageLevel;
    public int SpeedLevel => speedLevel;
    public int HealthLevel => healthLevel;
}