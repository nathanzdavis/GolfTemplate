using StarterAssets;
using System;
using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

public class GolfController : MonoBehaviour
{
    // ============================================================
    // REFERENCES
    // ============================================================

    [Header("References")]
    [SerializeField] private ThirdPersonController thirdPersonController;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private Animator animator;
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private Transform golfCameraTransform;
    [SerializeField] private GolfShotUI golfShotUI;

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
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference sprintAction;

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

    [SerializeField] private float minimumShotAngle = 20f;
    [SerializeField] private float maximumShotAngle = 60f;
    [SerializeField] private float shotAngle = 45f;

    [SerializeField] private float chargeSpeed = 20f;

    [Header("Shot Angle Input")]
    [SerializeField] private float angleScrollStep = 2.5f;

    [Header("Shot Aim Guide")]
    [SerializeField] private LineRenderer shotAimLine;
    [SerializeField] private float shotAimLineLength = 5f;
    [SerializeField] private float shotAimLineHeightOffset = 0.05f;

    [Header("Shot Aim Line Smoothing")]
    [SerializeField] private float shotAimLineRotationSpeed = 10f;

    private Vector3 smoothedShotAimDirection;
    private bool shotAimLineInitialized;

    // ============================================================
    // AUDIO
    // ============================================================

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioSource chargeAudioSource;

    [Header("Charge Sound")]
    [SerializeField] private AudioClip chargeSound;
    [SerializeField] private float chargePitchMin = 0.8f;
    [SerializeField] private float chargePitchMax = 1.4f;

    [Header("Charge Sound Fade")]
    [SerializeField] private float chargeFadeInSpeed = 12f;
    [SerializeField] private float chargeFadeOutSpeed = 12f;

    private bool chargeFadingOut;

    [Header("Swing Sounds")]
    [SerializeField] private AudioClip lowChargeSwingSound;
    [SerializeField] private AudioClip mediumChargeSwingSound;
    [SerializeField] private AudioClip highChargeSwingSound;

    [Header("Release Sounds")]
    [SerializeField] private AudioClip lowChargeReleaseSound;
    [SerializeField] private AudioClip mediumChargeReleaseSound;
    [SerializeField] private AudioClip highChargeReleaseSound;

    [Header("Hit Sounds")]
    [SerializeField] private AudioClip lowPowerHitSound;
    [SerializeField] private AudioClip mediumPowerHitSound;
    [SerializeField] private AudioClip highPowerHitSound;

    [Header("Hit Power Thresholds")]
    [SerializeField, Range(0f, 1f)] private float mediumPowerThreshold = 0.33f;
    [SerializeField, Range(0f, 1f)] private float highPowerThreshold = 0.66f;

    [Header("Audio Variation")]
    [SerializeField] private float hitPitchVariation = 0.08f;
    [SerializeField] private float swingPitchVariation = 0.05f;

    // ============================================================
    // SHOT POSITIONING
    // ============================================================

    [Header("Shot Positioning")]
    [SerializeField] private float playerBallDistance = 1f;
    [SerializeField] private float orbitSpeed = 2f;
    [SerializeField] private float orbitMoveSpeed = 12f;
    [SerializeField] private float orbitRotationSpeed = 720f;
    [SerializeField] private float prepareBallLostDistance = 2f;

    private float orbitAngle;
    private Vector3 prepareTargetPosition;
    private Quaternion prepareTargetRotation;
    private Quaternion freePrepareTargetRotation;
    private bool prepareQueued;

    // ============================================================
    // SWING ANIMATION
    // ============================================================

    [Header("Swing Animation")]
    [SerializeField] private float hitFrame = 35f;

    private const float SwingTopFrame = 26f;
    private const float SwingTotalFrames = 60f;
    private const float MinimumFinishFrame = 37f;

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
    public bool lockMovement;
    public bool driving;

    private StarterAssets.StarterAssetsInputs starterInputs;

    [Header("Camera")]
    [SerializeField] private Transform normalCameraTarget;

    private Transform normalCameraTargetOriginalParent;
    private Vector3 normalCameraTargetOriginalLocalPosition;
    private Quaternion normalCameraTargetOriginalLocalRotation;

    private Transform originalNormalFollow;
    private Transform originalNormalLookAt;

    private CinemachineVirtualCamera normalCamera;
    private CinemachineVirtualCamera golfCamera;

