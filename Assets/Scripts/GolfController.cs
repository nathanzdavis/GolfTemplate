using StarterAssets;
using System;
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

    private StarterAssets.StarterAssetsInputs starterInputs;

    [Header("Camera Prepositioning")]
    [SerializeField] private Transform normalCameraTarget;
    [SerializeField] private Transform cameraPrepositionTarget;
    [SerializeField] private float cameraPrepositionSpeed = 5f;
    [SerializeField] private float cameraPrepositionDelay = 1f;
    [SerializeField] private float cameraActivationDelay = 0.5f;
    [SerializeField] private float cameraRotationTolerance = 1f;
    private Transform normalCameraTargetOriginalParent;
    private Vector3 normalCameraTargetOriginalLocalPosition;
    private bool normalCameraTargetDetached;

    private bool waitingForPrepareRotation;

    private float cameraPrepositionTimer;
    private float cameraActivationTimer;
    private bool cameraPrepositionStarted;

    private Transform originalNormalFollow;
    private Transform originalNormalLookAt;

    private Quaternion golfCameraTargetRotation;
    private CinemachineVirtualCamera normalCamera;
    private CinemachineVirtualCamera golfCamera;

    [Header("Camera Reattach")]
    [SerializeField] private float cameraReattachSpeed = 8f;

    private bool reattachingNormalCameraTarget;

    private static readonly int SwingState =
        Animator.StringToHash("GolfSwing");

    [Header("Hit Effects")]
    [SerializeField] private ParticleSystem ballHitParticlePrefab;

    [Header("Camera Shake")]
    [SerializeField] private CinemachineImpulseSource cameraShakeSource;
    [SerializeField] private float minimumShakeForce = 0.1f;
    [SerializeField] private float maximumShakeForce = 1.0f;

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
        UpdateGolfClub();

        if (reattachingNormalCameraTarget)
        {
            UpdateCameraTargetReattach();
        }

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

        // Activate the golf camera only after preparation rotation is finished.
        UpdatePrepareCameraActivation();

        // Secretly move the normal camera toward the golf camera.
        PrepositionNormalCamera();
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
        waitingForPrepareRotation = false;

        thirdPersonController.SetCameraTargetLocked(false);

        // Stop charge audio.
        StopChargeSound();

        preparingShot = false;
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

        // Return to normal camera.
        SetGolfCameraActive(false);

        StopCameraPreposition();
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

        // Detach while preserving its current world position/rotation.
        normalCameraTarget.SetParent(null);

        normalCameraTargetDetached = true;
    }

    private void ReAttachNormalCameraTarget()
    {
        if (normalCameraTarget == null)
            return;

        // Detach while preserving its current world position/rotation.
        normalCameraTarget.SetParent(normalCameraTargetOriginalParent);
        normalCameraTarget.localPosition = normalCameraTargetOriginalLocalPosition;
        normalCameraTargetDetached = false;
    }

    private void StartCameraTargetReattach()
    {
        if (normalCameraTarget == null || !normalCameraTargetDetached)
            return;

        // Reparent while preserving the camera target's current
        // world position and rotation.
        normalCameraTarget.SetParent(transform, true);

        reattachingNormalCameraTarget = true;
    }

    private void UpdateCameraTargetReattach()
    {
        if (normalCameraTarget == null)
        {
            reattachingNormalCameraTarget = false;
            normalCameraTargetDetached = false;
            return;
        }

        // Smoothly move from the current local position back
        // to the original position it had before detaching.
        normalCameraTarget.localPosition =
            Vector3.Lerp(
                normalCameraTarget.localPosition,
                normalCameraTargetOriginalLocalPosition,
                cameraReattachSpeed * Time.deltaTime
            );

        // Also smoothly restore the original local rotation if needed.
        // This is optional, but prevents any rotation offset from
        // remaining after the camera preparation.
        normalCameraTarget.localRotation =
            Quaternion.Slerp(
                normalCameraTarget.localRotation,
                Quaternion.identity,
                cameraReattachSpeed * Time.deltaTime
            );

        // Finish once we're sufficiently close.
        if (Vector3.Distance(
                normalCameraTarget.localPosition,
                normalCameraTargetOriginalLocalPosition) < 0.001f)
        {
            normalCameraTarget.localPosition =
                normalCameraTargetOriginalLocalPosition;

            reattachingNormalCameraTarget = false;
            normalCameraTargetDetached = false;
        }
    }

    private void StartCameraPreposition()
    {
        if (cameraPrepositionTarget == null ||
            golfCameraTransform == null)
            return;

        cameraPrepositionTarget.rotation =
            golfCameraTransform.rotation;
    }

    private void StopCameraPreposition()
    {
        ReAttachNormalCameraTarget();

        if (normalCamera == null)
            return;

        normalCamera.Follow = originalNormalFollow;
        normalCamera.LookAt = originalNormalLookAt;
    }

    private void PrepositionNormalCamera()
    {
        if (normalCameraTarget == null ||
            cameraPrepositionTarget == null ||
            golfCameraTransform == null)
            return;

        if (!cameraPrepositionStarted)
        {
            cameraPrepositionTimer += Time.deltaTime;

            if (cameraPrepositionTimer < cameraPrepositionDelay)
                return;

            cameraPrepositionStarted = true;

            // Start the temporary target at the current
            // normal target rotation.
            cameraPrepositionTarget.rotation =
                normalCameraTarget.rotation;
        }

        // Slowly move the temporary target toward
        // the golf camera's current orientation.
        cameraPrepositionTarget.rotation =
            Quaternion.Slerp(
                cameraPrepositionTarget.rotation,
                golfCameraTransform.rotation,
                cameraPrepositionSpeed * Time.deltaTime
            );

        // Then move the ORIGINAL camera target toward it.
        normalCameraTarget.rotation =
            Quaternion.Slerp(
                normalCameraTarget.rotation,
                cameraPrepositionTarget.rotation,
                cameraPrepositionSpeed * Time.deltaTime
            );

        Vector3 angles = normalCameraTarget.rotation.eulerAngles;

        thirdPersonController.SetCameraAngles(
            angles.y,
            angles.x
        );
    }

    private void UpdatePrepareCameraActivation()
    {
        if (!preparingShot || !waitingForPrepareRotation)
            return;

        // Always wait this long before even attempting
        // to activate the golf camera.
        cameraActivationTimer += Time.deltaTime;

        if (cameraActivationTimer < cameraActivationDelay)
            return;

        bool rotationFinished;

        if (golfBall != null)
        {
            // Wait until the player is facing the ball.
            rotationFinished =
                Quaternion.Angle(
                    transform.rotation,
                    prepareTargetRotation
                ) <= cameraRotationTolerance;
        }
        else
        {
            // Free preparation has no target rotation.
            rotationFinished = true;
        }

        if (!rotationFinished)
            return;

        waitingForPrepareRotation = false;

        SetGolfCameraActive(true);
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
        if (characterController != null &&
            !characterController.isGrounded)
        {
            prepareQueued = true;
            return;
        }

        StartPreparingShot();
    }

    private void StartPreparingShot()
    {
        thirdPersonController.SetCameraTargetLocked(true);
        DetachNormalCameraTarget();

        // Try to find a nearby golf ball.
        FindGolfBall();

        // We can prepare a shot even if there is no ball nearby.
        preparingShot = true;
        lockMovement = true;

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

        StartCameraPreposition();

        cameraPrepositionTimer = 0f;
        cameraActivationTimer = 0f;
        cameraPrepositionStarted = false;

        waitingForPrepareRotation = true;

        if (golfShotUI != null)
        {
            golfShotUI.SetPreparing(true);
            golfShotUI.SetCharge(0f);
            golfShotUI.SetAngle(shotAngle);
        }

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

        StopCameraPreposition();
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

            PlayHitEffects(
                chargeMultiplier
            );

            PlayHitSound();
        }
    }

    // ============================================================
    // AUDIO
    // ============================================================

    private void StartChargeSound()
    {
        if (chargeAudioSource == null ||
            chargeSound == null)
            return;

        if (chargeAudioSource.isPlaying)
            return;

        chargeAudioSource.clip = chargeSound;
        chargeAudioSource.loop = true;
        chargeAudioSource.volume = 1f;

        // Start at the current charge pitch.
        float chargePercent = chargeAmount / 100f;

        chargeAudioSource.pitch =
            Mathf.Lerp(
                chargePitchMin,
                chargePitchMax,
                chargePercent
            );

        chargeAudioSource.Play();
    }

    private void UpdateChargeSound()
    {
        if (chargeAudioSource == null ||
            !chargeAudioSource.isPlaying)
            return;

        float chargePercent =
            chargeAmount / 100f;

        float targetPitch =
            Mathf.Lerp(
                chargePitchMin,
                chargePitchMax,
                chargePercent
            );

        // Smooth the pitch instead of changing it abruptly every frame.
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

        if (chargeAudioSource.isPlaying)
            chargeAudioSource.Stop();

        chargeAudioSource.pitch = chargePitchMin;
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