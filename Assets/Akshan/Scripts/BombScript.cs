using UnityEngine;

public class BombScript : MonoBehaviour
{
    public float Damage = 20f;

    [Header("Explosion Settings")]
    public bool destroyOnImpact = true;
    public GameObject explosionEffect;
    public AudioSource audioSource;

    private void OnCollisionEnter(Collision collision)
    {
        BoatHealth boat = collision.gameObject.GetComponent<BoatHealth>();

        // In case the collider is on a child/parent object
        if (boat == null)
            boat = collision.gameObject.GetComponentInParent<BoatHealth>();

        if (boat != null)
        {
            boat.TakeDamage(Damage);
        }

        Explode();
    }

    void Explode()
    {
        if (explosionEffect != null)
        {
            Instantiate(explosionEffect, transform.position, Quaternion.identity);
        }
        if (audioSource != null)
        {
            audioSource.Play();
        }
        if (destroyOnImpact)
        {
            Destroy(gameObject);
        }
    }
}