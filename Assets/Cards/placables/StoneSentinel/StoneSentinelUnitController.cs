using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class StoneSentinelUnit : NetworkBehaviour
{
    public float moveSpeed = 3f;
    public float attackRange = 1.5f;
    public float attackCooldown = 2f;
    public int attackDamage = 15;
    public int maxHP = 150;

    private bool isMoving = false;
    private bool shouldAttack = false;
    private bool isDead = false;
    private Rigidbody rb;
    private Animator animator;
    private Transform target;
    private float cooldownTimer = 0f;
    private int currentHP;

    public override void OnNetworkSpawn()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();

        if (!IsServer) return;

        currentHP = maxHP;
    }

    void Update()
    {
        animator.SetBool("IsWalking", isMoving);

        if (shouldAttack)
            animator.SetTrigger("Attack");

        if (isDead)
            animator.SetTrigger("Death");
    }
    private void FixedUpdate()
    {
        if (!IsServer) return;

        cooldownTimer -= Time.fixedDeltaTime;

        if (target == null || !target.gameObject.activeInHierarchy)
        {
            FindNewTarget();
        }

        if (target != null)
        {
            isMoving = true;
            shouldAttack = false;
            float distance = Vector3.Distance(transform.position, target.position);

            if (distance <= attackRange)
            {
                rb.linearVelocity = Vector3.zero;
                if (cooldownTimer <= 0f)
                {
                    AttackTarget();
                    cooldownTimer = attackCooldown;
                }
            }
            else
            {
                Vector3 direction = (target.position - transform.position).normalized;
                rb.linearVelocity = direction * moveSpeed;

                // Play walk animation
                animator.SetBool("IsWalking", true);
            }
        }
        else
        {
            // No target found — just move forward
            rb.linearVelocity = Vector3.forward * moveSpeed;
            animator.SetBool("IsWalking", true);
        }
    }

    private void FindNewTarget()
    {
        // Look for paddles or placeables only
        var targets = FindObjectsOfType<NetworkObject>();

        foreach (var obj in targets)
        {
            if (obj.TryGetComponent<PaddleController>(out var paddle) ||
                obj.TryGetComponent<IPlaceable>(out var placeable))
            {
                target = obj.transform;
                break;
            }
        }
    }

    private void AttackTarget()
    {
        if (target == null) return;
        shouldAttack = true;
        isMoving = false;

        animator.SetTrigger("Attack");

        if (target.TryGetComponent<IPlaceable>(out var placeable))
        {
            placeable.TakeDamage(attackDamage);
        }
        else if (target.TryGetComponent<PaddleController>(out var paddle))
        {
            paddle.ReduceHP(attackDamage);
        }
    }

    public void TakeDamage(int amount)
    {
        currentHP -= amount;
        if (currentHP <= 0)
        {
            StartCoroutine(HandleDeath());
        }
    }

    private IEnumerator HandleDeath()
    {
        isMoving = false;
        shouldAttack = false;
        isDead = true;
        animator.SetTrigger("Death");
        rb.linearVelocity = Vector3.zero;

        yield return new WaitForSeconds(1f); // Assume death animation is 1 second

        if (IsServer)
            GetComponent<NetworkObject>().Despawn();
    }
}
