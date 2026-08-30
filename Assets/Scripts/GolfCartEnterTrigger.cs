using UnityEngine;

public class GolfCartEnterTrigger : MonoBehaviour, IInteractable
{
    [SerializeField] private GolfCartController golfCart;

    private InteractionSystem playerInteraction;

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other))
            return;

        playerInteraction =
            other.GetComponentInParent<InteractionSystem>();

        if (playerInteraction != null)
        {
            playerInteraction.SetInteractable(this);
        }

        golfCart.SetPlayerNearby(other, true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsPlayer(other))
            return;

        if (playerInteraction != null)
        {
            playerInteraction.SetInteractable(null);
            playerInteraction = null;
        }

        golfCart.SetPlayerNearby(other, false);
    }

    public void Interact()
    {
        golfCart.Interact();

        // Hide the prompt after pressing interact.
        if (playerInteraction != null)
        {
            playerInteraction.SetInteractable(null);
        }
    }

    private bool IsPlayer(Collider other)
    {
        if (!other.transform.root.CompareTag("Player"))
            return false;

        return other.GetComponentInParent<CharacterController>() != null;
    }
}