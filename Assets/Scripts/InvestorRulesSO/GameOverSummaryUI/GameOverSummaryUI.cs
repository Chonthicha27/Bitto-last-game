using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;
using System.Collections.Generic;

public class GameOverSummaryUI : MonoBehaviour
{
    [Header("Root")]
    public GameObject panel;

    [Header("Investor Rules / Mode")]
    [Tooltip("SO ที่ใช้เกณฑ์จัดประเภทนักลงทุน + ข้อความคำอธิบาย")]
    public InvestorRulesSO investorRules;

    [Tooltip("ใช้ข้อมูล Peak (ตลอดเกม) แทน Snapshot ล่าสุด")]
    public bool usePeakForFinal = true;

    [Tooltip("ใช้ข้อมูลการใช้จ่าย (Spend Buckets) แทน Peak/Snapshot")]
    public bool useSpendForFinal = false;

    [Header("Texts")]
    public TMP_Text titleText;
    public TMP_Text descText;

    [Header("Description Scroll")]
    [Tooltip("ScrollRect ที่หุ้ม descText อยู่ (ไว้รีเซ็ตให้เลื่อนไปบนสุดตอนเปิด)")]
    public ScrollRect descScrollRect;

    [Header("Breakdown %")]
    public TMP_Text lowPctText;
    public TMP_Text medPctText;
    public TMP_Text highPctText;

    [Header("Final Rank")]
    public TMP_Text rankText; // อันดับสุดท้าย (ของผู้เล่นที่โฟกัส)

    [Header("Investor Icon")]
    [Tooltip("รูป Icon แสดงประเภทนักลงทุน (ดึงจาก InvestorRulesSO.InvestorText.icon)")]
    public Image investorIcon;

    [Header("Leaderboard")]
    [Tooltip("คอนเทนเนอร์ที่มี VerticalLayoutGroup สำหรับวางรายการ 1..6")]
    public Transform leaderboardRoot;

    [Tooltip("LeaderboardItemUI ที่ติดอยู่กับ UI ของอันดับ 1 ในซีน")]
    public LeaderboardItemUI rank1ItemUI;

    [Tooltip("พรีแฟบ UI สำหรับอันดับ 2-6")]
    public GameObject leaderboardItemPrefabRankOther;

    [Range(1, 6)]
    public int leaderboardMaxEntries = 6;

    [Tooltip("BG Sprite ที่มีหมายเลขอันดับ (index 0=Rank 2, index 1=Rank 3, ...)")]
    public List<Sprite> rankBackgrounds;

    [Header("Buttons")]
    public Button closeButton;

    [Header("Next Summary Panels")]
    public GameObject nextRankPanel;

    [Tooltip("Reference ไปยังสคริปต์ FinalRankPanelUI สำหรับแสดง 3 อันดับแรก")]
    public FinalRankPanelUI finalRankPanelUI;

    [Header("End Credit")]
    public EndCreditController endCreditController;

