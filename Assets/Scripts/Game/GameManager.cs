using UnityEngine;

public class GameManager : MonoBehaviour
{
    public InvestmentManager investmentManager;

    private void Start()
    {
        StartNewRound();
    }

    private void StartNewRound()
    {
        // เริ่มวันใหม่และสุ่ม Event ให้บริษัท
        investmentManager.RandomEventForEachCompany();
        Debug.Log("New Day Started");
    }
}
