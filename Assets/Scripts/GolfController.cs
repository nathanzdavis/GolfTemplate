using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

public class GolfController : MonoBehaviour
{
    // ============================================================
    // REFERENCES
    // ============================================================

    [Header("References")]
    [SerializeField] private CharacterController characterController;
    [SerializeField] private Animator animator;
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private Transform golfCameraTransform;

    [Header("Golf Club")]
    [SerializeField] private float clubLerpSpeed = 10f;
    [SerializeField] private Transform golfClub;
    [SerializeField] private Transform clubPositionReady;
    [SerializeField] private Transform clubPositionNotReady;

    [Header("Downswing Speed")]
    [SerializeField] private float minimumDownswingSpeed = 0.5f;
    [SerializeField] private float maximumDownswingSpeed = 2.0f;

    // ============================================================
    // INPUT
    // ============================================================

    [Header("Input")]
    [SerializeField] private InputActionReference prepareShotAction;
    [SerializeField] private InputActionReference shootAction;

    // ============================================================
    // GOLF BALL
    // ============================================================

    [Header("Golf Ball")]
    [SerializeField] private LayerMask ballLayer;
    [SerializeField] private float ballDetectionRadius = 1f;

    // ============================================================
    // SHOT
    // ============================================================

    [Header("Shot")]
    [SerializeField] private float maxShotForce = 20f;
    [SerializeField] private float shotAngle = 45f;
    [SerializeField] private float chargeSpeed = 20f;

    // ============================================================
    // SHOT POSITIONING
    // ============================================================

    [Header("Shot Positioning")]
    [SerializeField] private float playerBallDistance = 1f;
    [SerializeField] private float orbitSpeed = 2f;
    [SerializeField] private float orbitMoveSpeed = 12f;
    [SerializeField] private float orbitRotationSpeed = 720f;

    private float orbitAngle;
    private Vector3 prepareTargetPosition;
    private Quaternion prepareTargetRotation;

    // ============================================================
    // SWING ANIMATION
    // ============================================================

    [Header("Swing Animation")]
    [SerializeField] private float downSwingSpeed = 3f;
    [SerializeField] private float hitFrame = 35f;

    private const float SwingTopFrame = 26f;
    private const float SwingTotalFrames = 60f;

    // ============================================================
    // ANIMATOR
    // ============================================================

    [Header("Animator Parameters")]
    [SerializeField] private string preparingParameter = "Preparing";
    [SerializeField] private string chargeParameter = "Charge";
    [SerializeField] private string speedParameter = "Speed";

    // ============================================================
    // RUNTIME STATE
    // ============================================================

    private Rigidbody golfBall;
    private Vector3 golfBallCenter;

    private float chargeAmount;
    private float swingFrame;

    private bool swingPlaying;
    private bool swingReleased;
    private bool ballHit;

    [HideInInspector]
    public bool preparingShot;

    private StarterAssets.StarterAssetsInputs starterInputs;

    private CinemachineVirtualCamera normalCamera;
    private CinemachineVirtualCamera golfCamera;

    private static readonly int SwingState =
        Animator.StringToHash("GolfSwing");
    private bool swingActive;


    // ============================================================
    // UNITY LIFECYCLE
    // ============================================================

    private void Awake()
    {
        if (characterController == null)
            characterController = GetComponent<CharacterController>();

        starterInputs =
            GetComponent<StarterAssets.StarterAssetsInputs>();

        CacheCameras();
    }

    private void OnEnable()
    {
        if (prepareShotAction != null)
        {
            prepareShotAction.action.Enable();

            prepareShotAction.action.started += PrepareStarted;
            prepareShotAction.action.canceled += PrepareCanceled;
        }

        if (shootAction != null)
        {
            shootAction.action.Enable();

            shootAction.action.started += ShootStarted;
            shootAction.action.canceled += ShootCanceled;
        }
    }

    private void OnDisable()
    {
        if (prepareShotAction != null)
        {
            prepareShotAction.action.started -= PrepareStarted;
            prepareShotAction.action.canceled -= PrepareCanceled;

            prepareShotAction.action.Disable();
        }

        if (shootAction != null)
        {
            shootAction.action.started -= ShootStarted;
            shootAction.action.canceled -= ShootCanceled;

            shootAction.action.Disable();
        }
    }

