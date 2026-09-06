using UnityEngine;

public class CloudShadowMover : MonoBehaviour
{
    [SerializeField] private float speed = 1f;
    [SerializeField] private Vector3 direction = Vector3.right;
    [SerializeField] private Vector2 bounds = new Vector2(-50, 50);

    void Update()
    {
        transform.position += direction * speed * Time.deltaTime;

        if (transform.position.x > bounds.y)
        {
            Vector3 pos = transform.position;
            pos.x = bounds.x;
            transform.position = pos;
        }
        else if (transform.position.x < bounds.x)
        {
            Vector3 pos = transform.position;
            pos.x = bounds.y;
            transform.position = pos;
        }

        if (transform.position.z > bounds.y)
        {
            Vector3 pos = transform.position;
            pos.z = bounds.x;
            transform.position = pos;
        }
        else if (transform.position.z < bounds.x)
        {
            Vector3 pos = transform.position;
            pos.z = bounds.y;
            transform.position = pos;
        }
    }
}
