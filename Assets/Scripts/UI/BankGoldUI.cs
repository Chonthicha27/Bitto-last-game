/*
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;
using System.Linq;

public class BankGoldUI : MonoBehaviour
{
    public Image characterSprite;
    
    private const string BANK_ASSET_NAME = "เงินฝากธนาคาร";

    [Header("Description")]
    public TextMeshProUGUI descriptionText;

    [Header("Amount Texts")]
    public TextMeshProUGUI depositAmountText;   // ✅ ยอดรวมที่ฝากไว้ใน Bank
    public TextMeshProUGUI withdrawAmountText;  // ✅ จำนวนเงินที่ผู้เล่นกำลังจะฝาก/ถอน (ตามที่พิมพ์)

    [Header("Buttons")]
    public Button depositButton;
    public Button withdrawButton;
    public Button depositAllButton;
    public Button withdrawAllButton;
    public Button closeButton;

    [Header("Numpad")]
    public TextMeshProUGUI inputText;
    public Button[] numberButtons;
    public Button decimalButton;
    public Button clearButton;

    [Header("Bank Asset")]
    [Tooltip("ลาก SubAsset ของ Bank มาใส่ตรงนี้ (ต้องเป็นตัวเดียวกับใน InvestmentCompany)")]
    public SubAssetData bankSubAsset;

    [Tooltip("อ้างอิง InvestmentManager ในซีน")]
    public InvestmentManager investmentManager;

    private PlayerData localPlayer;
    private BuyManager buyManager;
    private int playerId;

    // ==========================
    //        LIFECYCLE
    // ==========================
    void Awake()
    {
        localPlayer = PlayerDataManager.Instance.localPlayer;
        buyManager = FindAnyObjectByType<BuyManager>();

        if (investmentManager == null)
        {
            investmentManager = InvestmentManager.Instance ?? FindObjectOfType<InvestmentManager>();
        }

        if (localPlayer != null && PlayerDataManager.Instance.players != null)
        {
            playerId = PlayerDataManager.Instance.players.IndexOf(localPlayer);

            // ✅ subscribe ให้ BankGoldUI รีเฟรชทุกครั้งที่ holding เปลี่ยน
            localPlayer.OnHoldingChanged += OnPlayerHoldingChanged;
        }

        // ✅ ถ้า bankSubAsset ยังไม่ถูกเซ็ต ลองหาใน holdings จากชื่อ "เงินฝากธนาคาร"
        AutoBindBankSubAsset();

        SetupNumpad();
        SetupButtons();
        UpdateUI();
    }

    void OnDestroy()
    {
        if (localPlayer != null)
        {
            localPlayer.OnHoldingChanged -= OnPlayerHoldingChanged;
        }
    }

    private void OnPlayerHoldingChanged(SubAssetData asset, float shares)
    {
        if (asset == null) return;

        // ✅ ถ้า asset ที่เปลี่ยนชื่อ "เงินฝากธนาคาร" ให้รีเฟรช UI
        if (asset.assetName == BANK_ASSET_NAME)
        {
            // ถ้า bankSubAsset ยังไม่ได้เซ็ต ให้ใช้ตัวนี้เลย
            if (bankSubAsset == null)
                bankSubAsset = asset;

            UpdateUI();
        }
    }

    void OnEnable()
    {
        if (inputText != null)
            inputText.text = "0";

        if (withdrawAmountText != null)
            withdrawAmountText.text = "0";

        // เผื่อมี instance ใหม่เกิดขึ้นโดยที่ bankSubAsset ยังไม่ถูกเซ็ต
        AutoBindBankSubAsset();

        UpdateUI();
    }

    // ---------- UI ปิด ----------
    private void OnCloseButtonClicked()
    {
        PlayButtonClick();
        gameObject.SetActive(false);
    }

    // ---------- เสียง ----------
    private void PlayButtonClick()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.Play(AudioManager.SoundType.Button);
        }
    }

    private void PlayTradeSfx()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.Play(AudioManager.SoundType.Event);
        }
    }

    // ==========================
    //    AUTO BIND BANK ASSET
    // ==========================
    void AutoBindBankSubAsset()
    {
        if (bankSubAsset != null) return;
        if (localPlayer == null || localPlayer.holdings == null) return;

        var h = localPlayer.holdings
            .FirstOrDefault(x => x != null && x.subAsset != null && x.subAsset.assetName == BANK_ASSET_NAME);

        if (h != null)
        {
            bankSubAsset = h.subAsset;
            Debug.Log($"[BankGoldUI] AutoBind bankSubAsset = {bankSubAsset.assetName}");
        }
    }

    // ==========================
    //         NUMPAD
    // ==========================
    void SetupNumpad()
    {
        if (inputText) inputText.text = "0";
        if (withdrawAmountText != null) withdrawAmountText.text = "0";

        if (numberButtons != null)
        {
            foreach (var btn in numberButtons)
            {
                int digit = int.Parse(btn.GetComponentInChildren<TextMeshProUGUI>().text);
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() =>
                {
                    PlayButtonClick();
                    OnNumberPressed(digit);
                });
            }
        }

        if (decimalButton != null)
        {
            decimalButton.onClick.RemoveAllListeners();
            decimalButton.onClick.AddListener(() =>
            {
                PlayButtonClick();
                OnDecimalPressed();
            });
        }

        if (clearButton != null)
        {
            clearButton.onClick.RemoveAllListeners();
            clearButton.onClick.AddListener(() =>
            {
                PlayButtonClick();
                if (!string.IsNullOrEmpty(inputText.text))
                {
                    string raw = inputText.text.Replace(",", "");
                    if (raw.Length > 0)
                    {
                        raw = raw.Substring(0, raw.Length - 1);
                    }
                    inputText.text = FormatInput(raw);

                    if (withdrawAmountText != null)
                    {
                        withdrawAmountText.text = inputText.text;
                    }
                }
            });
        }
    }

    void OnNumberPressed(int digit)
    {
        if (inputText == null) return;
        string raw = inputText.text.Replace(",", "");

        if (raw == "0")
            raw = "";

        if (raw.Length >= 15) return;

        raw += digit.ToString();
        inputText.text = FormatInput(raw);

        if (withdrawAmountText != null)
        {
            withdrawAmountText.text = inputText.text;
        }
    }

    void OnDecimalPressed()
    {
        if (inputText == null) return;

        string raw = inputText.text.Replace(",", "");
        if (raw.Contains(".")) return;

        if (string.IsNullOrEmpty(raw))
            raw = "0.";
        else
            raw += ".";

        inputText.text = FormatInput(raw);

        if (withdrawAmountText != null)
        {
            withdrawAmountText.text = inputText.text;
        }
    }

    string FormatInput(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "0";

        string integerPart = raw;
        string fractionPart = "";
        int dotIndex = raw.IndexOf('.');
        if (dotIndex >= 0)
        {
            integerPart = raw.Substring(0, dotIndex);
            fractionPart = raw.Substring(dotIndex);
        }

        if (string.IsNullOrEmpty(integerPart))
            integerPart = "0";

        if (long.TryParse(integerPart, out long val))
        {
            return val.ToString("#,0") + fractionPart;
        }

        return raw;
    }

    float ParseInput()
    {
        if (inputText == null) return 0f;
        string raw = inputText.text.Replace(",", "");
        if (float.TryParse(raw, out float val))
            return val;
        return 0f;
    }

    // ==========================
    //       BUTTON LOGIC
    // ==========================
    void SetupButtons()
    {
        depositButton.onClick.RemoveAllListeners();
        depositButton.onClick.AddListener(() =>
        {
            PlayTradeSfx();
            OnDepositClicked();
        });

        withdrawButton.onClick.RemoveAllListeners();
        withdrawButton.onClick.AddListener(() =>
        {
            PlayTradeSfx();
            OnWithdrawClicked();
        });

        depositAllButton.onClick.RemoveAllListeners();
        depositAllButton.onClick.AddListener(() =>
        {
            PlayTradeSfx();
            OnDepositAllClicked();
        });

        withdrawAllButton.onClick.RemoveAllListeners();
        withdrawAllButton.onClick.AddListener(() =>
        {
            PlayTradeSfx();
            OnWithdrawAllClicked();
        });

        closeButton?.onClick.RemoveAllListeners();
        closeButton?.onClick.AddListener(OnCloseButtonClicked);
    }

    // ==========================
    //   BANK VALUE / BALANCE
    // ==========================
    float GetBankPrice()
    {
        if (bankSubAsset == null) return 1f;

        float price = 0f;

        if (investmentManager != null && investmentManager.activeCompanies != null)
        {
            var rt = investmentManager.activeCompanies
                .FirstOrDefault(r => r != null && r.subAsset != null && r.subAsset == bankSubAsset);

            if (rt != null)
                price = rt.currentPrice;
        }

        if (price <= 0f)
            price = bankSubAsset.currentPrice;

        if (price <= 0f)
            price = 1f;

        return price;
    }

    // ❗ ดึงจำนวนหุ้นของ “เงินฝากธนาคาร” จาก holdings โดยดูชื่อ
    float GetOwnedBankShares()
    {
        if (localPlayer == null || localPlayer.holdings == null) return 0f;

        float total = 0f;
        foreach (var h in localPlayer.holdings)
        {
            if (h != null && h.subAsset != null && h.subAsset.assetName == BANK_ASSET_NAME)
            {
                total += h.shares;
            }
        }

        Debug.Log($"[BankGoldUI] GetOwnedBankShares: {total} shares of {BANK_ASSET_NAME}");
        return total;
    }

    decimal GetBankBalance()
    {
        float shares = GetOwnedBankShares();
        if (shares <= 0f) return 0m;

        float price = GetBankPrice();
        decimal val = (decimal)shares * (decimal)price;
        val = Math.Truncate(val * 100m) / 100m;

        Debug.Log($"[BankGoldUI] GetBankBalance = {val} (shares={shares}, price={price})");
        return val;
    }

    // ==========================
    //      DEPOSIT / WITHDRAW
    // ==========================
    void OnDepositClicked()
    {
        if (localPlayer == null || buyManager == null) return;

        AutoBindBankSubAsset();
        if (bankSubAsset == null) return;

        float moneyInput = ParseInput();
        if (moneyInput <= 0f) return;

        decimal wallet = localPlayer.money;
        decimal want = (decimal)moneyInput;
        if (want > wallet) want = wallet;
        if (want <= 0m) return;

        float bankPrice = GetBankPrice();
        decimal sharesDec = want / (decimal)bankPrice;
        sharesDec = Math.Truncate(sharesDec * 100m) / 100m;

        float sharesToBuy = (float)sharesDec;
        if (sharesToBuy < 0.01f) return;

        buyManager.BuySubAsset(playerId, bankSubAsset, sharesToBuy, null);

        if (inputText != null) inputText.text = "0";
        if (withdrawAmountText != null) withdrawAmountText.text = "0";
    }

    void OnWithdrawClicked()
    {
        if (localPlayer == null || buyManager == null) return;

        AutoBindBankSubAsset();
        if (bankSubAsset == null) return;

        float moneyInput = ParseInput();
        if (moneyInput <= 0f) return;

        decimal bankBalance = GetBankBalance();
        if (bankBalance <= 0m) return;

        decimal want = (decimal)moneyInput;
        if (want > bankBalance) want = bankBalance;
        if (want <= 0m) return;

        float bankPrice = GetBankPrice();
        decimal sharesDec = want / (decimal)bankPrice;
        sharesDec = Math.Truncate(sharesDec * 100m) / 100m;

        float sharesToSell = (float)sharesDec;
        if (sharesToSell < 0.01f) return;

        buyManager.SellSubAsset(playerId, bankSubAsset, sharesToSell, null);

        if (inputText != null) inputText.text = "0";
        if (withdrawAmountText != null) withdrawAmountText.text = "0";
    }

    void OnDepositAllClicked()
    {
        if (localPlayer == null || buyManager == null) return;

        AutoBindBankSubAsset();
        if (bankSubAsset == null) return;

        decimal wallet = localPlayer.money;
        if (wallet <= 0m) return;

        float bankPrice = GetBankPrice();
        decimal sharesDec = wallet / (decimal)bankPrice;
        sharesDec = Math.Truncate(sharesDec * 100m) / 100m;

        float sharesToBuy = (float)sharesDec;
        if (sharesToBuy < 0.01f) return;

        buyManager.BuySubAsset(playerId, bankSubAsset, sharesToBuy, null);

        if (inputText != null) inputText.text = "0";
        if (withdrawAmountText != null) withdrawAmountText.text = "0";
    }

    void OnWithdrawAllClicked()
    {
        if (localPlayer == null || buyManager == null) return;

        AutoBindBankSubAsset();
        if (bankSubAsset == null) return;

        float sharesOwned = GetOwnedBankShares();
        if (sharesOwned <= 0f) return;

        buyManager.SellSubAsset(playerId, bankSubAsset, sharesOwned, null);

        if (inputText != null) inputText.text = "0";
        if (withdrawAmountText != null) withdrawAmountText.text = "0";
    }

    // ==========================
    //       UPDATE UI
    // ==========================
    public void UpdateUI()
    {
        if (localPlayer == null)
            localPlayer = PlayerDataManager.Instance.localPlayer;

        if (localPlayer == null) return;

        if (depositAmountText != null)
        {
            decimal bankVal = GetBankBalance();
            depositAmountText.text = FormatMoney(bankVal);
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
                
                img.rectTransform.localScale = img.rectTransform.localScale;
            }
            else
            {
                Debug.LogWarning($"❌ ไม่พบ Sprite สำหรับ characterSpriteIndex={localPlayer.characterSpriteIndex}");
            }
        }
    }

    string FormatMoney(decimal v)
    {
        v = Math.Truncate(v * 100m) / 100m;
        return v.ToString("#,0.00");
    }
}
*/

