// PlayerData.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

[Serializable]
public class PlayerHolding
{
    public SubAssetData subAsset;
    public float shares;
    public float lastPrice;

    // ✅ ราคาเข้าซื้อล็อตปัจจุบัน (คงไว้)
    public float entryPrice;

    // ✅ ใหม่: จำนวนหุ้น “ตอนเริ่มวัน” (ไว้คำนวณ P/L รายวัน)
    public float sharesAtStartOfDay;

    // ✅ ใหม่: ต้นทุนรวมสะสมของล็อตปัจจุบัน (หน่วยเป็นเงิน)
    public decimal totalCost;
}

[Serializable]
public struct RiskBuckets
{
    public float low, med, high;
    public void Clamp01() { low = Mathf.Clamp(low, 0f, 100f); med = Mathf.Clamp(med, 0f, 100f); high = Mathf.Clamp(high, 0f, 100f); }
    public void MaxWith(RiskBuckets other)
    {
        low = Mathf.Max(low, other.low);
        med = Mathf.Max(med, other.med);
        high = Mathf.Max(high, other.high);
    }
}

[Serializable]
public class PlayerData
{
    public string playerName;
    public int characterSpriteIndex;

    // ✅ Stamina
    public int maxStamina = 3;
    public int currentStamina = 3;

    public decimal money = 10_000_000m;
    
    public int rank;

    // รายการหุ้นที่ถือ
    public List<PlayerHolding> holdings = new List<PlayerHolding>();

    public decimal startingCapital = 10_000_000m; // ฐานคำนวณพอร์ต/ความเสี่ยง

    // ✅ Buckets จาก “เงินที่ซื้อจริงสะสม” (ฝั่งพฤติกรรม)
    [Header("Spend Buckets (%)")]
    public RiskBuckets spendBuckets;

    // ✅ Snapshot สิ้นวัน & Peak ทั้งเกม (ฝั่งพอร์ตจริง)
    [Header("Exposure Buckets (%)")]
    public RiskBuckets snapshotBuckets;   // วันนี้
    public RiskBuckets peakBuckets;       // พีคตลอดเกม

    public bool riskReduceOnSell = false;

    public event Action<decimal> OnMoneyChanged;
    public event Action<SubAssetData, float> OnHoldingChanged;
    
    public string FinalTopAssetAbbreviation { get; set; } = "-";

    
    public PlayerData(string name, int spriteIndex)
    {
        playerName = name;
        characterSpriteIndex = spriteIndex;
        money = 10_000_000m;
        startingCapital = money;
        spendBuckets = new RiskBuckets();
        snapshotBuckets = new RiskBuckets();
        peakBuckets = new RiskBuckets();
    }

    // ---------- Floor helpers ----------
    private static decimal Floor2(decimal v) => Math.Floor(v * 100m) / 100m;
    private static decimal Floor2(float v) => Floor2((decimal)v);

    // ---------- ซื้อ/ขาย (ทศนิยม) + บันทึก spend ----------
    public bool BuySharesByCountFloat(SubAssetData subAsset, float sharesToBuy, float pricePerShare)
    {
        if (subAsset == null || pricePerShare <= 0f) return false;
        sharesToBuy = Mathf.Floor(sharesToBuy * 100f) / 100f;
        if (sharesToBuy < 0.01f) return false;

        decimal qty = (decimal)sharesToBuy;
        decimal price = Floor2(pricePerShare);
        decimal cost = qty * price;
        if (cost > money) return false;

        var h = holdings.Find(x => x.subAsset == subAsset);
        if (h == null)
        {
            h = new PlayerHolding { subAsset = subAsset, shares = 0f, entryPrice = 0f, totalCost = 0m };
            holdings.Add(h);
        }

        bool startingNewLot = h.shares <= 0f;
        h.shares += sharesToBuy;

        // ✅ เริ่มล็อตใหม่: เซ็ต entryPrice และรีเซ็ตต้นทุนรวม
        if (startingNewLot)
        {
            h.entryPrice = Mathf.Max(0.01f, pricePerShare);
            h.totalCost = 0m;
        }

        // ✅ ตัดเงิน + สะสมต้นทุนรวมจริง
        //money -= cost;
        h.totalCost += cost;

        // พฤติกรรมการซื้อ (Spend)
        AddSpendRisk(subAsset.risk, cost);

        OnMoneyChanged?.Invoke(money);
        OnHoldingChanged?.Invoke(subAsset, h.shares);
        return true;
    }