    public void Show(PlayerData player, InvestmentManager inv, int finalRank = -1)
    {
        if (player == null || investorRules == null)
        {
            Debug.LogWarning("[GameOverSummaryUI] player หรือ investorRules ยังไม่พร้อม");
            return;
        }

        if (inv != null) player.AccumulateDailyRiskExposure(inv, updatePeak: true);

        float lowRaw, medRaw, highRaw;
        if (useSpendForFinal)
        {
            lowRaw = Mathf.Max(0f, player.spendBuckets.low);
            medRaw = Mathf.Max(0f, player.spendBuckets.med);
            highRaw = Mathf.Max(0f, player.spendBuckets.high);
        }
        else
        {
            (lowRaw, medRaw, highRaw) = player.GetRiskBreakdown(usePeakForFinal);
            lowRaw = Mathf.Max(0f, lowRaw);
            medRaw = Mathf.Max(0f, medRaw);
            highRaw = Mathf.Max(0f, highRaw);
        }

        var type = investorRules.Evaluate(lowRaw, medRaw, highRaw);
        var text = investorRules.GetText(type);

        float lowDisp = lowRaw, medDisp = medRaw, highDisp = highRaw;
        NormalizeTo100(ref lowDisp, ref medDisp, ref highDisp);

        if (titleText) titleText.text = text.title;
        if (descText) descText.text = text.description;

        // ✅ รีเซ็ต Scroll ให้กลับไปด้านบนสุดทุกครั้งที่เปิด
        if (descScrollRect != null)
            descScrollRect.verticalNormalizedPosition = 1f;

        if (lowPctText) lowPctText.text = $"Low: {lowDisp:N2}%";
        if (medPctText) medPctText.text = $"Med: {medDisp:N2}%";
        if (highPctText) highPctText.text = $"High: {highDisp:N2}%";

        if (investorIcon != null)
        {
            if (text.icon != null)
            {
                investorIcon.sprite = text.icon;
                investorIcon.gameObject.SetActive(true);
            }
            else
            {
                investorIcon.gameObject.SetActive(false);
            }
        }

        if (rankText != null)
        {
            rankText.text = (finalRank > 0)
                ? $"🏆 Rank {finalRank}{GetRankSuffix(finalRank)}"
                : "Rank: -";
        }

        if (panel) panel.SetActive(true);

        if (closeButton)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(() =>
            {
                if (panel) panel.SetActive(false);

                if (finalRankPanelUI != null)
                {
                    var allPlayers = PlayerDataManager.Instance.players;
                    finalRankPanelUI.ShowTop3(allPlayers);
                }
                else if (nextRankPanel)
                {
                    nextRankPanel.SetActive(true);
                }
                else
                {
                    Debug.LogWarning("[GameOverSummaryUI] ไม่มี Panel ถัดไปที่จะแสดงผล");
                }

                if (endCreditController != null)
                {
                    var ecGO = endCreditController.gameObject;
                    if (!ecGO.activeSelf)
                        ecGO.SetActive(true);

                    endCreditController.PlayCredits();
                }
            });
        }
    }
    // ------------------------------------------------------------
    //                  แสดง Leaderboard 1..6 คน
    // ------------------------------------------------------------
    public void ShowWithLeaderboard(List<PlayerData> allPlayers, InvestmentManager inv)
    {
        if (allPlayers == null || allPlayers.Count == 0 || inv == null)
        {
            Debug.LogWarning("[GameOverSummaryUI] ข้อมูล leaderboard ไม่ครบ");
            return;
        }

        if (leaderboardRoot == null || leaderboardItemPrefabRankOther == null || rank1ItemUI == null)
        {
            Debug.LogWarning("[GameOverSummaryUI] Reference ไม่ครบ: leaderboardRoot, Prefab (Rank Other) หรือ Rank 1 Item ยังไม่ถูกตั้งค่า");
            return;
        }

        // สร้างตารางคะแนน
        /*var rows = new List<Row>(allPlayers.Count);
        foreach (var p in allPlayers)
        {
            var nw = GetNetWorth(p, inv);              // เงินสด + มูลค่าถือครองปัจจุบัน
            var profit = nw - p.startingCapital;       // กำไรสุทธิ
            var (topSub, topPnl) = GetTopEarningSub(p, inv);

            rows.Add(new Row
            {
                player = p,
                displayName = p.playerName,
                netWorth = nw,
                profit = profit,
                topSubAsset = topSub,
                topSubPnl = topPnl
            });
        }*/
        var rows = new List<Row>(allPlayers.Count);
        foreach (var p in allPlayers)
        {
            // Host: คำนวณ Net Worth (ถ้าเป็น Host หรือ Client ที่ไม่มีข้อมูลซิงค์)
            // Client: ใช้ค่า TotalWealth ที่ซิงค์มาจาก Host แล้ว
            var nw = p.TotalWealth; // เริ่มต้นใช้ TotalWealth ที่ถูกซิงค์มา (ถ้ามี) หรือ 0

            // ถ้า TotalWealth เป็น 0 หรือไม่ได้ถูกซิงค์มา (และเป็น Host) ให้คำนวณจาก holding
            if (nw == 0m && PlayerDataManager.Instance.localPlayer.playerName == p.playerName) 
            {
                // ถ้าเป็น Local Player และ TotalWealth ยังเป็น 0 ให้คำนวณเอง (กรณี Host)
                nw = GetNetWorth(p, inv); 
            }
            
            // 📢 NEW: เพิ่ม Debug Log เพื่อแสดงข้อมูล Net Worth และ PlayerName
            Debug.Log($"[Leaderboard Data] PlayerName: {p.playerName}, NetWorth: {nw:N2} (TotalWealth)");


            var profit = nw - p.startingCapital;       // กำไรสุทธิ
            var (topSub, topPnl) = GetTopEarningSub(p, inv);

            rows.Add(new Row
            {
                player = p,
                displayName = p.playerName,
                netWorth = nw,
                profit = profit,
                topSubAsset = topSub,
                topSubPnl = topPnl
            });
        }

        // เรียงลำดับ
        /*rows.Sort((a, b) =>
        {
            int cmp = b.profit.CompareTo(a.profit);
            if (cmp != 0) return cmp;
            cmp = b.netWorth.CompareTo(a.netWorth);
            if (cmp != 0) return cmp;
            return string.Compare(a.displayName, b.displayName, StringComparison.Ordinal);
        });*/
        rows.Sort((a, b) =>
        {
            // ✅ เปลี่ยน: เรียงตาม Net Worth (netWorth/TotalWealth) ก่อน
            int cmp = b.netWorth.CompareTo(a.netWorth);
            if (cmp != 0) return cmp;
            
            // Profit เป็นเกณฑ์รอง
            cmp = b.profit.CompareTo(a.profit);
            if (cmp != 0) return cmp;
            
            return string.Compare(a.displayName, b.displayName, StringComparison.Ordinal);
        });

        // ล้างของเก่า: ลบเฉพาะรายการที่เคยถูกสร้างด้วย Prefab (ยกเว้น UI ของ Rank 1)
        for (int i = leaderboardRoot.childCount - 1; i >= 0; i--)
        {
            var child = leaderboardRoot.GetChild(i).gameObject;
            if (child != rank1ItemUI.gameObject)
            {
                Destroy(child);
            }
        }

        // สร้างรายการ 1..N (สูงสุด 6)
        int maxEntriesCount = Mathf.Min(leaderboardMaxEntries, rows.Count);

        // ===============================================
        // 1. จัดการอันดับ 1 (ใช้ rank1ItemUI)
        // ===============================================
        if (maxEntriesCount > 0 && rank1ItemUI != null)
        {
            var r = rows[0]; // อันดับ 1

            Sprite playerAvatar = PlayerDataManager.Instance.GetCharacterSprite(r.player.characterSpriteIndex);

            string assetName = "-";
            Sprite assetIcon = null;
            if (r.topSubAsset != null)
            {
                assetName = !string.IsNullOrEmpty(r.topSubAsset.assetNameAbbreviation)
                    ? r.topSubAsset.assetNameAbbreviation
                    : (!string.IsNullOrEmpty(r.topSubAsset.assetName) ? r.topSubAsset.assetName : "-");
                assetIcon = r.topSubAsset.assetIcon;
            }
            
            if (r.topSubAsset != null)
            {
                assetName = !string.IsNullOrEmpty(r.topSubAsset.assetNameAbbreviation)
                    ? r.topSubAsset.assetNameAbbreviation
                    : (!string.IsNullOrEmpty(r.topSubAsset.assetName) ? r.topSubAsset.assetName : "-");
                assetIcon = r.topSubAsset.assetIcon;
            }
            else if (inv != null && !string.IsNullOrEmpty(r.player.FinalTopAssetAbbreviation) && r.player.FinalTopAssetAbbreviation != "-")
            {
                var syncedAsset = FindSubAssetByAbbr(r.player.FinalTopAssetAbbreviation, inv);
                if (syncedAsset != null)
                {
                    assetName = syncedAsset.assetNameAbbreviation;
                    assetIcon = syncedAsset.assetIcon;
                    Debug.Log($"[Client UI] Found asset icon for {r.displayName} using abbreviation: {syncedAsset.assetNameAbbreviation}");
                }
            }
            
            rank1ItemUI.Setup(rank: 1,
            playerName: r.displayName,
             netWorth: r.netWorth,
            topAssetName: assetName,
            topAssetSprite: assetIcon,
            playerAvatarSprite: playerAvatar);


            rank1ItemUI.gameObject.SetActive(true); // แสดง Rank 1 UI
        }
        else if (rank1ItemUI != null)
        {
            rank1ItemUI.gameObject.SetActive(false); // ซ่อน Rank 1 UI ถ้าไม่มีข้อมูลผู้เล่น
        }

        // ===============================================
        // 2. จัดการอันดับ 2 ถึง N (สร้างด้วย Prefab)
        // ===============================================
        for (int i = 1; i < maxEntriesCount; i++)
        {
            var r = rows[i];
            int rank = i + 1; // อันดับปัจจุบัน (2, 3, 4...)

            GameObject selectedPrefab = leaderboardItemPrefabRankOther;
            var itemGO = Instantiate(selectedPrefab, leaderboardRoot);

            // จัดการ Background
            {
                // Rank 2 -> Index 0 | Rank 3 -> Index 1 | ...
                int bgIndex = rank - 2;
                if (rankBackgrounds != null && bgIndex >= 0 && bgIndex < rankBackgrounds.Count)
                {
                    var bgImage = itemGO.GetComponent<Image>();
                    if (bgImage != null)
                    {
                        bgImage.sprite = rankBackgrounds[bgIndex];
                    }
                }
            }

            var itemUI = itemGO.GetComponent<LeaderboardItemUI>();

            // ชื่อสินทรัพย์
            string assetName = "-";
            Sprite assetIcon = null;
            if (r.topSubAsset != null)
            {
                assetName = !string.IsNullOrEmpty(r.topSubAsset.assetNameAbbreviation)
                    ? r.topSubAsset.assetNameAbbreviation
                    : (!string.IsNullOrEmpty(r.topSubAsset.assetName) ? r.topSubAsset.assetName : "-");

                assetIcon = r.topSubAsset.assetIcon;
            }
            else if (inv != null && !string.IsNullOrEmpty(r.player.FinalTopAssetAbbreviation) && r.player.FinalTopAssetAbbreviation != "-")
            {
                var syncedAsset = FindSubAssetByAbbr(r.player.FinalTopAssetAbbreviation, inv);
                if (syncedAsset != null)
                {
                    assetName = syncedAsset.assetNameAbbreviation;
                    assetIcon = syncedAsset.assetIcon;
                }
            }
            if (itemUI != null)
            {
                Sprite playerAvatar = PlayerDataManager.Instance.GetCharacterSprite(r.player.characterSpriteIndex);

                itemUI.Setup(rank: rank,
                playerName: r.displayName,
                netWorth: r.netWorth,       
                topAssetName: assetName,
                topAssetSprite: assetIcon,
                playerAvatarSprite: playerAvatar);

            }
            else
            {
                Debug.LogError($"[GameOverSummaryUI] Prefab '{selectedPrefab.name}' ไม่มีคอมโพเนนต์ LeaderboardItemUI!");
            }
        }
        // แสดงแผง (เผื่อซ่อนอยู่)
        if (panel) panel.SetActive(true);
    }

    /// <summary>
    /// สะดวกเรียกจากหน้า Ranking: ดึงรายชื่อผู้เล่นจาก PlayerDataManager อัตโนมัติ
    /// </summary>
    public void ShowFromPlayerManager(InvestmentManager inv)
    {
        var mgr = PlayerDataManager.Instance;
        if (mgr == null || mgr.players == null || mgr.players.Count == 0)
        {
            Debug.LogWarning("[GameOverSummaryUI] PlayerDataManager ไม่มีผู้เล่น");
            return;
        }
        ShowWithLeaderboard(mgr.players, inv);
    }

    // ================= Helpers =================

    // บังคับให้ค่าเป็น 0..∞ ก่อน แล้วสเกลให้ผลรวม = 100%
    private void NormalizeTo100(ref float low, ref float med, ref float high)
    {
        low = Mathf.Max(0f, low);
        med = Mathf.Max(0f, med);
        high = Mathf.Max(0f, high);

        float sum = low + med + high;

        if (sum <= 0.0001f)
        {
            low = med = high = 100f / 3f;
            return;
        }

        float k = 100f / sum;
        low *= k;
        med *= k;
        high *= k;

        float roundedSum = Round2(low) + Round2(med) + Round2(high);
        float diff = 100f - roundedSum;

        if (Mathf.Abs(diff) >= 0.005f)
        {
            if (low >= med && low >= high) low = Round2(low + diff);
            else if (med >= low && med >= high) med = Round2(med + diff);
            else high = Round2(high + diff);
        }
        else
        {
            low = Round2(low);
            med = Round2(med);
            high = Round2(high);
        }
    }

    private float Round2(float v) => Mathf.Round(v * 100f) / 100f;

    // 1st, 2nd, 3rd, 4th...
    private string GetRankSuffix(int rank)
    {
        if (rank % 100 >= 11 && rank % 100 <= 13) return "th";
        switch (rank % 10)
        {
            case 1: return "st";
            case 2: return "nd";
            case 3: return "rd";
            default: return "th";
        }
    }

    // ---------- ยูทิลการเงิน ----------
    private static decimal DFloor2(decimal v) => Math.Floor(v * 100m) / 100m;

    private static bool TryGetSubAssetPrice(InvestmentManager inv, SubAssetData sub, out decimal price)
    {
        price = 0m;
        if (inv == null || sub == null || inv.activeCompanies == null) return false;

        var rt = inv.activeCompanies.Find(c => c != null && c.subAsset == sub);
        if (rt == null) return false;

        price = DFloor2((decimal)rt.currentPrice);
        return price > 0m;
    }

    public static decimal GetNetWorth(PlayerData p, InvestmentManager inv)
    {
        if (p == null) return 0m;
        decimal total = DFloor2(p.money);   // เงินสด

        if (inv != null && p.holdings != null)
        {
            foreach (var h in p.holdings)
            {
                if (h?.subAsset == null || h.shares <= 0f) continue;
                if (!TryGetSubAssetPrice(inv, h.subAsset, out var px)) continue;

                total += DFloor2((decimal)h.shares * px);   // หุ้นทั้งหมด × ราคา ณ ตอนนี้
            }
        }
        return DFloor2(total);
    }


    /// <summary>
    /// หา SubAsset ที่กำไรสูงสุดของผู้เล่น
    /// </summary>
    public static (SubAssetData subAsset, decimal pnl) GetTopEarningSub(PlayerData p, InvestmentManager inv)
    {
        SubAssetData best = null;
        decimal bestPnl = decimal.MinValue;

        if (p?.holdings != null)
        {
            foreach (var h in p.holdings)
            {
                if (h?.subAsset == null || h.shares <= 0f) continue;
                if (h.entryPrice <= 0f) continue;
                if (!TryGetSubAssetPrice(inv, h.subAsset, out var cur)) continue;

                decimal pnl = ((decimal)cur - (decimal)h.entryPrice) * (decimal)h.shares;
                pnl = DFloor2(pnl);

                if (pnl > bestPnl)
                {
                    bestPnl = pnl;
                    best = h.subAsset;
                }
            }
        }

        if (best == null) return (null, 0m);
        return (best, bestPnl);
    }

    // โครงสร้างเก็บผลคำนวณภายใน
    public class Row
    {
        public PlayerData player;
        public string displayName;
        public decimal netWorth;
        public decimal profit;
        public SubAssetData topSubAsset;
        public decimal topSubPnl;
    }

    public void ShowLeaderboardAuto()
    {
        var inv = FindObjectOfType<InvestmentManager>();
        if (inv == null)
        {
            Debug.LogError("[GameOverSummaryUI] ไม่พบ InvestmentManager ในซีน");
            return;
        }
        var mgr = PlayerDataManager.Instance;
        if (mgr == null || mgr.players == null || mgr.players.Count == 0)
        {
            Debug.LogWarning("[GameOverSummaryUI] ไม่มี players ใน PlayerDataManager");
            return;
        }
        ShowWithLeaderboard(mgr.players, inv);
    }

    // ใช้กดทดสอบจากเมนู Component ได้เลยใน Play Mode
    [ContextMenu("Debug → Show Leaderboard (Auto)")]
    private void Debug_ShowLeaderboardAuto() => ShowLeaderboardAuto();
    
    private static SubAssetData FindSubAssetByAbbr(string abbr, InvestmentManager inv)
    {
        if (string.IsNullOrEmpty(abbr) || abbr == "-") return null;
    
        // ค้นหา SubAssetData โดยใช้ Abbreviation จาก Active Companies
        if (inv?.activeCompanies != null)
        {
            var rt = inv.activeCompanies.Find(c => 
                c?.subAsset != null && 
                c.subAsset.assetNameAbbreviation == abbr);
            return rt?.subAsset;
        }
        return null;
    }
}