    private void Update()
    {
        // Smoothly move the club between its two positions.
        UpdateGolfClub();

        if (!preparingShot)
            return;

        DisablePlayerMovement();
        LockVerticalCameraInput();

        // Smoothly move and rotate into the shot position.
        UpdatePreparePosition();

        if (golfBall == null)
            return;

        UpdateCharge();
    }

    private void UpdateGolfClub()
    {
        if (golfClub == null ||
            clubPositionReady == null ||
            clubPositionNotReady == null)
            return;

        Transform target =
            preparingShot
                ? clubPositionReady
                : clubPositionNotReady;

        // Smoothly move the club to the target.
        golfClub.position =
            Vector3.Lerp(
                golfClub.position,
                target.position,
                clubLerpSpeed * Time.deltaTime
            );

        // Smoothly rotate the club to the target.
        golfClub.rotation =
            Quaternion.Slerp(
                golfClub.rotation,
                target.rotation,
                clubLerpSpeed * Time.deltaTime
            );
    }

    // ============================================================
    // CAMERA
    // ============================================================

    private void CacheCameras()
    {
        if (cameraTransform != null)
        {
            normalCamera =
                cameraTransform.GetComponent<CinemachineVirtualCamera>();
        }

        if (golfCameraTransform != null)
        {
            golfCamera =
                golfCameraTransform.GetComponent<CinemachineVirtualCamera>();
        }
    }

    private void SetGolfCameraActive(bool active)
    {
        if (normalCamera != null)
            normalCamera.Priority = active ? 0 : 1;

        if (golfCamera != null)
            golfCamera.Priority = active ? 1 : 0;
    }


    // ============================================================
    // PREPARE SHOT
    // ============================================================

    private void PrepareStarted(InputAction.CallbackContext context)
    {
        FindGolfBall();

        if (golfBall == null)
            return;

        preparingShot = true;

        golfBallCenter = golfBall.position;

        // Disable CharacterController while manually positioning the player.
        SetCharacterControllerEnabled(false);

        // Calculate the initial position on the LEFT side of the ball
        // relative to the current camera.
        CalculateInitialOrbitPosition();

        // Face the ball.
        UpdatePrepareRotation();

        SetPreparingAnimation(true);

        // Switch to golf camera.
        SetGolfCameraActive(true);
    }

    private void PrepareCanceled(InputAction.CallbackContext context)
    {
        // Finish any active swing immediately.
        FinishSwing();

        preparingShot = false;

        SetPreparingAnimation(false);

        DisablePlayerMovement();

        golfBall = null;

        SetGolfCameraActive(false);

        SetCharacterControllerEnabled(true);
    }

    private void CalculateInitialOrbitPosition()
    {
        Camera mainCamera = Camera.main;

        if (mainCamera == null)
            return;

        Vector3 leftDirection =
            -mainCamera.transform.right;

        leftDirection.y = 0f;

        if (leftDirection.sqrMagnitude < 0.001f)
            return;

        leftDirection.Normalize();

        orbitAngle = Mathf.Atan2(
            leftDirection.z,
            leftDirection.x
        );

        prepareTargetPosition =
            golfBallCenter +
            leftDirection * playerBallDistance;

        prepareTargetPosition.y =
            transform.position.y;
    }


    // ============================================================
    // PREPARE POSITIONING
    // ============================================================

    private void CalculatePreparePosition()
    {
        Camera mainCamera = Camera.main;

        if (mainCamera == null)
            return;

        // Screen-left relative to the actual rendered camera.
        Vector3 leftDirection =
            -mainCamera.transform.right;

        // Ignore camera pitch.
        leftDirection.y = 0f;

        if (leftDirection.sqrMagnitude < 0.001f)
            return;

        leftDirection.Normalize();

        // Fixed distance from the golf ball.
        prepareTargetPosition =
            golfBallCenter +
            leftDirection * playerBallDistance;

        // Preserve the player's current height.
        prepareTargetPosition.y =
            transform.position.y;
    }

    private void CalculatePrepareRotation()
    {
        Vector3 directionToBall =
            golfBallCenter -
            prepareTargetPosition;

        directionToBall.y = 0f;

        if (directionToBall.sqrMagnitude < 0.001f)
            return;

        prepareTargetRotation =
            Quaternion.LookRotation(directionToBall);
    }