    public bool SellSharesByCountFloat(SubAssetData subAsset, float sharesToSell, float pricePerShare)
    {
        if (subAsset == null || pricePerShare <= 0f) return false;
        sharesToSell = Mathf.Floor(sharesToSell * 100f) / 100f;
        if (sharesToSell < 0.01f) return false;

        var h = holdings.Find(x => x.subAsset == subAsset);
        if (h == null || h.shares < sharesToSell) return false;

        decimal qty = (decimal)sharesToSell;
        decimal price = Floor2(pricePerShare);
        decimal income = qty * price;

        // ✅ หักต้นทุนรวมตามสัดส่วน (Average cost method) ก่อนลดจำนวนหุ้น
        float prevShares = h.shares;
        if (prevShares > 0f && h.totalCost > 0m)
        {
            decimal ratio = (decimal)(sharesToSell / prevShares); // สัดส่วนที่ขายออก
            decimal reduceCost = h.totalCost * ratio;
            h.totalCost -= reduceCost;
            if (h.totalCost < 0m) h.totalCost = 0m; // กันติดลบจากการปัดเศษ
        }

        h.shares -= sharesToSell;

        money += income;

        if (riskReduceOnSell) SubSpendRisk(subAsset.risk, income);

        // ✅ ปิดล็อต: ล้างต้นทุนรวม
        if (h.shares <= 0f)
        {
            h.totalCost = 0m;
            holdings.Remove(h);
        }

        OnMoneyChanged?.Invoke(money);
        OnHoldingChanged?.Invoke(subAsset, h?.shares ?? 0f);
        return true;
    }

    private void AddSpendRisk(RiskLevel risk, decimal amount)
    {
        if (startingCapital <= 0m || amount <= 0m) return;
        float addPct = (float)((amount / startingCapital) * 100m);
        switch (risk)
        {
            case RiskLevel.Low: spendBuckets.low = Mathf.Clamp(spendBuckets.low + addPct, 0f, 100f); break;
            case RiskLevel.Medium: spendBuckets.med = Mathf.Clamp(spendBuckets.med + addPct, 0f, 100f); break;
            case RiskLevel.High: spendBuckets.high = Mathf.Clamp(spendBuckets.high + addPct, 0f, 100f); break;
        }
    }
    private void SubSpendRisk(RiskLevel risk, decimal amount)
    {
        if (startingCapital <= 0m || amount <= 0m) return;
        float subPct = (float)((amount / startingCapital) * 100m);
        switch (risk)
        {
            case RiskLevel.Low: spendBuckets.low = Mathf.Clamp(spendBuckets.low - subPct, 0f, 100f); break;
            case RiskLevel.Medium: spendBuckets.med = Mathf.Clamp(spendBuckets.med - subPct, 0f, 100f); break;
            case RiskLevel.High: spendBuckets.high = Mathf.Clamp(spendBuckets.high - subPct, 0f, 100f); break;
        }
    }

    // ---------- คำนวณสัดส่วนพอร์ตจริง ณ สิ้นวัน ----------
    public void AccumulateDailyRiskExposure(InvestmentManager inv, bool updatePeak = true)
    {
        if (inv == null || inv.activeCompanies == null)
            return;

        decimal lowVal = 0m, medVal = 0m, highVal = 0m;

        foreach (var h in holdings)
        {
            if (h?.subAsset == null || h.shares <= 0f) continue;
            var rt = inv.activeCompanies.Find(c => c.subAsset == h.subAsset);
            if (rt == null) continue;

            // มูลค่าตลาดปัจจุบัน = currentPrice × shares
            decimal price = (decimal)Floor2(rt.currentPrice);
            decimal val = (decimal)h.shares * price;

            if (val <= 0m) continue;

            switch (h.subAsset.risk)
            {
                case RiskLevel.Low: lowVal += val; break;
                case RiskLevel.Medium: medVal += val; break;
                case RiskLevel.High: highVal += val; break;
            }
        }

        decimal total = lowVal + medVal + highVal;

        if (total <= 0m)
        {
            snapshotBuckets.low = 0f;
            snapshotBuckets.med = 0f;
            snapshotBuckets.high = 0f;
        }
        else
        {
            snapshotBuckets.low = (float)(lowVal / total * 100m);
            snapshotBuckets.med = (float)(medVal / total * 100m);
            snapshotBuckets.high = (float)(highVal / total * 100m);
        }

        snapshotBuckets.Clamp01();

        if (updatePeak)
        {
            peakBuckets.MaxWith(snapshotBuckets);
            peakBuckets.Clamp01();
        }
    }


    public (float low, float med, float high) GetRiskBreakdown(bool usePeak) =>
        usePeak ? (peakBuckets.low, peakBuckets.med, peakBuckets.high)
                : (snapshotBuckets.low, snapshotBuckets.med, snapshotBuckets.high);

    public InvestorType GetInvestorType(InvestorRulesSO rules, bool usePeak)
    {
        var (l, m, h) = GetRiskBreakdown(usePeak);
        return rules != null ? rules.Evaluate(l, m, h) : InvestorType.Minimal;
    }

