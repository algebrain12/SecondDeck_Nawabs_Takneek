using UnityEngine;

public class mysteryBoxScript : MonoBehaviour
{

    [Header("Explosion Settings")]
    public bool destroyOnImpact = true;
    public GameObject explosionEffect;

    private void OnCollisionEnter(Collision collision)
    {
        BoatHealth boat = collision.gameObject.GetComponent<BoatHealth>();

        // In case the collider is on a child/parent object
        if (boat == null)
            boat = collision.gameObject.GetComponentInParent<BoatHealth>();

        if (boat != null)
        {
            boat.currentHealth -= UnityEngine.Random.Range(-30f, 30f);
            boat.currentHealth = Mathf.Clamp(boat.currentHealth, 0f, boat.maxHealth);
        }

        Explode();
    }

    void Explode()
    {
        if (destroyOnImpact)
        {
            Destroy(gameObject);
        }
    }
}
