using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BuyManager : MonoBehaviour
{
    private ParticleSystem tempVFX;
    public static BuyManager Instance;

    [Header("UI Result Panel")]
    public GameObject resultPanel;
    public Image resultImage;
    private Button resultButton;

    //List เก็บ Sprite ที่จะใช้แสดง
    [Header("Result Sprites")]
    [Tooltip("Sprite ที่ 0 = สำเร็จ, Sprite ที่ 1 = ไม่สำเร็จ (ซื้อ)")]
    public List<Sprite> successBuySprites;

    [Tooltip("Sprite ที่ 0 = สำเร็จ, Sprite ที่ 1 = ไม่สำเร็จ (ขาย)")]
    public List<Sprite> successSellSprites;

    // ✅ ขั้นต่ำต่อการซื้อขาย: 0.01 หุ้น
    private const float MIN_LOT = 0.01f;

    // ดึงรายชื่อผู้เล่นจากระบบกลาง (safety null checks)
    private List<PlayerData> Players => PlayerDataManager.Instance?.currentRoom?.Players;

    [Header("Refs")]
    [SerializeField] private InvestmentManager investmentManager;
    [SerializeField] private StaminaUI staminaUI;

    [Header("UI Reference")]
    public TMP_Text moneyText;
    public TMP_Text buyQuoteText;   // แสดงยอดจ่ายระหว่างพิมพ์ซื้อ
    public TMP_Text sellQuoteText;  // แสดงยอดรับระหว่างพิมพ์ขาย

    [Header("Active Selection (ตั้งตอนเปิดหน้าบริษัท/สินทรัพย์)")]
    public SubAssetData activeBuyAsset;
    public SubAssetData activeSellAsset;

    private int localPlayerId = 0;

    void Awake()
    {
        if (staminaUI == null)
        {
            staminaUI = FindObjectOfType<StaminaUI>();
            if (staminaUI == null)
            {
                Debug.LogError("[BuyManager] StaminaUI object is missing in the scene.");
            }
        }
        if (investmentManager == null)
        {
            investmentManager = FindObjectOfType<InvestmentManager>();
        }
        if (PlayerDataManager.Instance != null)
        {
            var local = PlayerDataManager.Instance.localPlayer;
            if (local != null)
            {
                localPlayerId = PlayerDataManager.Instance.players.IndexOf(local);
            }
        }
        // ✅ ผูก Button เข้ากับ resultImage (GameObject ที่แสดงรูป) โดยตรง
        if (resultImage != null)
        {
            // 1. ลองหา Button บน GameObject เดียวกับ resultImage
            resultButton = resultImage.GetComponent<Button>();

            // 2. ถ้าไม่มี Component Button ก็ Add เข้าไป
            if (resultButton == null)
            {
                // เพิ่ม Button ลงบน GameObject ของ Image ที่แสดงผล
                resultButton = resultImage.gameObject.AddComponent<Button>();

                // 💡 สำคัญ: กำหนด Image ที่มีอยู่แล้วให้เป็น Target Graphic ของปุ่ม
                // ทำให้ปุ่มรู้ว่าควรรับการคลิกตรงไหน
                resultButton.targetGraphic = resultImage;
            }

            // 3. ล้าง Event เก่าและผูก Event ใหม่
            resultButton.onClick.RemoveAllListeners();
            resultButton.onClick.AddListener(HideResultPanel);
        }
        // Button และผูก Event ให้กับ resultPanel โดยตรง (เพื่อให้กดปิดได้ทั้ง Panel)
        if (resultPanel != null)
        {
            // 1. ลองหา Button บน resultPanel
            Button panelButton = resultPanel.GetComponent<Button>();

            // 2. ถ้าไม่มี Component Button ก็ Add เข้าไป
            if (panelButton == null)
            {
                panelButton = resultPanel.AddComponent<Button>();

                // 💡 ตั้งค่า Target Graphic ให้เป็น Image หรือ Graphic อื่นๆ ของ Panel เอง
                // ถ้า Panel เป็น Image:
                Image panelImage = resultPanel.GetComponent<Image>();
                if (panelImage != null)
                {
                    panelButton.targetGraphic = panelImage;

                    // ⭐ สำคัญ: เพื่อให้ Panel กดได้ง่าย ควรตั้งค่าสีใส (A=0) 
                    // เพื่อไม่ให้รบกวนการแสดงผลของ resultImage ที่อยู่ข้างใน
                    Color tempColor = panelImage.color;
                    tempColor.a = 0.01f; // ให้มีค่า A น้อยที่สุดเพื่อให้เป็นพื้นที่คลิก
                    panelImage.color = tempColor;
                }
            }

            // 3. ผูก Event ปิด Panel
            panelButton.onClick.RemoveAllListeners();
            panelButton.onClick.AddListener(HideResultPanel);
        }
    }
    void Start()
    {
        if (PlayerDataManager.Instance != null)
        {
            var local = PlayerDataManager.Instance.localPlayer;
            if (local != null && PlayerDataManager.Instance.players != null)
            {
                localPlayerId = PlayerDataManager.Instance.players.IndexOf(local);
                Debug.Log($"[BuyManager] localPlayerId = {localPlayerId}, playerName = {local.playerName}");
            }
            else
            {
                Debug.LogWarning("[BuyManager] localPlayer หรือ players ยังไม่พร้อมตอน Start()");
            }
        }
        else
        {
            Debug.LogError("[BuyManager] PlayerDataManager.Instance เป็น null");
        }
    }

    private void OnEnable()
    {
        var plist = Players;
        if (plist != null && localPlayerId >= 0 && localPlayerId < plist.Count)
        {
            plist[localPlayerId].OnMoneyChanged += _ => UpdateMoneyUI();
            plist[localPlayerId].OnHoldingChanged += (_, __) => UpdateMoneyUI();
        }
    }

    private void OnDisable()
    {
        var plist = Players;
        if (plist != null && localPlayerId >= 0 && localPlayerId < plist.Count)
        {
            plist[localPlayerId].OnMoneyChanged -= _ => UpdateMoneyUI();
            plist[localPlayerId].OnHoldingChanged -= (_, __) => UpdateMoneyUI();
        }
    }

    private void Update()
    {
        UpdateMoneyUI();
    }

    // ========= Active selection helpers =========
    public void SetActiveBuyAsset(SubAssetData sa) { activeBuyAsset = sa; }
    public void SetActiveSellAsset(SubAssetData sa) { activeSellAsset = sa; }

    // ========= Handlers ผูกกับ TMP_InputField.OnValueChanged =========
    public void OnBuyAmountChanged(string text)
    {
        float shares = ParseShares(text);
        PreviewBuy(activeBuyAsset, shares);
    }

    public void OnSellAmountChanged(string text)
    {
        float shares = ParseShares(text);
        PreviewSell(activeSellAsset, shares, checkHolding: true);
    }

    private float ParseShares(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return 0f;

        // รองรับผู้ใช้ใส่คอมม่า/ช่องว่าง
        s = s.Trim().Replace(" ", "");
        // ใช้ . เป็นทศนิยมเสมอ (InvariantCulture)
        if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
            return v;
        // สำรอง: ลองลบคอมม่า
        s = s.Replace(",", "");
        if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v))
            return v;

        return 0f;
    }

    // ===========================
    // ✅ Preview (เรียลไทม์)
    // ===========================
    public void PreviewBuy(SubAssetData subAsset, float sharesInput)
    {
        if (buyQuoteText == null) return;

        if (subAsset == null)
        {
            buyQuoteText.text = "—";
            return;
        }

        float price = GetFlooredRuntimePrice(subAsset);
        if (price <= 0f)
        {
            buyQuoteText.text = "—";
            return;
        }

        float shares = Floor2(sharesInput);
        if (shares <= 0f)
        {
            buyQuoteText.text = "ใส่จำนวนหุ้น";
            return;
        }
        if (shares < MIN_LOT)
        {
            buyQuoteText.text = $"ต่ำสุด {MIN_LOT:0.00} หุ้น";
            return;
        }

        float cost = shares * price;
        buyQuoteText.text = $"ต้องจ่าย {cost:N2} ฿  ( {shares:0.00} × {price:N2} )";
    }

    public void PreviewSell(SubAssetData subAsset, float sharesInput, bool checkHolding)
    {
        if (sellQuoteText == null) return;

        if (subAsset == null)
        {
            sellQuoteText.text = "—";
            return;
        }

        float price = GetFlooredRuntimePrice(subAsset);
        if (price <= 0f)
        {
            sellQuoteText.text = "—";
            return;
        }

        float shares = Floor2(sharesInput);
        if (shares <= 0f)
        {
            sellQuoteText.text = "ใส่จำนวนหุ้น";
            return;
        }
        if (shares < MIN_LOT)
        {
            sellQuoteText.text = $"ต่ำสุด {MIN_LOT:0.00} หุ้น";
            return;
        }

        // เช็คจำนวนหุ้นที่ผู้เล่นถืออยู่ (เฉพาะ local player)
        var plist = Players;
        var player = (plist != null && localPlayerId >= 0 && localPlayerId < plist.Count) ? plist[localPlayerId] : null;

        if (checkHolding && player != null)
        {
            float owned = player.GetOwnedAmount(subAsset);
            float ownedFloor = Floor2(owned);

            if (ownedFloor <= 0f)
            {
                sellQuoteText.text = "ไม่มีหุ้นขาย";
                return;
            }

            if (shares > ownedFloor)
            {
                float valueMax = ownedFloor * price;
                sellQuoteText.text = $"ถืออยู่ {ownedFloor:0.00} หุ้น • ขายได้สูงสุด {ownedFloor:0.00}\nจะได้เงิน {valueMax:N2} ฿ (ถ้าขายสูงสุด)";
                return;
            }
        }

        float value = shares * price;
        sellQuoteText.text = $"จะได้เงิน {value:N2} ฿  ( {shares:0.00} × {price:N2} )";
    }

    // ===========================
    // ซื้อ/ขายจริง (ขั้นต่ำ 0.01, ปัดลง 2 ตำแหน่ง)
    // ===========================
    public void BuySubAsset(int playerId, SubAssetData subAsset, float shares, ParticleSystem vfxToPlay)
    {
        var plist = Players;
        if (plist == null || playerId < 0 || playerId >= plist.Count)
        {
            Debug.LogError("[BuyManager] BuySubAsset: Player list / playerId ไม่ถูกต้อง");
            return;
        }

        // ✅ เช็ค Stamina ก่อน ถ้าไม่พอ → ไม่ส่งคำสั่งซื้อ, แสดง panel + กระพริบแดง
        if (staminaUI != null && !staminaUI.TryUseStamina(1))
        {
            Debug.Log("[BuyManager] ซื้อไม่สำเร็จ: Stamina ไม่พอ");
            return;
        }

        shares = Floor2(shares);
        if (shares < MIN_LOT)
        {
            Debug.LogError($"[BuyManager] BuySubAsset: จำนวนหุ้นต้อง ≥ {MIN_LOT:0.00}");
            if (buyQuoteText) buyQuoteText.text = $"ต่ำสุด {MIN_LOT:0.00} หุ้น";
            return;
        }
        float price = GetFlooredRuntimePrice(subAsset);
        if (price <= 0f)
        {
            Debug.LogError("BuySubAsset: ราคาไม่ถูกต้อง");
            if (buyQuoteText) buyQuoteText.text = "—";
            return;
        }

        long sharesLong = (long)Mathf.Floor(shares * 100f);
        int sharesInt = (int)Mathf.Clamp(sharesLong, 0, int.MaxValue);
        var player = plist[playerId];

        if (investmentManager != null)
        {
            tempVFX = vfxToPlay;
            investmentManager.TryBuyStock(subAsset, sharesInt, player.playerName);

            Debug.Log($"[BuyManager] ส่งคำสั่งซื้อ {subAsset.assetName} x{sharesInt} ไป Host");
        }
    }

    public void SellSubAsset(int playerId, SubAssetData subAsset, float shares, ParticleSystem vfxToPlay)
    {
        var plist = Players;
        if (plist == null || playerId < 0 || playerId >= plist.Count)
        {
            Debug.LogError("[BuyManager] SellSubAsset: Player list / playerId ไม่ถูกต้อง");
            return;
        }

        // ✅ เช็ค Stamina ก่อนขาย
        if (staminaUI != null && !staminaUI.TryUseStamina(1))
        {
            Debug.Log("[BuyManager] ขายไม่สำเร็จ: Stamina ไม่พอ");
            return;
        }

        shares = Floor2(shares);
        if (shares < MIN_LOT)
        {
            Debug.LogError($"[BuyManager] SellSubAsset: จำนวนหุ้นต้อง ≥ {MIN_LOT:0.00}");
            if (sellQuoteText) sellQuoteText.text = $"ต่ำสุด {MIN_LOT:0.00} หุ้น";
            return;
        }
        float price = GetFlooredRuntimePrice(subAsset);
        if (price <= 0f)
        {
            Debug.LogError("SellSubAsset: ราคาไม่ถูกต้อง");
            if (sellQuoteText) sellQuoteText.text = "—";
            return;
        }

        int sharesIntFixed = Mathf.FloorToInt(shares * 100f);
        var player = plist[playerId];

        if (investmentManager != null)
        {
            tempVFX = vfxToPlay;
            investmentManager.TrySellStock(subAsset, sharesIntFixed, player.playerName);

            Debug.Log($"[BuyManager] ส่งคำสั่งขาย {subAsset.assetName} x{sharesIntFixed} (fixed) ไป Host");
        }
    }


    // ========= Overload int (เดิม) =========
    public void BuySubAsset(int playerId, SubAssetData subAsset, int sharesInt)
    {
        if (sharesInt < 1) { Debug.LogError("BuySubAsset(int): ต้อง ≥ 1"); return; }
        BuySubAsset(playerId, subAsset, (float)sharesInt, null);
    }

    public void SellSubAsset(int playerId, SubAssetData subAsset, int sharesInt)
    {
        if (sharesInt < 1) { Debug.LogError("SellSubAsset(int): ต้อง ≥ 1"); return; }
        SellSubAsset(playerId, subAsset, (float)sharesInt, null);
    }

    // ========= Legacy wrappers =========
    public void BuyOneShare(int playerId, InvestmentCompanyRuntime company)
    {
        if (!ValidateCompanyCommon(company, playerId)) return;
        var defaultSub = company.data.subAssets.FirstOrDefault();
        if (defaultSub == null) { Debug.LogError("BuyOneShare: ไม่มี SubAsset ให้ซื้อ"); return; }
        BuySubAsset(playerId, defaultSub, 1f, null);
    }

    public void BuyShares(int playerId, InvestmentCompanyRuntime company, int shares)
    {
        if (!ValidateCompanyCommon(company, playerId)) return;
        if (shares <= 0) { Debug.LogError("BuyShares: จำนวนหุ้นต้องมากกว่า 0"); return; }
        var defaultSub = company.data.subAssets.FirstOrDefault();
        if (defaultSub == null) { Debug.LogError("BuyShares: ไม่มี SubAsset ให้ซื้อ"); return; }
        BuySubAsset(playerId, defaultSub, (float)shares, null);
    }

    public void SellOneShare(int playerId, InvestmentCompanyRuntime company)
    {
        if (!ValidateCompanyCommon(company, playerId)) return;
        var defaultSub = company.data.subAssets.FirstOrDefault();
        if (defaultSub == null) { Debug.LogError("SellOneShare: ไม่มี SubAsset ให้ขาย"); return; }
        SellSubAsset(playerId, defaultSub, 1f, null);
    }

    public void SellShares(int playerId, InvestmentCompanyRuntime company, int shares)
    {
        if (!ValidateCompanyCommon(company, playerId)) return;
        if (shares <= 0) { Debug.LogError("SellShares: จำนวนหุ้นที่ขายต้องมากกว่า 0"); return; }
        var defaultSub = company.data.subAssets.FirstOrDefault();
        if (defaultSub == null) { Debug.LogError("SellShares: ไม่มี SubAsset ให้ขาย"); return; }
        SellSubAsset(playerId, defaultSub, (float)shares, null);
    }

    public void BuyStock(int playerId, InvestmentCompanyRuntime company, float amountTHB)
    {
        if (!ValidateCompanyCommon(company, playerId)) return;
        if (amountTHB <= 0f) { Debug.LogError("BuyStock: จำนวนเงินต้องมากกว่า 0"); return; }

        var defaultSub = company.data.subAssets.FirstOrDefault();
        if (defaultSub == null) { Debug.LogError("BuyStock: ไม่มี SubAsset ให้ซื้อ"); return; }

        float price = GetFlooredRuntimePrice(defaultSub);
        float shares = Floor2(amountTHB / price);
        if (shares < MIN_LOT)
        {
            Debug.Log($"⚠️ เงินไม่พอซื้อขั้นต่ำ {MIN_LOT:0.00} หุ้น");
            if (buyQuoteText) buyQuoteText.text = $"ต่ำสุด {MIN_LOT:0.00} หุ้น";
            return;
        }

        BuySubAsset(playerId, defaultSub, shares, null);
    }

    public void SellStock(int playerId, InvestmentCompanyRuntime company, float sharesToSellFloat)
    {
        if (!ValidateCompanyCommon(company, playerId)) return;

        sharesToSellFloat = Floor2(sharesToSellFloat);
        if (sharesToSellFloat < MIN_LOT)
        {
            Debug.LogError($"SellStock: จำนวนหุ้นที่ขายต้อง ≥ {MIN_LOT:0.00}");
            if (sellQuoteText) sellQuoteText.text = $"ต่ำสุด {MIN_LOT:0.00} หุ้น";
            return;
        }

        var defaultSub = company.data.subAssets.FirstOrDefault();
        if (defaultSub == null) { Debug.LogError("SellStock: ไม่มี SubAsset ให้ขาย"); return; }

        SellSubAsset(playerId, defaultSub, sharesToSellFloat, null);
    }

    // ========= Validation =========
    private bool ValidateCompanyCommon(InvestmentCompanyRuntime company, int playerId)
    {
        var plist = Players;
        if (plist == null || playerId < 0 || playerId >= plist.Count)
        {
            Debug.LogError("ValidateCompanyCommon: Player ID ไม่ถูกต้อง");
            return false;
        }
        if (company == null || company.data == null)
        {
            Debug.LogError("ValidateCompanyCommon: Company ไม่ถูกต้อง");
            return false;
        }
        if (company.data.subAssets == null || company.data.subAssets.Count == 0)
        {
            Debug.LogError("ValidateCompanyCommon: Company ไม่มี SubAssets");
            return false;
        }
        if (company.data.subAssets[0].currentPrice <= 0f)
        {
            Debug.LogError("ValidateCompanyCommon: ราคาของ SubAsset ไม่ถูกต้อง");
            return false;
        }
        return true;
    }

    // ========= UI =========
    private void UpdateMoneyUI()
    {
        var plist = Players;
        if (plist == null || localPlayerId < 0 || localPlayerId >= plist.Count) return;

        var player = plist[localPlayerId];
        if (moneyText != null)
            moneyText.text = player.MoneyToString();
    }

    // ========= Helpers =========
    private float GetFlooredRuntimePrice(SubAssetData subAsset)
    {
        if (subAsset == null) return 0f;
        var rt = investmentManager?.activeCompanies.FirstOrDefault(c => c.subAsset == subAsset);
        float raw = (rt != null ? rt.currentPrice : subAsset.currentPrice);
        return Floor2(Mathf.Max(0.01f, raw));
    }

    private static float Floor2(float v) => Mathf.Floor(v * 100f) / 100f;

    // 🔹 ฟังก์ชันแสดงข้อความ
    public void ShowResultMessage(bool success, bool isBuy)
    {
        if (resultPanel == null || resultImage == null)
        {
            Debug.LogError("[BuyManager] resultPanel/resultImage เป็น null! in Inspector");
            return;
        }

        if (!resultPanel.activeSelf)
        {
            resultPanel.SetActive(true);
        }

        // List ที่จะใช้ (ซื้อ หรือ ขาย)
        List<Sprite> targetSprites = isBuy ? successBuySprites : successSellSprites;

        if (targetSprites == null || targetSprites.Count < 2)
        {
            Debug.LogError($"[BuyManager] Target Sprites List สำหรับ {(isBuy ? "ซื้อ" : "ขาย")} ไม่สมบูรณ์ (ต้องมี 2 รูป)");
            return;
        }

        // Index 0 = สำเร็จ (success=true)
        // Index 1 = ไม่สำเร็จ (success=false)
        int spriteIndex = success ? 0 : 1;
        resultImage.sprite = targetSprites[spriteIndex];

        // แสดงผล Panel
        Debug.Log($"[BuyManager] ShowResultMessage: {(success ? "SUCCESS" : "FAIL")} | IsBuy: {isBuy}");
        resultPanel.SetActive(true);
        resultPanel.transform.SetAsLastSibling();
    }

    private void HideResultPanel()
    {
        resultPanel.SetActive(false);
    }

    public void ReceiveTradeResultFromHost(bool success, bool isBuy, string assetName, float costIncome)
    {
        Debug.Log($"[BUY MANAGER RECEIVE] Called. Success: {success}, IsBuy: {isBuy}, Cost: {costIncome}");

        var player = PlayerDataManager.Instance.localPlayer;
        if (player == null) return;

        // หา runtime + subAsset ที่เกี่ยวข้อง
        var rt = investmentManager?.activeCompanies
            .FirstOrDefault(x => x != null && x.subAsset != null && x.subAsset.assetName == assetName);

        var subAsset = rt != null ? rt.subAsset : null;

        if (success)
        {
            Debug.Log($"[Trade Result] {(isBuy ? "ซื้อ" : "ขาย")} {assetName} สำเร็จ! | {(isBuy ? "Cost" : "Income")}: {costIncome:N2}");

            // 🔹 ตัด stamina ไปแล้วตอนกดคำสั่งซื้อ/ขาย (ใน BuySubAsset/SellSubAsset)
            // ที่นี่เลยไม่ต้องตัดซ้ำ

            // เล่น VFX
            if (tempVFX != null) tempVFX.Play();

            // อัปเดตข้อความ quote
            if (isBuy)
            {
                if (buyQuoteText) buyQuoteText.text = $"ชำระแล้ว {costIncome:N2} ฿";
            }
            else
            {
                if (sellQuoteText) sellQuoteText.text = $"ได้รับ {costIncome:N2} ฿";
            }

            // อัปเดตเงินสด
            UpdateMoneyUI();

            // อัปเดตจำนวนหุ้นใน SubAssetUI
            if (subAsset != null)
            {
                var ui = FindObjectsOfType<SubAssetUI>()
                         .FirstOrDefault(x => x != null && x.subAsset == subAsset);
                if (ui != null)
                    ui.UpdateOwnedAmount(player.GetOwnedAmount(subAsset));
            }

            // ✅ รีเฟรช BankGoldUI ทุกตัวในซีน ให้ยอดฝากอัปเดตทันที
            foreach (var bankUi in FindObjectsOfType<BankGoldUI>())
            {
                bankUi.UpdateUI();
            }
        }
        else
        {
            Debug.LogWarning($"[Trade Result] {(isBuy ? "ซื้อ" : "ขาย")} {assetName} ล้มเหลว! (Host ปฏิเสธ)");

            if (isBuy)
            {
                if (buyQuoteText) buyQuoteText.text = "⚠️ เงินไม่พอ! ทำรายการไม่สำเร็จ";
            }
            else
            {
                if (sellQuoteText) sellQuoteText.text = "⚠️ หุ้นไม่พอ! ทำรายการไม่สำเร็จ";
            }
        }

        // แสดงรูปผลลัพธ์
        ShowResultMessage(success, isBuy);

        // ล้าง VFX ชั่วคราว
        tempVFX = null;

        // เคลียร์ input ของ CompanyUI ปกติ (ถ้ามี)
        var companyUI = FindObjectOfType<CompanyUI>();
        if (companyUI != null)
        {
            if (companyUI.inputText != null)
                companyUI.inputText.text = "";

            if (isBuy) companyUI.UpdateTotalPriceDisplay();
        }
    }
}
