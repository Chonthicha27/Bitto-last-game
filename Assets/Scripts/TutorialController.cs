using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TutorialController : MonoBehaviour
{
    public static TutorialController Instance { get; private set; }

    [Header("Main Panel")]
    public GameObject tutorialPanel;

    [Header("Pages (แต่ละหน้า Tutorial)")]
    public List<GameObject> pages = new List<GameObject>();

    [Header("Buttons")]
    public Button nextButton;
    public Button prevButton;
    public Button closeButton;

    public event Action<int> OnPageChanged;

    public int CurrentPageIndex => currentIndex;

    private int currentIndex = 0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        if (tutorialPanel != null)
            tutorialPanel.SetActive(false);

        if (nextButton != null) nextButton.onClick.AddListener(NextPage);
        if (prevButton != null) prevButton.onClick.AddListener(PrevPage);
        if (closeButton != null) closeButton.onClick.AddListener(CloseTutorial);

        HideAllPages();
    }

    public void OpenTutorial()
    {
        if (tutorialPanel != null)
            tutorialPanel.SetActive(true);

        if (pages == null || pages.Count == 0)
        {
            Debug.LogWarning("[TutorialController] ยังไม่ได้ใส่ pages ใน Inspector");
            return;
        }

        currentIndex = 0;
        ShowPage(currentIndex);
    }

    public void CloseTutorial()
    {
        if (tutorialPanel != null)
            tutorialPanel.SetActive(false);
    }

    public void NextPage()
    {
        if (pages == null || pages.Count == 0) return;
        if (currentIndex >= pages.Count - 1) return;

        currentIndex++;
        ShowPage(currentIndex);
    }

    public void PrevPage()
    {
        if (pages == null || pages.Count == 0) return;
        if (currentIndex <= 0) return;

        currentIndex--;
        ShowPage(currentIndex);
    }

    private void ShowPage(int index)
    {
        if (pages == null || pages.Count == 0) return;

        for (int i = 0; i < pages.Count; i++)
        {
            if (pages[i] != null)
                pages[i].SetActive(i == index);
        }

        if (prevButton != null)
            prevButton.interactable = (index > 0);

        if (nextButton != null)
            nextButton.interactable = (index < pages.Count - 1);

        OnPageChanged?.Invoke(index);
    }

    private void HideAllPages()
    {
        if (pages == null) return;
        foreach (var p in pages)
        {
            if (p != null) p.SetActive(false);
        }
    }
}
