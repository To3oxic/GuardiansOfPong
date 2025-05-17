using UnityEngine;
using Unity.Netcode;

public class GuardiansProjectile : NetworkBehaviour
{
    public float speed = 15f;
    private Transform target;
    private int damage;
    private ulong ownerId; // To ensure projectile doesn't hit owner's units if needed, or for other logic

    public void Initialize(Transform target, int damage, ulong ownerId)
    {
        this.target = target;
        this.damage = damage;
        this.ownerId = ownerId;
    }

    void Update()
    {
        if (!IsServer) return;

        if (target == null)
        {
            // If the target is lost, destroy the projectile
            if (NetworkObject != null && NetworkObject.IsSpawned)
            {
                NetworkObject.Despawn(true); // true to destroy the object on all clients
            }
            else
            {
                Destroy(gameObject); // Fallback for non-networked or unspawned objects
            }
            return;
        }

        Vector3 dir = (target.position - transform.position).normalized;
        transform.position += dir * speed * Time.deltaTime;

        // Check for proximity to target
        if (Vector3.Distance(transform.position, target.position) < 0.5f)
        {
            // Attempt to get the UnitController from the target
            // This could be BaseUnitController or any other unit type
            var unit = target.GetComponent<BaseUnitController>(); // Assuming targets are BaseUnitController
            if (unit != null)
            {
                // Optional: Check if the unit belongs to a different owner before applying damage
                // if (unit.OwnerClientId != ownerId) 
                // {
                unit.TakeDamage(damage);
                // }
            }
            
            // Projectile has reached its target or close enough, despawn it
            if (NetworkObject != null && NetworkObject.IsSpawned)
            {
                NetworkObject.Despawn(true);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}