    [Header("Camera Reattach")]
    [SerializeField] private float cameraReattachSpeed = .25f;

    // Tracks whether the golf camera actually became active.
    private bool golfCameraWasActivated;

    // Coroutine replacing Invoke().
    private Coroutine golfCameraActivationCoroutine;

    private static readonly int SwingState =
        Animator.StringToHash("GolfSwing");

    [Header("Hit Effects")]
    [SerializeField] private ParticleSystem ballHitParticlePrefab;

    [Header("Camera Shake")]
    [SerializeField] private CinemachineImpulseSource cameraShakeSource;
    [SerializeField] private float minimumShakeForce = 0.1f;
    [SerializeField] private float maximumShakeForce = 1.0f;

    [Header("Golf Camera Vertical Orbit")]
    [SerializeField] private Transform golfCameraOrbitCenter;
    [SerializeField] private float golfCameraLookSpeed = 1f;
    [SerializeField] private float golfCameraTopClamp = 60f;
    [SerializeField] private float golfCameraBottomClamp = -30f;
    [SerializeField] private float golfCameraInitialPitch = 20f;
    [SerializeField] private float golfCameraDistance = 5f;
    [SerializeField] private float golfCameraHeight = 2f;
    [SerializeField] private float golfCameraLookAtOffset = 0.5f;

    // ============================================================
    // MINIMAP ACCESS
    // ============================================================

    [Header("Minimap")]
    public float CurrentShotAngle => shotAngle;

    public float CurrentChargePercent =>
        chargeAmount / 100f;

    public float MaxShotForce => maxShotForce;

    public float CurrentLaunchSpeed
    {
        get
        {
            if (golfBall == null)
                return 0f;

            float charge =
                CurrentChargePercent;

            // Show full-power prediction before charging.
            if (charge <= 0.001f)
                charge = 1f;

            float force =
                maxShotForce * charge;

            return force / golfBall.mass;
        }
    }

    public Rigidbody CurrentGolfBall => golfBall;

    private float golfCameraPitch;

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

        // Save the target's original offset from the player.
        normalCameraTargetOriginalLocalPosition =
            normalCameraTarget.localPosition;

        normalCameraTargetOriginalParent = normalCameraTarget.parent;
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

        golfClub.gameObject.SetActive(true);
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

