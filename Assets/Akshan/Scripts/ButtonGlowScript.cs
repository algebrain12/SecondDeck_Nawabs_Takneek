using UnityEngine;
using UnityEngine.UI;

// Attach to any UI Button/Image to make it pulse with a soft glowing outline.
// Uses Unity's built-in Outline component, so no custom shader or extra
// glow sprite is required. Can pulse continuously (decorative) or be
// triggered on/off externally - e.g. "flash when the ability is ready."
[RequireComponent(typeof(Graphic))]
public class ButtonGlow : MonoBehaviour
{
    [Header("Glow Appearance")]
    [Tooltip("Color of the glow. Alpha is animated automatically - leave this at full alpha.")]
    public Color glowColor = new Color(0f, 153f/255f, 219f/255f, 1f);

    [Tooltip("How far the glow extends from the button's edges, in pixels.")]
    public float glowDistance = 6f;

    [Header("Pulse Timing")]
    [Tooltip("Full pulse cycles per second.")]
    public float pulseSpeed = 1.5f;

    [Tooltip("Lowest alpha the glow dips to between pulses.")]
    [Range(0f, 1f)] public float minAlpha = 0.15f;

    [Tooltip("If true, starts pulsing automatically when enabled. If false, call StartGlow() yourself.")]
    public bool autoStart = true;

    private Outline outline;
    private bool isGlowing;
    private float t;

    void Awake()
    {
        // Reuse an existing Outline if one's already configured on this
        // object in the Inspector, otherwise add one fresh.
        outline = GetComponent<Outline>();
        if (outline == null)
            outline = gameObject.AddComponent<Outline>();

        outline.effectColor = new Color(glowColor.r, glowColor.g, glowColor.b, 0f);
        outline.effectDistance = new Vector2(glowDistance, glowDistance);
        outline.enabled = false;
    }

    void OnEnable()
    {
        if (autoStart) StartGlow();
    }

    void Update()
    {
        if (!isGlowing) return;

        t += Time.deltaTime * pulseSpeed;

        // Sine wave remapped from [-1,1] into [minAlpha, 1] for a smooth pulse.
        float wave = (Mathf.Sin(t * Mathf.PI * 2f) + 1f) * 0.5f;
        float alpha = Mathf.Lerp(minAlpha, 1f, wave);

        outline.effectColor = new Color(glowColor.r, glowColor.g, glowColor.b, alpha);
    }

    // Call externally to start glowing - e.g. when an ability comes off cooldown.
    public void StartGlow()
    {
        isGlowing = true;
        outline.enabled = true;
        t = 0f;
    }

    // Call externally to stop and hide the glow - e.g. when the button is pressed.
    public void StopGlow()
    {
        isGlowing = false;
        outline.enabled = false;
    }
}