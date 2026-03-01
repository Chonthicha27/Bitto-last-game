using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class EventData
{
    [Header("ข้อมูลอีเวนต์")]
    public string marketEventTopicText;

    [TextArea]
    public string description;

    // ⭐ ข้อความ Flavor ไว้ใช้โชว์ใน DailyFlavorTextUI
    [Header("Flavor Text (สำหรับ Popup)")]
    [TextArea(2, 3)]
    public string flavorText;

    [Tooltip("ผลกระทบเป็น % แบบคงที่ เช่น 5 = +5%, -10 = -10%")]
    public float priceChange;

    public Sprite eventIcon;  // ใช้โชว์ใน UI

    public enum TargetMode
    {
        AllSubAssets,     // โดนทุก sub-asset ของบริษัท
        SpecificIndices,  // โดนเฉพาะ index ที่กำหนด
        SpecificNames,    // โดนเฉพาะชื่อที่กำหนด (ต้องไม่ซ้ำในบริษัท)
        RandomN           // สุ่ม N รายการจากทุก sub-asset ของบริษัท
    }

    [Header("เลือกเป้าหมาย")]
    public TargetMode targetMode = TargetMode.AllSubAssets;

    [Tooltip("ใช้เมื่อเลือก SpecificIndices (เช่น 0, 2, 3)")]
    public List<int> targetIndices = new List<int>();

    [Tooltip("ใช้เมื่อเลือก SpecificNames (ต้องตรงกับ SubAsset.assetName ในบริษัทนั้น)")]
    public List<string> targetNames = new List<string>();

    [Tooltip("ใช้เมื่อเลือก RandomN (ขั้นต่ำ 1)")]
    [Min(1)]
    public int randomCount = 1;
}
