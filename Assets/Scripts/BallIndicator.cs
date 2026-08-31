using UnityEngine;

public class BallIndicator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private Transform ball;
    [SerializeField] private Camera cam;
    [SerializeField] private RectTransform indicator;

    [Header("Position Settings")]
    [SerializeField] private float showDistance = 10f;
    [SerializeField] private float screenPadding = 50f;
    [SerializeField] private float heightAboveBall = 80f;

    [Header("Size Settings")]
    [SerializeField] private float maxDistanceForScaling = 100f;
    [SerializeField] private float minimumScale = 0.65f;

    private Vector3 defaultScale;

    private void Awake()
    {
        if (indicator != null)
            defaultScale = indicator.localScale;
    }

    private void Update()
    {
        if (player == null || ball == null || cam == null || indicator == null)
            return;

        if (!ball.gameObject.activeSelf)
        {
            indicator.gameObject.SetActive(false);
            return;
        }

        Vector3 screenPosition = cam.WorldToScreenPoint(ball.position);

        bool onScreen =
            screenPosition.z > 0f &&
            screenPosition.x >= 0f &&
            screenPosition.x <= Screen.width &&
            screenPosition.y >= 0f &&
            screenPosition.y <= Screen.height;

        // ============================================
        // BALL IS ON SCREEN
        // ============================================

        if (onScreen)
        {
            float distance = Vector3.Distance(
                player.position,
                ball.position
            );

            if (distance <= showDistance)
            {
                indicator.gameObject.SetActive(false);
                return;
            }

            indicator.gameObject.SetActive(true);

            screenPosition.y += heightAboveBall;

            indicator.position = screenPosition;
            indicator.rotation = Quaternion.identity;

            UpdateIndicatorScale(distance);

            return;
        }

        // ============================================
        // BALL IS OFF SCREEN
        // ============================================

        indicator.gameObject.SetActive(true);

        // Always use the normal/default size when off-screen.
        indicator.localScale = defaultScale;

        SetEdgeIndicator(screenPosition);
    }

    private void UpdateIndicatorScale(float distance)
    {
        // 0 = closest/show distance
        // 1 = max scaling distance
        float t = Mathf.InverseLerp(
            showDistance,
            maxDistanceForScaling,
            distance
        );

        // Scale from 1 down to minimumScale.
        float scale = Mathf.Lerp(
            1f,
            minimumScale,
            t
        );

        indicator.localScale =
            defaultScale * scale;
    }

    private void SetEdgeIndicator(Vector3 screenPosition)
    {
        Vector2 screenCenter = new Vector2(
            Screen.width * 0.5f,
            Screen.height * 0.5f
        );

        Vector2 ballPosition = new Vector2(
            screenPosition.x,
            screenPosition.y
        );

        // Ball behind camera.
        if (screenPosition.z < 0f)
        {
            ballPosition =
                screenCenter * 2f - ballPosition;
        }

        Vector2 direction =
            ballPosition - screenCenter;

        if (direction.sqrMagnitude < 0.001f)
            direction = Vector2.down;

        float halfWidth = Screen.width * 0.5f;
        float halfHeight = Screen.height * 0.5f;

        float scaleX =
            Mathf.Abs(direction.x) > 0.001f
                ? halfWidth / Mathf.Abs(direction.x)
                : float.MaxValue;

        float scaleY =
            Mathf.Abs(direction.y) > 0.001f
                ? halfHeight / Mathf.Abs(direction.y)
                : float.MaxValue;

        float scale =
            Mathf.Min(scaleX, scaleY);

        Vector2 edgePosition =
            screenCenter + direction * scale;

        edgePosition.x = Mathf.Clamp(
            edgePosition.x,
            screenPadding,
            Screen.width - screenPadding
        );

        edgePosition.y = Mathf.Clamp(
            edgePosition.y,
            screenPadding,
            Screen.height - screenPadding
        );

        indicator.position = edgePosition;

        float angle =
            Mathf.Atan2(direction.y, direction.x)
            * Mathf.Rad2Deg;

        indicator.rotation =
            Quaternion.Euler(
                0f,
                0f,
                angle + 90f
            );
    }
}