using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class GolfBall : MonoBehaviour
{
    // ============================================================
    // REFERENCES
    // ============================================================

    private Rigidbody rb;

    [Header("Trail")]
    [SerializeField] private TrailRenderer trailRenderer;
    [SerializeField] private float trailStartSpeed = 0.08f;

    // ============================================================
    // ROLLING PHYSICS
    // ============================================================

    [Header("Rolling Physics")]
    [SerializeField] private float rollingResistance = 0.8f;

    [SerializeField] private float angularDrag = 1.5f;

    [SerializeField] private float minimumRollSpeed = 0.08f;

    [SerializeField] private float stopDelay = 0.15f;

    // ============================================================
    // SLOPE SETTINGS
    // ============================================================

    [Header("Slope")]
    [SerializeField] private float maximumStoppingSlope = 8f;

    [SerializeField] private float slopeStopStrength = 2f;

    // ============================================================
    // STATE
    // ============================================================

    private float stoppedTime;

    private bool grounded;

    private Vector3 groundNormal = Vector3.up;

    // ============================================================
    // UNITY
    // ============================================================

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        rb.interpolation =
            RigidbodyInterpolation.Interpolate;

        rb.collisionDetectionMode =
            CollisionDetectionMode.ContinuousDynamic;

        rb.angularDamping = angularDrag;

        if (trailRenderer != null)
            trailRenderer.emitting = false;
    }

    private void FixedUpdate()
    {
        if (!grounded)
        {
            stoppedTime = 0f;
            UpdateTrail();
            return;
        }

        ApplyRollingResistance();
        ApplySlopeStopping();
        StopTinyMovement();

        UpdateTrail();
    }

    // ============================================================
    // ROLLING RESISTANCE
    // ============================================================

    private void ApplyRollingResistance()
    {
        Vector3 velocity = rb.linearVelocity;

        // Only apply resistance to movement along the ground.
        Vector3 groundVelocity =
            Vector3.ProjectOnPlane(
                velocity,
                groundNormal
            );

        float speed = groundVelocity.magnitude;

        if (speed <= 0.001f)
            return;

        Vector3 resistanceDirection =
            -groundVelocity.normalized;

        rb.AddForce(
            resistanceDirection *
            rollingResistance,
            ForceMode.Acceleration
        );
    }

    // ============================================================
    // SLOPE STOPPING
    // ============================================================

    private void ApplySlopeStopping()
    {
        // Determine how steep the ground is.
        float slopeAngle =
            Vector3.Angle(
                groundNormal,
                Vector3.up
            );

        // If the slope is too steep, allow normal physics.
        if (slopeAngle > maximumStoppingSlope)
        {
            stoppedTime = 0f;
            return;
        }

        Vector3 groundVelocity =
            Vector3.ProjectOnPlane(
                rb.linearVelocity,
                groundNormal
            );

        float speed =
            groundVelocity.magnitude;

        // Only start considering stopping when
        // the ball is already moving slowly.
        if (speed > minimumRollSpeed)
        {
            stoppedTime = 0f;
            return;
        }

        stoppedTime += Time.fixedDeltaTime;

        if (stoppedTime < stopDelay)
            return;

        // Stop horizontal/ground movement.
        Vector3 velocity = rb.linearVelocity;

        Vector3 verticalVelocity =
            Vector3.Project(
                velocity,
                groundNormal
            );

        rb.linearVelocity =
            verticalVelocity;

        rb.angularVelocity *= 0.5f;

        // Apply a small counter-force to prevent gravity
        // from immediately starting the ball rolling again.
        Vector3 slopeDirection =
            Vector3.ProjectOnPlane(
                Physics.gravity,
                groundNormal
            );

        if (slopeDirection.sqrMagnitude > 0.001f)
        {
            rb.AddForce(
                -slopeDirection *
                slopeStopStrength,
                ForceMode.Acceleration
            );
        }
    }

    // ============================================================
    // TINY MOVEMENT
    // ============================================================

    private void StopTinyMovement()
    {
        if (rb.isKinematic)
            return;

        Vector3 groundVelocity =
            Vector3.ProjectOnPlane(
                rb.linearVelocity,
                groundNormal
            );

        if (groundVelocity.magnitude > minimumRollSpeed)
            return;

        // Make sure tiny numerical movement doesn't
        // keep the ball alive forever.
        Vector3 velocity = rb.linearVelocity;

        rb.linearVelocity =
            Vector3.Project(
                velocity,
                groundNormal
            );

        if (rb.angularVelocity.magnitude < 0.5f)
        {
            rb.angularVelocity = Vector3.zero;
        }
    }

    // ============================================================
    // COLLISION
    // ============================================================

    private void OnCollisionStay(Collision collision)
    {
        if (collision.contactCount == 0)
            return;

        grounded = true;

        // Average all contact normals.
        Vector3 normal = Vector3.zero;

        for (int i = 0; i < collision.contactCount; i++)
        {
            normal +=
                collision.GetContact(i).normal;
        }

        normal.Normalize();

        groundNormal = normal;
    }

    private void OnCollisionExit(Collision collision)
    {
        grounded = false;
        stoppedTime = 0f;
        groundNormal = Vector3.up;
    }

    // ============================================================
    // RESET
    // ============================================================

    public void StopBall()
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        stoppedTime = 0f;

        if (trailRenderer != null)
            trailRenderer.emitting = false;
    }

    public void Launch(
    Vector3 direction,
    float force)
    {
        stoppedTime = 0f;

        rb.AddForce(
            direction.normalized * force,
            ForceMode.Impulse
        );

        if (trailRenderer != null)
            trailRenderer.emitting = true;
    }

    private void UpdateTrail()
    {
        if (trailRenderer == null)
            return;

        Vector3 groundVelocity =
            Vector3.ProjectOnPlane(
                rb.linearVelocity,
                groundNormal
            );

        bool moving =
            groundVelocity.magnitude > trailStartSpeed;

        trailRenderer.emitting = moving;
    }
}