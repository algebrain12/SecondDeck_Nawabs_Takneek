using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HealthBarUI : MonoBehaviour
{
    public BoatHealth boatHealth;
    public Image fillImage;
    float finhealthpercentage = 100f;

    [Header("Color Thresholds")]
    [Range(0f, 1f)] public float highHealthThreshold = 0.6f;  // above this = green
    [Range(0f, 1f)] public float midHealthThreshold = 0.3f;   // above this = orange, below = red

    [Header("Colors")]
    public Color highHealthColor = Color.green;
    public Color midHealthColor = new Color(1f, 0.5f, 0f); // orange
    public Color lowHealthColor = Color.red;

    

    [SerializeField] private float maxBarWidth = 200f; // Total width in pixels when health is 100%

    void Awake()
    {
        if (fillImage == null) return;

        RectTransform rectTransform = fillImage.rectTransform;
        float oldPivotX = rectTransform.pivot.x;
        float width = rectTransform.rect.width;

        // Changing pivot.x re-anchors what anchoredPosition.x means. Shift
        // anchoredPosition by the same amount the reference point moves,
        // so the LEFT EDGE stays exactly where it visually is right now.
        rectTransform.anchoredPosition = new Vector2(
            rectTransform.anchoredPosition.x - oldPivotX * width,
            rectTransform.anchoredPosition.y
        );

        rectTransform.pivot = new Vector2(0f, rectTransform.pivot.y);
    }

    void Update()
    {
        if (boatHealth == null || fillImage == null) return;

        float healthPercent = boatHealth.GetHealthPercent();

        if (healthPercent != finhealthpercentage)
        {
            finhealthpercentage = healthPercent;

            RectTransform rectTransform = fillImage.rectTransform;
            rectTransform.sizeDelta = new Vector2(maxBarWidth * healthPercent, rectTransform.sizeDelta.y);
        }

        UpdateColor(healthPercent);
    }

    void UpdateColor(float healthPercent)
    {
        if (healthPercent > highHealthThreshold)
        {
            // Blend between orange and green in the upper range
            float t = Mathf.InverseLerp(highHealthThreshold, 1f, healthPercent);
            fillImage.color = Color.Lerp(midHealthColor, highHealthColor, t);
        }
        else
        {
            // Blend between red and orange in the lower range
            float t = Mathf.InverseLerp(0f, highHealthThreshold, healthPercent);
            fillImage.color = Color.Lerp(lowHealthColor, midHealthColor, t);
        }
    }
}