using StarterAssets;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class GolfShotUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Image gradient;
    [SerializeField] private RectTransform angleArmPivot;
    [SerializeField] private Text angleText;
    [SerializeField] private UIArc angleArc;

    [Header("Wind")]
    [SerializeField] private Image windDirection;
    [SerializeField] private Text windSpeed;
    [SerializeField] private WindGenerator windGenerator;
    [SerializeField] private ThirdPersonController player;

    [Header("Fade")]
    [SerializeField] private float fadeDuration = 0.2f;

    [Header("Angle Visual")]
    [SerializeField] private float minimumVisualAngle = 90f;
    [SerializeField] private float maximumVisualAngle = 160f;

    [Header("Actual Angle")]
    [SerializeField] private float minimumActualAngle = 0f;
    [SerializeField] private float maximumActualAngle = 60f;

    private Coroutine fadeCoroutine;

    private void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup != null)
            canvasGroup.alpha = 0f;

        SetCharge(0f);
        SetAngle(minimumActualAngle);
    }

    private void Update()
    {
        UpdateWindUI(player._mainCamera.GetComponent<Camera>());
    }

    public void SetPreparing(bool preparing)
    {
        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);

        fadeCoroutine = StartCoroutine(
            FadeCanvas(preparing ? 1f : 0f)
        );
    }

    public void SetCharge(float normalizedCharge)
    {
        if (gradient == null)
            return;

        gradient.fillAmount = Mathf.Clamp01(normalizedCharge);
    }

    public void SetAngle(float actualAngle)
    {
        actualAngle = Mathf.Clamp(
            actualAngle,
            minimumActualAngle,
            maximumActualAngle
        );

        if (angleArmPivot != null)
        {
            float normalizedAngle = Mathf.InverseLerp(
                minimumActualAngle,
                maximumActualAngle,
                actualAngle
            );

            float zAngle = Mathf.Lerp(
                minimumVisualAngle,
                maximumVisualAngle,
                normalizedAngle
            );

            Vector3 rotation =
                angleArmPivot.localEulerAngles;

            rotation.z = zAngle;

            angleArmPivot.localEulerAngles =
                rotation;
        }

        if (angleText != null)
        {
            angleText.text =
                $"{Mathf.RoundToInt(actualAngle)}°";
        }

        if (angleArc != null)
        {
            angleArc.SetAngle(actualAngle);
        }
    }

    // ============================================================
    // WIND
    // ============================================================

    public void UpdateWindUI(Camera camera)
    {
        if (windGenerator == null || camera == null)
            return;

        Vector3 windWorldDirection =
            windGenerator.WindDirection;

        windWorldDirection.y = 0f;

        if (windWorldDirection.sqrMagnitude < 0.001f)
            return;

        windWorldDirection.Normalize();

        // Get the camera's horizontal forward/right vectors.
        Vector3 cameraForward = camera.transform.forward;
        cameraForward.y = 0f;

        Vector3 cameraRight = camera.transform.right;
        cameraRight.y = 0f;

        if (cameraForward.sqrMagnitude < 0.001f ||
            cameraRight.sqrMagnitude < 0.001f)
            return;

        cameraForward.Normalize();
        cameraRight.Normalize();

        // Convert world wind direction into camera-relative coordinates.
        float horizontal =
            Vector3.Dot(
                windWorldDirection,
                cameraRight
            );

        float vertical =
            Vector3.Dot(
                windWorldDirection,
                cameraForward
            );

        Vector2 direction2D =
            new Vector2(
                horizontal,
                vertical
            );

        if (direction2D.sqrMagnitude < 0.001f)
            return;

        direction2D.Normalize();

        // ------------------------------------------------------------
        // WIND SPEED
        // ------------------------------------------------------------

        if (windSpeed != null)
        {
            windSpeed.text =
                $"{windGenerator.WindSpeed:0.0} m/s";
        }

        // ------------------------------------------------------------
        // WIND DIRECTION
        // ------------------------------------------------------------

        if (windDirection != null)
        {
            RectTransform arrow =
                windDirection.rectTransform;

            // The V starts pointing DOWN.
            Vector2 defaultDirection =
                Vector2.down;

            float angle =
                Vector2.SignedAngle(
                    defaultDirection,
                    direction2D
                );

            arrow.localRotation =
                Quaternion.Euler(
                    0f,
                    0f,
                    angle
                );
        }
    }

    private IEnumerator FadeCanvas(float targetAlpha)
    {
        if (canvasGroup == null)
            yield break;

        float startAlpha = canvasGroup.alpha;
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(
                elapsed / fadeDuration
            );

            t = t * t * (3f - 2f * t);

            canvasGroup.alpha = Mathf.Lerp(
                startAlpha,
                targetAlpha,
                t
            );

            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
        fadeCoroutine = null;
    }
}