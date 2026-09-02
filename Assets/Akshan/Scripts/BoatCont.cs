using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BoatCont : MonoBehaviour
{
    [Header("Movement Settings")]
    public float accelerationForce = 15f;
    public float maxSpeed = 10f;
    public float turnSpeed = 45f;
    public float waterDrag = 1f;
    public float waterAngularDrag = 2f;

    [Header("Buoyancy")]
    public float initialY;
    public float buoyancyStrength = 10f;
    public float floatDamping = 2f;

    [Header("Upright Correction")]
    public float uprightStrength = 5f;
    public float uprightDamping = 2f;

    public float dashSpeedBoost = 12f;

    [Header("Ability Modifiers")]
    // Multiplies acceleration/max speed while > 0 remaining. Used by
    // Striker's dash (multiplier > 1) and Saboteur's slow debuff
    // (multiplier < 1). Not exposed in the Inspector - purely runtime state.
    private float tempSpeedMultiplier = 1f;
    private float tempSpeedTimer = 0f;

    private Rigidbody rb;
    private float verticalInput;
    private float horizontalInput;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        initialY = transform.position.y;
        rb.linearDamping = waterDrag;
        rb.angularDamping = waterAngularDrag;
    }

    void Update()
    {
        verticalInput = Mathf.Max(0f, Input.GetAxis("Vertical"));
        horizontalInput = Input.GetAxis("Horizontal");

        // NEW: tick down any temporary speed modifier (dash boost or
        // sabotage slow) and reset to neutral once it expires.
        if (tempSpeedTimer > 0f)
        {
            tempSpeedTimer -= Time.deltaTime;
            if (tempSpeedTimer <= 0f)
            {
                tempSpeedTimer = 0f;
                tempSpeedMultiplier = 1f;
            }
        }
    }

    void FixedUpdate()
    {
        float displacement = initialY - transform.position.y;
        if (displacement != 0f)
        {
            Vector3 restoringForce = Vector3.up * (displacement * buoyancyStrength * rb.mass);
            rb.AddForce(restoringForce, ForceMode.Force);
        }

        Vector3 damping = Vector3.up * (-rb.linearVelocity.y * floatDamping);
        rb.AddForce(damping, ForceMode.Force);

        MoveBoat();
        TurnBoat();
        UprightCorrection();
    }

    void UprightCorrection()
    {
        Vector3 currentUp = transform.up;
        Quaternion correction = Quaternion.FromToRotation(currentUp, Vector3.up);

        correction.ToAngleAxis(out float angleDegrees, out Vector3 axis);

        if (angleDegrees > 180f) angleDegrees -= 360f;

        if (!float.IsNaN(axis.x) && angleDegrees != 0f)
        {
            Vector3 torque = axis * (angleDegrees * Mathf.Deg2Rad) * uprightStrength;
            rb.AddTorque(torque, ForceMode.Force);
        }

        Vector3 angVel = rb.angularVelocity;
        Vector3 tiltDamping = new Vector3(-angVel.x, 0f, -angVel.z) * uprightDamping;
        rb.AddTorque(tiltDamping, ForceMode.Force);
    }

    Vector3 HorizontalVelocity => new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);

    void MoveBoat()
    {
        float effectiveMaxSpeed = maxSpeed * tempSpeedMultiplier;

        if (HorizontalVelocity.magnitude < effectiveMaxSpeed && verticalInput > 0)
        {
            Vector3 forwardForce = transform.forward * verticalInput * accelerationForce * tempSpeedMultiplier;
            rb.AddForce(forwardForce, ForceMode.Acceleration);
        }
    }

    // NEW: public entry point for Striker's active ability (previously this
    // was a private, never-called Dash() wrapper around DashRoutine()).
    public void PerformDash()
    {
        StartCoroutine(DashRoutine());
    }

    private IEnumerator DashRoutine()
    {
        rb.AddForce(transform.forward * dashSpeedBoost * 3f, ForceMode.VelocityChange);

        // NEW: brief acceleration/top-speed boost layered on top of the
        // instant velocity kick, so the dash feels like a sustained burst
        // rather than a single jolt that water drag immediately eats away.
        ApplyTemporarySpeedMultiplier(5f, 1f);

        yield return new WaitForSeconds(0.25f);
    }

    /// <summary>
    /// Temporarily multiplies acceleration and top speed. Multiplier > 1
    /// speeds the boat up (Striker's dash); multiplier &lt; 1 slows it down
    /// (Saboteur's sabotage). Overwrites any existing temporary modifier
    /// rather than stacking, so re-triggering refreshes duration instead of
    /// compounding.
    /// </summary>
    public void ApplyTemporarySpeedMultiplier(float multiplier, float duration)
    {
        tempSpeedMultiplier = multiplier;
        tempSpeedTimer = duration;
    }

    /// <summary>
    /// Instantly relocates the boat forward by the given distance (Phantom's
    /// active). Uses Rigidbody.MovePosition so physics stays in sync instead
    /// of the transform and rigidbody state desyncing for a frame.
    /// </summary>
    public void Blink(float distance)
    {
        Vector3 targetPosition = transform.position + transform.forward * distance;
        rb.MovePosition(targetPosition);
    }

    public void MoveSS(float pitch, float yaw, float roll)
    {
        float effectiveMaxSpeed = maxSpeed * tempSpeedMultiplier;

        if (HorizontalVelocity.magnitude < effectiveMaxSpeed)
        {
            Vector3 forwardForce = transform.forward * yaw * accelerationForce * tempSpeedMultiplier;
            rb.AddForce(forwardForce, ForceMode.Acceleration);
        }

        float turnMultiplier = Mathf.Clamp01(HorizontalVelocity.magnitude / 2f);
        float rotationAmount = pitch * turnSpeed * turnMultiplier * Time.fixedDeltaTime;
        rb.MoveRotation(rb.rotation * Quaternion.Euler(0f, rotationAmount, 0f));
    }

    void TurnBoat()
    {
        if (Mathf.Abs(horizontalInput) > 0.1f)
        {
            float turnMultiplier = Mathf.Clamp01(HorizontalVelocity.magnitude / 2f);
            float rotationAmount = horizontalInput * turnSpeed * turnMultiplier * Time.fixedDeltaTime;
            rb.MoveRotation(rb.rotation * Quaternion.Euler(0f, rotationAmount, 0f));
        }
    }
}