using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    public int Points = 0;

    public void SpendPoints(int amount)
    {
        Points -= amount;
    }
}