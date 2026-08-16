using UnityEngine;

public class WindGenerator : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private Transform player;

    [Header("Wind")]
    [SerializeField] private float windSpeed = 10f;

    [Tooltip("Direction the wind PARTICLES travel.")]
    [SerializeField] private Vector3 windDirection = Vector3.forward;

    [Header("Spawn Area")]
    [SerializeField] private float forwardDistance = 50f;
    [SerializeField] private float height = 25f;
    [SerializeField] private float horizontalSpawnOffset = 25f;

    [Header("Particle")]
    [SerializeField] private GameObject windParticlePrefab;
    [SerializeField] private float spawnInterval = 0.5f;
    [SerializeField] private float particleLifetime = 5f;
    [SerializeField] private int maxParticles = 30;

    private float spawnTimer;

    public Vector3 WindDirection
    {
        get
        {
            Vector3 direction = windDirection;
            direction.y = 0f;

            if (direction.sqrMagnitude < 0.001f)
                return Vector3.forward;

            return direction.normalized;
        }
    }

    public float WindSpeed => windSpeed;

    private void Update()
    {
        if (player == null || windParticlePrefab == null)
            return;

        spawnTimer += Time.deltaTime;

        if (spawnTimer >= spawnInterval)
        {
            spawnTimer = 0f;

            if (transform.childCount < maxParticles)
            {
                SpawnWindParticle();
            }
        }
    }

    private void SpawnWindParticle()
    {
        Vector3 forward = player.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.001f)
            return;

        forward.Normalize();

        Vector3 right = player.right;
        right.y = 0f;
        right.Normalize();

        // Start 50 units in front and 25 units above the player.
        Vector3 spawnPosition =
            player.position +
            forward * forwardDistance +
            Vector3.up * height;

        // Randomly spawn anywhere between 25 units left
        // and 25 units right.
        float sideOffset =
            Random.Range(
                -horizontalSpawnOffset,
                horizontalSpawnOffset
            );

        spawnPosition += right * sideOffset;

        // Z axis faces the direction the wind is traveling.
        Quaternion rotation =
            Quaternion.LookRotation(
                WindDirection,
                Vector3.up
            );

        GameObject particle =
            Instantiate(
                windParticlePrefab,
                spawnPosition,
                rotation,
                transform
            );

        Destroy(
            particle,
            particleLifetime
        );
    }
}