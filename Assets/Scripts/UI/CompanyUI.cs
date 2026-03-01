using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Linq;
using System;

public class CompanyUI : MonoBehaviour
{
    [Header("หัวบริษัท")]
    public TextMeshProUGUI companyNameText;
    public TextMeshProUGUI description;
    public Image companyIcon;
    public Button closeButton;

    [Header("UI แสดงชื่อ SubAsset ที่เลือก")]
    public TextMeshProUGUI selectedSubAssetNameText;

    [Header("UI รายการสินทรัพย์ย่อย (SubAssets)")]
    public Transform subAssetContainer; // Content ของ ScrollView
    public GameObject subAssetPrefab;   // Prefab ที่มี SubAssetUI อยู่ข้างใน

    public TextMeshProUGUI showPrice;   // ช่องแสดงราคา (ต่อหุ้น หรือ ราคารวม)

    [Header("Numpad UI")]
    public TextMeshProUGUI inputText; // แสดงตัวเลขที่กด
    public Button[] numberButtons;    // ปุ่ม 0-9
    public Button decimalButton;      // ปุ่มทศนิยม
    public Button clearButton;

    [Header("ปุ่ม ซื้อ / ขาย")]
    public Button buyButton;  // ปุ่มซื้อ
    public Button sellButton; // ปุ่มขาย

    [Header("ปุ่มพิเศษ")]
    public Button maxButton;  // ✅ ปุ่ม MAX ที่เราพึ่งเพิ่ม
    public Button maxSellButton;

    [Header("VFX Effects")]
    public ParticleSystem psBuy;   // Buy effect
    public ParticleSystem psSell;  // Sell effect

    // สถานะเลือกปัจจุบัน
    private SubAssetData selectedSubAsset;                 // ข้อมูล SO
    private InvestmentCompanyRuntime selectedRuntime;      // runtime ของ subasset ที่เลือก (ไว้ใช้ currentPrice สด)
    private int playerId;

    private BuyManager buyManager;
    private InvestmentManager investmentManager;
    private Color defaultShowPriceColor;   // ✅ เก็บสีเดิมของ showPrice

    // ---------- Helpers: ตัดทศนิยม 2 ตำแหน่ง (ไม่ปัดเศษ) + ใส่คอมม่า ----------
    private static decimal Trunc2(decimal v) => Math.Truncate(v * 100m) / 100m;
    private static decimal Trunc2(float v) => Trunc2((decimal)v);

    // ✅ ฟอร์แมตราคาด้วยคอมมา เช่น 1000000 -> 1,000,000 / 1234.5 -> 1,234.50
    private static string FormatNoRound2(decimal v) => Trunc2(v).ToString("#,0.##");

