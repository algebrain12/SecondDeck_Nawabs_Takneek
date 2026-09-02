using System;
using UnityEngine;
using UnityEngine.Events;

public class BoatHealth : MonoBehaviour
{
    [Header("Health Settings")]
    public float maxHealth = 100f;
    public float currentHealth;

    [Header("Events")]
    public UnityEvent onDamaged;
    public UnityEvent onDeath;

    // NEW: plain C# event carrying (amount, intensityRatio 0-1). Kept
    // separate from onDamaged (a parameterless UnityEvent) so any existing
    // Inspector-wired listeners on onDamaged are untouched.
    public event Action<float, float> OnDamageTaken;

    public float InTimer = 0;

    [Header("Debug")]
    public bool logDamage = true;

    [Header("Visuals")]
    [Tooltip("Assign the hull's Renderer here so abilities can tint it via SetHullColor().")]
    public Renderer hullRenderer;
    public GameObject explosionEffect;
    public AudioSource audioSource;
    private float inhealth;

    // NEW: damage reduction (shield) support, used by Tank's active ability.
    private float damageReductionPercent = 0f;
    private float damageReductionTimer = 0f;

    // Cheap, explicit way for other scripts (like GameHandler) to check
    // "is this ship out of the game" without depending on Destroy() having
    // actually run yet (Destroy() is deferred to end-of-frame in Unity, so
    // currentHealth <= 0f is the reliable signal, not gameObject == null).
    public bool IsDead => currentHealth <= 0f;

    void Awake()
    {
        currentHealth = maxHealth;
    }

    public void BecomeInvincible()
    {
        inhealth = currentHealth;
        InTimer = 5f;
    }

    /// <summary>
    /// Reduces incoming damage by percent (0-1) for duration seconds. Used
    /// by Tank's active ability. Overwrites any existing shield rather than
    /// stacking, so re-triggering refreshes the duration instead of
    /// compounding the reduction.
    /// </summary>
    public void ActivateDamageReduction(float percent, float duration)
    {
        damageReductionPercent = Mathf.Clamp01(percent);
        damageReductionTimer = duration;
    }

    public void TakeDamage(float amount)
    {
        if (currentHealth <= 0f) return;

        // NEW: apply any active damage-reduction shield before subtracting.
        if (damageReductionTimer > 0f)
            amount *= (1f - damageReductionPercent);

        currentHealth -= amount;
        currentHealth = Mathf.Max(currentHealth, 0f);

        if (logDamage)
            Debug.Log($"{gameObject.name} took {amount} damage. Health: {currentHealth}/{maxHealth}");

        onDamaged?.Invoke();

        // NEW: notify haptic listeners with the amount and a 0-1 ratio
        // (clamped, since instakills like Randy's can exceed maxHealth).
        OnDamageTaken?.Invoke(amount, Mathf.Clamp01(amount / maxHealth));

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    public void Heal(float amount)
    {
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
    }

    void Die()
    {
        Debug.Log($"{gameObject.name} has sunk!");
        onDeath?.Invoke();

        // Destroy(gameObject, 2f);
    }

    public float GetHealthPercent()
    {
        return currentHealth / maxHealth;
    }

    void Update()
    {
        if (InTimer > 0)
        {
            currentHealth = inhealth;
            InTimer -= Time.deltaTime;
        }

        // NEW: tick down the damage-reduction shield timer.
        if (damageReductionTimer > 0f)
        {
            damageReductionTimer -= Time.deltaTime;
            if (damageReductionTimer <= 0f)
            {
                damageReductionTimer = 0f;
                damageReductionPercent = 0f;
            }
        }
    }

    public void DeathFunction()
    {
        if(audioSource != null)
        {
            audioSource.Play();
        }
        if (explosionEffect != null)
        {
            Instantiate(explosionEffect, transform.position, Quaternion.identity);
        }
        Destroy(gameObject);
        
    }

    /// <summary>
    /// Tints the hull to match this ship's ability, using a
    /// MaterialPropertyBlock so each ship gets its own color without
    /// instantiating a new material per-instance (which would leak in the
    /// editor and cost extra draw calls at runtime). Sets both "_Color"
    /// (Built-in/Standard shader) and "_BaseColor" (URP/HDRP Lit shader) -
    /// setting the property your project's shader doesn't use is harmless.
    /// </summary>
    public void SetHullColor(Color color)
    {
        if (hullRenderer == null) return;

        MaterialPropertyBlock mpb = new MaterialPropertyBlock();
        hullRenderer.GetPropertyBlock(mpb);
        mpb.SetColor("_Color", color);
        mpb.SetColor("_BaseColor", color);
        hullRenderer.SetPropertyBlock(mpb);
    }
}