            if (!driving)
            {
                shootAction.action.Disable();
            }
        }

        golfClub.gameObject.SetActive(false);
    }

    private void Update()
    {
        UpdateChargeSoundFade();

        UpdateGolfClub();

        if (!preparingShot &&
            prepareQueued &&
            characterController != null &&
            characterController.isGrounded)
        {
            prepareQueued = false;
            StartPreparingShot();
        }

        if (!preparingShot)
            return;

        LockVerticalCameraInput();

        // Check whether the ball has moved too far away.
        if (golfBall != null && !IsGolfBallStillInRange())
        {
            // The ball rolled away while we were preparing.
            CancelBallPreparation();
            return;
        }

        if (golfBall != null)
        {
            DisablePlayerMovement();
            UpdatePreparePosition();
        }
        else
        {
            UpdateFreePrepareRotation();
        }

        UpdateShotAngle();

        UpdateCharge();

        // Update aim guide
        UpdateShotAimLine();
    }

    private void UpdateShotAimLine()
    {
        if (shotAimLine == null)
            return;

        if (!preparingShot || golfBall == null)
        {
            shotAimLine.enabled = false;
            shotAimLineInitialized = false;
            return;
        }

        shotAimLine.enabled = true;

        Vector3 start =
            golfBall.position +
            Vector3.up * shotAimLineHeightOffset;

        Camera mainCamera = Camera.main;

        if (mainCamera == null)
            return;

        // Get horizontal camera direction.
        Vector3 forward = mainCamera.transform.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.001f)
            return;

        forward.Normalize();

        float angleRadians =
            shotAngle * Mathf.Deg2Rad;

        Vector3 targetDirection =
            forward * Mathf.Cos(angleRadians) +
            Vector3.up * Mathf.Sin(angleRadians);

        targetDirection.Normalize();

        // Initialize immediately when the aim line first appears.
        if (!shotAimLineInitialized)
        {
            smoothedShotAimDirection = targetDirection;
            shotAimLineInitialized = true;
        }

        // Smooth the direction instead of snapping to the camera.
        smoothedShotAimDirection =
            Vector3.Slerp(
                smoothedShotAimDirection,
                targetDirection,
                shotAimLineRotationSpeed * Time.deltaTime
            );

        Vector3 end =
            start +
            smoothedShotAimDirection * shotAimLineLength;

        shotAimLine.SetPosition(0, start);
        shotAimLine.SetPosition(1, end);
    }

    private void SetShotAimLineVisible(bool visible)
    {
        if (shotAimLine == null)
            return;

        shotAimLine.enabled = visible;
    }

    private void UpdateShotAngle()
    {
        if (!preparingShot)
            return;

        if (Mouse.current == null)
            return;

        float scroll = Mouse.current.scroll.ReadValue().y;

        if (Mathf.Abs(scroll) < 0.01f)
            return;

        shotAngle += Mathf.Sign(scroll) * angleScrollStep;

        shotAngle = Mathf.Clamp(
            shotAngle,
            minimumShotAngle,
            maximumShotAngle
        );

        UpdateAngleUI();
    }

    private void UpdateAngleUI()
    {
        if (golfShotUI == null)
            return;

        float normalizedAngle = Mathf.InverseLerp(
            minimumShotAngle,
            maximumShotAngle,
            shotAngle
        );

        golfShotUI.SetAngle(shotAngle);
    }

    private bool IsGolfBallStillInRange()
    {
        if (golfBall == null)
            return false;

        float distance =
            Vector3.Distance(
                transform.position,
                golfBall.position
            );

        return distance <= prepareBallLostDistance;
    }

    private void CancelBallPreparation()
    {
        thirdPersonController.SetCameraTargetLocked(false);

        // Stop charge audio.
        StopChargeSound();

        preparingShot = false;

        SetShotAimLineVisible(false);

        Invoke(nameof(SetLockMovementFalse), .2f);

        // Clear golf preparation state.
        golfBall = null;

        // Reset shot values.
        chargeAmount = 0f;
        swingFrame = 0f;
        swingPlaying = false;
        swingReleased = false;
        ballHit = false;

        // Reset UI.
        if (golfShotUI != null)
        {
            golfShotUI.SetCharge(0f);
            golfShotUI.SetPreparing(false);
        }

        // IMPORTANT:
        // Use the exact same animator exit state that
        // FinishSwing() uses.
        if (animator != null)
        {
            animator.SetFloat(
                chargeParameter,
                0f
            );

            animator.SetBool(
                preparingParameter,
                false
            );

            animator.SetTrigger("SwingFinished");
        }

        // Restore normal player control.
        SetCharacterControllerEnabled(true);
        RestorePlayerInput();

        // Cancel any pending golf-camera activation.
        if (golfCameraActivationCoroutine != null)
        {
            StopCoroutine(golfCameraActivationCoroutine);
            golfCameraActivationCoroutine = null;
        }

        // Return to the normal camera.
        SetGolfCameraActive(false);

        // If the golf camera never activated, this is an early cancel.
        // In that case restore the original target and do NOT modify
        // the player's camera angles.
        //
        // If the golf camera did activate, use the normal golf-camera
        // exit behavior.
        ReAttachNormalCameraTarget(golfCameraWasActivated);

        golfCameraWasActivated = false;
    }

    private void SetLockMovementFalse()
    {
        lockMovement = false;
    }

    private void UpdateFreePrepareRotation()
    {
        if (starterInputs == null)
            return;

        float horizontalInput = starterInputs.look.x;

        if (Mathf.Abs(horizontalInput) > 0.001f)
        {
            float rotationAmount =
                horizontalInput *
                orbitSpeed *
                Time.deltaTime *
                100f;

            // Build a target rotation from the current target rotation.
            freePrepareTargetRotation =
                Quaternion.AngleAxis(
                    rotationAmount,
                    Vector3.up
                ) * freePrepareTargetRotation;
        }

        // Smoothly rotate toward the target.
        transform.rotation =
            Quaternion.RotateTowards(
                transform.rotation,
                freePrepareTargetRotation,
                orbitRotationSpeed * Time.deltaTime
            );

        // This allows UpdatePrepareCameraActivation() to use
        // the same rotation-complete logic as ball preparation.
        prepareTargetRotation = freePrepareTargetRotation;
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

            if (normalCamera != null)
            {
                originalNormalFollow = normalCamera.Follow;
                originalNormalLookAt = normalCamera.LookAt;
            }
        }

        if (golfCameraTransform != null)
        {
            golfCamera =
                golfCameraTransform.GetComponent<CinemachineVirtualCamera>();
        }
    }

    private void DetachNormalCameraTarget()
    {
        if (normalCameraTarget == null)
            return;

        normalCameraTarget.SetParent(null, true);
    }

    private void ReAttachNormalCameraTarget(bool golfCameraWasActive)
    {
        if (normalCameraTarget == null)
            return;

        // Reattach to the original parent.
        normalCameraTarget.SetParent(
            normalCameraTargetOriginalParent,
            false
        );

        // Restore ONLY the known static local position.
        normalCameraTarget.localPosition =
            normalCameraTargetOriginalLocalPosition;

        if (golfCameraWasActive)
        {
            // Golf camera actually became active, so this is
            // the normal golf -> player camera transition.
            normalCameraTarget.eulerAngles =
                golfCameraOrbitCenter.eulerAngles;

            thirdPersonController.SetCameraAngles(
                transform.eulerAngles.y - 90f,
                golfCameraOrbitCenter.eulerAngles.x
            );
        }
    }

    private void ReAttachNormalCameraTargetEarlyCancel()
    {
        if (normalCameraTarget == null)
            return;

        // Reattach without changing its current world rotation.
        normalCameraTarget.SetParent(
            normalCameraTargetOriginalParent,
            true
        );

        // Position is static, so restore the original position.
        normalCameraTarget.localPosition =
            normalCameraTargetOriginalLocalPosition;
    }

    private void ReAttachNormalCameraTargetAfterGolf()
    {
        if (normalCameraTarget == null)
            return;

        normalCameraTarget.SetParent(
            normalCameraTargetOriginalParent,
            true
        );

        normalCameraTarget.localPosition =
            normalCameraTargetOriginalLocalPosition;

        normalCameraTarget.eulerAngles =
            golfCameraOrbitCenter.eulerAngles;

        thirdPersonController.SetCameraAngles(
            transform.eulerAngles.y - 90f,
            golfCameraOrbitCenter.eulerAngles.x
        );
    }

    private void SetGolfCameraActive(bool active)
    {
        if (normalCamera != null)
        {
            normalCamera.Priority = active ? 0 : 1;

            if (active)
                normalCamera.gameObject.SetActive(false);
            else
                normalCamera.gameObject.SetActive(true);
        }
            

        if (golfCamera != null)
        {
            golfCamera.Priority = active ? 1 : 0;
        }
            
    }


    // ============================================================
    // PREPARE SHOT
    // ============================================================

    private void PrepareStarted(InputAction.CallbackContext context)
    {
        // Find the ball first so we know which preparation mode we need.
        FindGolfBall();

        // Ball preparation requires the player to be grounded.
        if (characterController != null &&
            !characterController.isGrounded)
        {
            prepareQueued = true;
            return;
        }

        // No ball nearby:
        // Always allow free preparation immediately.
        if (golfBall == null)
        {
            prepareQueued = false;
            StartPreparingShot();
            return;
        }

        prepareQueued = false;
        StartPreparingShot();
    }

    private void StartPreparingShot()
    {
        thirdPersonController.SetCameraTargetLocked(true);
        InitializeGolfCameraPitch();

        // We can prepare a shot even if there is no ball nearby.
        preparingShot = true;
        lockMovement = true;

        SetShotAimLineVisible(golfBall != null);

        SetPreparingAnimation(true);

        if (golfShotUI != null)
            golfShotUI.SetPreparing(true);

        // If we found a ball, enter the normal ball-preparation mode.
        if (golfBall != null)
        {
            golfBallCenter = golfBall.position;

            // Disable CharacterController while manually positioning
            // the player around the ball.
            SetCharacterControllerEnabled(false);

            // Calculate the initial position on the LEFT side of the ball
            // relative to the current camera.
            CalculateInitialOrbitPosition();

            // Face the ball.
            UpdatePrepareRotation();
        }
        else
        {
            // No ball nearby.
            // Keep movement enabled while preparing.
            SetCharacterControllerEnabled(true);

            Camera mainCamera = Camera.main;

            if (mainCamera != null)
            {
                // The player's forward direction should be the
                // camera's right direction, so from the camera's
                // perspective the player is facing to the right.
                Vector3 rightDirection = mainCamera.transform.right;

                // Ignore camera pitch.
                rightDirection.y = 0f;

                if (rightDirection.sqrMagnitude > 0.001f)
                {
                    rightDirection.Normalize();

                    freePrepareTargetRotation =
                        Quaternion.LookRotation(rightDirection);

                    prepareTargetRotation = freePrepareTargetRotation;
                }
            }
        }

        DetachNormalCameraTarget();

        golfCameraWasActivated = false;

        if (golfCameraActivationCoroutine != null)
        {
            StopCoroutine(golfCameraActivationCoroutine);
        }

        golfCameraActivationCoroutine =
            StartCoroutine(ActivateGolfCameraAfterDelay());

        if (golfShotUI != null)
        {
            golfShotUI.SetPreparing(true);
            golfShotUI.SetCharge(0f);
            golfShotUI.SetAngle(shotAngle);
        }
    }

    private IEnumerator ActivateGolfCameraAfterDelay()
    {
        yield return new WaitForSeconds(cameraReattachSpeed);

        // Preparation may have been cancelled during the delay.
        if (!preparingShot)
        {
            golfCameraActivationCoroutine = null;
            yield break;
        }

        SetGolfCameraActive(true);

        golfCameraWasActivated = true;
        golfCameraActivationCoroutine = null;
    }

    private void PrepareCanceled(InputAction.CallbackContext context)
    {
        CancelBallPreparation();
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
        {
            return;
        }

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
        starterInputs.SprintInput(false);
    }
    private void RestorePlayerInput()
    {
        if (starterInputs == null)
            return;

        // Re-read the actual current state of the movement input.
        if (moveAction != null)
        {
            Vector2 move = moveAction.action.ReadValue<Vector2>();
            starterInputs.MoveInput(move);
        }

        // Re-read sprint as well.
        if (sprintAction != null)
        {
            bool sprinting = sprintAction.action.IsPressed();
            starterInputs.SprintInput(sprinting);
        }
    }

    private void LockVerticalCameraInput()
    {
        if (starterInputs == null)
            return;

        Vector2 look = starterInputs.look;

        // Horizontal look is still passed through normally.
        // Vertical look controls the golf camera directly.
        UpdateGolfCameraVerticalLook(-look.y);

        // Prevent the normal Starter Assets camera from
        // also processing the vertical input.
        look.y = 0f;

        starterInputs.LookInput(look);
    }

    private void InitializeGolfCameraPitch()
    {
        golfCameraPitch = Mathf.Clamp(
            golfCameraInitialPitch,
            golfCameraBottomClamp,
            golfCameraTopClamp
        );
    }

    private void UpdateGolfCameraVerticalLook(float verticalInput)
    {
        if (golfCameraTransform == null ||
            golfCameraOrbitCenter == null)
            return;

        // ------------------------------------------------------------
        // Change vertical orbit angle.
        // ------------------------------------------------------------

        if (Mathf.Abs(verticalInput) > 0.001f)
        {
            golfCameraPitch -=
                verticalInput *
                golfCameraLookSpeed;

            golfCameraPitch = Mathf.Clamp(
                golfCameraPitch,
                golfCameraBottomClamp,
                golfCameraTopClamp
            );
        }

        // ------------------------------------------------------------
        // Preserve the existing horizontal direction.
        // ------------------------------------------------------------

        Vector3 horizontalDirection =
            golfCameraTransform.position -
            golfCameraOrbitCenter.position;

        horizontalDirection.y = 0f;

        if (horizontalDirection.sqrMagnitude < 0.001f)
        {
            horizontalDirection =
                -golfCameraTransform.forward;

            horizontalDirection.y = 0f;

            if (horizontalDirection.sqrMagnitude < 0.001f)
                return;
        }

        horizontalDirection.Normalize();

        // ------------------------------------------------------------
        // Calculate vertical orbit.
        // ------------------------------------------------------------

        float pitchRadians =
            golfCameraPitch * Mathf.Deg2Rad;

        float horizontalDistance =
            golfCameraDistance *
            Mathf.Cos(pitchRadians);

        float verticalDistance =
            golfCameraDistance *
            Mathf.Sin(pitchRadians);

        Vector3 newPosition =
            golfCameraOrbitCenter.position +
            horizontalDirection * horizontalDistance;

        newPosition.y +=
            golfCameraHeight +
            verticalDistance;

        golfCameraTransform.position =
            newPosition;

        // ------------------------------------------------------------
        // Look at the invisible orbit center.
        // ------------------------------------------------------------

        Vector3 lookAtPosition =
            golfCameraOrbitCenter.position +
            Vector3.up * golfCameraLookAtOffset;

        Vector3 lookDirection =
            lookAtPosition -
            golfCameraTransform.position;

        if (lookDirection.sqrMagnitude > 0.001f)
        {
            golfCameraTransform.rotation =
                Quaternion.LookRotation(lookDirection);
        }
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
            // Start the looping charge sound.
            StartChargeSound();

            ChargeShot();

            // Update pitch as the shot gets stronger.
            UpdateChargeSound();

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

        if (golfShotUI != null)
        {
            golfShotUI.SetCharge(
                chargeAmount / 100f
            );
        }

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

        chargeAmount = 0f;
        swingFrame = 0f;

        swingReleased = false;
        swingPlaying = false;
        ballHit = false;

        StopChargeSound();

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

        // Stop the looping charge sound.
        StopChargeSound();

        // Play release sound based on charge.
        PlayReleaseSound();

        // Play swing sound based on charge.
        PlaySwingSound();

        swingReleased = true;
        swingPlaying = true;
        ballHit = false;

        float chargePercent =
            chargeAmount / 100f;

        swingFrame =
            Mathf.Lerp(
                hitFrame,
                SwingTopFrame,
                chargePercent
            );

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

        // Determine how far the swing should finish based on charge.
        // 0% charge  = frame 37
        // 100% charge = frame 60
        float finishFrame =
            Mathf.Lerp(
                MinimumFinishFrame,
                SwingTotalFrames,
                chargePercent
            );

        // Finish the swing once we reach the charge-dependent endpoint.
        if (swingFrame >= finishFrame)
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
        SetShotAimLineVisible(false);

        swingFrame = 0f;

        swingPlaying = false;
        swingReleased = false;

        chargeAmount = 0f;
        ballHit = false;

        if (golfShotUI != null)
            golfShotUI.SetCharge(0f);

        if (animator != null)
        {
            animator.SetFloat(
                chargeParameter,
                0f
            );

            animator.SetBool(
                preparingParameter,
                false
            );

            animator.SetTrigger("SwingFinished");
        }

        ReAttachNormalCameraTarget(true);
    }


    // ============================================================
    // HIT GOLF BALL
    // ============================================================

    private void HitGolfBall()
    {
        if (golfBall == null)
            return;

        Vector3 shotDirection =
            GetCurrentShotDirection();

        if (shotDirection.sqrMagnitude < 0.001f)
            return;

        float chargeMultiplier =
            chargeAmount / 100f;

        float force =
            maxShotForce *
            chargeMultiplier;

        GolfBall ball =
            golfBall.GetComponent<GolfBall>();

        if (ball != null)
        {
            ball.Launch(
                shotDirection,
                force
            );

            // Count the stroke only when the ball is actually launched.
            if (GameController.Instance != null)
            {
                GameController.Instance.AddStroke();
            }

            PlayHitEffects(
                chargeMultiplier
            );

            PlayHitSound();
        }
    }

    // ============================================================
    // SHOT DIRECTION
    // ============================================================

    public Vector3 GetCurrentShotDirection()
    {
        Camera mainCamera = Camera.main;

        if (mainCamera == null)
            return Vector3.zero;

        Vector3 forward = mainCamera.transform.forward;

        // Ignore camera pitch when determining horizontal aim.
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.001f)
            return Vector3.zero;

        forward.Normalize();

        float angle =
            shotAngle * Mathf.Deg2Rad;

        Vector3 shotDirection =
            forward * Mathf.Cos(angle) +
            Vector3.up * Mathf.Sin(angle);

        return shotDirection.normalized;
    }

    // ============================================================
    // AUDIO
    // ============================================================

    private void StartChargeSound()
    {
        if (chargeAudioSource == null ||
            chargeSound == null)
            return;

        chargeFadingOut = false;

        if (!chargeAudioSource.isPlaying)
        {
            chargeAudioSource.clip = chargeSound;
            chargeAudioSource.loop = true;
            chargeAudioSource.pitch = chargePitchMin;

            // Start silent so there is no click.
            chargeAudioSource.volume = 0f;

            float chargePercent = chargeAmount / 100f;

            chargeAudioSource.pitch =
                Mathf.Lerp(
                    chargePitchMin,
                    chargePitchMax,
                    chargePercent
                );

            chargeAudioSource.Play();
        }

        // Fade in.
        chargeAudioSource.volume =
            Mathf.MoveTowards(
                chargeAudioSource.volume,
                1f,
                chargeFadeInSpeed * Time.deltaTime
            );
    }

    private void UpdateChargeSoundFade()
    {
        if (chargeAudioSource == null)
            return;

        if (!chargeAudioSource.isPlaying)
            return;

        float targetVolume = chargeFadingOut ? 0f : 1f;

        float fadeSpeed =
            chargeFadingOut
                ? chargeFadeOutSpeed
                : chargeFadeInSpeed;

        chargeAudioSource.volume =
            Mathf.MoveTowards(
                chargeAudioSource.volume,
                targetVolume,
                fadeSpeed * Time.deltaTime
            );

        // Once completely faded out, actually stop it.
        if (chargeFadingOut &&
            chargeAudioSource.volume <= 0.001f)
        {
            chargeAudioSource.Stop();

            chargeAudioSource.volume = 0f;
            chargeAudioSource.pitch = chargePitchMin;

            chargeFadingOut = false;
        }
    }

    private void UpdateChargeSound()
    {
        if (chargeAudioSource == null ||
            !chargeAudioSource.isPlaying ||
            chargeFadingOut)
            return;

        float chargePercent =
            chargeAmount / 100f;

        float targetPitch =
            Mathf.Lerp(
                chargePitchMin,
                chargePitchMax,
                chargePercent
            );

        chargeAudioSource.pitch =
            Mathf.Lerp(
                chargeAudioSource.pitch,
                targetPitch,
                10f * Time.deltaTime
            );
    }

    private void StopChargeSound()
    {
        if (chargeAudioSource == null)
            return;

        if (!chargeAudioSource.isPlaying)
        {
            chargeAudioSource.volume = 0f;
            chargeAudioSource.pitch = chargePitchMin;
            chargeFadingOut = false;
            return;
        }

        chargeFadingOut = true;
    }

    private AudioClip GetChargeSound(AudioClip low,
                                     AudioClip medium,
                                     AudioClip high)
    {
        float chargePercent =
            chargeAmount / 100f;

        if (chargePercent < mediumPowerThreshold)
            return low;

        if (chargePercent < highPowerThreshold)
            return medium;

        return high;
    }

    private void PlaySwingSound()
    {
        if (audioSource == null)
            return;

        AudioClip clip =
            GetChargeSound(
                lowChargeSwingSound,
                mediumChargeSwingSound,
                highChargeSwingSound
            );

        if (clip == null)
            return;

        audioSource.pitch =
            1f +
            UnityEngine.Random.Range(
                -swingPitchVariation,
                swingPitchVariation
            );

        audioSource.PlayOneShot(clip);
    }

    private void PlayReleaseSound()
    {
        if (audioSource == null)
            return;

        AudioClip clip =
            GetChargeSound(
                lowChargeReleaseSound,
                mediumChargeReleaseSound,
                highChargeReleaseSound
            );

        if (clip == null)
            return;

        audioSource.pitch = 1f;

        audioSource.PlayOneShot(clip);
    }

    private void PlayHitSound()
    {
        if (audioSource == null)
            return;

        AudioClip clip =
            GetChargeSound(
                lowPowerHitSound,
                mediumPowerHitSound,
                highPowerHitSound
            );

        if (clip == null)
            return;

        audioSource.pitch =
            1f +
            UnityEngine.Random.Range(
                -hitPitchVariation,
                hitPitchVariation
            );

        audioSource.PlayOneShot(clip);
    }

    private void PlayHitEffects(float powerPercent)
    {
        // ============================================================
        // BALL PARTICLE
        // ============================================================

        SpawnBallHitParticle();

        // ============================================================
        // CAMERA SHAKE
        // ============================================================

        if (cameraShakeSource != null)
        {
            float shakeForce =
                Mathf.Lerp(
                    minimumShakeForce,
                    maximumShakeForce,
                    powerPercent
                );

            cameraShakeSource.GenerateImpulse(shakeForce);
        }
    }

    private void SpawnBallHitParticle()
    {
        if (ballHitParticlePrefab == null || golfBall == null)
            return;

        ParticleSystem particle =
            Instantiate(
                ballHitParticlePrefab,
                golfBall.position,
                Quaternion.identity
            );

        particle.Play();

        Destroy(
            particle.gameObject,
            particle.main.duration + particle.main.startLifetime.constantMax
        );
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