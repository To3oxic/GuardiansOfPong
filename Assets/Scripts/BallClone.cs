using UnityEngine;

public class BallClone : MonoBehaviour
{
    public float lifetime = 4f;

    private void Start()
    {
        Destroy(gameObject, lifetime);
    }

    private void OnTriggerEnter(Collider other)
    {
        // Clones should not score goals or damage paddles
        if (other.CompareTag("Goal") || other.CompareTag("Paddle"))
        {
            // Optionally, bounce or just ignore
            Debug.Log("[BallClone] Collision with " + other.name + " ignored.");
        }
    }
}
