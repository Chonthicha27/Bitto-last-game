using UnityEngine;
using UnityEngine.SceneManagement;

public class ChangeSceneButton : MonoBehaviour
{
    [Header("ชื่อ Scene ที่ต้องการเปลี่ยนไป")]
    public string sceneName;

    // ฟังก์ชันนี้จะถูกเรียกเมื่อกดปุ่ม
    public void ChangeScene()
    {
        if (!string.IsNullOrEmpty(sceneName))
        {
            SceneManager.LoadScene(sceneName);
        }
        else
        {
            Debug.LogWarning("⚠️ ยังไม่ได้ใส่ชื่อ Scene ใน Inspector!");
        }
    }
}
