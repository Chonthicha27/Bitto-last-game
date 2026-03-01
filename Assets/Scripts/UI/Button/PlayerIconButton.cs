using UnityEngine;
using UnityEngine.UI;

public class PlayerIconButton : MonoBehaviour
{
    [Header("Main Button (Player Icon)")]
    public Button playerIconButton;   // ปุ่ม Icon ของ Player

    [Header("Extra Buttons")]
    public RectTransform HelperCircle;
    public LeanTweenType EaseType;
    public float AnimationTime = 0.2f;
    public GameObject ButtonOpenStockportfolio;
    public GameObject ButtonOpenEvnet;

    private bool isExpanded = false;
    private bool isAnimating = false;   // กันกดรัวระหว่างอนิเมะ

    void Start()
    {
        // ซ่อนปุ่มเสริมตอนเริ่มเกม
        if (ButtonOpenStockportfolio != null) ButtonOpenStockportfolio.SetActive(false);
        if (ButtonOpenEvnet != null)
            ButtonOpenEvnet.GetComponent<Button>().onClick.AddListener(OpenEventPanel);


        if (HelperCircle != null)
        {
            HelperCircle.sizeDelta = Vector2.zero;
        }

        // event เวลา click
        if (playerIconButton != null)
            playerIconButton.onClick.AddListener(ToggleExtraButtons);

        // ✅ เซ็ต Sprite ของปุ่มจาก PlayerData
        SetIconFromPlayer();
    }

    private void SetIconFromPlayer()
    {
        var localPlayer = PlayerDataManager.Instance.localPlayer;
        if (localPlayer == null || playerIconButton == null) return;

        var spriteArray = PlayerDataManager.Instance.characterSprites;
        if (spriteArray != null && localPlayer.characterSpriteIndex >= 0 && localPlayer.characterSpriteIndex < spriteArray.Length)
        {
            var img = playerIconButton.GetComponent<Image>();
            if (img != null)
            {
                //Vector3 originalScale = img.rectTransform.localScale;

                img.sprite = spriteArray[localPlayer.characterSpriteIndex];

                //img.SetNativeSize();
                //img.rectTransform.localScale = originalScale;
            }
        }
        else
        {
            Debug.LogWarning($"ไม่พบ Sprite สำหรับ characterSpriteIndex={localPlayer.characterSpriteIndex}");
        }
    }

    void ToggleExtraButtons()
    {
        if (HelperCircle == null) return;

        // กันกดรัวให้ tween เพี้ยน
        if (isAnimating) return;

        isExpanded = !isExpanded;

        // ยกเลิก tween เดิมก่อนทุกครั้ง
        LeanTween.cancel(HelperCircle);

        isAnimating = true;

        if (isExpanded)
        {
            // ขยายวง
            HelperCircle.gameObject.SetActive(true);
            LeanTween.size(HelperCircle, new Vector2(600, 600), AnimationTime)
                     .setEase(EaseType)
                     .setOnComplete(() =>
                     {
                         isAnimating = false;
                     });
        }
        else
        {
            // หดวง
            LeanTween.size(HelperCircle, new Vector2(0, 0), AnimationTime / 4f)
                     .setOnComplete(() =>
                     {
                         isAnimating = false;
                         HelperCircle.gameObject.SetActive(false);
                     });
        }

        if (ButtonOpenStockportfolio != null)
            ButtonOpenStockportfolio.SetActive(isExpanded);

        if (ButtonOpenEvnet != null)
            ButtonOpenEvnet.SetActive(isExpanded);
    }

    /// <summary>
    /// เรียกจาก TurnManager ตอน EndDay → บังคับปิด helper circle + ปุ่มทั้งหมด
    /// </summary>
    public void ForceClose()
    {
        isExpanded = false;
        isAnimating = false;

        if (HelperCircle != null)
        {
            LeanTween.cancel(HelperCircle);
            HelperCircle.sizeDelta = Vector2.zero;
            HelperCircle.gameObject.SetActive(false);
        }

        if (ButtonOpenStockportfolio != null)
            ButtonOpenStockportfolio.SetActive(false);

        if (ButtonOpenEvnet != null)
            ButtonOpenEvnet.SetActive(false);
    }
    private void OpenEventPanel()
    {
        // ปิดวงกลมก่อน
        ForceClose();

        // เรียกเปิด Event Panel แบบ "ล่าสุด"
        if (EventPanelManager.Instance != null)
        {
            EventPanelManager.Instance.ShowLastEvents();
        }
        else
        {
            Debug.LogWarning("EventPanelManager.Instance = null");
        }
    }


}
