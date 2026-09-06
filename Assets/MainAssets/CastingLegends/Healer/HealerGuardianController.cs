using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class HealerGuardianController : GuardiansController
{
    [Header("Heal Particles Configuration")]
    public GameObject healParticlesPrefab;

    private GameObject instantiatedParticles;
    private Coroutine activeHealCoroutine;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Ensure fireRate is configured for a 15-second cooldown (1 / 15 = 0.0666)
        fireRate = 1f / 15f;

        // Initialize and attach the particle system to the owner's paddle
        StartCoroutine(InitializeHealParticles());
    }

    public override void OnNetworkDespawn()
    {
        if (instantiatedParticles != null)
        {
            Destroy(instantiatedParticles);
        }
        base.OnNetworkDespawn();
    }

    private void OnDestroy()
    {
        if (instantiatedParticles != null)
        {
            Destroy(instantiatedParticles);
        }
    }

    private IEnumerator InitializeHealParticles()
    {
        // Wait until target paddle is spawned and accessible
        PaddleController targetPaddle = null;
        while (targetPaddle == null)
        {
            PaddleController[] allPaddles = FindObjectsByType<PaddleController>(FindObjectsSortMode.None);
            foreach (var paddle in allPaddles)
            {
                if (paddle != null && paddle.OwnerClientId == OwnerClientId)
                {
                    targetPaddle = paddle;
                    break;
                }
            }
            yield return null;
        }

        if (healParticlesPrefab != null)
        {
            // Instantiate locally on all clients (and server) under the paddle
            instantiatedParticles = Instantiate(healParticlesPrefab, targetPaddle.transform);
            instantiatedParticles.transform.localPosition = Vector3.zero;
            instantiatedParticles.transform.localRotation = Quaternion.identity;
            instantiatedParticles.SetActive(false);
        }
        else
        {
        }
    }

    protected override void FindTarget()
    {
        // Healer target is always the owner paddle
        if (currentTarget != null) return;

        PaddleController[] allPaddles = FindObjectsByType<PaddleController>(FindObjectsSortMode.None);
        foreach (var paddle in allPaddles)
        {
            if (paddle != null && paddle.OwnerClientId == OwnerClientId)
            {
                currentTarget = paddle.transform;
                break;
            }
        }
    }

    protected override void HandleAttackAttempt()
    {
        if (currentTarget == null) return;

        if (currentTarget.TryGetComponent<PaddleController>(out var paddle))
        {
            // Only attempt healing if the paddle is alive, damaged, and cooldown has elapsed
            if (paddle.IsAlive() && paddle.currentHP.Value < paddle.maxHP && Time.time >= nextFireTime)
            {
                // Play attack/heal animation on clients
                if (animator != null && !string.IsNullOrEmpty(attackTriggerName))
                {
                    animator.SetTrigger(attackTriggerName);
                    PlayAttackAnimationClientRpc();
                }

                // Trigger instant healing
                ActualFireProjectile();

                // Enforce 15-second cooldown
                nextFireTime = Time.time + 15f;
            }
        }
    }

    protected override void ActualFireProjectile()
    {
        if (currentTarget == null) return;

        if (currentTarget.TryGetComponent<PaddleController>(out var paddle))
        {
            if (paddle.IsAlive())
            {
                // Deal negative damage (healing) to the paddle
                // Using projectileDamage value if configured on the prefab, or a default healing value
                float healAmount = projectileDamage > 0 ? projectileDamage : 150f;
                paddle.ReduceHP(-healAmount);

                // Trigger 2-second particle effect on all clients and server
                TriggerHealVfxClientRpc();

                // Optionally spawn visual/particle effect at paddle position if configured
                if (projectilePrefab != null)
                {
                    GameObject fx = Instantiate(projectilePrefab, paddle.transform.position, Quaternion.identity);
                    if (fx.TryGetComponent<NetworkObject>(out var no))
                    {
                        no.Spawn(true);
                        StartCoroutine(DespawnAfterDelay(no, 3f));
                    }
                    else
                    {
                        Destroy(fx, 3f);
                    }
                }
            }
        }
    }

    [ClientRpc]
    private void TriggerHealVfxClientRpc()
    {
        if (activeHealCoroutine != null)
        {
            StopCoroutine(activeHealCoroutine);
        }
        activeHealCoroutine = StartCoroutine(ActivateHealParticlesCoroutine());
    }

    private IEnumerator ActivateHealParticlesCoroutine()
    {
        if (instantiatedParticles != null)
        {
            instantiatedParticles.SetActive(true);
            ParticleSystem ps = instantiatedParticles.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                ps.Play(true);
            }

            yield return new WaitForSeconds(2f);

            if (instantiatedParticles != null)
            {
                if (ps != null)
                {
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
                // Wait briefly for existing particles to naturally disperse/fade
                yield return new WaitForSeconds(1.5f);
                if (instantiatedParticles != null)
                {
                    instantiatedParticles.SetActive(false);
                }
            }
        }
        activeHealCoroutine = null;
    }

    private IEnumerator DespawnAfterDelay(NetworkObject no, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (no != null && no.IsSpawned)
        {
            no.Despawn(true);
        }
    }
}
