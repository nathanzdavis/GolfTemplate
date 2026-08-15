using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

public class GolfHole : MonoBehaviour
{
    [Header("Hole")]
    [SerializeField] private Transform holeCenter;
    [SerializeField] private Transform particleSpawn;
    [SerializeField] private float maxEntrySpeed = 2f;

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

    private void OnTriggerEnter(Collider other)
    {
        if (ballSinking)
            return;

        if (!other.CompareTag("GolfBall"))
            return;

        Rigidbody rb = other.attachedRigidbody;

        if (rb == null)
            return;

        if (rb.linearVelocity.magnitude > maxEntrySpeed)
            return;

        StartCoroutine(SinkBall(other.transform, rb));
    }

    private IEnumerator SinkBall(Transform ball, Rigidbody rb)
    {
        ballSinking = true;

        rb.isKinematic = true;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        Vector3 startPosition = ball.position;

        Vector3 targetPosition = new Vector3(
            holeCenter.position.x,
            holeCenter.position.y - sinkDepth,
            holeCenter.position.z
        );

        float elapsed = 0f;

        while (elapsed < sinkDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / sinkDuration);
            float curvedT = sinkCurve.Evaluate(t);

            ball.position = Vector3.Lerp(
                startPosition,
                targetPosition,
                curvedT
            );

            yield return null;
        }

        ball.position = targetPosition;

        OnBallHoled(ball.gameObject);
    }

    private void OnBallHoled(GameObject ball)
    {
        Debug.Log("BALL HOLED!");

        SpawnHoleParticles();
        PlayHoleSound();
        ShakeCamera();
        ExplodeNearbyPlayers();

        ball.SetActive(false);
    }

    private void SpawnHoleParticles()
    {
        if (holeParticle == null)
            return;

        ParticleSystem particles = Instantiate(
            holeParticle,
            particleSpawn.position,
            Quaternion.identity
        );

        Destroy(particles.gameObject, particleLifetime);
    }

    private void PlayHoleSound()
    {
        if (holeAudioSource == null)
            return;

        holeAudioSource.Play();
    }

    private void ShakeCamera()
    {
        if (impulseSource == null)
            return;

        impulseSource.GenerateImpulse(cameraShakeForce);
    }

    private void ExplodeNearbyPlayers()
    {
        Collider[] nearbyObjects = Physics.OverlapSphere(
            transform.position,
            explosionRadius,
            playerLayer
        );

        foreach (Collider col in nearbyObjects)
        {
            StarterAssets.ThirdPersonController player =
                col.GetComponentInParent<StarterAssets.ThirdPersonController>();

            if (player == null)
                continue;

            Vector3 direction =
                player.transform.position - transform.position;

            direction.y = 0f;

            if (direction.sqrMagnitude < 0.001f)
                direction = Vector3.forward;

            direction.Normalize();

            Vector3 force = direction * explosionForce;
            force.y = explosionUpwardForce;

            player.ApplyKnockback(force);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(
            transform.position,
            explosionRadius
        );
    }
}