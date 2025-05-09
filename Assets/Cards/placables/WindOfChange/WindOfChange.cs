using UnityEngine;
using System.Collections;
using System;

[CreateAssetMenu(fileName = "WindOfChangeEffect", menuName = "Cards/Effects/WindOfChange")]
public class WindOfChangeEffect : CardEffect
{
    public GameObject WindOfChangePrefab;
    public float duration = 5f;
    public override bool RequiresPlacement() => true;

    public override void ApplyEffect(ulong playerId)
    {

    }

    public override void ApplyEffect(ulong playerId, Vector3 position, Vector3 direction)
    {
        // This will be called on server
        if (GameManager.Instance != null)
        {
            GameObject field = GameObject.Instantiate(WindOfChangePrefab, position, Quaternion.LookRotation(direction));
            Debug.Log("[Visual] Wind of Change field placed.");
        }
    }


    public override void StartPlacement(Action<Vector3, Vector3> onPlacementConfirmed)
    {
        WindOfChangePlacer.Instance.StartPlacing(onPlacementConfirmed);

    }


    public override void ApplyVisualEffect(ulong playerId, Vector3 position, Vector3 direction)
    {
        if (WindOfChangePrefab != null)
        {
            GameObject field = GameObject.Instantiate(WindOfChangePrefab, position, Quaternion.LookRotation(direction));
            Debug.Log("[Visual] Wind of Change field placed.");
        }
    }
}
