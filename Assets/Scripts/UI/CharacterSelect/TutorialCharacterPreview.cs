using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TutorialCharacterPreview : MonoBehaviour
{
    [Header("อ้างอิง TutorialController (ลากใส่ได้เลย)")]
    public TutorialController tutorialController;

    [Header("UI แสดงตัวละครผู้เล่น")]
    public Image characterImage;
    public TMP_Text playerNameText;

    [Header("ตัวเลือกการแสดงผล")]
    public bool useNativeSize = true;

    [Header("กำหนดว่าจะให้ตัวละครขึ้นหน้าไหนบ้าง (index เริ่มจาก 0)")]
    public List<int> visiblePages = new List<int>() { 0 };  // เช่น อยากให้ขึ้นหน้า 0,3 ก็ใส่ 0 กับ 3

    [Header("Root ที่จะเปิด/ปิด ทั้งกล่องตัวละคร")]
    public GameObject previewRoot;

    private void Awake()
    {
        if (previewRoot == null)
            previewRoot = gameObject;
    }

    private void OnEnable()
    {
        // หา TutorialController (เผื่อยังไม่ได้ลากใน Inspector)
        if (tutorialController == null)
        {
            // true = หาแม้กระทั่ง GameObject ที่ inactive
#if UNITY_2020_1_OR_NEWER
            tutorialController = FindObjectOfType<TutorialController>(true);
#else
            tutorialController = FindObjectOfType<TutorialController>();
#endif
        }

        SubscribeToTutorialController();
        UpdateFromPlayerData();
        ForceRefreshVisibility();
    }

    private void OnDisable()
    {
        if (tutorialController != null)
        {
            tutorialController.OnPageChanged -= HandlePageChanged;
        }
    }

    private void SubscribeToTutorialController()
    {
        if (tutorialController != null)
        {
            tutorialController.OnPageChanged -= HandlePageChanged;
            tutorialController.OnPageChanged += HandlePageChanged;
        }
        else
        {
            // ไม่มี TutorialController ใน scene → ซ่อนไว้
            if (previewRoot != null)
                previewRoot.SetActive(false);
        }
    }

    private void UpdateFromPlayerData()
    {
        var pdm = PlayerDataManager.Instance;
        if (pdm == null) return;

        var local = pdm.localPlayer;
        if (local == null) return;

        if (playerNameText != null)
            playerNameText.text = local.playerName;

        if (characterImage != null && pdm.characterSprites != null)
        {
            int idx = local.characterSpriteIndex;
            if (idx >= 0 && idx < pdm.characterSprites.Length)
            {
                var img = characterImage;
                Vector3 originalScale = img.rectTransform.localScale;

                img.sprite = pdm.characterSprites[idx];
                if (useNativeSize)
                    img.SetNativeSize();

                img.rectTransform.localScale = originalScale;
            }
        }
    }

    private void ForceRefreshVisibility()
    {
        if (tutorialController == null ||
            tutorialController.tutorialPanel == null ||
            !tutorialController.tutorialPanel.activeSelf)
        {
            // ถ้า Tutorial ยังไม่เปิด → ซ่อนไว้ก่อน
            if (previewRoot != null)
                previewRoot.SetActive(false);
            return;
        }

        int pageIndex = tutorialController.CurrentPageIndex;
        HandlePageChanged(pageIndex);
    }

    private void HandlePageChanged(int pageIndex)
    {
        // ต้องเปิด Tutorial Panel อยู่ด้วย ไม่งั้นไม่โชว์
        bool tutorialOpen = tutorialController != null &&
                            tutorialController.tutorialPanel != null &&
                            tutorialController.tutorialPanel.activeSelf;

        bool shouldShow = tutorialOpen &&
                          visiblePages != null &&
                          visiblePages.Contains(pageIndex);

        if (previewRoot != null)
            previewRoot.SetActive(shouldShow);
    }
}
