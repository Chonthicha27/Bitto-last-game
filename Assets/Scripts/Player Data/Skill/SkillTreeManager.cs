using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SkillTreeManager : MonoBehaviour
{
    [Header("Skill Data")]
    public SkillData[] allSkills;       // ลิสต์สกิลทั้งหมด (ลาก ScriptableObjects เข้ามาใน Inspector)

    [Header("Zone Parents")]
    public Transform zone1Parent;       // Panel สำหรับ Zone Profit
    public Transform zone2Parent;       // Panel สำหรับ Zone RiskReduce
    public Transform zone3Parent;       // Panel สำหรับ Zone Event

    [Header("Spacing Settings")]
    [SerializeField] private float zone1Spacing;
    [SerializeField] private float zone2Spacing;
    [SerializeField] private float zone3Spacing;
    
    [Header("Prefab")]
    public GameObject skillButtonPrefab; 

    private SkillTreeData skillTree;

    void Start()
    {
        skillTree = PlayerDataManager.Instance.localPlayerSkillTree;
        GenerateSkillButtons();
    }

    void GenerateSkillButtons()
    {
        /*foreach (SkillData skill in allSkills)
        {
            Transform parent = zone1Parent;
            if (skill.zone == SkillZone.RiskReduce) parent = zone2Parent;
            else if (skill.zone == SkillZone.EventBoost) parent = zone3Parent;

            GameObject buttonObj = Instantiate(skillButtonPrefab, parent);
            buttonObj.name = "Skill_" + skill.skillId;

            buttonObj.GetComponentInChildren<TMP_Text>().text = skill.skillName;
            buttonObj.GetComponent<Image>().sprite = skill.icon;

            Button btn = buttonObj.GetComponent<Button>();
            btn.onClick.AddListener(() => OnSkillClicked(skill));
        }*/
        
        
        int indexProfit = 0, indexRisk = 0, indexEvent = 0;

        foreach (SkillData skill in allSkills)
        {
            Transform parent = zone1Parent;
            float spacing = zone1Spacing;
            int index = 0;

            if (skill.zone == SkillZone.RiskReduce)
            {
                parent = zone2Parent;
                spacing = zone2Spacing;
                index = indexRisk++;
            }
            else if (skill.zone == SkillZone.EventBoost)
            {
                parent = zone3Parent;
                spacing = zone3Spacing;
                index = indexEvent++;
            }
            else
            {
                parent = zone1Parent;
                spacing = zone1Spacing;
                index = indexProfit++;
            }

            GameObject buttonObj = Instantiate(skillButtonPrefab, parent);
            buttonObj.name = "Skill_" + skill.skillId;

            // กำหนดตำแหน่งเรียงตาม spacing
            RectTransform rt = buttonObj.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(0, spacing * index);

            buttonObj.GetComponentInChildren<TMP_Text>().text = skill.skillName;
            buttonObj.GetComponent<Image>().sprite = skill.icon;

            Button btn = buttonObj.GetComponent<Button>();
            btn.onClick.AddListener(() => OnSkillClicked(skill));
        }
    }
    void OnSkillClicked(SkillData skill)
    {
        if (skillTree.UnlockSkill(skill.skillId, skill.costPoints))
        {
            Debug.Log($"Unlocked skill: {skill.skillName}");
            ApplySkillEffect(skill);
        }
        else
        {
            Debug.LogWarning($"ปลดล็อค {skill.skillName} ไม่ได้ (Skill Points ไม่พอหรือปลดล็อคแล้ว)");
        }
    }
    void ApplySkillEffect(SkillData skill)
    {
        if (skill.profitBonusPercent > 0)
        {
            Debug.Log($"+ กำไรเพิ่ม {skill.profitBonusPercent}%");
        }

        if (skill.riskReducePercent > 0)
        {
            Debug.Log($"- ลดความเสี่ยง {skill.riskReducePercent}%");
        }

        if (skill.eventBonusPercent > 0)
        {
            Debug.Log($"+ โอกาส Event ดี {skill.eventBonusPercent}%");
        }
    }
}
