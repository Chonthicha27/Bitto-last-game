using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Globalization;

public class PortfolioUI : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI playerNameText;
    public Image characterSprite;
    public Transform contentParent;             // ScrollView Content
    public GameObject portfolioItemPrefab;      // Prefab รายการในพอร์ต
    public GameObject portfolioPanel;           // Panel หลักของ Portfolio
    public Button openButton;                   // ปุ่มเปิดพอร์ต
    public Button closeButton;

    [Header("Total Value")]
    public TextMeshProUGUI totalValueText;

    [Header("All Companies (SO)")]
    public List<InvestmentCompany> allCompanies; // ใช้สำหรับแมปรู้ว่า SubAsset อยู่บริษัทไหน

    [Header("Filter Buttons (filter by companyName)")]
    public Button allButton;
    public Button stocksButton;
    public Button realEstateButton;
    public Button bankButton;
    public Button cryptoButton;
    public Button goldButton;

    [Header("Runtime Reference")]
    [Tooltip("อ้างถึง InvestmentManager ในซีน เพื่ออ่านราคา runtime.currentPrice")]
    public InvestmentManager investmentManager;


    private string currentFilter = "All";
    private PlayerData localPlayer;

    // Map: SubAsset -> Company (SO)
    private readonly Dictionary<SubAssetData, InvestmentCompany> subAssetToCompanyMap =
        new Dictionary<SubAssetData, InvestmentCompany>();

    // Map: SubAsset -> Runtime (lookup เร็วขึ้น)
    private readonly Dictionary<SubAssetData, InvestmentCompanyRuntime> subAssetToRuntimeMap =
        new Dictionary<SubAssetData, InvestmentCompanyRuntime>();

    void Awake()
    {
        BuildAssetCompanyMap();
    }

    void Start()
    {
        // หา player
        localPlayer = PlayerDataManager.Instance?.localPlayer;
        if (localPlayer != null)
            localPlayer.OnHoldingChanged += OnPlayerHoldingChanged;

        // ซ่อน panel
        if (portfolioPanel != null) portfolioPanel.SetActive(false);

        // ปุ่มเปิด/ปิด
        if (openButton) openButton.onClick.AddListener(OpenPortfolio);
        if (closeButton) closeButton.onClick.AddListener(ClosePortfolio);

        // ปุ่มกรองตามชื่อ company (ต้องตรงกับ InvestmentCompany.companyName)
        if (allButton) allButton.onClick.AddListener(() => SetFilter("All"));
        if (stocksButton) stocksButton.onClick.AddListener(() => SetFilter("StocksInvestment"));
        if (realEstateButton) realEstateButton.onClick.AddListener(() => SetFilter("RealEstateInvestment"));
        if (bankButton) bankButton.onClick.AddListener(() => SetFilter("BankInvestment"));
        if (cryptoButton) cryptoButton.onClick.AddListener(() => SetFilter("CryptoInvestment"));
        if (goldButton) goldButton.onClick.AddListener(() => SetFilter("GoldInvestment"));

        RebuildRuntimeMap(); // เตรียม lookup ราคา runtime
    }

    void OnEnable()
    {
        RebuildRuntimeMap(); // เผื่อมีการ spawn runtime หลังจาก Awake/Start
    }

    void OnDestroy()
    {
        if (localPlayer != null)
            localPlayer.OnHoldingChanged -= OnPlayerHoldingChanged;
    }

    // ============ Internal Maps ============

    private void BuildAssetCompanyMap()
    {
        subAssetToCompanyMap.Clear();
        if (allCompanies == null) return;

        foreach (var company in allCompanies)
        {
            if (company == null || company.subAssets == null) continue;
            foreach (var subAsset in company.subAssets)
            {
                if (subAsset != null && !subAssetToCompanyMap.ContainsKey(subAsset))
                    subAssetToCompanyMap.Add(subAsset, company);
            }
        }
    }

    private void RebuildRuntimeMap()
    {
        subAssetToRuntimeMap.Clear();
        if (investmentManager == null || investmentManager.activeCompanies == null) return;

        foreach (var rt in investmentManager.activeCompanies)
        {
            if (rt?.subAsset == null) continue;
            if (!subAssetToRuntimeMap.ContainsKey(rt.subAsset))
                subAssetToRuntimeMap.Add(rt.subAsset, rt);
        }
    }

    // ============ Events ============

    private void OnPlayerHoldingChanged(SubAssetData subAsset, float shares)
    {
        if (portfolioPanel != null && portfolioPanel.activeSelf && localPlayer != null)
            UpdatePortfolio(localPlayer);
    }

    // ============ Public UI actions ============

    public void SetFilter(string filterType)
    {
        // 🔊 เล่นเสียงปุ่มทุกครั้งที่เปลี่ยน filter
        if (AudioManager.Instance != null)
            AudioManager.Instance.Play(AudioManager.SoundType.Button);

        currentFilter = filterType;
        if (portfolioPanel != null && portfolioPanel.activeSelf && localPlayer != null)
            UpdatePortfolio(localPlayer);

        Debug.Log($"📊 เปลี่ยน Filter เป็น: {filterType}");
    }

    public void OpenPortfolio()
    {
        // 🔊 เล่นเสียงปุ่มตอนกดเปิดพอร์ต
        if (AudioManager.Instance != null)
            AudioManager.Instance.Play(AudioManager.SoundType.Button);

        if (portfolioPanel == null)
        {
            Debug.LogWarning("❌ PortfolioPanel ยังไม่ได้เซ็ตใน Inspector");
            return;
        }

        // 🔹 ปิดหน้าซื้อหุ้น (CompanyUI) ทั้งหมดก่อน – กันเปิดซ้อน
        var companyUIs = FindObjectsOfType<CompanyUI>(true);
        foreach (var c in companyUIs)
        {
            if (c != null && c.gameObject.activeSelf)
            {
                c.gameObject.SetActive(false);
            }
        }

        if (localPlayer == null)
        {
            localPlayer = PlayerDataManager.Instance?.localPlayer;
            if (localPlayer == null)
            {
                Debug.LogWarning("❌ ไม่พบ PlayerData ใน PlayerDataManager");
                return;
            }
        }

        // เซ็ตชื่อผู้เล่น + สี
        if (playerNameText != null)
        {
            playerNameText.text = localPlayer.playerName;

            if (ColorUtility.TryParseHtmlString("#331200", out var nameColor))
                playerNameText.color = nameColor;
        }

        // เซ็ตสไปรต์ตัวละคร
        if (characterSprite != null)
        {
            var spriteArray = PlayerDataManager.Instance.characterSprites;
            if (spriteArray != null &&
                localPlayer.characterSpriteIndex >= 0 &&
                localPlayer.characterSpriteIndex < spriteArray.Length)
            {
                var img = characterSprite.GetComponent<Image>();
                characterSprite.sprite = spriteArray[localPlayer.characterSpriteIndex];

                img.SetNativeSize();
                img.rectTransform.localScale = img.rectTransform.localScale;
            }
            else
            {
                Debug.LogWarning($"❌ ไม่พบ Sprite สำหรับ characterSpriteIndex={localPlayer.characterSpriteIndex}");
            }
        }

        if (subAssetToCompanyMap.Count == 0)
        {
            BuildAssetCompanyMap();
        }
        RebuildRuntimeMap();

        portfolioPanel.SetActive(true);
        UpdatePortfolio(localPlayer);
        Debug.Log("📊 เปิด Portfolio");
    }

    public void ClosePortfolio()
    {
        // 🔊 เล่นเสียงปุ่มตอนกดปิดพอร์ต
        if (AudioManager.Instance != null)
            AudioManager.Instance.Play(AudioManager.SoundType.Button);

        if (portfolioPanel != null)
        {
            portfolioPanel.SetActive(false);
            Debug.Log("❎ ปิด Portfolio");
        }
    }


    // ============ Core ============

    public void UpdatePortfolio(PlayerData player)
    {
        // ล้างรายการเดิม
        if (contentParent != null)
        {
            foreach (Transform child in contentParent)
                Destroy(child.gameObject);
        }

        if (player == null)
        {
            if (totalValueText)
            {
                totalValueText.text = "มูลค่ารวมทั้งหมด: 0.00";
                if (ColorUtility.TryParseHtmlString("#B6702E", out var tvColor))
                    totalValueText.color = tvColor;
            }
            return;
        }

        // รายการที่ถืออยู่จริง
        IEnumerable<PlayerHolding> allHoldings = player.holdings
            .Where(h => h != null && h.subAsset != null && h.shares > 0f);

        // กรองตามบริษัท (ถ้าไม่ใช่ All)
        IEnumerable<PlayerHolding> filteredHoldings = allHoldings;
        if (currentFilter != "All")
        {
            filteredHoldings = allHoldings.Where(h =>
            {
                if (subAssetToCompanyMap.TryGetValue(h.subAsset, out var company))
                    return company.companyName.Equals(currentFilter, StringComparison.OrdinalIgnoreCase);
                return false;
            });
        }

        // ✅ รวมจาก "Total Value ของแต่ละรายการ" (= currentPrice × shares)
        float totalFilteredValue = 0f;

        foreach (var holding in filteredHoldings)
        {
            float curPrice = GetRuntimePrice(holding.subAsset);

            // ✅ ใช้ต้นทุนรวมจริง ถ้ามี; ถ้าไม่มี fallback = entryPrice × shares
            float totalCost = (holding.totalCost > 0m)
                ? (float)holding.totalCost
                : (holding.entryPrice * holding.shares);

            // อินสแตนซ์ prefab
            var itemGO = Instantiate(portfolioItemPrefab, contentParent);
            var itemUI = itemGO.GetComponent<PortfolioItemUI>();
            if (itemUI != null)
            {
                // ใช้ signature เวอร์ชันเก่า: (subAsset, ownedShares, totalCost, currentPrice)
                itemUI.SetData(holding.subAsset, holding.shares, totalCost, curPrice);
            }
            else
            {
                Debug.LogWarning("❌ PortfolioItemPrefab ไม่มี PortfolioItemUI component");
            }

            // ✅ ใช้สูตรเดียวกับ PortfolioItemUI: totalValue = currentPrice × ownedShares
            float itemTotal = curPrice * holding.shares;

            // กันเพี้ยนทศนิยม 2 ตำแหน่งให้ตรงกับข้อความในแถว
            itemTotal = Mathf.Floor(itemTotal * 100f) / 100f;

            totalFilteredValue += itemTotal;
        }

        // ฟลอร์รวมอีกชั้นให้แสดงตรงกับรายการ
        totalFilteredValue = Mathf.Floor(totalFilteredValue * 100f) / 100f;

        // แสดงมูลค่ารวม (ฟอร์แมตใส่คอมม่า + ทศนิยมไม่เกิน 2 ตำแหน่ง)
        if (totalValueText != null)
        {
            totalValueText.text = $"มูลค่ารวม: {FormatNumber(totalFilteredValue)}";

            if (ColorUtility.TryParseHtmlString("#331200", out var tvColor))
                totalValueText.color = tvColor;
        }


        Debug.Log($"✅ แสดงพอร์ต: {filteredHoldings.Count()} รายการ (Filter: {currentFilter})");
    }

    private float GetRuntimePrice(SubAssetData subAsset)
    {
        // ใช้ราคา runtime ถ้ามี / ถ้าไม่มี fallback เป็นราคาใน SO
        if (subAsset == null) return 0f;

        if (subAssetToRuntimeMap.TryGetValue(subAsset, out var rt) && rt != null)
            return rt.currentPrice;

        // เผื่อ runtime map ยังไม่ทันอัปเดต
        if (investmentManager != null && investmentManager.activeCompanies != null)
        {
            var found = investmentManager.activeCompanies
                .FirstOrDefault(r => r != null && r.subAsset == subAsset);
            if (found != null)
            {
                subAssetToRuntimeMap[subAsset] = found; // cache
                return found.currentPrice;
            }
        }

        return subAsset.currentPrice; // fallback
    }

    /// <summary>
    /// ฟอร์แมตตัวเลขทั่วไปให้มีคอมม่า และทศนิยมไม่เกิน 2 ตำแหน่ง (ตัดทิ้ง ไม่ปัด)
    /// เช่น 10000000 -> 10,000,000 | 10000000.5 -> 10,000,000.5 | 10000000.529 -> 10,000,000.52
    /// </summary>
    private static string FormatNumber(float v)
    {
        decimal d = (decimal)v;
        // ตัดให้เหลือ 2 ตำแหน่ง (ไม่ปัด)
        d = Math.Truncate(d * 100m) / 100m;
        // ใส่คอมม่า, ทศนิยม "คงที่ 2 ตำแหน่ง" เช่น 1,000.00
        return d.ToString("#,0.00", CultureInfo.InvariantCulture);
    }

}
