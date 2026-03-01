using UnityEngine;

[CreateAssetMenu(
    fileName = "PlayerEventSO",
    menuName = "Scriptable Objects/Player Event (Simple)"
)]
public class PlayerEventSO : ScriptableObject
{
    [Header("Info")]
    [TextArea]
    public string description;   // ข้อความอธิบายเหตุการณ์

    [Tooltip("จำนวนเงินที่บวก/ลบให้ผู้เล่น (+/- ได้)")]
    public float price;          // มูลค่า/จำนวนเงิน (+/-)

    [Tooltip("ไอคอนของเหตุการณ์ (แสดงใน UI)")]
    public Sprite icon;          // << เดิมชื่อ assetIcon เปลี่ยนเป็น icon ให้ตรงกับที่อ้างใช้
}
