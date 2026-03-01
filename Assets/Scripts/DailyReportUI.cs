using System;
using System.Linq;
using System.Globalization;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using static SubAssetUI.PriceMath;

public class DailyReportUI : MonoBehaviour
{
    #region === UI REFERENCES ===
    [Header("Main Panel")]
    public GameObject panel;
    public TMP_Text rankText;
    public TMP_Text cashText;
    public TMP_Text assetText;
    public TMP_Text profitLossText;
    public TMP_Text nextDayChangeText;

    [Header("Rows")]
    public Transform rowContainer;
    public GameObject rowPrefab;
    
    [Header("Investor Summary")]
    [Tooltip("SO เกณฑ์จัดประเภทนักลงทุน + ข้อความปรับแต่ง")]
    public InvestorRulesSO investorRules;
    [Tooltip("หัวข้อประเภทนักลงทุน (เช่น Balanced Investor)")]
    public TMP_Text investorTitleText;
    [Tooltip("คำบรรยายประเภทนักลงทุน")]
    public TMP_Text investorDescText;
    [Tooltip("สรุปด้วยภาพรวม 'พีคตลอดเกม' (true) หรือ 'เฉพาะวันนี้' (false)")]
    public bool usePeakForInvestorSummary = true;
    
    [Header("Confirm")]
    public Button confirmButton;

    [Header("VFX")]
    [SerializeField] private ParticleSystem psProfit;     // Stonk_VFX
    [SerializeField] private ParticleSystem psBigProfit;  // Stonk_Bank
    [SerializeField] private ParticleSystem psLoss;       // Down
    
    #endregion
    
    #region === PUBLIC METHODS ===
    