using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;
using System.Linq;

public class BankGoldUI : MonoBehaviour
{
    public Image characterSprite;

    private const string BANK_ASSET_NAME = "เงินฝากธนาคาร";
    private const string PLACEHOLDER = "ใส่จำนวนเงิน";

    [Header("Description")]
    public TextMeshProUGUI descriptionText;

    [Header("Amount Texts")]
    public TextMeshProUGUI depositAmountText;
    public TextMeshProUGUI withdrawAmountText;

    [Header("Buttons")]
    public Button depositButton;
    public Button withdrawButton;
    public Button depositAllButton;
    public Button withdrawAllButton;
    public Button closeButton;

    [Header("Numpad")]
    public TextMeshProUGUI inputText;
    public Button[] numberButtons;
    public Button decimalButton;
    public Button clearButton;

    [Header("Bank Asset")]
    public SubAssetData bankSubAsset;
    public InvestmentManager investmentManager;

    private PlayerData localPlayer;
    private BuyManager buyManager;
    private int playerId;

    // ------------------------
    // LIFECYCLE
    // ------------------------
    void Awake()
    {
        localPlayer = PlayerDataManager.Instance.localPlayer;
        buyManager = FindAnyObjectByType<BuyManager>();

        if (investmentManager == null)
            investmentManager = InvestmentManager.Instance ?? FindObjectOfType<InvestmentManager>();

        if (localPlayer != null && PlayerDataManager.Instance.players != null)
        {
            playerId = PlayerDataManager.Instance.players.IndexOf(localPlayer);
            localPlayer.OnHoldingChanged += OnPlayerHoldingChanged;
        }

        AutoBindBankSubAsset();

        SetupNumpad();
        SetupButtons();
        UpdateUI();
    }

