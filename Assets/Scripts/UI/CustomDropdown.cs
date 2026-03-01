/*using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class CustomDropdown : MonoBehaviour
{
    [Header("UI References")]
    public Button toggleButton;
    public TMP_Text selectedText;
    public GameObject optionsPanel;
    public GameObject optionButtonPrefab;

    public string[] options;
    public Action<int> onValueChanged;

    private bool isOpen = false;

    void Start()
    {
        if (toggleButton == null || selectedText == null || optionsPanel == null || optionButtonPrefab == null)
        {
            Debug.LogError("CustomDropdown: UI References ไม่ครบ");
            return;
        }

        optionsPanel.SetActive(false);
        toggleButton.onClick.AddListener(ToggleDropdown);

        // เคลียร์ Option เดิม
        foreach (Transform child in optionsPanel.transform) Destroy(child.gameObject);

        // สร้างปุ่มใหม่
        for (int i = 0; i < options.Length; i++)
        {
            int index = i;
            GameObject btnObj = Instantiate(optionButtonPrefab, optionsPanel.transform, false);
            TMP_Text txt = btnObj.GetComponentInChildren<TMP_Text>();
            if (txt != null) txt.text = options[i];
            Button btn = btnObj.GetComponent<Button>();
            btn.onClick.AddListener(() => SelectOption(index));
        }

        if (options.Length > 0) SelectOption(0);
    }

    void ToggleDropdown()
    {
        isOpen = !isOpen;
        optionsPanel.SetActive(isOpen);
    }

    void SelectOption(int index)
    {
        selectedText.text = options[index];
        isOpen = false;
        optionsPanel.SetActive(false);

        onValueChanged?.Invoke(index);
    }
}*/