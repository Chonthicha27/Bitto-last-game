using System.Text;
using System.Linq;
using UnityEngine;
using System.Collections.Generic;

public static class BotReport
{
    public static void ShowDailyBotReports(List<PlayerData> players, InvestmentManager inv, int day)
    {
        if (players == null || inv == null) return;

        // เรียงลำดับผู้เล่นทั้งหมด (รวม Bot + คนจริง) ตาม TotalAssets
        var sorted = players.OrderByDescending(p => p.GetTotalAssets(inv)).ToList();

        // ไล่เฉพาะ Bot
        int rankCounter = 1;
        foreach (var bot in sorted)
        {
            if (bot is not BotData) continue; // ✅ เฉพาะ Bot

            StringBuilder sb = new StringBuilder();

            int rank = sorted.IndexOf(bot) + 1; // คำนวณอันดับ
            sb.AppendLine($"===== Day {day} BOT REPORT: {bot.playerName} (Rank {rank}) =====");
            sb.AppendLine($"Cash: ${bot.money:N0}");
            sb.AppendLine($"Total Assets: ${bot.GetTotalAssets(inv):N0}");
            sb.AppendLine("-------------------------------------------------");
            sb.AppendLine(string.Format("{0,-15} | {1,-5} | {2,-7} | {3,-7}", "Asset", "Held", "%Change", "Gain/Loss"));
            sb.AppendLine("-------------------------------------------------");
            
            foreach (var h in bot.holdings)
            {
                if (h == null || h.subAsset == null || h.shares <= 0) continue;

                // หา runtime ของ SubAsset
                var runtime = inv.activeCompanies.Find(c => c.subAsset == h.subAsset);
                if (runtime == null) continue;

                float currentValue = runtime.currentPrice * h.shares;
                float yesterdayValue = h.lastPrice * h.shares;
                float changePct = (yesterdayValue > 0) ? ((currentValue - yesterdayValue) / yesterdayValue) * 100f : 0f;
                float gainLoss = currentValue - yesterdayValue;

                sb.AppendLine(string.Format(
                    "{0,-15} | {1,-5} | {2,-8} | {3,-10}",
                    h.subAsset.assetName ?? "Unknown",
                    h.shares.ToString("0.#"),
                    float.IsNaN(changePct) ? "0%" : changePct.ToString("+0.##;-0.##") + "%",
                    float.IsNaN(gainLoss) ? "$0" : gainLoss.ToString("+0;-0")
                ));
            }


            sb.AppendLine("=================================================");
            Debug.Log(sb.ToString());
            rankCounter++;
        }
    }
}
