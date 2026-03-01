using UnityEngine;

public enum SkillZone
{
    Profit,      // Zone 1: เพิ่มกำไร
    RiskReduce,  // Zone 2: ลดความเสี่ยง
    EventBoost   // Zone 3: Event
}

[CreateAssetMenu(fileName = "NewSkill", menuName = "Skill Tree/Skill")]
public class SkillData : ScriptableObject
{
    [Header("Basic Info")]
    public string skillId;
    public string skillName;
    [TextArea] public string description;
    public SkillZone zone;
    public int costPoints = 1;

    [Header("Effects")]
    [Range(0, 100)] public float profitBonusPercent;   // เช่น +10% กำไร
    [Range(0, 100)] public float riskReducePercent;    // เช่น ลดขาดทุน 5%
    [Range(0, 100)] public float eventBonusPercent;    // เช่น Event ดีเพิ่มขึ้น 5%

    [Header("UI")]
    public Sprite icon;
}