    void OnEnable()
    {
        SetPlaceholder();
        AutoBindBankSubAsset();
        UpdateUI();
    }

    void OnDestroy()
    {
        if (localPlayer != null)
            localPlayer.OnHoldingChanged -= OnPlayerHoldingChanged;
    }

    private void OnPlayerHoldingChanged(SubAssetData asset, float shares)
    {
        if (asset == null) return;

        if (asset.assetName == BANK_ASSET_NAME)
        {
            if (bankSubAsset == null)
                bankSubAsset = asset;

            UpdateUI();
        }
    }

    // ------------------------
    // PLACEHOLDER
    // ------------------------
    private void SetPlaceholder()
    {
        if (inputText != null) inputText.text = PLACEHOLDER;
        if (withdrawAmountText != null) withdrawAmountText.text = PLACEHOLDER;
    }

    private bool IsPlaceholder(string s)
    {
        return s == PLACEHOLDER || string.IsNullOrEmpty(s);
    }

    // ------------------------
    // AUTO BIND BANK ASSET
    // ------------------------
    void AutoBindBankSubAsset()
    {
        if (bankSubAsset != null) return;
        if (localPlayer == null || localPlayer.holdings == null) return;

        var h = localPlayer.holdings.FirstOrDefault(x =>
            x != null && x.subAsset != null && x.subAsset.assetName == BANK_ASSET_NAME);

        if (h != null)
        {
            bankSubAsset = h.subAsset;
            Debug.Log($"[BankGoldUI] AutoBind bankSubAsset = {bankSubAsset.assetName}");
        }
    }

