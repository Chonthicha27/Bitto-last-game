using TMPro;
using UnityEngine;
using UnityEngine.UI;   // ✅ เพิ่มให้ใช้ Button แบบสั้น ๆ

public class SubAssetUI : MonoBehaviour
{
    [Header("UI Components")]
    public TextMeshProUGUI assetNameText;
    private Button assetNameButton;
    public TextMeshProUGUI priceText;
    public TextMeshProUGUI changePercentText;
    public TextMeshProUGUI ownedAmountText;
    public TextMeshProUGUI typesOfAssets;

    public SubAssetData subAsset;
    private BuyManager buyManager;
    private int playerId;
    private CompanyUI companyUI;

    // ✅ ราคาที่ “แสดงจริง” บน UI (ปัดลง 2 ตำแหน่งแล้ว)
    public float CurrentDisplayedPrice { get; private set; }

    // Init UI ของ SubAsset
    public void Init(SubAssetData data, BuyManager manager, int _playerId, CompanyUI parentUI)
    {
        subAsset = data;
        buyManager = manager;
        playerId = _playerId;
        companyUI = parentUI;

        var player = PlayerDataManager.Instance?.localPlayer;
        if (player != null)
        {
            player.OnHoldingChanged -= OnPlayerHoldingChanged;
            player.OnHoldingChanged += OnPlayerHoldingChanged;
        }

        if (assetNameText)
        {
            assetNameText.text = subAsset.assetName;

            // ✅ ให้ Text ตัวนี้คลิกได้ (มี Button)
            assetNameButton = assetNameText.GetComponent<Button>()
                              ?? assetNameText.gameObject.AddComponent<Button>();

            assetNameButton.onClick.RemoveAllListeners();
            assetNameButton.onClick.AddListener(() =>
            {
                // 🔊 เล่นเสียงปุ่มทุกครั้งที่กดชื่อ Asset
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.Play(AudioManager.SoundType.Button);
                }

                // เลือกสินทรัพย์ + อัปเดตรายละเอียดฝั่ง CompanyUI
                companyUI.SelectSubAsset(subAsset);
                companyUI.ShowSubAssetDetail(subAsset);
            });
        }

        // ✅ ใช้ floor 2 ตำแหน่งเสมอ เพื่อให้ตรงกับหน้าอื่น
        CurrentDisplayedPrice = PriceMath.Floor2(subAsset.currentPrice);
        if (priceText)
            priceText.text = PriceMath.ToMoneyWithSeparators(CurrentDisplayedPrice);

        if (changePercentText) changePercentText.text = "0.00 %";

        if (typesOfAssets)
            typesOfAssets.text = string.IsNullOrEmpty(subAsset.typesOfAssets) ? "-" : subAsset.typesOfAssets;

        if (ownedAmountText)
        {
            float owned = player != null ? player.GetOwnedAmount(subAsset) : 0f;
            // ✅ ใส่คอมม่า + บังคับ 2 ตำแหน่ง
            ownedAmountText.text = PriceMath.ToNumberWithSeparators(owned);
        }

        SubAssetUIRegistry.Register(subAsset, this);
    }

    private void OnDestroy()
    {
        var player = PlayerDataManager.Instance?.localPlayer;
        if (player != null) player.OnHoldingChanged -= OnPlayerHoldingChanged;
        SubAssetUIRegistry.Unregister(subAsset, this);
    }

    private void OnPlayerHoldingChanged(SubAssetData changedSubAsset, float newShares)
    {
        if (changedSubAsset == subAsset) UpdateOwnedAmount(newShares);
    }

    // ===== รวมเปอร์เซ็นต์ "รายวัน" + "อีเวนต์" และอัปเดตราคาแบบ floor =====
    public void UpdatePrice(float newPrice, float dailyPct, float eventPct)
    {
        // รวม % ทั้งหมดของวันนี้ (เคลื่อนไหวปกติ + event)
        float totalPct = dailyPct + eventPct;

        // ✅ ปัดราคาให้เหลือ 2 ตำแหน่ง แล้วจำไว้สำหรับระบบอื่นใช้ต่อ
        CurrentDisplayedPrice = PriceMath.Floor2(newPrice);

        if (priceText != null)
            priceText.text = PriceMath.ToMoneyWithSeparators(CurrentDisplayedPrice);

        if (changePercentText != null)
        {
            float shownPct = PriceMath.Floor2(totalPct);

            string absStr = PriceMath.ToNumberWithSeparators(Mathf.Abs(shownPct));
            string sign;

            if (shownPct > 0f)
                sign = "+";
            else if (shownPct < 0f)
                sign = "-";
            else
                sign = "";   // 0 ไม่ต้องมี +/-

            changePercentText.text = $"{sign}{absStr}%";

            // ใช้สีตามบวก/ลบ
            if (shownPct > 0f)
            {
                if (ColorUtility.TryParseHtmlString("#1FA800", out var col))
                    changePercentText.color = col;
            }
            else if (shownPct < 0f)
            {
                if (ColorUtility.TryParseHtmlString("#B21D1D", out var col))
                    changePercentText.color = col;
            }
            else
            {
                if (ColorUtility.TryParseHtmlString("#B6702E", out var neutral))
                    changePercentText.color = neutral;
                else
                    changePercentText.color = Color.black;
            }
        }
    }

    // Backward-compat: ถ้ายังมีที่เรียกแบบเดิมอยู่ จะถือว่าค่านั้นคือ “รวมแล้ว”
    public void UpdatePrice(float newPrice, float pctChange)
    {
        UpdatePrice(newPrice, pctChange, 0f);
    }

    public void UpdateOwnedAmount(float newAmount)
    {
        if (ownedAmountText != null)
            ownedAmountText.text = PriceMath.ToNumberWithSeparators(newAmount);
    }

    public static class PriceMath
    {
        public static float Floor2(float v) => Mathf.Floor(v * 100f) / 100f;

        // ✅ บังคับทศนิยม 2 ตำแหน่งเสมอ (0.00, 1,234.50, 10,000.00)
        public static string ToMoneyWithSeparators(float v)
            => Floor2(v).ToString("#,0.00");

        public static string ToNumberWithSeparators(float v)
            => Floor2(v).ToString("#,0.00");
    }
}
