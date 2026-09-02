using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BoatController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float accelerationForce = 15f;
    public float maxSpeed = 10f;
    public float turnSpeed = 45f;
    public float waterDrag = 1f;
    public float waterAngularDrag = 2f;

    private Rigidbody rb;
    private float verticalInput;
    private float horizontalInput;

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        // Configure drag to simulate water resistance
        rb.linearDamping = waterDrag;
        rb.angularDamping = waterAngularDrag;
    }

    void Update()
    {
        // Read input (W = Forward, A/D = Turn Left/Right)
        // Math.Max prevents reversing if you only want forward motion on W
        verticalInput = Mathf.Max(0f, Input.GetAxis("Vertical"));
        horizontalInput = Input.GetAxis("Horizontal");
    }

    void FixedUpdate()
    {
        MoveBoat();
        TurnBoat();
    }

    void MoveBoat()
    {
        // Only apply force if under max speed
        if (rb.linearVelocity.magnitude < maxSpeed && verticalInput > 0)
        {
            Vector3 forwardForce = transform.forward * verticalInput * accelerationForce;
            rb.AddForce(forwardForce, ForceMode.Acceleration);
        }
    }

    void TurnBoat()
    {
        // Turn the boat around its Y axis
        if (Mathf.Abs(horizontalInput) > 0.1f)
        {
            // Optional: Scale turning speed based on current movement so you can't spin while stationary
            float turnMultiplier = Mathf.Clamp01(rb.linearVelocity.magnitude / 2f);
            float rotationAmount = horizontalInput * turnSpeed * turnMultiplier * Time.fixedDeltaTime;

            Quaternion turnRotation = Quaternion.Euler(0f, rotationAmount, 0f);
            rb.MoveRotation(rb.rotation * turnRotation);
        }
    }
}