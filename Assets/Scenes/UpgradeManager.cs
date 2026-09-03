using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class UpgradeManager : MonoBehaviour
{
    [Header("Daño")]
    [SerializeField] private Button damageButton;
    [SerializeField] private TextMeshProUGUI damageCostText;
    [SerializeField] private int damageIncreasePerLevel = 2;
    private int damageLevel = 0;

    [Header("Velocidad")]
    [SerializeField] private Button speedButton;
    [SerializeField] private TextMeshProUGUI speedCostText;
    [SerializeField] private float speedIncreasePerLevel = 150f;
    private int speedLevel = 0;

    [Header("Salud")]
    [SerializeField] private Button healthButton;
    [SerializeField] private TextMeshProUGUI healthCostText;
    [SerializeField] private int healthIncreasePerLevel = 1;
    private int healthLevel = 0;

    [Header("Continuar a la siguiente oleada")]
    [SerializeField] private Button continueButton;

    // WaveController se suscribe a esto para saber cuándo el jugador
    // terminó de comprar mejoras y hay que arrancar la siguiente oleada.
    public Action OnContinue;

    [Header("Configuración de costo")]
    [SerializeField] private int baseCost = 10;
    [SerializeField] private int costIncreasePerLevel = 5;

    [Header("Referencias")]
    [SerializeField] private PlayerController playerController;

    void OnEnable()
    {
        RefreshUI();
    }

    void Start()
    {
        Debug.Log("[DEBUG] UpgradeManager.Start() ejecutado"); // TEMPORAL, borrar después
        if (damageButton != null) damageButton.onClick.AddListener(PurchaseDamage);
        if (speedButton != null) speedButton.onClick.AddListener(PurchaseSpeed);
        if (healthButton != null) healthButton.onClick.AddListener(PurchaseHealth);
        if (continueButton != null) continueButton.onClick.AddListener(() => OnContinue?.Invoke());
    }

    private int GetCost(int level)
    {
        return baseCost + (level * costIncreasePerLevel);
    }

    private void PurchaseDamage()
    {
        Debug.Log("[DEBUG] PurchaseDamage() llamado"); // TEMPORAL, borrar después
        int cost = GetCost(damageLevel);
        if (GameManager.Instance == null || !GameManager.Instance.TrySpendPoints(cost)) return;

        damageLevel++;
        if (playerController != null) playerController.IncreaseDamage(damageIncreasePerLevel);
        if (damageCostText != null) damageCostText.text = GetCost(damageLevel).ToString();
    }

    private void PurchaseSpeed()
    {
        int cost = GetCost(speedLevel);
        if (GameManager.Instance == null || !GameManager.Instance.TrySpendPoints(cost)) return;

        speedLevel++;
        if (playerController != null) playerController.IncreaseSpeed(speedIncreasePerLevel);
        if (speedCostText != null) speedCostText.text = GetCost(speedLevel).ToString();
    }

    private void PurchaseHealth()
    {
        int cost = GetCost(healthLevel);
        if (GameManager.Instance == null || !GameManager.Instance.TrySpendPoints(cost)) return;

        healthLevel++;
        if (playerController != null) playerController.IncreaseMaxHealth(healthIncreasePerLevel);
        if (healthCostText != null) healthCostText.text = GetCost(healthLevel).ToString();
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