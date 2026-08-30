using UnityEngine;

public class MinimapCameraFollow : MonoBehaviour
{
    [SerializeField] private float height = 100f;

    private void LateUpdate()
    {
        Vector3 position = transform.localPosition;
        position.y = height;

        transform.localPosition = position;
    }
}