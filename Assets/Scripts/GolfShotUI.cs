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

    [Header("Fade")]
    [SerializeField] private float fadeDuration = 0.2f;

    [Header("Angle Visual")]
    [SerializeField] private float minimumVisualAngle = 90f;
    [SerializeField] private float maximumVisualAngle = 160f;

    private Coroutine fadeCoroutine;

    private void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup != null)
            canvasGroup.alpha = 0f;

        SetCharge(0f);
        SetAngle(0f);
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
        if (angleArmPivot != null)
        {
            float normalizedAngle = Mathf.InverseLerp(
                20f,
                60f,
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
            angleText.text =
                $"{Mathf.RoundToInt(actualAngle)}°";

        // Tell the arc the actual shot angle.
        if (angleArc != null)
            angleArc.SetAngle(actualAngle);
    }

    private void UpdateAngleText(float normalizedAngle)
    {
        if (angleText == null)
            return;

        float actualAngle = Mathf.Lerp(
            20f,
            60f,
            normalizedAngle
        );

        angleText.text = $"{Mathf.RoundToInt(actualAngle)}°";
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