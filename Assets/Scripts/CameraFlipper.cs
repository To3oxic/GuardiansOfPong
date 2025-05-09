using System.Collections;
using UnityEngine;
using Unity.Netcode;

public class CameraFlipper : MonoBehaviour
{

    IEnumerator Start()
    {
        // Wait for NetworkManager and ownership
        while (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            yield return null;

        // Wait until local paddle is spawned
        PaddleController localPaddle = null;
        float timeout = 10f;
        float elapsed = 0f;

        while (localPaddle == null && elapsed < timeout)
        {
            PaddleController[] paddles = FindObjectsByType<PaddleController>(FindObjectsSortMode.None);
            foreach (var paddle in paddles)
            {
                if (paddle.IsOwner)
                {
                    localPaddle = paddle;
                    break;
                }
            }
            elapsed += 0.2f;
            yield return new WaitForSeconds(0.2f);
        }

        if (localPaddle == null)
        {
            Debug.LogWarning("[CameraFlipper] Could not find local paddle.");
            yield break;
        }

        // Rotate camera if the local paddle is at the top half of the map
        if (localPaddle.transform.position.z < 0)
        {
            transform.Rotate(0f, 0f, 180f);
            Debug.Log("[CameraFlipper] Rotated camera to follow top paddle.");
        }
        else
        {
            Debug.Log("[CameraFlipper] Camera facing bottom paddle already.");
        }

    }
}
