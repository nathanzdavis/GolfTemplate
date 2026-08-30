using UnityEngine;

public class MinimapController : MonoBehaviour
{
    // ============================================================
    // REFERENCES
    // ============================================================

    [Header("References")]
    [SerializeField] private GolfController golfController;
    [SerializeField] private Transform player;
    [SerializeField] private WindGenerator windGenerator;

    [Tooltip("The UI RectTransform containing the minimap.")]
    [SerializeField] private RectTransform minimapRect;

    [Tooltip("The UI Image/RectTransform used for the aim line.")]
    [SerializeField] private RectTransform aimLine;

    // ============================================================
    // AIM LINE
    // ============================================================

    [Header("Aim Line")]
    [SerializeField] private float minimumLineLength = 20f;

    [SerializeField] private float maximumLineLength = 150f;

    [SerializeField] private float lineHeight = 4f;

    // ============================================================
    // UNITY
    // ============================================================

    private void Update()
    {
        if (golfController == null ||
            player == null ||
            minimapRect == null ||
            aimLine == null)
        {
            return;
        }

        if (!golfController.preparingShot)
        {
            aimLine.gameObject.SetActive(false);
            return;
        }

        aimLine.gameObject.SetActive(true);

        UpdateAimLine();
    }

    // ============================================================
    // AIM LINE
    // ============================================================

    private void UpdateAimLine()
    {
        // ============================================================
        // GET SHOT DIRECTION
        // ============================================================

        Vector3 shotDirection =
            golfController.GetCurrentShotDirection();

        shotDirection.y = 0f;

        if (shotDirection.sqrMagnitude < 0.001f)
            return;

        shotDirection.Normalize();

        // ============================================================
        // CONVERT WORLD DIRECTION TO PLAYER-LOCAL SPACE
        // ============================================================

        Vector3 localDirection =
            player.InverseTransformDirection(shotDirection);

        // The minimap camera is offset along local -X
        // and rotated 270 degrees.
        //
        // The UI Image's local X axis points RIGHT.

        float uiAngle =
            Mathf.Atan2(
                localDirection.z,
                localDirection.x
            ) * Mathf.Rad2Deg;

        uiAngle -= 90f;

        Quaternion targetRotation =
            Quaternion.Euler(
                0f,
                0f,
                uiAngle
            );

        // Smooth rotation.
        aimLine.localRotation =
            Quaternion.RotateTowards(
                aimLine.localRotation,
                targetRotation,
                720f * Time.deltaTime
            );

        // ============================================================
        // CALCULATE PREDICTED SHOT DISTANCE
        // ============================================================

        float targetDistance =
            CalculatePredictedDistance();

        // ============================================================
        // GET MINIMAP CAMERA
        // ============================================================

        Camera minimapCamera =
            FindMinimapCamera();

        if (minimapCamera == null)
            return;

        // ============================================================
        // WORLD UNITS -> UI PIXELS
        // ============================================================

        float minimapWorldSize =
            minimapCamera.orthographicSize * 2f;

        if (minimapWorldSize <= 0.001f)
            return;

        float pixelsPerWorldUnit =
            minimapRect.rect.width /
            minimapWorldSize;

        float targetLength =
            targetDistance *
            pixelsPerWorldUnit;

        targetLength =
            Mathf.Clamp(
                targetLength,
                minimumLineLength,
                maximumLineLength
            );

        // ============================================================
        // SMOOTH LENGTH
        // ============================================================

        Vector2 size =
            aimLine.sizeDelta;

        float smoothedLength =
            Mathf.Lerp(
                size.x,
                targetLength,
                10f * Time.deltaTime
            );

        size.x = smoothedLength;
        size.y = lineHeight;

        aimLine.sizeDelta = size;
    }

    // ============================================================
    // MINIMAP SCALE
    // ============================================================

    private float GetMinimapWorldDiameter()
    {
        if (Camera.main == null)
        {
            // Fallback if no camera is available.
            return 1f;
        }

        // Orthographic size represents half the vertical
        // world-space size of the camera.
        //
        // For a square minimap:
        //
        // diameter = orthographicSize * 2

        Camera minimapCamera =
            FindMinimapCamera();

        if (minimapCamera == null)
            return 1f;

        return minimapCamera.orthographicSize * 2f;
    }

    private Camera FindMinimapCamera()
    {
        Camera[] cameras =
            FindObjectsByType<Camera>();

        foreach (Camera camera in cameras)
        {
            if (camera.CompareTag("MinimapCamera"))
                return camera;
        }

        return null;
    }

    // ============================================================
    // SHOT DISTANCE
    // ============================================================

    private float CalculatePredictedDistance()
    {
        Rigidbody ball =
            golfController.CurrentGolfBall;

        if (ball == null)
            return 0f;

        // ============================================================
        // CHARGE
        // ============================================================

        float charge =
            golfController.CurrentChargePercent;

        // Before charging, show a full-power prediction.
        if (charge <= 0.001f)
            charge = 1f;

        // ============================================================
        // INITIAL VELOCITY
        // ============================================================

        float force =
            golfController.MaxShotForce * charge;

        // GolfBall uses ForceMode.Impulse.
        // Impulse = mass * change in velocity.
        float launchSpeed =
            force / ball.mass;

        // ============================================================
        // SHOT DIRECTION
        // ============================================================

        Camera mainCamera = Camera.main;

        if (mainCamera == null)
            return 0f;

        Vector3 forward =
            mainCamera.transform.forward;

        forward.y = 0f;

        if (forward.sqrMagnitude < 0.001f)
            return 0f;

        forward.Normalize();

        float angle =
            golfController.CurrentShotAngle *
            Mathf.Deg2Rad;

        Vector3 velocity =
            forward *
            Mathf.Cos(angle) *
            launchSpeed;

        velocity +=
            Vector3.up *
            Mathf.Sin(angle) *
            launchSpeed;

        // ============================================================
        // SIMULATE
        // ============================================================

        Vector3 position =
            ball.position;

        float simulationTime = 0f;

        const float timeStep = 0.02f;
        const float maxSimulationTime = 30f;

        while (simulationTime < maxSimulationTime)
        {
            // Gravity
            velocity +=
                Physics.gravity *
                timeStep;

            // Wind
            if (windGenerator != null)
            {
                Vector3 windDirection =
                    windGenerator.WindDirection;

                float windSpeed =
                    windGenerator.WindSpeed;

                if (windDirection.sqrMagnitude > 0.001f)
                {
                    float windAcceleration =
                        windSpeed *
                        windSpeed *
                        0.01f;

                    windAcceleration =
                        Mathf.Min(
                            windAcceleration,
                            10f
                        );

                    velocity +=
                        windDirection.normalized *
                        windAcceleration *
                        timeStep;
                }
            }

            // Move simulated ball
            position +=
                velocity *
                timeStep;

            simulationTime += timeStep;

            // Ball has returned to its original height.
            if (position.y <= ball.position.y)
                break;
        }

        // ============================================================
        // HORIZONTAL DISTANCE
        // ============================================================

        Vector3 displacement =
            position -
            ball.position;

        displacement.y = 0f;

        return displacement.magnitude;
    }
}