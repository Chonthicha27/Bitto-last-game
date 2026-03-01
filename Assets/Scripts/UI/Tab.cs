using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Tab : MonoBehaviour
{
    public Button buttonTab;

    void Start()
    {
        buttonTab.onClick.AddListener(OnButtonClickedTab);
    }

    public void OnButtonClickedTab()
    {
        SceneManager.LoadScene("CharacterSelect");
        Debug.Log("Tab");
    }
}

