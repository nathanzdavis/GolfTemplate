using UnityEngine;

public class GolfCartEnterTrigger : MonoBehaviour
{
    [SerializeField] private GolfCartController golfCart;

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other))
            return;

        golfCart.SetPlayerNearby(other, true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsPlayer(other))
            return;

        golfCart.SetPlayerNearby(other, false);
    }

    private bool IsPlayer(Collider other)
    {
        // Check the object or one of its parents for the Player tag.
        if (!other.transform.root.CompareTag("Player"))
            return false;

        // Make sure it actually has a CharacterController.
        return other.GetComponentInParent<CharacterController>() != null;
    }
}