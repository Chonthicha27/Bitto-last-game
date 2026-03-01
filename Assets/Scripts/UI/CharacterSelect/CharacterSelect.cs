using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class CharacterSelector : MonoBehaviour
{
    [Header("Character Sprites")]
    public Sprite[] characterSprites;   // เก็บ Sprite ตัวละครทั้งหมด

    [Header("UI")]
    public Image characterDisplay;      // ตัวละคร (เลือกอยู่) รูปตัวละครที่แสดงตอนนี้
    public TMP_InputField playerNameInput;  // ช่องให้ผู้เล่นกรอกชื่อเอง

    private int currentIndex = 0;

    void Start()
    {
        playerNameInput.characterLimit = 17; // ความยาวตัวอัคษร playerNameInput ไม่เกิด 17 ตัว
        UpdateCharacterDisplay();
    }
    public void OnClickNext()
    {
        currentIndex++;
        if (currentIndex >= characterSprites.Length)
        {
            currentIndex = 0;
        }
        UpdateCharacterDisplay();
    }
    public void OnClickPrevious()
    {
        currentIndex--;
        if (currentIndex < 0)
        {
            currentIndex = characterSprites.Length - 1;
        }
        UpdateCharacterDisplay();
    }
    private void UpdateCharacterDisplay()
    {
        if (characterSprites.Length == 0)
        {
            return;
        }
        characterDisplay.sprite = characterSprites[currentIndex];
        characterDisplay.SetNativeSize();
        SetSpriteKeepScale(characterDisplay, characterSprites[currentIndex]);
    }
    private void SetSpriteKeepScale(Image img, Sprite sprite)
    {
        if (sprite == null) return;

        // เก็บ scale ที่ตั้งไว้ใน Unity
        Vector3 originalScale = img.rectTransform.localScale;

        // เซ็ต sprite และปรับขนาด native
        img.sprite = sprite;
        img.SetNativeSize();

        // คืนค่า scale ที่ตั้งไว้
        img.rectTransform.localScale = originalScale;
    }

    public void ConfirmSelection()
    {
        string playerName = playerNameInput.text.Trim();
        if (string.IsNullOrEmpty(playerName))
        {
            Debug.LogWarning("กรุณากรอกชื่อผู้เล่นก่อน");
            return;
        }

        // บันทึกข้อมูลลง PlayerDataManager
        PlayerDataManager.Instance.InitializeLocalPlayer(playerName, currentIndex);

        Debug.Log($"เลือกตัวละครตัวที่: {currentIndex} | ชื่อผู้เล่น: {playerName}");

        SceneManager.LoadScene("Lobby");
    }

}