    private void UpdatePreparePosition()
    {
        if (golfBall == null)
            return;

        // Read horizontal look input.
        float horizontalInput = 0f;

        if (starterInputs != null)
        {
            horizontalInput = starterInputs.look.x;
        }

        // Invert the input so:
        // Mouse LEFT  = orbit LEFT
        // Mouse RIGHT = orbit RIGHT
        orbitAngle -=
            horizontalInput *
            orbitSpeed *
            Time.deltaTime;

        // Calculate position around the ball.
        Vector3 orbitDirection = new Vector3(
            Mathf.Cos(orbitAngle),
            0f,
            Mathf.Sin(orbitAngle)
        );

        prepareTargetPosition =
            golfBallCenter +
            orbitDirection * playerBallDistance;

        // Preserve player's current height.
        prepareTargetPosition.y =
            transform.position.y;

        // Smoothly move toward the orbit position.
        transform.position =
            Vector3.MoveTowards(
                transform.position,
                prepareTargetPosition,
                orbitMoveSpeed * Time.deltaTime
            );

        // Always face the ball.
        UpdatePrepareRotation();
    }

    private void UpdatePrepareRotation()
    {
        if (golfBall == null)
            return;

        Vector3 directionToBall =
            golfBallCenter - transform.position;

        directionToBall.y = 0f;

        if (directionToBall.sqrMagnitude < 0.001f)
            return;

        prepareTargetRotation =
            Quaternion.LookRotation(directionToBall);

        transform.rotation =
            Quaternion.RotateTowards(
                transform.rotation,
                prepareTargetRotation,
                orbitRotationSpeed * Time.deltaTime
            );
    }

    // ============================================================
    // PLAYER / INPUT CONTROL
    // ============================================================

    private void DisablePlayerMovement()
    {
        if (starterInputs == null)
            return;

        starterInputs.MoveInput(Vector2.zero);
    }

    private void LockVerticalCameraInput()
    {
        if (starterInputs == null)
            return;

        Vector2 look = starterInputs.look;

        // Horizontal camera movement is still allowed.
        // Vertical camera movement is locked.
        look.y = 0f;

        starterInputs.LookInput(look);
    }

    private void SetCharacterControllerEnabled(bool enabled)
    {
        if (characterController != null)
            characterController.enabled = enabled;
    }


    // ============================================================
    // FIND GOLF BALL
    // ============================================================

    private void FindGolfBall()
    {
        Collider[] hits = Physics.OverlapSphere(
            transform.position,
            ballDetectionRadius,
            ballLayer
        );

        float closestDistance = float.MaxValue;

        golfBall = null;

        foreach (Collider hit in hits)
        {
            Rigidbody rb = hit.attachedRigidbody;

            if (rb == null)
                continue;

            float distance =
                Vector3.Distance(
                    transform.position,
                    rb.position
                );

            if (distance >= closestDistance)
                continue;

            closestDistance = distance;
            golfBall = rb;
        }
    }


    // ============================================================
    // ANIMATION
    // ============================================================

    private void SetPreparingAnimation(bool preparing)
    {
        if (animator == null)
            return;

        animator.SetBool(
            preparingParameter,
            preparing
        );

        if (preparing)
        {
            animator.SetFloat(
                speedParameter,
                0f
            );
        }
    }

    private void ResetShotState()
    {
        swingActive = false;

        chargeAmount = 0f;
        swingFrame = 0f;

        swingPlaying = false;
        swingReleased = false;
        ballHit = false;

        if (animator == null)
            return;

        animator.SetFloat(
            chargeParameter,
            0f
        );
    }


    // ============================================================
    // CHARGE
    // ============================================================

    private void UpdateCharge()
    {
        if (shootAction.action.IsPressed() && !swingReleased)
        {
            ChargeShot();
            return;
        }

        if (swingPlaying)
        {
            UpdateDownSwing();
        }
    }

