using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.UI;

public class FinalRankPanelUI : MonoBehaviour
{
    [Header("Root Panel")]
    public GameObject panelRank; 

    // 🚨 LeaderboardItemUI สำหรับผู้เล่น 3 อันดับแรก
    [Header("Leaderboard Items (3 Ranks)")]
    [Tooltip("UI สำหรับผู้เล่นอันดับ 1 (ตรงกลาง)")]
    public Image rank1ItemUI;
    [Tooltip("UI สำหรับผู้เล่นอันดับ 2 (ด้านซ้าย)")]
    public Image rank2ItemUI;
    [Tooltip("UI สำหรับผู้เล่นอันดับ 3 (ด้านขวา)")]
    public Image rank3ItemUI;
    
    [Header("Dependencies")]
    [Tooltip("Reference ไปยัง GameOverSummaryUI เพื่อดึงข้อมูลกำไร/สินทรัพย์")]
    public GameOverSummaryUI summaryUI;
    
    // ตัวจัดการสินทรัพย์ที่จำเป็นในการคำนวณกำไร
    private InvestmentManager investmentManager => FindObjectOfType<InvestmentManager>();

    /// <summary>
    /// แสดงผู้เล่น 3 อันดับแรก
    /// </summary>
    public void ShowTop3(List<PlayerData> allPlayers)
    {
        if (allPlayers == null || allPlayers.Count == 0 || summaryUI == null || investmentManager == null)
        {
            Debug.LogError("[FinalRankPanelUI] ข้อมูลผู้เล่น, Summary UI, หรือ Investment Manager ไม่พร้อม!");
            if (panelRank) panelRank.SetActive(false);
            return;
        }

        // 1. เรียงลำดับผู้เล่นตาม Logic เดียวกับ GameOverSummaryUI
        var rows = CreateSortedRows(allPlayers);
        
        // 2. จัดการแสดงผล 3 อันดับแรก
        if (panelRank) panelRank.SetActive(true);
        
        // อันดับ 1
        if (rows.Count >= 1 && rank1ItemUI)
        {
            SetupRankItem(rank1ItemUI, rows[0], 1);
            rank1ItemUI.gameObject.SetActive(true);
        }
        else if (rank1ItemUI) rank1ItemUI.gameObject.SetActive(false);

        // อันดับ 2
        if (rows.Count >= 2 && rank2ItemUI)
        {
            SetupRankItem(rank2ItemUI, rows[1], 2);
            rank2ItemUI.gameObject.SetActive(true);
        }
        else if (rank2ItemUI) rank2ItemUI.gameObject.SetActive(false);

        // อันดับ 3
        if (rows.Count >= 3 && rank3ItemUI)
        {
            SetupRankItem(rank3ItemUI, rows[2], 3);
            rank3ItemUI.gameObject.SetActive(true);
        }
        else if (rank3ItemUI) rank3ItemUI.gameObject.SetActive(false);

        // 3. กำหนดปุ่มปิด/ไปต่อ (ถ้ามี)
        // คุณสามารถเพิ่มปุ่มใน Panel นี้เพื่อสลับไป Panel ถัดไปได้

        Debug.Log("[FinalRankPanelUI] แสดงผู้เล่น 3 อันดับแรกแล้ว");
    }

    /// <summary>
    /// Helper: สร้าง Row Data และจัดเรียงตามกำไร/NetWorth
    /// </summary>
    private List<GameOverSummaryUI.Row> CreateSortedRows(List<PlayerData> allPlayers)
    {
        var inv = investmentManager;
        var rows = new List<GameOverSummaryUI.Row>(allPlayers.Count);
        
        // ใช้ Logic เดียวกับ GameOverSummaryUI
        foreach (var p in allPlayers) 
        { 
            var nw = GameOverSummaryUI.GetNetWorth(p, inv);          
            var profit = nw - p.startingCapital;       
            var (topSub, topPnl) = GameOverSummaryUI.GetTopEarningSub(p, inv);

            rows.Add(new GameOverSummaryUI.Row 
            { 
                player = p,
                displayName = p.playerName,
                netWorth = nw,
                profit = profit,
                topSubAsset = topSub,
                topSubPnl = topPnl 
            }); 
        }

        // เรียง: กำไรสูง → ต่ำ → NetWorth → ชื่อ
        rows.Sort((a, b) => 
        { 
            int cmp = b.profit.CompareTo(a.profit);
            if (cmp != 0) return cmp;
            cmp = b.netWorth.CompareTo(a.netWorth);
            if (cmp != 0) return cmp;
            return string.Compare(a.displayName, b.displayName, StringComparison.Ordinal); 
        }); 
        
        return rows;
    }
    
    private void SetupRankItem(Image playerImage, GameOverSummaryUI.Row row, int rank)
    {
        // 1. ดึง Sprite ของผู้เล่น
        Sprite playerAvatar = PlayerDataManager.Instance.GetCharacterSprite(row.player.characterSpriteIndex);
    
        // 2. กำหนด Sprite ให้กับ Image Component
        if (playerImage != null)
        {
            playerImage.sprite = playerAvatar;
        }
    }
}