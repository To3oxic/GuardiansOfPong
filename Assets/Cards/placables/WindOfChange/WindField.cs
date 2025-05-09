using UnityEngine;

public class WindFieldBehavior : MonoBehaviour
{
    public Vector3 direction = Vector3.forward;
    public float forceMultiplier = 1.0f;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Ball"))
        {
            Rigidbody rb = other.GetComponent<Rigidbody>();
            if (rb != null)
            {
                Vector3 newDirection = direction.normalized * rb.linearVelocity.magnitude * forceMultiplier;
                rb.linearVelocity = newDirection;
                Debug.Log("[WindField] Ball redirected!");
            }
        }
    }

}