    // 🔊 ฟังก์ชันช่วยเล่นเสียงปุ่มทั่วไป
    private void PlayButtonClick()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.Play(AudioManager.SoundType.Button);
        }
    }

    // 🔊 ฟังก์ชันช่วยเล่นเสียงซื้อ/ขาย (Trade SFX = eventClip)
    private void PlayTradeSfx()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.Play(AudioManager.SoundType.Event);
        }
    }

    void Awake()
    {
        buyManager = FindAnyObjectByType<BuyManager>();
        investmentManager = FindAnyObjectByType<InvestmentManager>();
        // ✅ ถ้ามี showPrice ให้เก็บสีเดิมไว้
        if (showPrice != null)
        {
            defaultShowPriceColor = showPrice.color;
        }
    }

    public void SetCompany(InvestmentCompanyRuntime companyRuntime)
    {
        if (companyRuntime == null || investmentManager == null) return;

        // 🔹 ปิด Portfolio ถ้าเปิดอยู่ (ห้ามให้ซ้อนกัน)
        var portfolio = FindObjectOfType<PortfolioUI>(true);
        if (portfolio != null && portfolio.portfolioPanel != null && portfolio.portfolioPanel.activeSelf)
        {
            portfolio.portfolioPanel.SetActive(false);
            Debug.Log("[CompanyUI] ปิด PortfolioPanel เพราะกำลังเปิดหน้าซื้อหุ้น");
        }

        if (companyNameText != null) companyNameText.text = companyRuntime.data.companyName;
        if (description != null) description.text = companyRuntime.data.descriptionAssets;
        if (companyIcon != null) companyIcon.sprite = companyRuntime.data.companyIcon;

        // ล้าง list เดิม
        foreach (Transform child in subAssetContainer)
            Destroy(child.gameObject);

        // ดึง runtime ทั้งหมดของบริษัทนี้
        var runtimes = investmentManager.activeCompanies
            .Where(rt => rt != null && rt.data == companyRuntime.data)
            .ToList();

        playerId = PlayerDataManager.Instance.players.IndexOf(PlayerDataManager.Instance.localPlayer);

        // สร้างแถว SubAssetUI ตาม runtimes
        foreach (var rt in runtimes)
        {
            var row = Instantiate(subAssetPrefab, subAssetContainer);
            var ui = row.GetComponent<SubAssetUI>();

            ui.Init(rt.subAsset, buyManager, playerId, this);
            rt.ui = ui;
            ui.UpdatePrice(rt.currentPrice, rt.lastChangePct, rt.lastEventPct);
        }

        // เคลียร์สถานะเลือกก่อน
        selectedSubAsset = null;
        selectedRuntime = null;

        // ⭐ ถ้าบริษัทนี้มี subasset เดียว ให้ auto เลือกให้เลย
        if (runtimes.Count == 1 && runtimes[0] != null && runtimes[0].subAsset != null)
        {
            SelectSubAsset(runtimes[0].subAsset);
            // - selectedSubAsset ถูกเซ็ต
            // - selectedRuntime ถูกหา
            // - selectedSubAssetNameText เปลี่ยนชื่อ
            // - showPrice โชว์ราคา/หุ้น
        }

        // ปุ่มปิด
        closeButton?.onClick.RemoveAllListeners();
        closeButton?.onClick.AddListener(OnCloseButtonClicked);

        // ตั้งค่า numpad + อัปเดตราคารวมตาม selectedSubAsset (ถ้ามี)
        SetupNumpad();
    }

    private void OnCloseButtonClicked()
    {
        PlayButtonClick();          // 🔊 เสียงปิดหน้าต่าง
        gameObject.SetActive(false);
    }

    void SetupNumpad()
    {
        if (inputText) inputText.text = "ใส่จำนวนสินทรัพย์";
        UpdateTotalPriceDisplay();

        if (numberButtons != null)
        {
            foreach (var btn in numberButtons)
            {
                int digit = int.Parse(btn.GetComponentInChildren<TextMeshProUGUI>().text);
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() =>
                {
                    PlayButtonClick();      // 🔊 เสียงกดเลข
                    OnNumberPressed(digit);
                });
            }
        }

        if (decimalButton != null)
        {
            decimalButton.onClick.RemoveAllListeners();
            decimalButton.onClick.AddListener(() =>
            {
                PlayButtonClick();          // 🔊 เสียงกด .
                OnDecimalPressed();
            });
        }

        if (clearButton != null)
        {
            clearButton.onClick.RemoveAllListeners();
            clearButton.onClick.AddListener(() =>
            {
                PlayButtonClick();          // 🔊 เสียงกดล้าง
                
                const string placeholder = "ใส่จำนวนสินทรัพย์";
                
                // ❗ ถ้าเป็น placeholder อยู่แล้ว → ไม่ต้องทำอะไรเลย
                if (inputText.text == placeholder)
                {
                    return;
                }

                string raw = inputText.text.Replace(",", "");

                // ถ้าจะลบจนไม่เหลือ → ให้กลับเป็น placeholder
                if (string.IsNullOrEmpty(raw) || raw.Length <= 1)
                {
                    inputText.text = placeholder;
                    UpdateTotalPriceDisplay();
                    return;
                }

                // ลบทีละตัว
                raw = raw.Substring(0, raw.Length - 1);
                inputText.text = FormatInputWithSeparators(raw);

                UpdateTotalPriceDisplay();
            });
        }

        if (buyButton != null)
        {
            buyButton.onClick.RemoveAllListeners();
            buyButton.onClick.AddListener(() =>
            {
                PlayTradeSfx();             // 🔊 ใช้เสียงซื้อ/ขาย (eventClip)
                HandleTrade(true);
            });
        }

        if (sellButton != null)
        {
            sellButton.onClick.RemoveAllListeners();
            sellButton.onClick.AddListener(() =>
            {
                PlayTradeSfx();             // 🔊 ใช้เสียงซื้อ/ขาย (eventClip)
                HandleTrade(false);
            });
        }

        // ✅ ปุ่ม MAX
        if (maxButton != null)
        {
            maxButton.onClick.RemoveAllListeners();
            maxButton.onClick.AddListener(() =>
            {
                PlayButtonClick();
                OnMaxPressed();
            });
        }
        // ปุ่ม MAX SELL
        if (maxSellButton != null)
        {
            maxSellButton.onClick.RemoveAllListeners();
            maxSellButton.onClick.AddListener(() =>
            {
                PlayButtonClick();
                OnMaxSellPressed();
            });
        }
    }

    private InvestmentCompanyRuntime FindRuntime(SubAssetData data)
    {
        if (data == null || investmentManager == null) return null;
        return investmentManager.activeCompanies
            .FirstOrDefault(r => r != null && r.subAsset == data);
    }

    public void SelectSubAsset(SubAssetData data)
    {
        selectedSubAsset = data;
        selectedRuntime = FindRuntime(data);

        if (inputText) inputText.text = "";
        if (selectedSubAssetNameText != null) selectedSubAssetNameText.text = data.assetName;

        if (showPrice != null)
        {
            decimal cur = (selectedRuntime != null) ? (decimal)selectedRuntime.currentPrice
                                                    : (decimal)data.currentPrice;
            showPrice.text = FormatNoRound2(cur);   // ✅ ตอนนี้มีคอมม่าแล้ว
        }

        UpdateTotalPriceDisplay();
    }

    void HandleTrade(bool isBuy)
    {
        if (selectedSubAsset == null) return;
        if (inputText == null || string.IsNullOrWhiteSpace(inputText.text)) return;

        // ✅ ลบคอมม่าออกก่อน Parse เสมอ
        string raw = inputText.text.Replace(",", "");
        if (!float.TryParse(raw, out float inputValue) || inputValue <= 0f) return;

        var player = PlayerDataManager.Instance.localPlayer;
        var staminaUI = FindAnyObjectByType<StaminaUI>();

        if (staminaUI != null && player.currentStamina < 1)
        {
            staminaUI.ShowStaminaAlert();
            inputText.text = "";
            return;
        }

        int pid = PlayerDataManager.Instance.players.IndexOf(player);
        if (pid < 0) return;

        float sharesFloat = Mathf.Floor(inputValue * 100f) / 100f;
        if (sharesFloat < 0.01f)
        {
            buyManager.ShowResultMessage(false, isBuy);
            inputText.text = "";
            return;
        }

        if (isBuy)
        {
            buyManager.BuySubAsset(pid, selectedSubAsset, sharesFloat, psBuy);
        }
        else
        {
            buyManager.SellSubAsset(pid, selectedSubAsset, sharesFloat, psSell);
        }

        inputText.text = "";
        UpdateTotalPriceDisplay();
    }

    // ✅ ฟังก์ชันช่วย: ฟอร์แมต string ที่เป็นเลข ให้มีคอมม่า แต่ยังเก็บทศนิยมไว้
    private string FormatInputWithSeparators(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";

        // แยกส่วนหน้าจุด / หลังจุด
        string integerPart = raw;
        string fractionPart = "";
        int dotIndex = raw.IndexOf('.');
        if (dotIndex >= 0)
        {
            integerPart = raw.Substring(0, dotIndex);
            fractionPart = raw.Substring(dotIndex); // รวมเครื่องหมาย .
        }

        // กันเคสกด '.' ก่อน > integerPart อาจเป็น "" → ให้เป็น "0"
        if (string.IsNullOrEmpty(integerPart))
            integerPart = "0";

        if (long.TryParse(integerPart, out long intVal))
        {
            string intFormatted = intVal.ToString("#,0");
            return intFormatted + fractionPart;   // เช่น "1,234.5"
        }

        // ถ้า parse ไม่ได้ก็คืนค่าเดิมไป
        return raw;
    }

    void OnNumberPressed(int num)
    {
        const string placeholder = "ใส่จำนวนสินทรัพย์";

        if (inputText.text == placeholder)
        {
            inputText.text = "";
        }

        if (inputText == null)
        {
            return;
        }

        string raw = inputText.text.Replace(",", "");

        if (raw.Length >= 15)
        {
            return;
        }

        raw += num.ToString();

        // ฟอร์แมตกลับให้มีคอมม่า
        inputText.text = FormatInputWithSeparators(raw);
        UpdateTotalPriceDisplay();
    }

    void OnDecimalPressed()
    {
        const string placeholder = "ใส่จำนวนสินทรัพย์";

        if (inputText.text == placeholder)
        {
            inputText.text = "";
        }

        if (inputText == null)
        {
            return;
        }

        string raw = inputText.text.Replace(",", "");

        if (raw.Contains("."))
        {
            return;
        }

        if (string.IsNullOrEmpty(raw))
        {
            raw = "0.";
        }
        else
        {
            raw += ".";
        }

        inputText.text = FormatInputWithSeparators(raw);
        UpdateTotalPriceDisplay();
    }

    // 🔹 ปุ่ม MAX: ใส่จำนวนหุ้นสูงสุดที่ซื้อได้ลง inputText
    void OnMaxPressed()
    {
        if (inputText == null) return;
        if (selectedSubAsset == null) return;

        var player = PlayerDataManager.Instance?.localPlayer;
        if (player == null) return;

        decimal pricePerShare = (selectedRuntime != null)
            ? (decimal)selectedRuntime.currentPrice
            : (decimal)selectedSubAsset.currentPrice;

        if (pricePerShare <= 0m) return;

        decimal wallet = player.money;
        if (wallet <= 0m) return;

        // คำนวนจำนวนหุ้นสูงสุดที่ซื้อได้ (ตัดทศนิยม 2 ตำแหน่ง ไม่ปัดขึ้น)
        decimal maxShares = wallet / pricePerShare;
        maxShares = Trunc2(maxShares);

        if (maxShares < 0.01m) return;

        // แปลงเป็น string แบบไม่ใส่คอมม่า แล้วค่อยไปฟอร์แมตให้มีคอมม่า
        string raw = maxShares.ToString("0.##"); // max 2 ทศนิยม
        inputText.text = FormatInputWithSeparators(raw);

        UpdateTotalPriceDisplay();
    }
    // ปุ่ม MAX SELL
    void OnMaxSellPressed()
    {
        if (inputText == null) return;
        if (selectedSubAsset == null) return;

        var player = PlayerDataManager.Instance?.localPlayer;
        if (player == null) return;

        // ดึงจำนวนหุ้นที่ผู้เล่นถืออยู่สำหรับ SubAsset ที่เลือก
        float sharesHeldFloat = player.GetOwnedAmount(selectedSubAsset);
        
        decimal sharesHeld = (decimal)sharesHeldFloat;
        
        if (sharesHeld <= 0m) return;
        
        // ตัดทศนิยม 2 ตำแหน่ง (ไม่ปัดเศษ) เพื่อให้เป็นไปตามกฎการเทรด (0.01)
        sharesHeld = Trunc2(sharesHeld); // Trunc2 คือ Helper ที่มีอยู่ใน CompanyUI แล้ว

        // แปลงเป็น string (max 2 ทศนิยม) แล้วฟอร์แมตให้มีคอมม่า
        string raw = sharesHeld.ToString("0.##"); // เช่น 1234.56
        inputText.text = FormatInputWithSeparators(raw);

        // อัปเดตราคารวม
        UpdateTotalPriceDisplay();
    }

    public void ShowSubAssetDetail(SubAssetData data)
    {
        if (data == null) return;

        if (companyNameText != null) companyNameText.text = data.assetName;
        if (description != null) description.text = data.description;
        if (companyIcon != null && data.assetIcon != null) companyIcon.sprite = data.assetIcon;
    }

    public void UpdateTotalPriceDisplay()
    {
        if (showPrice == null) return;

        // ถ้ายังไม่ได้เลือก SubAsset → เคลียร์ข้อความ + คืนสีเดิม
        if (selectedSubAsset == null)
        {
            showPrice.text = "";
            showPrice.color = defaultShowPriceColor;
            return;
        }

        decimal pricePerShare = (selectedRuntime != null)
            ? (decimal)selectedRuntime.currentPrice
            : (decimal)selectedSubAsset.currentPrice;

        var player = PlayerDataManager.Instance?.localPlayer;
        decimal playerMoney = player != null ? player.money : 0m;

        // ✅ ถ้า input ว่างหรือ parse ไม่ได้ → โชว์ราคา/หุ้น และใช้สีเดิม
        if (inputText == null || string.IsNullOrWhiteSpace(inputText.text))
        {
            showPrice.text = FormatNoRound2(pricePerShare);
            showPrice.color = defaultShowPriceColor;
            return;
        }

        // อ่านค่าจาก input โดยลบคอมม่าออกก่อน
        string raw = inputText.text.Replace(",", "");
        if (!float.TryParse(raw, out float qtyF) || qtyF <= 0f)
        {
            showPrice.text = FormatNoRound2(pricePerShare);
            showPrice.color = defaultShowPriceColor;
            return;
        }

        decimal qty = (decimal)qtyF;
        decimal total = qty * pricePerShare;

        // ✅ แสดงราคารวมแบบมีคอมม่า
        showPrice.text = FormatNoRound2(total);

        // ✅ เช็คว่าราคารวมเกินเงินที่มีไหม
        if (player != null && total > playerMoney)
        {
            // เกินเงิน → สีแดง
            if (ColorUtility.TryParseHtmlString("#B21D1D", out var red))
                showPrice.color = red;
            else
                showPrice.color = Color.red;
        }
        else
        {
            // ไม่เกิน หรือไม่มี player → ใช้สีเดิม
            showPrice.color = defaultShowPriceColor;
        }
    }
}
