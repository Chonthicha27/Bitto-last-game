using UnityEngine;
using UnityEngine.UI;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [Header("Main Audio Source")]
    public AudioSource sfxSource;      // เสียงเอฟเฟกต์
    public AudioSource bgmSource;      // เสียงพื้นหลัง (Loop)

    [Header("Audio Clips")]
    [Tooltip("เสียงพื้นหลังเกม")]
    public AudioClip bgmClip;

    [Tooltip("เสียงกดปุ่มทั่วไป")]
    public AudioClip buttonClip;

    [Tooltip("เสียงนาฬิกา 10 วิสุดท้าย")]
    public AudioClip clockClip;

    [Tooltip("เสียงจบเกม")]
    public AudioClip endgameClip;

    [Tooltip("เสียงซื้อ / ขาย (Trade SFX)")]
    public AudioClip eventClip;   // ใช้เป็นเสียงซื้อขาย

    [Tooltip("เสียงตอนกระดาษ / Event Panel เลื่อนขึ้นมา")]
    public AudioClip paperClip;

    [Header("Button Auto Assign")]
    public Button[] uiButtons;

    [Header("Volume Controls")]
    public Slider sfxSlider;   // สไลด์ปรับเสียง SFX (ล่าง – ไอคอนลำโพง)
    public Slider bgmSlider;   // สไลด์ปรับเสียง BGM (บน – ไอคอนโน้ตเพลง)

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        AssignButtons();
        PlayBGM();
        SetupVolumeControl();
    }

    public enum SoundType
    {
        BGM,
        Button,
        Clock,
        GameOver,
        Event,   // ✅ ตอนนี้ให้ใช้เป็นเสียงซื้อขาย
        Paper
    }

    public void Play(SoundType type)
    {
        switch (type)
        {
            case SoundType.BGM:
                PlayBGM();
                break; // 🔊 เสียงพื้นหลัง

            case SoundType.Button:
                PlaySFX(buttonClip);
                break; // 🔊 เสียงปุ่ม

            case SoundType.Clock:
                PlaySFX(clockClip);
                break; // 🔊 เสียงนาฬิกา

            case SoundType.Event:
                PlaySFX(eventClip);
                break; // 🔊 เสียงซื้อขาย (Trade)

            case SoundType.Paper:
                PlaySFX(paperClip);
                break; // 🔊 เสียงกระดาษ / Event Panel

            case SoundType.GameOver:
                PlaySFX(endgameClip);
                break; // 🔊 เสียงจบเกม
        }
    }

    // ---------------- BGM ----------------
    void PlayBGM()
    {
        if (bgmSource == null || bgmClip == null)
        {
            Debug.LogWarning("[AudioManager] BGM source หรือคลิปว่าง");
            return;
        }

        // ถ้าเล่นอยู่แล้วไม่ต้องสั่งซ้ำ
        if (bgmSource.isPlaying && bgmSource.clip == bgmClip)
            return;

        bgmSource.clip = bgmClip;
        bgmSource.loop = true;
        bgmSource.Play();
    }

    /// หยุดเพลง BGM (ใช้ตอนจบเกม)
    public void StopBGM()
    {
        if (bgmSource != null && bgmSource.isPlaying)
        {
            bgmSource.Stop();
        }
    }

    // ---------------- SFX ----------------
    void PlaySFX(AudioClip clip)
    {
        Debug.Log($"[AudioManager] PlaySFX called | clip = {(clip ? clip.name : "NULL")}");

        if (clip == null || sfxSource == null)
        {
            Debug.Log("[AudioManager] Cannot play sound — clip or sfxSource missing");
            return;
        }

        sfxSource.PlayOneShot(clip);
    }

    /// หยุดเสียง SFX ทั้งหมด (ใช้ตัดเสียง Clock ตอนจบวัน)
    public void StopClockSFX()
    {
        if (sfxSource != null)
        {
            sfxSource.Stop();
        }
    }

    // ---------------- UI Setup ----------------
    private void AssignButtons()
    {
        uiButtons = FindObjectsOfType<Button>();  // หา Button ผูกเสียงปุ่มอัตโนมัติ

        foreach (Button btn in uiButtons)
        {
            btn.onClick.AddListener(() => { Play(SoundType.Button); });
        }
    }

    private void SetupVolumeControl()
    {
        // ควบคุมความดัง BGM ด้วย bgmSlider → bgmSource.volume
        if (bgmSlider != null)
        {
            bgmSlider.value = bgmSource != null ? bgmSource.volume : 1f;
            bgmSlider.onValueChanged.AddListener((value) =>
            {
                if (bgmSource != null)
                {
                    bgmSource.volume = value;
                }
            });
        }

        // ควบคุมความดัง SFX ด้วย sfxSlider → sfxSource.volume
        if (sfxSlider != null)
        {
            sfxSlider.value = sfxSource != null ? sfxSource.volume : 1f;
            sfxSlider.onValueChanged.AddListener((value) =>
            {
                if (sfxSource != null)
                {
                    sfxSource.volume = value;
                }
            });
        }
    }
}



//  ตัวอย่างการเรียนใช้เสียง
//case SoundType.Clock: PlaySFX(clockClip); break; // 🔊 เสียงนาฬิกา
//case SoundType.Event: PlaySFX(eventClip); break; // 🔊 เสียงตอนอีเว้นท์
//case SoundType.Paper: PlaySFX(paperClip); break; // 🔊 เสียงกระดาษ
//case SoundType.GameOver: PlaySFX(endgameClip); break; // 🔊 เสียงจบเกม
/*///

/*public void StartClockWarning()
{
    
    if (AudioManager.Instance != null)
    {
        // เรียกเล่นเสียง Clock
        AudioManager.Instance.Play(AudioManager.SoundType.Clock);
    }
}

public void OpenPaperPanel()
{
   
    if (AudioManager.Instance != null)
    {
        // เรียกเล่นเสียง Paper
        AudioManager.Instance.Play(AudioManager.SoundType.Paper);
    }
}#1#
/// */