    // ------------------------
    // NUMPAD
    // ------------------------
    void SetupNumpad()
    {
        SetPlaceholder();

        // number buttons
        foreach (var btn in numberButtons)
        {
            int digit = int.Parse(btn.GetComponentInChildren<TextMeshProUGUI>().text);
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() =>
            {
                PlayButtonClick();
                OnNumberPressed(digit);
            });
        }

        // decimal button
        if (decimalButton != null)
        {
            decimalButton.onClick.RemoveAllListeners();
            decimalButton.onClick.AddListener(() =>
            {
                PlayButtonClick();
                OnDecimalPressed();
            });
        }

        // clear button
        if (clearButton != null)
        {
            clearButton.onClick.RemoveAllListeners();
            clearButton.onClick.AddListener(() =>
            {
                PlayButtonClick();
                OnClearPressed();
            });
        }
    }

    void OnNumberPressed(int digit)
    {
        if (inputText == null) return;

        string raw = inputText.text;

        if (IsPlaceholder(raw))
            raw = "";
        else
            raw = raw.Replace(",", "");

        if (raw.Length >= 15) return;

        raw += digit.ToString();
        string formatted = FormatInput(raw);

        inputText.text = formatted;
        if (withdrawAmountText) withdrawAmountText.text = formatted;
    }

    void OnDecimalPressed()
    {
        if (inputText == null) return;

        string raw = inputText.text;

        if (IsPlaceholder(raw))
            raw = "0";
        else
            raw = raw.Replace(",", "");

        if (raw.Contains(".")) return;

        raw += ".";

        string formatted = FormatInput(raw);

        inputText.text = formatted;
        if (withdrawAmountText) withdrawAmountText.text = formatted;
    }

    void OnClearPressed()
    {
        if (inputText == null) return;

        if (IsPlaceholder(inputText.text))
        {
            SetPlaceholder();
            return;
        }

        string raw = inputText.text.Replace(",", "");

        if (raw.Length <= 1)
        {
            SetPlaceholder();
            return;
        }

        raw = raw.Substring(0, raw.Length - 1);

        string formatted = FormatInput(raw);
        inputText.text = formatted;
        if (withdrawAmountText != null) withdrawAmountText.text = formatted;
    }

    string FormatInput(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return PLACEHOLDER;

        if (raw == PLACEHOLDER)
            return PLACEHOLDER;

        string integerPart = raw;
        string fractionPart = "";

        if (raw.Contains("."))
        {
            int dot = raw.IndexOf('.');
            integerPart = raw.Substring(0, dot);
            fractionPart = raw.Substring(dot);
        }

        if (integerPart == "")
            integerPart = "0";

        long val;
        if (long.TryParse(integerPart, out val))
            return val.ToString("#,0") + fractionPart;

        return raw;
    }

    float ParseInput()
    {
        if (inputText == null) return 0f;

        if (IsPlaceholder(inputText.text))
            return 0f;

        string raw = inputText.text.Replace(",", "");
        float v;
        if (float.TryParse(raw, out v))
            return v;

        return 0f;
    }

    // ------------------------
    // BUTTON LOGIC
    // ------------------------
    void SetupButtons()
    {
        depositButton.onClick.AddListener(() =>
        {
            PlayTradeSfx();
            OnDepositClicked();
        });

        withdrawButton.onClick.AddListener(() =>
        {
            PlayTradeSfx();
            OnWithdrawClicked();
        });

        depositAllButton.onClick.AddListener(() =>
        {
            PlayTradeSfx();
            OnDepositAllClicked();
        });

        withdrawAllButton.onClick.AddListener(() =>
        {
            PlayTradeSfx();
            OnWithdrawAllClicked();
        });

        closeButton?.onClick.AddListener(OnCloseButtonClicked);
    }

    private void ResetInput()
    {
        SetPlaceholder();
    }

    // ------------------------
    // BANK PRICE / BALANCE
    // ------------------------
    float GetBankPrice()
    {
        if (bankSubAsset == null) return 1f;

        float price = 0f;

        var rt = investmentManager.activeCompanies?
            .FirstOrDefault(r => r != null && r.subAsset == bankSubAsset);

        if (rt != null) price = rt.currentPrice;

        if (price <= 0f) price = bankSubAsset.currentPrice;
        if (price <= 0f) price = 1f;

        return price;
    }

    float GetOwnedBankShares()
    {
        if (localPlayer == null || localPlayer.holdings == null) return 0;

        float total = 0;
        foreach (var h in localPlayer.holdings)
            if (h != null && h.subAsset != null && h.subAsset.assetName == BANK_ASSET_NAME)
                total += h.shares;

        return total;
    }

    decimal GetBankBalance()
    {
        float shares = GetOwnedBankShares();
        if (shares <= 0f) return 0m;

        decimal value = (decimal)shares * (decimal)GetBankPrice();
        return Math.Truncate(value * 100) / 100;
    }

    // ------------------------
    // DEPOSIT / WITHDRAW
    // ------------------------
    void OnDepositClicked()
    {
        float moneyInput = ParseInput();
        if (moneyInput <= 0) return;

        decimal wallet = localPlayer.money;
        decimal want = (decimal)moneyInput;

        if (want > wallet) want = wallet;
        if (want <= 0) return;

        decimal sharesDec = want / (decimal)GetBankPrice();
        sharesDec = Math.Truncate(sharesDec * 100) / 100;

        buyManager.BuySubAsset(playerId, bankSubAsset, (float)sharesDec, null);

        ResetInput();
    }

    void OnWithdrawClicked()
    {
        float moneyInput = ParseInput();
        if (moneyInput <= 0) return;

        decimal bankBalance = GetBankBalance();
        if (bankBalance <= 0) return;

        decimal want = (decimal)moneyInput;
        if (want > bankBalance) want = bankBalance;

        decimal sharesDec = want / (decimal)GetBankPrice();
        sharesDec = Math.Truncate(sharesDec * 100) / 100;

        buyManager.SellSubAsset(playerId, bankSubAsset, (float)sharesDec, null);

        ResetInput();
    }

    void OnDepositAllClicked()
    {
        decimal wallet = localPlayer.money;
        if (wallet <= 0) return;

        decimal shares = wallet / (decimal)GetBankPrice();
        shares = Math.Truncate(shares * 100) / 100;

        buyManager.BuySubAsset(playerId, bankSubAsset, (float)shares, null);

        ResetInput();
    }

    void OnWithdrawAllClicked()
    {
        float shares = GetOwnedBankShares();
        if (shares <= 0) return;

        buyManager.SellSubAsset(playerId, bankSubAsset, shares, null);

        ResetInput();
    }

    // ------------------------
    // UPDATE UI
    // ------------------------
    public void UpdateUI()
    {
        if (depositAmountText != null)
            depositAmountText.text = FormatMoney(GetBankBalance());

        if (characterSprite != null)
        {
            var sprites = PlayerDataManager.Instance.characterSprites;
            if (sprites == null) return;

            int idx = localPlayer.characterSpriteIndex;
            if (idx >= 0 && idx < sprites.Length)
                characterSprite.sprite = sprites[idx];
        }
    }

    string FormatMoney(decimal v)
    {
        v = Math.Truncate(v * 100m) / 100m;
        return v.ToString("#,0.00");
    }

    private void OnCloseButtonClicked()
    {
        gameObject.SetActive(false);
    }

    private void PlayButtonClick()
    {
        if (AudioManager.Instance)
            AudioManager.Instance.Play(AudioManager.SoundType.Button);
    }

    private void PlayTradeSfx()
    {
        if (AudioManager.Instance)
            AudioManager.Instance.Play(AudioManager.SoundType.Event);
    }
}
