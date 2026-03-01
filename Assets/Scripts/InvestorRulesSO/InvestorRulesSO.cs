using UnityEngine;
using System;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "InvestorRules", menuName = "Game/Investor Rules")]
public class InvestorRulesSO : ScriptableObject
{
    [Header("Minimal")]
    [Tooltip("ลงทุนน้อย / รวมทั้งพอร์ตต่ำกว่าค่านี้")]
    public float minimalTotalMaxPct = 50f;

    [Header("Conservative")]
    public float conservativeLowMin = 50f;
    public float conservativeHighMax = 20f;

    [Header("Cautious Balancer")]
    public Vector2 cautiousLowRange = new Vector2(50f, 70f);
    public Vector2 cautiousHighRange = new Vector2(20f, 40f);

    [Header("Balanced")]
    public float balancedMedMin = 50f;
    public float balancedHighMax = 40f;

    [Header("Progressive")]
    public Vector2 progressiveMedRange = new Vector2(30f, 50f);
    public Vector2 progressiveHighRange = new Vector2(40f, 60f);

    [Header("Adventurous")]
    public float adventurousHighMin = 50f;

    [Header("Dynamic Strategist")]
    [Tooltip("ไม่มีหมวดใดเกินค่านี้")]
    public float dynamicMaxAnyBucket = 60f;
    [Tooltip("อย่างน้อย 2 หมวดรวมกัน ≥ ค่านี้")]
    public float dynamicAtLeastTwoSumMin = 80f;
    [Tooltip("เกณฑ์ขั้นต่ำของแต่ละหมวดที่จะนับว่า 'มีส่วนร่วม'")]
    public float dynamicEachMinToCount = 20f;

    // ---------- NEW: ปรับแต่งชื่อ/คำอธิบาย + รูป ต่อประเภท ----------
    [Serializable]
    public struct InvestorText
    {
        public InvestorType type;
        public string title;
        [TextArea(2, 4)] public string description;

        [Header("Icon")]
        [Tooltip("รูปแทนประเภทนักลงทุนนี้")]
        public Sprite icon;     // 🟢 ตรงนี้แหละ ตัวเก็บ Sprite แต่ละประเภท
    }

    [Header("Display Texts (Editable on SO)")]
    public List<InvestorText> texts = new List<InvestorText>
    {
        new InvestorText{ type=InvestorType.Minimal,       title="Minimal Investor",       description="ยังไม่กล้าลงทุน ถือเงินสดส่วนใหญ่ สำรวจทางเลือกอยู่", icon=null },
        new InvestorText{ type=InvestorType.Conservative,  title="Conservative Investor",  description="เน้นสินทรัพย์เสี่ยงต่ำ รักษาเงินต้น มั่นคง ปลอดภัย", icon=null },
        new InvestorText{ type=InvestorType.CautiousBalancer, title="Cautious Balancer",  description="สมดุลแบบระวังตัว เปิดรับความเสี่ยงบางส่วน", icon=null },
        new InvestorText{ type=InvestorType.Balanced,      title="Balanced Investor",      description="กระจายกลาง ๆ ให้น้ำหนัก Medium ชัดเจน", icon=null },
        new InvestorText{ type=InvestorType.Progressive,   title="Progressive Investor",   description="เน้นเติบโต รับความเสี่ยงเพิ่มขึ้น", icon=null },
        new InvestorText{ type=InvestorType.Adventurous,   title="Adventurous Investor",   description="ลุยเสี่ยงสูง มุ่งผลตอบแทนสูงสุด", icon=null },
        new InvestorText{ type=InvestorType.DynamicStrategist, title="Dynamic Strategist", description="วางแผนยืดหยุ่น กระจายหลายหมวด ปรับตามสถานการณ์", icon=null },
    };

    public InvestorText GetText(InvestorType type)
    {
        foreach (var t in texts)
            if (t.type == type) return t;

        // fallback เผื่อถูกลบรายการ
        return new InvestorText { type = type, title = type.ToString(), description = "", icon = null };
    }

    // ---------- กฎจัดประเภท ----------
    public InvestorType Evaluate(float low, float med, float high)
    {
        float total = low + med + high;

        // 🔹 Minimal: รวม ๆ แล้วแทบไม่มีการลงทุน (เช่น ต่ำกว่า 5%)
        if (total < 5f)
            return InvestorType.Minimal;

        float maxBucket = Mathf.Max(low, Mathf.Max(med, high));
        int contributeCount = 0;
        if (low >= dynamicEachMinToCount) contributeCount++;
        if (med >= dynamicEachMinToCount) contributeCount++;
        if (high >= dynamicEachMinToCount) contributeCount++;

        // ลุยเสี่ยงสูง
        if (high >= adventurousHighMin) return InvestorType.Adventurous;

        // เน้นกลาง
        if (med >= balancedMedMin && high <= balancedHighMax) return InvestorType.Balanced;

        // เติบโต/รับเสี่ยงมากขึ้น
        if ((med >= progressiveMedRange.x && med <= progressiveMedRange.y) ||
            (high >= progressiveHighRange.x && high <= progressiveHighRange.y))
            return InvestorType.Progressive;

        // เน้นปลอดภัย
        if (low >= conservativeLowMin && high <= conservativeHighMax) return InvestorType.Conservative;

        // low เยอะ + high ปานกลาง
        if (low >= cautiousLowRange.x && low <= cautiousLowRange.y &&
            high >= cautiousHighRange.x && high <= cautiousHighRange.y)
            return InvestorType.CautiousBalancer;

        // กระจายหลายหมวด
        if (maxBucket <= dynamicMaxAnyBucket && contributeCount >= 2 && total >= dynamicAtLeastTwoSumMin)
            return InvestorType.DynamicStrategist;

        // fallback: ถ้ายังไม่เข้าเกณฑ์อะไรเลย → Balanced แทนที่จะโยนกลับ Minimal
        return InvestorType.Balanced;
    }


}

public enum InvestorType
{
    Minimal,
    Conservative,
    CautiousBalancer,
    Balanced,
    Progressive,
    Adventurous,
    DynamicStrategist
}
