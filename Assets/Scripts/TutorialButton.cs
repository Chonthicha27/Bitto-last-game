using UnityEngine;

public class TutorialButton : MonoBehaviour
{
    [Header("อ้างอิงตัวควบคุม Tutorial")]
    public TutorialController tutorialController;

    // เรียกจากปุ่มกด
    public void OnClickOpenTutorial()
    {
        if (tutorialController != null)
        {
            tutorialController.OpenTutorial();
        }
        else
        {
            Debug.LogWarning("[TutorialButton] ยังไม่ได้ลาก TutorialController มาใส่ใน Inspector");
        }
    }
}
