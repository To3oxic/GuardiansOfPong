using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class BalistaController : NetworkBehaviour, IPlaceable
{
    public int maxHP = 100;
    private int currentHP;
    public GameObject arrowPrefab;
    public Transform firePoint;
    public float drainDuration = 15f; // total time to live
    private float drainRate;
    public float fireRate = 0.5f;

    private float timer;

    private Vector3 shootDirection = Vector3.forward;

    public void SetShootDirection(Vector3 dir)
    {
        shootDirection = dir.normalized;
    }
    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        currentHP = maxHP;
        drainRate = maxHP / drainDuration;

        StartCoroutine(DrainOverTime());
    }
    IEnumerator DrainOverTime()
    {
        while (currentHP > 0)
        {
            yield return new WaitForSeconds(1f);
            currentHP -= Mathf.CeilToInt(drainRate);

            if (currentHP <= 0)
            {
                GetComponent<NetworkObject>().Despawn();
            }
        }
    }

    void Update()
    {
        if (!IsServer) return;

        timer += Time.deltaTime;
        if (timer >= fireRate)
        {
            timer = 0f;
            FireArrow();
        }
    }
    public void TakeDamage(int amount)
    {
        currentHP -= amount;
        if (currentHP <= 0)
        {
            GetComponent<NetworkObject>().Despawn();
        }
    }

    void FireArrow()
    {
        Debug.Log($"[BalistaController] Firing arrow. Ballista world position: {transform.position}, FirePoint world position: {firePoint.position}, ShootDirection: {shootDirection}");
        GameObject arrow = Instantiate(arrowPrefab, firePoint.position, Quaternion.LookRotation(shootDirection));
        arrow.GetComponent<ArrowProjectile>().Initialize(shootDirection, OwnerClientId);
        arrow.GetComponent<NetworkObject>().SpawnWithOwnership(OwnerClientId);


        var arrowScript = arrow.GetComponent<ArrowProjectile>();


        if (arrowScript != null)
        {
            arrowScript.SetDirection(shootDirection);
        }
    }

}
