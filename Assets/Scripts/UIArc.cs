using UnityEngine;
using UnityEngine.UI;

public class UIArc : MaskableGraphic
{
    [Header("Endpoints")]
    [SerializeField] private RectTransform startPoint;
    [SerializeField] private RectTransform endPoint;

    [Header("Arc")]
    [SerializeField] private float width = 4f;

    [SerializeField] private float minimumBow = 0f;
    [SerializeField] private float maximumBow = 20f;

    [SerializeField] private int minimumSegments = 4;
    [SerializeField] private int maximumSegments = 32;

    [Header("Angle Range")]
    [SerializeField] private float minimumAngle = 20f;
    [SerializeField] private float maximumAngle = 60f;

    [SerializeField, Range(0.5f, 4f)]
    private float bowCurve = 2f;

    private float currentAngle;

    private void Update()
    {
        // The angle arms can move at runtime, so force the
        // UI mesh to rebuild every frame.
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        if (startPoint == null || endPoint == null)
            return;

        // ------------------------------------------------------------
        // NORMALIZE ANGLE
        // ------------------------------------------------------------

        float normalizedAngle =
            Mathf.InverseLerp(
                minimumAngle,
                maximumAngle,
                currentAngle
            );

        // ------------------------------------------------------------
        // HIDE AT MINIMUM ANGLE
        // ------------------------------------------------------------

        if (normalizedAngle <= 0.001f)
            return;

        // ------------------------------------------------------------
        // MAKE LOW ANGLES MUCH FLATTER
        // ------------------------------------------------------------

        float curvedAmount =
            Mathf.Pow(
                normalizedAngle,
                bowCurve
            );

        float currentBow =
            Mathf.Lerp(
                minimumBow,
                maximumBow,
                curvedAmount
            );

        // ------------------------------------------------------------
        // SEGMENTS
        // ------------------------------------------------------------

        int currentSegments =
            Mathf.RoundToInt(
                Mathf.Lerp(
                    minimumSegments,
                    maximumSegments,
                    curvedAmount
                )
            );

        // ------------------------------------------------------------
        // ENDPOINTS
        // ------------------------------------------------------------

        Vector2 start =
            WorldToLocal(
                startPoint.position
            );

        Vector2 end =
            WorldToLocal(
                endPoint.position
            );

        Vector2 direction =
            end - start;

        if (direction.sqrMagnitude < 0.001f)
            return;

        Vector2 perpendicular =
            new Vector2(
                -direction.y,
                direction.x
            ).normalized;

        // ------------------------------------------------------------
        // DRAW
        // ------------------------------------------------------------

        for (int i = 0; i < currentSegments; i++)
        {
            float t0 =
                i / (float)currentSegments;

            float t1 =
                (i + 1) / (float)currentSegments;

            Vector2 p0 =
                GetArcPoint(
                    start,
                    end,
                    perpendicular,
                    t0,
                    currentBow
                );

            Vector2 p1 =
                GetArcPoint(
                    start,
                    end,
                    perpendicular,
                    t1,
                    currentBow
                );

            AddSegment(
                vh,
                p0,
                p1
            );
        }
    }

    private Vector2 WorldToLocal(Vector3 worldPosition)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTransform,
            RectTransformUtility.WorldToScreenPoint(
                null,
                worldPosition
            ),
            null,
            out Vector2 localPoint
        );

        return localPoint;
    }

    private Vector2 GetArcPoint(
    Vector2 start,
    Vector2 end,
    Vector2 perpendicular,
    float t,
    float currentBow)
    {
        Vector2 point =
            Vector2.Lerp(
                start,
                end,
                t
            );

        float curve =
            Mathf.Sin(t * Mathf.PI) *
            currentBow;

        point += perpendicular * curve;

        return point;
    }

    private void AddSegment(
        VertexHelper vh,
        Vector2 start,
        Vector2 end)
    {
        Vector2 direction =
            (end - start).normalized;

        Vector2 perpendicular =
            new Vector2(
                -direction.y,
                direction.x
            ) * (width * 0.5f);

        int index = vh.currentVertCount;

        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = color;

        vertex.position = start + perpendicular;
        vh.AddVert(vertex);

        vertex.position = start - perpendicular;
        vh.AddVert(vertex);

        vertex.position = end - perpendicular;
        vh.AddVert(vertex);

        vertex.position = end + perpendicular;
        vh.AddVert(vertex);

        vh.AddTriangle(
            index,
            index + 1,
            index + 2
        );

        vh.AddTriangle(
            index,
            index + 2,
            index + 3
        );
    }

    public void SetAngle(float angle)
    {
        currentAngle = Mathf.Clamp(
            angle,
            minimumAngle,
            maximumAngle
        );

        SetVerticesDirty();
    }
}