using UnityEngine;

public class Cannonball : MonoBehaviour
{
    public float lifetime = 5f;
    public float impactForce = 10f;
    public float DamageValue = 10f;
    public bool lockVelocity = true; // set false if you want gravity/drag/fields to affect it after launch

    private Vector3 lockedVelocity;
    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        lockedVelocity = rb.linearVelocity;
        Destroy(gameObject, lifetime);
    }

    void FixedUpdate()
    {
        if (lockVelocity)
            rb.linearVelocity = lockedVelocity;
    }

void OnCollisionEnter(Collision collision)
{
    BoatHealth health = collision.gameObject.GetComponent<BoatHealth>();
    if (health != null)
    {
        health.TakeDamage(DamageValue); // adjust damage value as needed
    }

    Rigidbody hitRb = collision.rigidbody;
    if (hitRb != null)
    {
        Vector3 hitDirection = (collision.transform.position - transform.position).normalized;
        hitRb.AddForce(hitDirection * impactForce, ForceMode.Impulse);
    }

    Destroy(gameObject);
}
}