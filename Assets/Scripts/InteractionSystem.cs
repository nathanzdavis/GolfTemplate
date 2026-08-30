using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class InteractionSystem : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InputActionReference interactAction;

    [Header("UI")]
    [SerializeField] private GameObject interactionPrompt;
    [SerializeField] private Text interactionText;

    private IInteractable currentInteractable;

    private void OnEnable()
    {
        if (interactAction != null)
        {
            interactAction.action.Enable();
            interactAction.action.performed += OnInteract;
        }
    }

    private void OnDisable()
    {
        if (interactAction != null)
        {
            interactAction.action.performed -= OnInteract;
            interactAction.action.Disable();
        }
    }

    public void SetInteractable(IInteractable interactable)
    {
        currentInteractable = interactable;

        if (currentInteractable != null)
        {
            ShowPrompt();
        }
        else
        {
            HidePrompt();
        }
    }

    private void ShowPrompt()
    {
        if (interactionPrompt == null || interactionText == null)
            return;

        string key =
            interactAction.action.GetBindingDisplayString();

        interactionText.text =
            $"Press [{key}] To Interact";

        interactionPrompt.SetActive(true);
    }

    private void HidePrompt()
    {
        if (interactionPrompt != null)
            interactionPrompt.SetActive(false);
    }

    private void OnInteract(InputAction.CallbackContext context)
    {
        if (currentInteractable == null)
            return;

        currentInteractable.Interact();
    }
}