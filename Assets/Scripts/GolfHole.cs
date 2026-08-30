using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

public class GolfHole : MonoBehaviour
{
    [Header("Hole")]
    [SerializeField] private Transform holeCenter;
    [SerializeField] private Transform particleSpawn;
    [SerializeField] private float maxEntrySpeed = 2f;

    [Header("Vertical / Chipped Entry")]
    [SerializeField] private float verticalEntryAngle = 60f;
    [SerializeField] private float minimumHeightAboveHole = 0.05f;

    [Header("Ball Sink")]
    [SerializeField] private float sinkDepth = 0.25f;
    [SerializeField] private float sinkDuration = 0.4f;

    [SerializeField]
    private AnimationCurve sinkCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Effects")]
    [SerializeField] private ParticleSystem holeParticle;
    [SerializeField] private float particleLifetime = 2f;
    [SerializeField] private AudioSource holeAudioSource;

    [Header("Player Explosion")]
    [SerializeField] private float explosionRadius = 5f;
    [SerializeField] private float explosionForce = 8f;
    [SerializeField] private float explosionUpwardForce = 5f;
    [SerializeField] private LayerMask playerLayer;

    [Header("Camera Shake")]
    [SerializeField] private CinemachineImpulseSource impulseSource;
    [SerializeField] private float cameraShakeForce = 1f;

    private bool ballSinking;

    // ============================================================
    // COLLISION
    // ============================================================

    private void OnTriggerEnter(Collider other)
    {
        if (ballSinking)
            return;

        if (!other.CompareTag("GolfBall"))
            return;

        Rigidbody rb = other.attachedRigidbody;

        if (rb == null)
            return;

        // --------------------------------------------------------
        // NORMAL ENTRY
        // --------------------------------------------------------

        bool normalEntryIsFast =
            rb.linearVelocity.magnitude > maxEntrySpeed;

        // --------------------------------------------------------
        // VERTICAL / CHIPPED ENTRY
        // --------------------------------------------------------

        bool verticalEntry =
            IsVerticalEntry(other.transform, rb);

        // Fast balls are rejected unless they are
        // entering the hole from above at a steep angle.
        if (normalEntryIsFast && !verticalEntry)
            return;

        StartCoroutine(
            SinkBall(
                other.transform,
                rb
            )
        );
    }

    // ============================================================
    // VERTICAL ENTRY
    // ============================================================

    private bool IsVerticalEntry(
        Transform ball,
        Rigidbody rb)
    {
        if (holeCenter == null)
            return false;

        Vector3 velocity =
            rb.linearVelocity;

        // Must actually be moving.
        if (velocity.sqrMagnitude < 0.001f)
            return false;

        // --------------------------------------------------------
        // HOLE LOCAL UP
        //
        // This allows the hole to be rotated anywhere.
        // --------------------------------------------------------

        Vector3 holeUp =
            holeCenter.up.normalized;

        // --------------------------------------------------------
        // HEIGHT ABOVE HOLE
        // --------------------------------------------------------

        Vector3 ballToHole =
            ball.position -
            holeCenter.position;

        float heightAboveHole =
            Vector3.Dot(
                ballToHole,
                holeUp
            );

        if (heightAboveHole <
            minimumHeightAboveHole)
        {
            return false;
        }

        // --------------------------------------------------------
        // MOVING DOWN TOWARD THE HOLE
        // --------------------------------------------------------

        float downwardSpeed =
            -Vector3.Dot(
                velocity,
                holeUp
            );

        if (downwardSpeed <= 0f)
            return false;

        // --------------------------------------------------------
        // HORIZONTAL VELOCITY RELATIVE TO HOLE
        // --------------------------------------------------------

        Vector3 verticalVelocity =
            holeUp *
            Vector3.Dot(
                velocity,
                holeUp
            );

        Vector3 horizontalVelocity =
            velocity -
            verticalVelocity;

        float horizontalSpeed =
            horizontalVelocity.magnitude;

        // --------------------------------------------------------
        // ENTRY ANGLE
        //
        // 0°  = completely horizontal
        // 90° = completely vertical
        // --------------------------------------------------------

        float angle =
            Mathf.Atan2(
                downwardSpeed,
                horizontalSpeed
            ) * Mathf.Rad2Deg;

        return angle >= verticalEntryAngle;
    }

