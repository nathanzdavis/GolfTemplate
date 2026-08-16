using UnityEngine;

public class WindGenerator : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private Transform player;

    [Header("Wind")]
    [SerializeField] private float windSpeed = 10f;

    [Tooltip("Initial direction the wind PARTICLES travel.")]
    [SerializeField] private Vector3 windDirection = Vector3.forward;

    [Header("Wind Direction Change")]
    [Tooltip("How often the wind chooses a new direction.")]
    [SerializeField] private float directionChangeInterval = 5f;

    [Tooltip("How quickly the wind rotates toward the new direction.")]
    [SerializeField] private float directionChangeSpeed = 1f;

    [Tooltip("Maximum angle the wind can change from its current direction.")]
    [SerializeField] private float maxDirectionChangeAngle = 45f;

    [Header("Wind Speed Change")]
    [Tooltip("Minimum possible wind speed.")]
    [SerializeField] private float minimumWindSpeed = 5f;

    [Tooltip("Maximum possible wind speed.")]
    [SerializeField] private float maximumWindSpeed = 15f;

    [Tooltip("How quickly the wind speed changes.")]
    [SerializeField] private float windSpeedChangeSpeed = 1f;

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

    private Vector3 currentWindDirection;
    private Vector3 targetWindDirection;

    private float currentWindSpeed;
    private float targetWindSpeed;

    private float directionChangeTimer;

    public Vector3 WindDirection => currentWindDirection;

    public float WindSpeed => currentWindSpeed;

    private void Start()
    {
        // Start with a random horizontal wind direction.
        float randomAngle = Random.Range(0f, 360f);

        currentWindDirection =
            Quaternion.Euler(0f, randomAngle, 0f) *
            Vector3.forward;

        currentWindDirection.y = 0f;
        currentWindDirection.Normalize();

        targetWindDirection = currentWindDirection;

        // Start with a random wind speed.
        currentWindSpeed = Random.Range(
            minimumWindSpeed,
            maximumWindSpeed
        );

        targetWindSpeed = currentWindSpeed;
    }

    private void Update()
    {
        if (player == null || windParticlePrefab == null)
            return;

        UpdateWind();

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

    private void UpdateWind()
    {
        directionChangeTimer += Time.deltaTime;

        // Pick a new wind direction and speed.
        if (directionChangeTimer >= directionChangeInterval)
        {
            directionChangeTimer = 0f;

            // -----------------------------
            // RANDOM DIRECTION
            // -----------------------------

            float randomAngle = Random.Range(
                -maxDirectionChangeAngle,
                maxDirectionChangeAngle
            );

            targetWindDirection =
                Quaternion.Euler(0f, randomAngle, 0f) *
                currentWindDirection;

            targetWindDirection.y = 0f;

            if (targetWindDirection.sqrMagnitude < 0.001f)
            {
                targetWindDirection = Vector3.forward;
            }

            targetWindDirection.Normalize();

            // -----------------------------
            // RANDOM SPEED
            // -----------------------------

            targetWindSpeed = Random.Range(
                minimumWindSpeed,
                maximumWindSpeed
            );
        }

        // -----------------------------
        // SMOOTH DIRECTION
        // -----------------------------

        currentWindDirection = Vector3.Slerp(
            currentWindDirection,
            targetWindDirection,
            directionChangeSpeed * Time.deltaTime
        );

        currentWindDirection.y = 0f;

        if (currentWindDirection.sqrMagnitude < 0.001f)
        {
            currentWindDirection = Vector3.forward;
        }

        currentWindDirection.Normalize();

        // -----------------------------
        // SMOOTH SPEED
        // -----------------------------

        currentWindSpeed = Mathf.Lerp(
            currentWindSpeed,
            targetWindSpeed,
            windSpeedChangeSpeed * Time.deltaTime
        );
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

        if (right.sqrMagnitude < 0.001f)
            return;

        right.Normalize();

        // Start forwardDistance units in front
        // and height units above the player.
        Vector3 spawnPosition =
            player.position +
            forward * forwardDistance +
            Vector3.up * height;

        // Randomly spawn between
        // -horizontalSpawnOffset and +horizontalSpawnOffset.
        float sideOffset = Random.Range(
            -horizontalSpawnOffset,
            horizontalSpawnOffset
        );

        spawnPosition += right * sideOffset;

        // Rotate the particle so its forward/Z axis
        // faces the direction the wind is traveling.
        Quaternion rotation =
            Quaternion.LookRotation(
                WindDirection,
                Vector3.up
            );

        GameObject particle = Instantiate(
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