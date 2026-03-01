using TMPro;
using UnityEngine;
using System;
using System.Globalization;

public class DailyReportRowUI : MonoBehaviour
{
    #region === UI REFERENCE ===
    
    public TMP_Text assetNameText;
    public TMP_Text heldText;
    public TMP_Text changePctText;      // % ของวันถัดไป
    public TMP_Text gainLossText;       // P/L วันนี้ (เทียบเมื่อวาน)
    public TMP_Text nextGainLossText;   // P/L วันถัดไป (เทียบวันนี้)

    // ราคา ณ ปัจจุบัน และราคาหลัง +/−% วันถัดไป
    public TMP_Text currentPriceText;   // ราคาวันนี้
    public TMP_Text nextPriceText;      // ราคาวันถัดไป

    // กำไร/ขาดทุนของ "วันถัดไป" เทียบ "วันนี้" = (nextDayPrice - currentPrice) × held
    public TMP_Text nextPerText;
    
    #endregion
    
    #region === UNITY LIFECYCLE ===
    void Awake()
    {
        // Auto-bind กันลืมลากใน Inspector
        TryAutoBind(ref assetNameText, "AssetNameText");
        TryAutoBind(ref heldText, "HeldText");
        TryAutoBind(ref changePctText, "ChangePctText");
        TryAutoBind(ref gainLossText, "GainLossText");
        TryAutoBind(ref nextGainLossText, "NextGainLossText");
        TryAutoBind(ref currentPriceText, "CurrentPriceText");
        TryAutoBind(ref nextPriceText, "NextPriceText");
        TryAutoBind(ref nextPerText, "NextPerText");
    }
    
    #endregion
    
    #region === UI BINDING HELPERS ===
    private void TryAutoBind(ref TMP_Text field, string childName)
    {
        if (field != null)
        {
            return;
        }
        var t = transform.Find(childName);
        if (t != null)
        {
            field = t.GetComponent<TMP_Text>();
        }
    }
    #endregion
    
    #region === MATH HELPERS ===
    
    // ปัดทศนิยม 2 ตำแหน่งแบบ floor ให้ตรงกับ UI 
    private static float Floor2(float v) => Mathf.Floor(v * 100f) / 100f;
    #endregion
    
    #region === FORMATTING HELPERS ===
    // ---------- ฟอร์แมตเลข / % แบบมีคอมม่า ----------
    /// <summary>
    /// ฟอร์แมตตัวเลขทั่วไปให้มีคอมม่า และทศนิยมไม่เกิน 2 ตำแหน่ง (ตัด ไม่ปัด)
    /// 10000000 -> 10,000,000
    /// 10000000.5 -> 10,000,000.5
    /// 10000000.529 -> 10,000,000.52
    /// </summary>
    private static string FormatNumber(float v)
    {
        decimal d = (decimal)v;
        d = Math.Truncate(d * 100m) / 100m;
        return d.ToString("#,0.##", CultureInfo.InvariantCulture);
    }
    /// <summary>
    /// ฟอร์แมตตัวเลขมีเครื่องหมาย + / - และคอมม่า เช่น +10,000,000.52 / -1,234.5 / 0
    /// </summary>
    private static string FormatSignedNumber(float v)
    {
        decimal d = (decimal)v;
        d = Math.Truncate(d * 100m) / 100m;
        // +#,0.## = ใส่ + ถ้าบวก, -#,0.## ถ้าลบ, 0 ถ้าเป็นศูนย์
        return d.ToString("+#,0.##;-#,0.##;0", CultureInfo.InvariantCulture);
    }
    /// <summary>
    /// ฟอร์แมต % พร้อม + / - และคอมม่า เช่น +10,000,000.52% / -1,234.5% / 0%
    /// </summary>
    private static string FormatSignedPercent(float v)
    {
        return FormatSignedNumber(v) + "%";
    }
    #endregion
    
    #region === PUBLIC SETUP (FULL) ===
    /// <summary>
    /// nextPerText จะคำนวณจาก (nextDayPrice - currentPrice) × held ภายในฟังก์ชัน
    /// พารามิเตอร์ nextDeltaFromCost จะถูก "รับไว้เฉย ๆ" เพื่อคงความเข้ากันได้ย้อนหลัง
    /// </summary>
    public void Setup(
        string assetAbbreviation,
        float held,
        float nextDayPct,
        float todayGainLoss,
        float nextDayGainLoss,
        float currentPrice,
        float nextDayPrice,
        float nextDeltaFromCost   // (ไม่ได้ใช้แสดงผลแล้ว)
    )
    {
        // ชื่อสินทรัพย์
        if (assetNameText)
        {
            assetNameText.text = assetAbbreviation;
        }
        // จำนวนถือ
        if (heldText)
        {
            heldText.text = FormatNumber(held);
        }
        // % วันถัดไป + สี
        if (changePctText)
        {
            changePctText.text = $"{nextDayPct:+0.##;-0.##}%";

            if (nextDayPct > 0 && ColorUtility.TryParseHtmlString("#1FA800", out var g))
            {
                changePctText.color = g;
            }
            else if (nextDayPct < 0 && ColorUtility.TryParseHtmlString("#B21D1D", out var r))
            {
                changePctText.color = r;
            }
            else
            {
                changePctText.color = Color.black;
            }
        }
        // P/L วันนี้
        if (gainLossText)
        {
            var sign = todayGainLoss >= 0f ? "+" : "";
            gainLossText.text = $"{sign}{todayGainLoss:N2}";
            gainLossText.color = todayGainLoss >= 0f ? (ColorUtility.TryParseHtmlString("#1FA800", out var g2) ? g2 : Color.green) : (ColorUtility.TryParseHtmlString("#B21D1D", out var r2) ? r2 : Color.red);
        }
        // P/L วันถัดไป
        if (nextGainLossText)
        {
            var signN = nextDayGainLoss >= 0f ? "+" : "";
            nextGainLossText.text = $"{signN}{nextDayGainLoss:N2}";
            nextGainLossText.color = nextDayGainLoss >= 0f ? (ColorUtility.TryParseHtmlString("#1FA800", out var g3) ? g3 : Color.green) : (ColorUtility.TryParseHtmlString("#B21D1D", out var r3) ? r3 : Color.red);
        }
        // ราคา: วันนี้ / วันถัดไป
        if (currentPriceText)
        {
            currentPriceText.text = currentPrice.ToString("N2");
        }
        if (nextPriceText)
        {
            nextPriceText.text = nextDayPrice.ToString("N2");
        }
        // ผลกำไรของวันถัดไปเทียบวันนี้
        if (nextPerText)
        {
            float deltaVsToday = Floor2((nextDayPrice - currentPrice) * held);
            nextPerText.text = FormatSignedNumber(deltaVsToday);
            nextPerText.color = deltaVsToday >= 0f ? (ColorUtility.TryParseHtmlString("#1FA800", out var g4) ? g4 : Color.green) : (ColorUtility.TryParseHtmlString("#B21D1D", out var r4) ? r4 : Color.red);
        }
    }
    #endregion
}