    private void ChargeShot()
    {
        chargeAmount +=
            chargeSpeed *
            Time.deltaTime;

        chargeAmount =
            Mathf.Clamp(
                chargeAmount,
                0f,
                100f
            );

        animator.SetFloat(
            chargeParameter,
            chargeAmount
        );

        // 0% = frame 0
        // 100% = frame 26
        float normalizedTime =
            (chargeAmount / 100f) *
            (SwingTopFrame / SwingTotalFrames);

        animator.Play(
            SwingState,
            1,
            normalizedTime
        );

        animator.Update(0f);

        swingFrame =
            (chargeAmount / 100f) *
            SwingTopFrame;
    }


    // ============================================================
    // START SWING
    // ============================================================

    private void ShootStarted(InputAction.CallbackContext context)
    {
        if (!CanShoot())
            return;

        swingActive = true;

        chargeAmount = 0f;
        swingFrame = 0f;

        swingReleased = false;
        swingPlaying = false;
        ballHit = false;

        animator.SetFloat(
            chargeParameter,
            0f
        );

        animator.Play(
            SwingState,
            1,
            0f
        );

        animator.Update(0f);
    }


    // ============================================================
    // RELEASE SWING
    // ============================================================

    private void ShootCanceled(InputAction.CallbackContext context)
    {
        if (!CanShoot())
            return;

        if (chargeAmount <= 0f)
            return;

        swingReleased = true;
        swingPlaying = true;
        ballHit = false;

        float chargePercent =
            chargeAmount / 100f;

        // Map the charge percentage from
        // the upswing range (0-26)
        // into the downswing-to-hit range (26-35).
        swingFrame =
            Mathf.Lerp(
                SwingTopFrame,
                hitFrame,
                chargePercent
            );

        // Immediately show the corresponding
        // downswing frame.
        animator.Play(
            SwingState,
            1,
            swingFrame / SwingTotalFrames
        );

        animator.Update(0f);
    }

    private bool CanShoot()
    {
        return preparingShot &&
               golfBall != null &&
               animator != null;
    }


    // ============================================================
    // DOWNSWING
    // ============================================================

    private void UpdateDownSwing()
    {
        // Convert charge to 0-1.
        float chargePercent =
            chargeAmount / 100f;

        // Low charge = slow downswing.
        // High charge = fast downswing.
        float currentDownswingSpeed =
            Mathf.Lerp(
                minimumDownswingSpeed,
                maximumDownswingSpeed,
                chargePercent
            );

        swingFrame +=
            currentDownswingSpeed *
            Time.deltaTime *
            SwingTotalFrames;

        // Hit the ball at frame 35.
        if (!ballHit && swingFrame >= hitFrame)
        {
            ballHit = true;
            HitGolfBall();
        }

        // Finish the swing.
        if (swingFrame >= SwingTotalFrames)
        {
            FinishSwing();
            return;
        }

        float normalizedTime =
            swingFrame / SwingTotalFrames;

        animator.Play(
            SwingState,
            1,
            normalizedTime
        );

        animator.Update(0f);
    }

    private void FinishSwing()
    {
        if (!swingActive)
            return;

        swingActive = false;

        swingFrame = 0f;

        swingPlaying = false;
        swingReleased = false;

        chargeAmount = 0f;
        ballHit = false;

        if (animator != null)
        {
            animator.SetFloat(
                chargeParameter,
                0f
            );

            animator.SetTrigger(
                "SwingFinished"
            );
        }
    }


    // ============================================================
    // HIT GOLF BALL
    // ============================================================

    private void HitGolfBall()
    {
        if (golfBall == null)
            return;

        Camera mainCamera = Camera.main;

        if (mainCamera == null)
            return;

        Vector3 forward =
            mainCamera.transform.forward;

        forward.y = 0f;

        if (forward.sqrMagnitude < 0.001f)
            return;

        forward.Normalize();

        float angle =
            shotAngle *
            Mathf.Deg2Rad;

        Vector3 shotDirection =
            forward * Mathf.Cos(angle) +
            Vector3.up * Mathf.Sin(angle);

        shotDirection.Normalize();

        float chargeMultiplier =
            chargeAmount / 100f;

        float force =
            maxShotForce *
            chargeMultiplier;

        GolfBall ball = golfBall.GetComponent<GolfBall>();

        if (ball != null)
        {
            ball.Launch(
                shotDirection,
                force
            );
        }
    }


    // ============================================================
    // DEBUG
    // ============================================================

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;

        Gizmos.DrawWireSphere(
            transform.position,
            ballDetectionRadius
        );
    }
}