    // แสดงรายงานประจำวัน
    public void ShowDailyReport(PlayerData player, InvestmentManager inv, int day, int rankFromHost)
    {
        if (player == null || inv == null)
        {
            return;
        }

        panel.SetActive(true);

        // สีหลักของ Text
        Color mainColor;
        bool hasMainColor = ColorUtility.TryParseHtmlString("#331200", out mainColor);

        // ====== Main header ======
        rankText.text = $"{rankFromHost}";

        // cashText: ไม่ปัดเศษ + ทศนิยม 0.00 + สี #331200
        if (cashText != null)
        {
            decimal moneyDec = Math.Truncate(player.money * 100m) / 100m;
            cashText.text = moneyDec.ToString("#,0.00", CultureInfo.InvariantCulture);
            if (hasMainColor)
            {
                cashText.color = mainColor;
            }
        }

        // ====== Rows ======
        decimal sumNextDayGainLoss = 0m;   // ผลรวม P/L "วันถัดไป"
        float portfolioValue = 0f;         // มูลค่าพอร์ตจาก “ราคาพรุ่งนี้ (pre-roll)”

        foreach (Transform child in rowContainer)
        {
            Destroy(child.gameObject);
        }
        foreach (var h in player.holdings)
        {
            if (h == null || h.subAsset == null || h.shares <= 0)
            {
                continue;
            }
            var runtime = inv.activeCompanies.FirstOrDefault(c => c.subAsset == h.subAsset);
            if (runtime == null)
            {
                continue;
            }
            // ราคาปัจจุบันจาก UI (ถ้าไม่มี ใช้ runtime + floor2)
            float currentPrice;
            if (!SubAssetUIRegistry.TryGetPrice(h.subAsset, out currentPrice))
            {
                currentPrice = Floor2(runtime.currentPrice);
            }

            float currentValue = Floor2(currentPrice * h.shares);

            // % พรุ่งนี้ (pre-roll) + ราคาพรุ่งนี้ (ปัดลงให้ตรง UI)
            float nextDayPct = runtime.PeekNextDayPct();
            float nextDayPrice = Floor2(currentPrice * (1f + nextDayPct / 100f));
            float projectedValue = Floor2(nextDayPrice * h.shares);

            // P/L วันนี้ (เทียบเมื่อวาน)
            float yesterdayPrice = Floor2(h.lastPrice);
            float yesterdayValue = Floor2(yesterdayPrice * h.shares);
            float todayGainLoss = Floor2(currentValue - yesterdayValue);

            // P/L พรุ่งนี้ (เทียบวันนี้) — ใช้ตัวนี้ไป sum ด้านล่าง
            float nextDayGainLoss = Floor2(projectedValue - currentValue);

            // “กำไร/ขาดทุนวันถัดไปเทียบต้นทุน”
            float entry = Mathf.Max(0.01f, h.entryPrice);
            float nextDeltaFromCost = Floor2(entry * (nextDayPct / 100f) * h.shares);

            // 6) วาดแถว
            var rowObj = Instantiate(rowPrefab, rowContainer);
            var rowUI = rowObj.GetComponent<DailyReportRowUI>();
            rowUI.Setup(
                h.subAsset.assetNameAbbreviation,
                h.shares,
                nextDayPct,
                todayGainLoss,
                nextDayGainLoss,
                currentPrice,
                nextDayPrice,
                nextDeltaFromCost
            );

            // รวมมูลค่าพอร์ตด้วยราคาพรุ่งนี้
            portfolioValue = Floor2(portfolioValue + projectedValue);

            // รวมกำไร/ขาดทุนวันถัดไป (ใช้ decimal ลด error)
            sumNextDayGainLoss += (decimal)nextDayGainLoss;
        }

        // ====== Profit/Loss รวม (หัวขวา) ======
        decimal truncatedSum = Math.Truncate(sumNextDayGainLoss * 100m) / 100m;
        float truncatedFloat = (float)truncatedSum;

        // ใช้ฟังก์ชันฟอร์แมตแบบ 0.00 ไม่ปัดเศษ
        string absText = FormatNumberFixed2(Mathf.Abs(truncatedFloat));

        string sign = "";
        if (truncatedSum > 0m)
        {
            sign = "+";
        }
        else if (truncatedSum < 0m)
        {
            sign = "-";
        }
        if (profitLossText != null)
        {
            profitLossText.text = sign + absText;

            // เลือกสีตามบวก/ลบ, ถ้า 0 ใช้ #331200
            if (truncatedSum > 0m)
            {
                if (ColorUtility.TryParseHtmlString("#1FA800", out var up))
                {
                    profitLossText.color = up;
                }
                else
                {
                    profitLossText.color = Color.green;
                }
            }
            else if (truncatedSum < 0m)
            {
                if (ColorUtility.TryParseHtmlString("#B21D1D", out var down))
                {
                    profitLossText.color = down;
                }
                else
                {
                    profitLossText.color = Color.red;
                }
            }
            else
            {
                if (ColorUtility.TryParseHtmlString("#331200", out var neu))
                {
                    profitLossText.color = neu;
                }
            }
        }
        // ====== Asset Text (มูลค่าสินทรัพย์) ======
        if (assetText != null)
        {
            decimal assetDec = Math.Truncate((decimal)portfolioValue * 100m) / 100m;
            assetText.text = assetDec.ToString("#,0.00", CultureInfo.InvariantCulture);
            if (hasMainColor)
            {
                assetText.color = mainColor;
            }
        }
        // ===== VFX (อิงผลรวมวันถัดไป) =====
        float sumNextAsFloat = (float)sumNextDayGainLoss;
        if (sumNextAsFloat > 0f)
        {
            const float BIG_PROFIT_THRESHOLD = 10000f;
            if (sumNextAsFloat >= BIG_PROFIT_THRESHOLD)
            {
                if (psBigProfit != null)
                {
                    psBigProfit.Play();
                }
            }
            else
            {
                if (psProfit != null)
                {
                    psProfit.Play();
                }
            }
        }
        else if (sumNextAsFloat < 0f)
        {
            if (psLoss != null)
            {
                psLoss.Play();
            }
        }
        // ===== Investor Summary =====
        if (investorRules != null && investorTitleText != null && investorDescText != null)
        {
            var type = player.GetInvestorType(investorRules, usePeakForInvestorSummary);
            var text = investorRules.GetText(type);
            investorTitleText.text = text.title;
            investorDescText.text = text.description;
        }
    }
    #endregion
    
    #region === MISC PUBLIC ===
    public void Hide()
    {
        panel.SetActive(false);
    }
    public void UpdateRankText(int rank)
    {
        if (rankText != null)
        {
            rankText.text = $"{rank}";
        }
    }
    #endregion
    
    #region === FORMAT HELPERS ===
    /// ฟอร์แมตตัวเลขเป็น string แบบมีคอมม่า และทศนิยม 2 ตำแหน่ง "0.00"
    /// โดยใช้การตัด (truncate) ไม่ใช่ปัดเศษ
    private static string FormatNumberFixed2(float v)
    {
        decimal d = (decimal)v;
        d = Math.Truncate(d * 100m) / 100m; // ไม่ปัดเศษ
        return d.ToString("#,0.00", CultureInfo.InvariantCulture);
    }
    #endregion
}
