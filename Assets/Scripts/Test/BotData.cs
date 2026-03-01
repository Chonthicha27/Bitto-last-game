using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class BotData : PlayerData
{
    public bool isBot = true;

    // CHANGED: อนุญาตจำนวนหุ้นทศนิยมต่อออเดอร์
    public float maxSharesPerTrade = 3f;

    public BotData(string name, int spriteIndex) : base(name, spriteIndex)
    {
        // ให้ตรงชนิดกับ PlayerData (decimal)
        money = 10_000_000m;
    }

    // ✅ ซื้อผ่าน BuyManager (ทศนิยม)
    public void PerformRandomBuy(BuyManager buyManager, InvestmentManager inv)
    {
        if (buyManager == null || inv == null || inv.activeCompanies == null || inv.activeCompanies.Count == 0) return;

        int botId = GetMyPlayerIndex();
        if (botId < 0) { Debug.LogWarning("[BOT] ไม่ได้อยู่ใน Players list"); return; }

        var rt = inv.activeCompanies[Random.Range(0, inv.activeCompanies.Count)];
        if (rt == null || rt.subAsset == null || rt.currentPrice <= 0f) return;

        // ราคา runtime ปัดลง 2 ตำแหน่ง
        float flooredPrice = Floor2(Mathf.Max(0.01f, rt.currentPrice));
        decimal priceDec = (decimal)flooredPrice;
        if (priceDec <= 0m) return;

        // คำนวณจำนวนหุ้นสูงสุดจากเงิน (เป็น float เพื่อรองรับทศนิยม)
        float maxByMoney = (float)(money / priceDec);
        float cap = Mathf.Min(maxSharesPerTrade, maxByMoney);
        if (cap <= 0f) return;

        // สุ่มจำนวนหุ้นทศนิยม แล้วปัดลง 2 ตำแหน่ง (เช่น 1.37)
        float sharesToBuy = Floor2(Random.Range(0.1f, cap + 0.0001f));

        // เรียกผ่าน SubAsset โดยตรง (BuyManager รองรับ float แล้ว)
        buyManager.BuySubAsset(botId, rt.subAsset, sharesToBuy, null);
    }

    // ✅ ขายผ่าน BuyManager (ทศนิยม)
    public void PerformRandomSell(BuyManager buyManager, InvestmentManager inv)
    {
        if (buyManager == null || inv == null || inv.activeCompanies == null || inv.activeCompanies.Count == 0) return;
        if (holdings == null || holdings.Count == 0) return;

        int botId = GetMyPlayerIndex();
        if (botId < 0) { Debug.LogWarning("[BOT] ไม่ได้อยู่ใน Players list"); return; }

        // สุ่ม holding ที่มีหุ้น > 0
        PlayerHolding h = null;
        for (int tries = 0; tries < 6; tries++)
        {
            var cand = holdings[Random.Range(0, holdings.Count)];
            if (cand != null && cand.subAsset != null && cand.shares > 0f) { h = cand; break; }
        }
        if (h == null) return;

        // หา runtime ของตัวที่ถือ เพื่อความตรงกับราคา runtime
        InvestmentCompanyRuntime rt = null;
        foreach (var c in inv.activeCompanies) { if (c != null && c.subAsset == h.subAsset) { rt = c; break; } }
        if (rt == null || rt.currentPrice <= 0f) return;

        // ขายแบบทศนิยม: สุ่มได้ไม่เกินที่ถือและเพดานต่อครั้ง
        float cap = Mathf.Min(h.shares, maxSharesPerTrade);
        if (cap <= 0f) return;

        float sharesToSell = Floor2(Random.Range(0.1f, cap + 0.0001f));

        buyManager.SellSubAsset(botId, rt.subAsset, sharesToSell, null);
    }

    // helper: หา index ของบอทใน Players list
    private int GetMyPlayerIndex()
    {
        var room = PlayerDataManager.Instance?.currentRoom;
        if (room == null || room.Players == null) return -1;
        return room.Players.IndexOf(this);
    }

    [ContextMenu("BOT: Test Buy 0.5 share via Manager")]
    private void _TestBuyHalf()
    {
        var bm = Object.FindObjectOfType<BuyManager>();
        var im = Object.FindObjectOfType<InvestmentManager>();
        if (bm == null || im == null || im.activeCompanies.Count == 0) return;
        int botId = GetMyPlayerIndex(); if (botId < 0) return;
        var rt = im.activeCompanies[0];

        bm.BuySubAsset(botId, rt.subAsset, 0.5f, null);
    }

    // helper ปัดลง 2 ตำแหน่ง (ทั้งราคาและจำนวนหุ้น)
    private static float Floor2(float v) => Mathf.Floor(v * 100f) / 100f;
}
