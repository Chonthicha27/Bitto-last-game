using UnityEngine;
using UnityEngine.UI;

public class ButtonSFX : MonoBehaviour
{
    public AudioManager.SoundType soundType = AudioManager.SoundType.Button;

    private void Awake()
    {
        var btn = GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.AddListener(() =>
            {
                AudioManager.Instance.Play(soundType);
            });
        }
    }
}