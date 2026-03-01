// SubAssetData.cs
using UnityEngine;

public enum RiskLevel { Low, Medium, High }

[System.Serializable]
public class SubAssetData
{
    public string assetName;
    public string assetNameAbbreviation; //ชื่อตัวย่อของหุ้น 
    public float currentPrice;
    public float minPrice;
    public float maxPrice;
    public Sprite assetIcon;       // Icon ของ asset นั้นๆ
    public string typesOfAssets; // ข้อมูลประเภทสินทรัพย์
    [TextArea] public string description; 
    
    [Header("Risk Tag")]
    public RiskLevel risk = RiskLevel.Medium; // ✅ ตั้งหมวดความเสี่ยงให้แต่ละสินทรัพย์
}
