using UnityEngine;
using UnityEngine.UI;

public class CanonCooldownUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The CanonWork on this same ship.")]
    public CanonWork canonWork;

    [Tooltip("The fill Image, same setup as the health bar's fill image.")]
    public Image fillImage;

    [Header("Bar Settings")]
    [Tooltip("Full width of the bar when the cannon is ready to fire.")]
    public float maxBarWidth = 240f;

    [Header("Colors (optional)")]
    [Tooltip("Color while the cannon is on cooldown / charging back up.")]
    public Color chargingColor = new Color(0.85f, 0.2f, 0.2f);
    [Tooltip("Color once the cannon is ready to fire.")]
    public Color readyColor = new Color(0.2f, 0.85f, 0.3f);

    private float lastProgress = -1f;

    void Awake()
    {
        if (fillImage == null) return;

        RectTransform rectTransform = fillImage.rectTransform;
        float oldPivotX = rectTransform.pivot.x;
        float width = rectTransform.rect.width;

        // Same pivot/anchoredPosition fix as the health bar: flipping
        // pivot.x without compensating anchoredPosition makes the bar
        // visually jump on the first frame it updates.
        rectTransform.anchoredPosition = new Vector2(
            rectTransform.anchoredPosition.x - oldPivotX * width,
            rectTransform.anchoredPosition.y
        );

        rectTransform.pivot = new Vector2(0f, rectTransform.pivot.y);
    }

    void Update()
    {
        if (canonWork == null || fillImage == null) return;

        // 0 = just fired, 1 = ready again. Flip to (1f - progress) here if
        // you'd rather the bar start full and drain instead of charging up.
        float progress = canonWork.GetCooldownProgress();

        if (progress != lastProgress)
        {
            lastProgress = progress;

            RectTransform rectTransform = fillImage.rectTransform;
            rectTransform.sizeDelta = new Vector2(maxBarWidth * progress, rectTransform.sizeDelta.y);

            fillImage.color = Color.Lerp(chargingColor, readyColor, progress);
        }
    }
}
