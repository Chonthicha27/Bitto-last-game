using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Setting : MonoBehaviour
{
    [Header("UI Elements")]
    [Tooltip("Panel UI ที่เป็นตัวหลักของหน้า Setting")]
    public GameObject settingPanel;

    public Button settingButton;
    public Button closeSettingButton;
    public Button homeButton;
    public Button resumeButton;

    [Tooltip("Panel UI สำหรับหน้าต่างยืนยันการกลับเมนูหลัก")]
    public GameObject confirmationPanel;

    [Header("Confirmation Buttons")]
    public Button confirmOKButton;     // ปุ่ม "ตกลง"
    public Button confirmCancelButton; // ปุ่ม "ยกเลิก"

    private void Start()
    {
        if (settingPanel != null)
        {
            settingPanel.SetActive(false);
        }
        if (confirmationPanel != null)
        {
            confirmationPanel.SetActive(false);
        }

        // ผูกปุ่มต่าง ๆ
        if (settingButton != null)
            settingButton.onClick.AddListener(OpenSettingPanel);

        if (closeSettingButton != null)
            closeSettingButton.onClick.AddListener(CloseSettingPanel);

        if (homeButton != null)
            homeButton.onClick.AddListener(ShowConfirmationDialog);

        if (resumeButton != null)
            resumeButton.onClick.AddListener(ResumeGame);

        if (confirmOKButton != null)
        {
            // ถ้าตกลง ให้กลับหน้า CharacterSelect พร้อม Reset
            confirmOKButton.onClick.AddListener(ConfirmGoToCharacterSelect);
        }
        if (confirmCancelButton != null)
        {
            // ถ้ายกเลิก ให้ปิด Confirmation Panel
            confirmCancelButton.onClick.AddListener(HideConfirmationDialog);
        }
    }

    /// <summary>
    /// เล่นเสียงปุ่มแบบรวมศูนย์
    /// </summary>
    private void PlayButtonSFX()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.Play(AudioManager.SoundType.Button);
        }
    }

    // เปิด Panel Setting ขึ้นมา
    public void OpenSettingPanel()
    {
        PlayButtonSFX();

        if (settingPanel != null)
        {
            settingPanel.SetActive(true);
            Debug.Log("เปิด Panel Setting");
        }
    }

    // ปิด Panel Setting ลงไป
    public void CloseSettingPanel()
    {
        PlayButtonSFX();

        if (settingPanel != null)
        {
            settingPanel.SetActive(false);
            Debug.Log("ปิด Panel Setting");
        }
    }

    // ฟังก์ชันสำหรับปุ่ม Resume
    public void ResumeGame()
    {
        PlayButtonSFX();

        // ในที่นี้คือการปิด Panel Setting เพื่อกลับสู่เกม
        CloseSettingPanel();

        Debug.Log("Resume Game: ทำงานในช่องว่าง ResumeGame()");
    }

    // แสดง Panel ยืนยันการกลับเมนูหลัก
    public void ShowConfirmationDialog()
    {
        PlayButtonSFX();

        if (confirmationPanel != null)
        {
            // เปิดหน้าต่างยืนยัน
            confirmationPanel.SetActive(true);
            Debug.Log("แสดงหน้าต่างยืนยันการกลับเมนูหลัก");
        }
    }

    // ปิด Panel ยืนยัน
    public void HideConfirmationDialog()
    {
        PlayButtonSFX();

        if (confirmationPanel != null)
        {
            // ปิดหน้าต่างยืนยัน
            confirmationPanel.SetActive(false);
            Debug.Log("ยกเลิกการกลับเมนูหลัก");
        }
    }

    /// กลับไปยัง Scene "CharacterSelect" (ปุ่มตกลง)
    public void ConfirmGoToCharacterSelect()
    {
        PlayButtonSFX();

        // 1. ปิดหน้าต่างยืนยัน (เผื่อไว้)
        HideConfirmationDialog();

        // 2. ดำเนินการกลับเมนูหลักและรีเซ็ตเกม
        //GoToCharacterSelectAndReset();
        OnConfirmOKClicked();
    }

    private void OnConfirmOKClicked()
    {
        // 1. เล่นเสียงและปิด Panel ยืนยัน
        PlayButtonSFX();
        HideConfirmationDialog();

        Debug.Log("[Setting] confirmOKButton ถูกกด -> ส่งสัญญาณให้ทุกคนออกจากเกม");

        // 2. ถ้ามี NetworkManager (โหมด Multiplayer) ให้ Broadcast สัญญาณออก
        if (NetworkManager.Instance != null)
        {
            // NetworkManager จะเป็นคนตัดสินใจว่าต้องทำอย่างไรต่อ:
            // - ถ้าเป็น Host: สั่งทุกคนออกและออกเอง
            // - ถ้าเป็น Client: ส่ง Request ไป Host ให้ Host สั่งทุกคนออก
            NetworkManager.Instance.BroadcastForceLeaveRoom();
        }
        else
        {
            // 3. ถ้าไม่มี NetworkManager (โหมด Single Player) ให้ Reset ตัวเองเลย
            LocalResetAndReturnToMenu();
        }
    }
    private void LocalResetAndReturnToMenu()
    {
        // ล้างข้อมูล PlayerDataManager (ฝั่งนี้)
        if (PlayerDataManager.Instance != null)
        {
            PlayerDataManager.Instance.ResetAll();
        }

        // กลับไปหน้า CharacterSelect
        SceneManager.LoadScene("CharacterSelect");
    }
    
    // กลับไปยัง Scene ชื่อ "CharacterSelect" พร้อม Reset เกม
    public void GoToCharacterSelectAndReset()
    {
        ResetGameData();

        try
        {
            SceneManager.LoadScene("CharacterSelect");
            Debug.Log("กำลังโหลด Scene: CharacterSelect (พร้อม Reset)");
        }
        catch (System.Exception ex)
        {
            Debug.LogError("ไม่พบ Scene ชื่อ 'CharacterSelect' หรือยังไม่ได้เพิ่มใน Build Settings! ข้อผิดพลาด: " + ex.Message);
        }
    }

    // Reset ข้อมูลเกมทั้งหมด
    private void ResetGameData()
    {
        Time.timeScale = 1f;
        Debug.Log("ทำการ Reset ข้อมูลเกมทั้งหมดแล้ว");
    }
}
