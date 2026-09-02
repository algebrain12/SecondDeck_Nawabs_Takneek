using UnityEngine;

public class CanonWork : MonoBehaviour
{
    [Header("Launch Points")]
    public Transform LeftLaunchPoint;
    public Transform RightLaunchPoint;

    [Header("Cannonball Settings")]
    public GameObject CannonballPrefab;
    public float LaunchSpeed = 15f;

    [Header("Firing")]
    public float cooldownTime = 0.75f;
    public KeyCode leftFireKey = KeyCode.Q;
    public KeyCode rightFireKey = KeyCode.E;

    public float CanonDamage = 10f;

    private bool canFire = true;

    // NEW: timestamp of the last successful shot. Set once, inside Fire()
    // itself, so it's correct regardless of whether the shot came from
    // FireLeft()/FireRight() (network requests) or the local key-based
    // Update() path - single funnel point, same as canFire already is.
    private float lastFireTime = -999f; // far enough in the past to start "ready"

    void Fire(Transform muzzle, Vector3 localDirection)
    {
        if (CannonballPrefab == null || muzzle == null)
        {
            Debug.LogWarning("BoatCannon: missing muzzle or prefab reference.");
            return;
        }

        GameObject ball = Instantiate(CannonballPrefab, muzzle.position, muzzle.rotation);
        ball.GetComponent<Cannonball>().DamageValue = CanonDamage;
        Rigidbody rb = ball.GetComponent<Rigidbody>();

        if (rb != null)
        {
            // Convert the local-space direction (e.g. Vector3.left/right)
            // into world space using this transform's current rotation.
            Vector3 worldDirection = transform.TransformDirection(localDirection).normalized;

            // Set velocity directly instead of applying an impulse,
            // so speed stays constant regardless of mass/drag.
            rb.linearVelocity = worldDirection * LaunchSpeed;

            // Optional: zero out gravity influence if this ball
            // shouldn't slow down or arc at all.
            rb.useGravity = false;
        }

        ball = Instantiate(CannonballPrefab, muzzle.position + muzzle.forward * 10f, muzzle.rotation);
        ball.GetComponent<Cannonball>().DamageValue = CanonDamage;
        rb = ball.GetComponent<Rigidbody>();

        if (rb != null)
        {
            // Convert the local-space direction (e.g. Vector3.left/right)
            // into world space using this transform's current rotation.
            Vector3 worldDirection = transform.TransformDirection(localDirection).normalized;

            // Set velocity directly instead of applying an impulse,
            // so speed stays constant regardless of mass/drag.
            rb.linearVelocity = worldDirection * LaunchSpeed;

            // Optional: zero out gravity influence if this ball
            // shouldn't slow down or arc at all.
            rb.useGravity = false;
        }

        ball = Instantiate(CannonballPrefab, muzzle.position - muzzle.forward * 10f, muzzle.rotation);
        ball.GetComponent<Cannonball>().DamageValue = CanonDamage;
        rb = ball.GetComponent<Rigidbody>();

        if (rb != null)
        {
            // Convert the local-space direction (e.g. Vector3.left/right)
            // into world space using this transform's current rotation.
            Vector3 worldDirection = transform.TransformDirection(localDirection).normalized;

            // Set velocity directly instead of applying an impulse,
            // so speed stays constant regardless of mass/drag.
            rb.linearVelocity = worldDirection * LaunchSpeed;

            // Optional: zero out gravity influence if this ball
            // shouldn't slow down or arc at all.
            rb.useGravity = false;
        }

        lastFireTime = Time.time; // NEW: marks the start of this cooldown window
        StartCoroutine(Cooldown());
    }

    // Update is called once per frame
    void Update()
    {
        if (!canFire) return;

        if (Input.GetKeyDown(leftFireKey))
            Fire(LeftLaunchPoint, Vector3.left);
        else if (Input.GetKeyDown(rightFireKey))
            Fire(RightLaunchPoint, Vector3.right);
    }

    public bool FireLeft()
    {
        if (!canFire) return false;
        Fire(LeftLaunchPoint, Vector3.left);
        return true;
    }

    public bool FireRight()
    {
        if (!canFire) return false;
        Fire(RightLaunchPoint, Vector3.right);
        return true;
    }

    // NEW: read-only accessors for UI (e.g. CanonCooldownUI) to query
    // cooldown state without needing their own separate timer.
    public bool CanFire => canFire;
    public float CooldownDuration => cooldownTime;

    // 0 immediately after firing, ramping linearly up to 1 once ready
    // again. Clamped so it never reports outside 0-1 even across frame-rate
    // hiccups, and guarded against cooldownTime being set to 0 in the
    // Inspector (which would otherwise divide by zero).
    public float GetCooldownProgress()
    {
        if (canFire) return 1f;
        return Mathf.Clamp01((Time.time - lastFireTime) / Mathf.Max(cooldownTime, 0.0001f));
    }

    System.Collections.IEnumerator Cooldown()
    {
        canFire = false;
        yield return new WaitForSeconds(cooldownTime);
        canFire = true;
    }
}