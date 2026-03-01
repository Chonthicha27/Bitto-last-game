using System.Collections.Generic;
using UnityEngine;

public enum CompanyType
{
    Normal,
    Bank,
    Gold
}

[CreateAssetMenu(fileName = "InvestmentCompany", menuName = "Scriptable Objects/InvestmentCompany")]
public class InvestmentCompany : ScriptableObject
{
    public string companyName;
    [TextArea] public string descriptionAssets;
    public Sprite companyIcon; 

    public EventData[] goodEvents;
    public EventData[] badEvents;
    
    [Header("Sub Assets")]
    public List<SubAssetData> subAssets;   // Asset สินทรัพย์ที่ซื้อขายได้
    
    public CompanyType companyType = CompanyType.Normal;

}
