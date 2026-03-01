using UnityEngine;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
public class LeaderboardItemUI : MonoBehaviour
{
    [Header("Texts")]
    public TMP_Text playerNameText;    // ex. "Alice"
    public TMP_Text profitText;        // ใช้โชว์ "มูลค่ารวม (เงินสด + หุ้นทั้งหมด)"
    public TMP_Text topAssetNameText;  // ex. "TECH" หรือ "Tech Fund"

    [Header("Visual")]
    public Image topAssetIcon;         // ไอคอนของสินทรัพย์ที่ทำกำไรสูงสุด
    public Image iconPlayer;           // Icon/Avatar ของผู้เล่น

    /// <summary>
    /// กำหนดค่าให้กับรายการใน Leaderboard 1 แถว
    /// profit = มูลค่ารวมของผู้เล่น (Cash + Asset) จาก DailyReportUI.totalNetWorthToday
    /// </summary>
    public void Setup(
    int rank,
    string playerName,
    decimal netWorth,               // << ชื่อใหม่ แทน profit
    string topAssetName,
    Sprite topAssetSprite,
    Sprite playerAvatarSprite)
    {
        if (playerNameText)
            playerNameText.text = string.IsNullOrWhiteSpace(playerName) ? "-" : playerName;

        if (profitText)
        {
            // แสดงมูลค่ารวม (เงินสด + หุ้นทั้งหมด)
            profitText.text = $"{netWorth:N0}";
        }

        if (topAssetNameText)
            topAssetNameText.text = string.IsNullOrWhiteSpace(topAssetName) ? "-" : topAssetName;

        if (topAssetIcon)
        {
            topAssetIcon.sprite = topAssetSprite;
            topAssetIcon.enabled = topAssetSprite != null;
        }

        if (iconPlayer)
        {
            iconPlayer.sprite = playerAvatarSprite;
            iconPlayer.enabled = playerAvatarSprite != null;
        }
    }


    private void Reset()
    {
        if (!playerNameText) playerNameText = FindTMP("PlayerName");
        if (!profitText) profitText = FindTMP("Profit");
        if (!topAssetNameText) topAssetNameText = FindTMP("AssetName");
        if (!topAssetIcon) topAssetIcon = GetComponentInChildren<Image>(true);
    }

    private TMP_Text FindTMP(string childName)
    {
        var t = transform.Find(childName);
        return t ? t.GetComponent<TMP_Text>() : null;
    }
}