    // ---------- Utils ----------
        /*public float GetTotalAssets(InvestmentManager manager)
        {
            decimal total = money;
            foreach (var h in holdings)
            {
                if (h.subAsset != null && h.shares > 0f)
                {
                    var rt = manager.activeCompanies.Find(c => c.subAsset == h.subAsset);
                    if (rt != null)
                    {
                        decimal price = Floor2(rt.currentPrice);
                        total += (decimal)h.shares * price;
                    }
                }
            }
            return (float)total;
        }*/
        public decimal GetTotalAssets(InvestmentManager manager) // ✅ เปลี่ยนเป็น decimal
        {
            decimal total = money;
            foreach (var h in holdings)
            {
                if (h.subAsset != null && h.shares > 0f)
                {
                    var rt = manager.activeCompanies.Find(c => c.subAsset == h.subAsset);
                    if (rt != null)
                    {
                        decimal price = Floor2(rt.currentPrice);
                
                        // ⚠️ คำเตือน: h.shares น่าจะเป็น float/double 
                        // ควรแปลง h.shares เป็น decimal ก่อนการคำนวณ
                        total += (decimal)h.shares * price; 
                    }
                }
            }
    
            // ✅ ลบการแปลง (float) ออก
            return total; 
        }

    public string MoneyToString() => Floor2(money).ToString("N2");

    // =========================
    // Player Event (บวก/ลบเงินตรง ๆ จาก SO)
    // =========================
    public void ApplyPlayerEvent(PlayerEventSO evt)
    {
        if (evt == null) return;
        money += (decimal)evt.price;
        OnMoneyChanged?.Invoke(money);
        Debug.Log($"[PlayerEvent] {playerName} : {evt.description} ({evt.price:+0;-0}) → เงินคงเหลือ {MoneyToString()}฿");
    }

    // ของเดิม (ถ้าไม่ได้ใช้สามารถลบได้)
    public Dictionary<SubAssetData, int> ownedAssets = new Dictionary<SubAssetData, int>();

    // === Compatibility helpers (used by BuyManager / SubAssetUI) ===
    public float GetOwnedAmount(SubAssetData asset)
    {
        if (asset == null) return 0f;
        var h = holdings.Find(x => x.subAsset == asset);
        return h != null ? h.shares : 0f;
    }

    public float GetOwnedAmount(InvestmentCompany company)
    {
        if (company == null || company.subAssets == null || company.subAssets.Count == 0) return 0f;
        return GetOwnedAmount(company.subAssets[0]);
    }

    public void AddHolding(SubAssetData subAsset, float amount)
    {
        if (subAsset == null || amount <= 0f) return;
        var h = holdings.Find(x => x.subAsset == subAsset);
        if (h == null)
        {
            h = new PlayerHolding { subAsset = subAsset, shares = 0f };
            holdings.Add(h);
        }
        h.shares += amount;
        OnHoldingChanged?.Invoke(subAsset, h.shares);
    }

    // -----------------------------------
    // ฟังก์ชันช่วยเหลือสำหรับ InvestmentManager
    // -----------------------------------
    public int GetHoldingAmount(SubAssetData subAsset)
    {
        var h = holdings.FirstOrDefault(x => x.subAsset == subAsset);
        return h != null ? (int)h.shares : 0;
    }

    public void RemoveHolding(SubAssetData subAsset, int amount)
    {
        var h = holdings.FirstOrDefault(x => x.subAsset == subAsset);
        if (h != null)
        {
            h.shares -= amount;
            if (h.shares <= 0)
            {
                holdings.Remove(h);
            }
        }
    }

    public void AddHolding(SubAssetData subAsset, int amount)
    {
        var h = holdings.FirstOrDefault(x => x.subAsset == subAsset);
        if (h != null)
        {
            h.shares += amount;
        }
        else
        {
            holdings.Add(new PlayerHolding { subAsset = subAsset, shares = amount });
        }
    }

    // ลด Stamina
    public bool UseStamina()
    {
        if (currentStamina > 0)
        {
            currentStamina--;
            return true;
        }
        return false;
    }

    // รีเซ็ตตอนเริ่มวันใหม่
    public void ResetStamina()
    {
        currentStamina = maxStamina;
    }

