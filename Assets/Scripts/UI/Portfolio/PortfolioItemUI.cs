using UnityEngine;
using TMPro;
using System;
using System.Globalization;

public class PortfolioItemUI : MonoBehaviour
{
    [Header("Text Fields")]
    public TMP_Text assetNameText;
    public TMP_Text priceText;          // แสดง "ต้นทุนรวมทั้งก้อน"
    public TMP_Text changePercentText;  // % ตามสูตร ((currentPrice − (ต้นทุน/จำนวนถือ)) / (ต้นทุน/จำนวนถือ)) × 100
    public TMP_Text totalValueText;     // มูลค่าปัจจุบัน = currentPrice × ownedShares
    public TMP_Text ownedAmountText;
    public TMP_Text typesOfAssetsText;

    /// <summary>
    /// subAsset      : ข้อมูลสินทรัพย์
    /// ownedShares   : จำนวนหุ้นที่ถือรวม
    /// totalCost     : ต้นทุนรวมทั้งหมด
    /// currentPrice  : ราคาปัจจุบัน "ต่อ 1 หุ้น"
    /// </summary>
    public void SetData(SubAssetData subAsset, float ownedShares, float totalCost, float currentPrice)
    {
        // 🎨 กำหนดสีหลักของ Text ทุกช่อง (ยกเว้นช่อง %)
        Color mainColor;
        if (!ColorUtility.TryParseHtmlString("#B6702E", out mainColor))
            mainColor = Color.black; // เผื่อ parse ไม่ได้

        // ชื่อสินทรัพย์
        if (assetNameText)
        {
            assetNameText.text = subAsset != null ? subAsset.assetName : "-";
            assetNameText.color = mainColor;
        }

        // ✅ Price Text = ต้นทุนรวมทั้งก้อน
        if (priceText)
        {
            priceText.text = FormatNumber(totalCost);
            priceText.color = mainColor;
        }

        // จำนวนที่ถือ
        if (ownedAmountText)
        {
            ownedAmountText.text = FormatNumber(ownedShares);
            ownedAmountText.color = mainColor;
        }

        // ประเภทสินทรัพย์
        if (typesOfAssetsText)
        {
            typesOfAssetsText.text = subAsset != null ? subAsset.typesOfAssets : "-";
            typesOfAssetsText.color = mainColor;
        }

        // ✅ ต้นทุนเฉลี่ยต่อหุ้น = totalCost / ownedShares
        float avgCostPerShare = 0f;
        if (ownedShares > 0.0001f)
            avgCostPerShare = totalCost / ownedShares;

        // ✅ มูลค่าปัจจุบันทั้งก้อน = currentPrice × ownedShares
        float currentTotalValue = currentPrice * ownedShares;

        // ✅ ((currentPrice − avgCostPerShare) / avgCostPerShare) × 100
        float changePercent = 0f;
        if (avgCostPerShare > 0.0001f)
        {
            changePercent = (currentPrice - avgCostPerShare) / avgCostPerShare * 100f;
        }

        if (changePercentText)
        {
            changePercentText.text = FormatSignedPercent(changePercent);

            if (changePercent > 0f)
            {
                if (ColorUtility.TryParseHtmlString("#1FA800", out var up))
                    changePercentText.color = up;
            }
            else if (changePercent < 0f)
            {
                if (ColorUtility.TryParseHtmlString("#B21D1D", out var dn))
                    changePercentText.color = dn;
            }
            else
            {
                // 0% → ใช้สี B6702E ตามที่ตั้งใจ
                if (ColorUtility.TryParseHtmlString("#B6702E", out var neutral))
                    changePercentText.color = neutral;
                else
                    changePercentText.color = mainColor;
            }
        }

        // ✅ Total Value Text = มูลค่าปัจจุบันทั้งก้อน
        if (totalValueText)
        {
            totalValueText.text = FormatNumber(currentTotalValue);
            totalValueText.color = mainColor;
        }
    }


    // เผื่อที่อื่นยังเรียกด้วย “ต้นทุนเฉลี่ยต่อหุ้น” อยู่ — helper แปลงให้เป็น totalCost แล้วเรียกตัวหลัก
    public void SetDataFromAvgCost(SubAssetData subAsset, float ownedShares, float avgCostPerShare, float currentPrice)
    {
        float totalCost = ownedShares * avgCostPerShare;
        SetData(subAsset, ownedShares, totalCost, currentPrice);
    }

    // ----------------- Helpers ฟอร์แมตตัวเลข -----------------

    /// <summary>
    /// ฟอร์แมตตัวเลขทั่วไปให้มีคอมม่า และทศนิยม "2 ตำแหน่งตรงๆ" (ตัดทิ้ง ไม่ปัด)
    /// เช่น 10000000    -> 10,000,000.00
    ///      10000000.5  -> 10,000,000.50
    ///      10000000.529-> 10,000,000.52
    /// </summary>
    private static string FormatNumber(float v)
    {
        decimal d = (decimal)v;
        d = Math.Truncate(d * 100m) / 100m;                // ตัดเหลือ 2 ตำแหน่ง
        return d.ToString("#,0.00", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// ฟอร์แมต % พร้อมเครื่องหมาย + / - และคอมม่า เช่น:
    /// +12.50% / -1,234.56% / 0.00%
    /// </summary>
    private static string FormatSignedPercent(float v)
    {
        decimal d = (decimal)v;
        d = Math.Truncate(d * 100m) / 100m;

        // +#,0.00 = แสดง + ถ้าเป็นบวก
        // -#,0.00 = แสดง - ถ้าเป็นลบ
        // 0.00    = กรณีเป็น 0 พอดี
        string core = d.ToString("+#,0.00;-#,0.00;0.00", CultureInfo.InvariantCulture);
        return core + "%";
    }
}
