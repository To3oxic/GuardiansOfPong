using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Linq;
public class LegendTowerController : NetworkBehaviour
{
    public float attackRange = 10f;
    public float fireRate;
    public int damage = 10;
    public LayerMask targetLayers; // Assign layers like "Unit"
    public Transform firePoint; // Where projectiles spawn from (assign in prefab)
    public GameObject projectilePrefab;
    private Collider[] hitBuffer = new Collider[10]; // set once outside method
    private float fireCooldown = 0f;
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
    private void Start()
    {
        if (IsServer)
            targetLayers = LayerMask.GetMask("Unit");
    }
    void Update()
    {
        if (!IsServer) return;
        fireCooldown -= Time.deltaTime;
        if (fireCooldown <= 0f)
        {
            TryShoot();
        }
    }
    void TryShoot()
    {
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, attackRange, hitBuffer, targetLayers);
        if (hitCount > 0)
        {
            Transform target = hitBuffer[0].transform;
            float minDist = Vector3.Distance(transform.position, target.position);
            for (int i = 1; i < hitCount; i++)
            {
                float dist = Vector3.Distance(transform.position, hitBuffer[i].transform.position);
                if (dist < minDist)
                {
                    minDist = dist;
                    target = hitBuffer[i].transform;
                }
            }
            fireCooldown = 1f / fireRate;
            Shoot(target);
        }
        Animator anim = gameObject.GetComponent<Animator>();
    }
    IEnumerator ResetAnimatorSpeed(float delay)
    {
        yield return new WaitForSeconds(delay);
        Animator anim = gameObject.GetComponent<Animator>();
        if (anim != null)
            anim.speed = 1f;
    }
    void Shoot(Transform target)
    {
        if (projectilePrefab != null && firePoint != null)
        {
            Vector3 shootDirection = (target.position - firePoint.position).normalized;
            GameObject proj = Instantiate(projectilePrefab, firePoint.position, Quaternion.LookRotation(shootDirection));

            // Use universal ProjectileController
            if (proj.TryGetComponent<ProjectileController>(out var projController))
            {
                projController.InitializeWithTarget(target, damage, OwnerClientId);
            }

            proj.GetComponent<NetworkObject>().Spawn();
        }
        Animator anim = gameObject.GetComponent<Animator>();
        if (anim != null)
        {
            anim.speed = fireRate;
            anim.SetTrigger("attack");
            SetAnimationTriggerClientRpc("attack");
        }
        StartCoroutine(ResetAnimatorSpeed(1f / fireRate));
    }
    [ClientRpc]
    void SetAnimationTriggerClientRpc(string triggerName)
    {
        Animator anim = gameObject.GetComponent<Animator>();
        if (!IsServer && anim != null)
        {
            anim.speed = fireRate;
            anim.SetTrigger(triggerName);
        }
    }
}
