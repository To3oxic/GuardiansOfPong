using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(Rigidbody))]
public class ArrowProjectile : NetworkBehaviour
{
    public float speed = 10f;
    public float lifetime = 5f;
    public int damage = 20;
    private Rigidbody rb;

    private Vector3 direction = Vector3.zero;
    private bool directionSet = false;
    private ulong ownerClientId;


    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public void SetDirection(Vector3 dir)
    {
        if (IsServer)
        {
            direction = dir.normalized;
            rb.linearVelocity = direction * speed;
            Debug.Log("direction" + direction);
            SetDirectionClientRpc(direction);
        }
    }

    public void Initialize(Vector3 direction, ulong ownerId)
    {
        this.direction = direction.normalized;
        this.ownerClientId = ownerId;
    }


    [ClientRpc]
    private void SetDirectionClientRpc(Vector3 dir)

    {
        Debug.Log($"[Arrow] Direction: {direction}, Velocity set: {direction * speed}");

        direction = dir.normalized;
        rb.linearVelocity = direction * speed;

        directionSet = true;
    }

    private void Start()
    {
        if (IsServer)
        {
            rb.linearVelocity = direction * speed;
            Destroy(gameObject, lifetime);
        }
    }

    public override void OnDestroy()
    {
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }
    }
    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;

        // Option 1: Code-based check to ignore the Ball.
        // Make sure your Ball prefab has the tag "Ball".
        // For a more robust solution, use Physics Layers and the Collision Matrix (Edit > Project Settings > Physics).
        if (other.CompareTag("Ball"))
        {
            return; // Arrow does not interact with or despawn on hitting the ball.
        }

        // --- From this point, any collision will likely result in the arrow despawning --- 

        // Handle hitting a Unit
        if (other.TryGetComponent<UnitController>(out var unit))
        {
            if (unit.OwnerClientId != ownerClientId) // Check for friendly fire
            {
                unit.TakeDamage(damage);
            }
            Destroy(gameObject); // Arrow despawns after hitting any unit (friendly or enemy)
            return; // Collision handled
        }

        // Handle hitting a Paddle
        if (other.TryGetComponent<PaddleController>(out var paddle))
        {
            Debug.Log($"[ArrowProjectile] Arrow hit paddle: {paddle.name}, despawning.");
            NetworkObject paddleNetObj = paddle.GetComponent<NetworkObject>();
            if (paddleNetObj != null && paddleNetObj.OwnerClientId != ownerClientId) // Check for friendly fire
            {
                paddle.ReduceHP(damage);
            }
            Destroy(gameObject); // Arrow despawns after hitting any paddle
            return; // Collision handled
        }

        // Handle hitting an IPlaceable
        if (other.TryGetComponent<IPlaceable>(out var placeable))
        {
            Component placeableComponent = placeable as Component;
            if (placeableComponent != null)
            {
                NetworkObject placeableNetObj = placeableComponent.GetComponent<NetworkObject>();
                if (placeableNetObj != null && placeableNetObj.OwnerClientId != ownerClientId) // Check for friendly fire
                {
                    placeable.TakeDamage(damage);
                }
            }
            Destroy(gameObject); // Arrow despawns after hitting any IPlaceable
            return; // Collision handled
        }

        // Handle hitting general environment like Walls, Goals, Obstacles, etc.
        // Add more tags here if you have other types of static colliders that should stop arrows.
        if (other.CompareTag("Wall") ||
            other.CompareTag("Goal") ||
            other.CompareTag("Environment") ||
            other.CompareTag("Obstacle"))
        {
            Debug.Log($"[ArrowProjectile] Arrow hit environment/goal: {other.name}, despawning.");
            Destroy(gameObject); // Arrow despawns
            return; // Collision handled
        }

        // If the arrow has hit any other Rigidbody or Trigger that wasn't handled above
        // (and wasn't the Ball), it should despawn. This makes it despawn on unexpected colliders too.
        // This check is broad. If you have specific triggers arrows should pass through, they need explicit checks like the Ball.
        if (other.gameObject != null) // Ensures 'other' is a valid GameObject
        {
            // Consider if 'other.isTrigger' matters. If it hit another trigger that's not special, should it despawn?
            // For simplicity and to match "collide with other colliders and despawn", we'll despawn.
            Debug.Log($"[ArrowProjectile] Arrow hit unhandled collider: {other.name}, despawning.");
            Destroy(gameObject);
        }
    }

}
