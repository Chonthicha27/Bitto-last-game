using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class EventItemUI : MonoBehaviour
{
    public TMP_Text descriptionText;
    public TMP_Text priceText;
    public Image iconImage;

    public void Setup(string description, float price, Sprite icon)
    {
        descriptionText.text = description;
        priceText.text = price >= 0 ? $"+{price}" : price.ToString();
        iconImage.sprite = icon;
    }
}