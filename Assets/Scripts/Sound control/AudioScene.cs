using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class AudioScene : MonoBehaviour
{
    // ใช้ Singleton Pattern เพื่อให้เข้าถึงง่ายจากที่อื่น
    public static AudioScene Instance;

    [Header("Main Audio Source")]
    public AudioSource sfxSource;      // เสียงเอฟเฟกต์
    public AudioSource bgmSource;      // เสียงพื้นหลัง (Loop)

    [Header("Audio Clips")]
    [Tooltip("เสียงพื้นหลังเกม")]
    public AudioClip bgmClip;

    [Tooltip("เสียงกดปุ่มทั่วไป")]
    public AudioClip buttonClip;

    [Header("Button Auto Assign")]
    [Tooltip("ปุ่ม UI ที่ต้องการผูกเสียงคลิกทั่วไป")]
    public Button[] uiButtons;

    void Awake()
    {
        // 1. Singleton Setup
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            // ทำลายตัวเองถ้ามี Instance อื่นอยู่แล้ว
            Destroy(gameObject);
            return;
        }

        // 2. ตั้งค่า AudioSource พื้นฐาน
        if (sfxSource == null) sfxSource = gameObject.AddComponent<AudioSource>();
        if (bgmSource == null) bgmSource = gameObject.AddComponent<AudioSource>();

        // 3. ตั้งค่า BGM ให้เล่นวนลูป
        bgmSource.loop = true;
        bgmSource.playOnAwake = false; // จะสั่งเล่นด้วย PlayBGM() แทน

        // 4. ผูกเสียงคลิกเข้ากับปุ่ม
        AutoAssignButtonSounds();
    }

    void Start()
    {
        // เริ่มเล่น BGM เมื่อ Scene เริ่มทำงาน
        PlayBGM();
    }
    
    // --- Public Methods ---

    // 🔊 เล่น BGM
    public void PlayBGM()
    {
        if (bgmClip != null && !bgmSource.isPlaying)
        {
            bgmSource.clip = bgmClip;
            bgmSource.Play();
        }
    }

    // 🔊 หยุด BGM
    public void StopBGM()
    {
        bgmSource.Stop();
    }

    // 🔊 เล่นเสียงเอฟเฟกต์ (เช่นเสียงกดปุ่ม)
    public void PlaySFX(AudioClip clip)
    {
        if (sfxSource != null && clip != null)
        {
            sfxSource.PlayOneShot(clip);
        }
    }

    // ⚙️ ผูกเสียงคลิกเข้ากับปุ่มทั้งหมดใน Array
    private void AutoAssignButtonSounds()
    {
        if (buttonClip == null)
        {
            Debug.LogWarning("[AudioScene] buttonClip ยังไม่ได้ถูกกำหนด จะไม่สามารถผูกเสียงเข้ากับปุ่มได้");
            return;
        }

        foreach (Button btn in uiButtons)
        {
            if (btn != null)
            {
                // ตรวจสอบว่าปุ่มยังไม่มี Listener ที่ผูกกับเสียงคลิกอยู่
                // และเพิ่ม Listener ใหม่เข้าไป
                btn.onClick.AddListener(() => PlaySFX(buttonClip));
                Debug.Log($"[AudioScene] ผูกเสียงคลิกเข้ากับปุ่ม: {btn.name}");
            }
        }
    }
}