    // 🚩 NEW: ฟังก์ชันนี้ใช้สำหรับ Client/Host ในการซิงค์ Holding จาก Network
    // (โดยข้าม Logic การซื้อขาย/การเงินปกติ)
    // ซิงค์ผลเทรดจาก Host (ใช้ทั้ง Host/Client)
    public void SyncTradeResult(
        SubAssetData subAsset,
        float sharesAfterTrade,
        decimal moneyChange,
        decimal tradeGross,   // มูลค่าดีลแบบบวกเสมอ (ราคา × จำนวน)
        bool isBuy            // true = ซื้อ, false = ขาย
    )
    {
        if (subAsset == null) return;

        // 1) อัปเดตเงินในกระเป๋า
        money += moneyChange;
        OnMoneyChanged?.Invoke(money);

        // 🔥 NEW: อัปเดต Spend Buckets จากดีลนี้ (ใช้ tradeGross ที่เป็นจำนวนเงินบวก)
        if (tradeGross > 0m)
        {
            if (isBuy)
            {
                // ซื้อ = เพิ่มสัดส่วน bucket ตามความเสี่ยงของสินทรัพย์
                AddSpendRisk(subAsset.risk, tradeGross);
            }
            else if (riskReduceOnSell)
            {
                // ขาย แล้วอยากให้ “ลด” exposure จากมุมมองพฤติกรรม
                SubSpendRisk(subAsset.risk, tradeGross);
            }
        }

        // 2) หา holding เดิม
        var h = holdings.Find(x => x.subAsset == subAsset);

        // ถ้าไม่มี holding เลย แต่ยังมีหุ้นหลังเทรด → สร้างใหม่
        if (h == null && sharesAfterTrade > 0f)
        {
            h = new PlayerHolding
            {
                subAsset = subAsset,
                shares = 0f,
                entryPrice = 0f,
                totalCost = 0m
            };
            holdings.Add(h);
        }

        // ถ้าหลังเทรดไม่เหลือหุ้นแล้ว → ลบ holding ทิ้ง
        if (sharesAfterTrade <= 0f)
        {
            if (h != null)
            {
                h.shares = 0f;
                h.totalCost = 0m;
                h.entryPrice = 0f;
                holdings.Remove(h);
            }
        }
        else
        {
            // ยังมีหุ้นเหลือ → ต้องปรับ totalCost ตามประเภทดีล
            if (h != null)
            {
                if (isBuy)
                {
                    // ซื้อเพิ่ม → ต้นทุนรวมเพิ่มตามมูลค่าดีล
                    h.totalCost += tradeGross;
                }
                else
                {
                    // ขาย → หาต้นทุนเฉลี่ยก่อน แล้วหักต้นทุนส่วนที่ขายออก
                    float oldShares = h.shares;
                    float newShares = sharesAfterTrade;
                    float soldShares = Mathf.Max(0f, oldShares - newShares);

                    if (oldShares > 0f && soldShares > 0f && h.totalCost > 0m)
                    {
                        decimal avgCostPerShare = h.totalCost / (decimal)oldShares;
                        decimal costToRemove = avgCostPerShare * (decimal)soldShares;
                        h.totalCost -= costToRemove;

                        if (h.totalCost < 0m) h.totalCost = 0m; // กันติดลบเพราะทศนิยม
                    }
                }

                // อัปเดตจำนวนหุ้นล่าสุด
                h.shares = sharesAfterTrade;

                // อัปเดตราคาเข้าซื้อเฉลี่ยใหม่
                if (h.shares > 0f && h.totalCost > 0m)
                {
                    h.entryPrice = (float)(h.totalCost / (decimal)h.shares);
                }
                else
                {
                    h.entryPrice = 0f;
                }
            }
        }

        // 3) แจ้ง UI อื่น ๆ ให้รีเฟรช (Portfolio / HUD ฯลฯ)
        OnHoldingChanged?.Invoke(subAsset, sharesAfterTrade);

        Debug.Log($"[Sync] {playerName} => Money={money:N2}, {subAsset.assetName}={sharesAfterTrade:0.##} sh, Cost={(h != null ? h.totalCost : 0m):N2}");
    }



    public decimal TotalInvestmentAssetValue
    {
        get
        {
            decimal total = 0m;
        
            // ⚠️ ต้องมั่นใจว่า InvestmentManager เป็น Singleton และมี Instance
            var invManager = InvestmentManager.Instance; 
            if (invManager == null || invManager.activeCompanies == null) return 0m;
        
            foreach (var h in holdings)
            {
                if (h.subAsset != null && h.shares > 0f)
                {
                    var rt = invManager.activeCompanies.Find(c => c.subAsset == h.subAsset);
                    if (rt != null)
                    {
                        // ⚠️ ต้องมั่นใจว่า Floor2 เป็นเมธอด static ในคลาสนี้
                        decimal price = Floor2(rt.currentPrice); 
                        total += (decimal)h.shares * price;
                    }
                }
            }
            return total;
        }
    }

// 💡 Property ใหม่: ความมั่งคั่งรวม (Net Worth) ที่ใช้จัดอันดับ
    public decimal TotalWealth { get; set; }
}
