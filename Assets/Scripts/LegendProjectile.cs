using UnityEngine;
using Unity.Netcode;

public class LegendProjectile : NetworkBehaviour
{
    public float speed = 15f;
    private Transform target;
    private int damage;

    public void Initialize(Transform target, int damage)
    {
        this.target = target;
        this.damage = damage;
    }

    void Update()
    {
        if (!IsServer) return;
        if (target == null)
        {
            Destroy(gameObject);
        }

        Vector3 dir = (target.position - transform.position).normalized;
        transform.position += dir * speed * Time.deltaTime;

        if (Vector3.Distance(transform.position, target.position) < 0.5f)
        {
            var unit = target.GetComponent<UnitController>();
            if (unit != null)
            {
                unit.TakeDamage(damage);
            }


            Destroy(gameObject);
        }
    }

}