    // ============================================================
    // SINK BALL
    // ============================================================

    private IEnumerator SinkBall(
        Transform ball,
        Rigidbody rb)
    {
        ballSinking = true;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;

        Vector3 startPosition =
            ball.position;

        // Sink along the hole's local down direction.
        Vector3 targetPosition =
            holeCenter.position -
            holeCenter.up.normalized *
            sinkDepth;

        float elapsed = 0f;

        while (elapsed < sinkDuration)
        {
            elapsed += Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed / sinkDuration
                );

            float curvedT =
                sinkCurve.Evaluate(t);

            ball.position =
                Vector3.Lerp(
                    startPosition,
                    targetPosition,
                    curvedT
                );

            yield return null;
        }

        ball.position =
            targetPosition;

        OnBallHoled(
            ball.gameObject
        );
    }

    // ============================================================
    // BALL HOLED
    // ============================================================

    private void OnBallHoled(
        GameObject ball)
    {
        Debug.Log("BALL HOLED!");

        SpawnHoleParticles();
        PlayHoleSound();
        ShakeCamera();
        ExplodeNearbyPlayers();

        GameController.Instance.CompleteHole();

        ball.SetActive(false);
    }

    // ============================================================
    // PARTICLES
    // ============================================================

    private void SpawnHoleParticles()
    {
        if (holeParticle == null)
            return;

        ParticleSystem particles =
            Instantiate(
                holeParticle,
                particleSpawn.position,
                Quaternion.identity
            );

        Destroy(
            particles.gameObject,
            particleLifetime
        );
    }

    // ============================================================
    // AUDIO
    // ============================================================

    private void PlayHoleSound()
    {
        if (holeAudioSource == null)
            return;

        holeAudioSource.Play();
    }

    // ============================================================
    // CAMERA SHAKE
    // ============================================================

    private void ShakeCamera()
    {
        if (impulseSource == null)
            return;

        impulseSource.GenerateImpulse(
            cameraShakeForce
        );
    }

    // ============================================================
    // PLAYER EXPLOSION
    // ============================================================

    private void ExplodeNearbyPlayers()
    {
        Collider[] nearbyObjects =
            Physics.OverlapSphere(
                holeCenter.position,
                explosionRadius,
                playerLayer
            );

        foreach (Collider col in nearbyObjects)
        {
            StarterAssets.ThirdPersonController player =
                col.GetComponentInParent<
                    StarterAssets.ThirdPersonController
                >();

            if (player == null)
                continue;

            // Direction away from the hole.
            Vector3 direction =
                player.transform.position -
                holeCenter.position;

            // Keep the horizontal portion relative
            // to the hole's orientation.
            direction =
                Vector3.ProjectOnPlane(
                    direction,
                    holeCenter.up
                );

            if (direction.sqrMagnitude < 0.001f)
            {
                direction =
                    holeCenter.forward;
            }

            direction.Normalize();

            // Horizontal explosion.
            Vector3 force =
                direction *
                explosionForce;

            // Upward force relative to the hole.
            force +=
                holeCenter.up *
                explosionUpwardForce;

            player.ApplyKnockback(
                force
            );
        }
    }

    // ============================================================
    // GIZMOS
    // ============================================================

    private void OnDrawGizmosSelected()
    {
        if (holeCenter == null)
            return;

        Gizmos.DrawWireSphere(
            holeCenter.position,
            explosionRadius
        );

        // Show the hole's local up direction.
        Gizmos.DrawLine(
            holeCenter.position,
            holeCenter.position +
            holeCenter.up
        );
    }
}