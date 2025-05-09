using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Linq;

public class UnitController : NetworkBehaviour
{
    private Transform enemyPaddle;
    public float moveSpeed = 3f;
    public int maxHP = 100;
    public float attackRange = 2f;
    public float attackCooldown = 1f;
    public int damage = 10;
    public LayerMask targetLayers;

    private int currentHP;
    private float cooldownTimer = 0f;
    private Transform currentTarget;
    private Rigidbody rb;


    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            currentHP = maxHP;
            FindEnemyPaddle(); // ✅ Set target on spawn
        }

        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        if (!IsServer) return;

        cooldownTimer -= Time.deltaTime;

        if (currentTarget == null || !IsTargetValid(currentTarget))
        {
            currentTarget = FindTarget();
        }

        // Move toward enemy paddle if nothing else is closer
        Transform moveTarget = currentTarget ?? enemyPaddle;
        if (moveTarget == null)
        {
            return;
        }

        float distance = Vector3.Distance(transform.position, moveTarget.position);

        if (distance <= attackRange)
        {
            if (cooldownTimer <= 0f)
            {
                Attack(moveTarget);
                cooldownTimer = attackCooldown;
            }
            rb.linearVelocity = Vector3.zero;
        }
        else
        {
            Vector3 dir = (moveTarget.position - transform.position).normalized;
            rb.linearVelocity = dir * moveSpeed;
            Debug.DrawRay(transform.position, dir * 3f, Color.green);
        }
    }

    void FindEnemyPaddle()
    {
        PaddleController[] paddles = FindObjectsOfType<PaddleController>();
        foreach (var paddle in paddles)
        {
            if (paddle.OwnerClientId != OwnerClientId)
            {
                enemyPaddle = paddle.transform;
                Debug.Log($"[UnitController] Enemy paddle set to {enemyPaddle.name}");
                return;
            }
        }
        Debug.LogWarning("[UnitController] No enemy paddle found!");
    }

    Transform FindTarget()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, attackRange + 5f, targetLayers);

        if (hits.Length > 0)
        {
            return hits
                .OrderBy(h => Vector3.Distance(transform.position, h.transform.position))
                .Select(h => h.transform)
                .FirstOrDefault();
        }

        return null;
    }

    bool IsTargetValid(Transform target)
    {
        if (target == null) return false;
        return Vector3.Distance(transform.position, target.position) < attackRange + 5f;
    }

    void Attack(Transform target)
    {
        var paddle = target.GetComponent<PaddleController>();
        var otherUnit = target.GetComponent<UnitController>();

        if (paddle != null)
        {
            paddle.ReduceHP(damage);
        }
        else if (otherUnit != null)
        {
            otherUnit.TakeDamage(damage);
        }
    }

    public void TakeDamage(int amount)
    {
        if (!IsServer) return;

        currentHP -= amount;
        Debug.Log("[UnitController] Took damage: " + amount + ". Current HP: " + currentHP);

        if (currentHP <= 0)
        {
            Debug.Log("[UnitController] Died.");
            GetComponent<NetworkObject>().Despawn();
        }
    }
}