using System;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using StarterAssets;

public class GolfCartController : MonoBehaviour
{
    // ============================================================
    // REFERENCES
    // ============================================================

    [Header("Player")]
    [SerializeField] private ThirdPersonController thirdPersonController;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private GolfController golfController;
    [SerializeField] private Animator playerAnimator;
    [SerializeField] private Transform playerTransform;

    [Header("Vehicle Player Collision")]
    [SerializeField] private Collider[] cartColliders;
    private Collider[] playerColliders;

    [Header("Cart")]
    [SerializeField] private Rigidbody rb;
    [SerializeField] private Transform seatPoint;
    [SerializeField] private Transform exitPoint;

    [Header("Cart Camera")]
    [SerializeField] private CinemachineVirtualCamera normalCamera;
    [SerializeField] private Transform normalCameraTarget;
    [SerializeField] private Transform cartCameraTarget;

    [SerializeField] private float bottomClamp = -30f;
    [SerializeField] private float topClamp = 70f;

    [SerializeField] private float cameraAngleOverride = 0f;

    [SerializeField] private float cameraLookThreshold = 0.01f;

    [SerializeField] private bool lockCameraPosition = false;

    [SerializeField] private bool invertLookY = true;

    private float cartCameraYaw;
    private float cartCameraPitch;
    private bool isCurrentDeviceMouse;

    [Header("Input")]
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference lookAction;
    [SerializeField] private InputActionReference interactAction;

    [Header("Wheels")]
    [SerializeField] private WheelCollider frontLeftWheel;
    [SerializeField] private WheelCollider frontRightWheel;
    [SerializeField] private WheelCollider rearLeftWheel;
    [SerializeField] private WheelCollider rearRightWheel;

    [Header("Wheel Meshes")]
    [SerializeField] private Transform frontLeftMesh;
    [SerializeField] private Transform frontRightMesh;
    [SerializeField] private Transform rearLeftMesh;
    [SerializeField] private Transform rearRightMesh;

    [Header("Wheel Visual Rotation")]
    [SerializeField] private Vector3 leftWheelRotationOffset;
    [SerializeField] private Vector3 rightWheelRotationOffset;

    [Header("Dust")]
    [SerializeField] private ParticleSystem frontLeftDust;
    [SerializeField] private ParticleSystem frontRightDust;
    [SerializeField] private ParticleSystem rearLeftDust;
    [SerializeField] private ParticleSystem rearRightDust;

    [Header("Audio")]
    [SerializeField] private AudioSource engineAudio;

    // ============================================================
    // PHYSICS
    // ============================================================

    [Header("Driving")]
    [SerializeField] private float maxForwardSpeed = 12f;
    [SerializeField] private float maxReverseSpeed = 5f;
    [SerializeField] private float motorTorque = 650f;
    [SerializeField] private float brakeTorque = 1000f;
    [SerializeField] private float rollingBrakeTorque = 150f;

    [Header("Steering")]
    [SerializeField] private float maxSteerAngle = 32f;
    [SerializeField] private float steeringResponse = 8f;
    [SerializeField] private float highSpeedSteeringMultiplier = 0.55f;

    [Header("Acceleration")]
    [SerializeField] private float accelerationResponse = 4f;
    [SerializeField] private float throttleDeadZone = 0.05f;

    [Header("Stability")]
    [SerializeField] private float centerOfMassHeight = -0.35f;
    [SerializeField] private float antiRollForce = 5000f;

    // ============================================================
    // AUDIO
    // ============================================================

    [Header("Engine Audio")]
    [SerializeField] private float idlePitch = 0.8f;
    [SerializeField] private float maxPitch = 1.6f;
    [SerializeField] private float idleVolume = 0.35f;
    [SerializeField] private float maxVolume = 0.8f;
    [SerializeField] private float audioSmoothSpeed = 5f;

    // ============================================================
    // DUST
    // ============================================================

    [Header("Wheel Dust")]
    [SerializeField] private float dustMinimumSpeed = 2f;
    [SerializeField] private float dustSlipThreshold = 0.25f;

    // ============================================================
    // STATE
    // ============================================================

    private bool playerNearby;
    private bool driving;

    private Vector2 moveInput;
    private Vector2 lookInput;

    private float currentThrottle;
    private float currentSteer;

    private float cameraYaw;
    private float cameraPitch;

    private Transform previousCameraFollow;

    private Transform previousPlayerParent;
    private Vector3 previousPlayerPosition;
    private Quaternion previousPlayerRotation;

    private Transform originalCameraTargetParent;
    private Vector3 originalCameraTargetLocalPosition;
    private Quaternion originalCameraTargetLocalRotation;


    // ============================================================
    // UNITY
    // ============================================================

    private void Awake()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (playerTransform == null && thirdPersonController != null)
            playerTransform = thirdPersonController.transform;

        if (characterController == null && thirdPersonController != null)
            characterController = thirdPersonController.GetComponent<CharacterController>();

        if (playerAnimator == null && thirdPersonController != null)
            playerAnimator = thirdPersonController.GetComponentInChildren<Animator>();

        if (rb != null)
        {
            rb.centerOfMass = new Vector3(
                rb.centerOfMass.x,
                centerOfMassHeight,
                rb.centerOfMass.z
            );
        }

        if (normalCameraTarget != null)
        {
            originalCameraTargetParent = normalCameraTarget.parent;
            originalCameraTargetLocalPosition = normalCameraTarget.localPosition;
            originalCameraTargetLocalRotation = normalCameraTarget.localRotation;
        }

        if (engineAudio != null)
        {
            engineAudio.loop = true;
            engineAudio.playOnAwake = false;
        }

        if (playerTransform != null)
        {
            playerColliders = playerTransform.GetComponentsInChildren<Collider>();
        }
    }

    private void SetPlayerCartCollision(bool enabled)
    {
        if (playerColliders == null || cartColliders == null)
            return;

        foreach (Collider playerCollider in playerColliders)
        {
            if (playerCollider == null)
                continue;

            foreach (Collider cartCollider in cartColliders)
            {
                if (cartCollider == null)
                    continue;

                if (playerCollider == cartCollider)
                    continue;

                Physics.IgnoreCollision(
                    playerCollider,
                    cartCollider,
                    !enabled
                );
            }
        }
    }

    private void OnEnable()
    {
        if (moveAction != null)
            moveAction.action.Enable();

        if (lookAction != null)
            lookAction.action.Enable();

        if (interactAction != null)
        {
            interactAction.action.Enable();
            interactAction.action.started += OnInteract;
        }
    }

    private void OnDisable()
    {
        if (interactAction != null)
            interactAction.action.started -= OnInteract;

        if (moveAction != null)
            moveAction.action.Disable();

        if (lookAction != null)
            lookAction.action.Disable();

        if (interactAction != null)
            interactAction.action.Disable();
    }

    private void Update()
    {
        if (!driving)
            return;

        KeepPlayerInSeat();

        ReadInput();

        UpdateEngineAudio();
        UpdateDust();
    }

    private void CameraRotation()
    {
        if (lookInput.sqrMagnitude >= cameraLookThreshold && !lockCameraPosition)
        {
            // Same mouse/controller handling as Starter Assets.
            float deltaTimeMultiplier = thirdPersonController.IsCurrentDeviceMouse
                ? 1.0f
                : Time.deltaTime;

            cartCameraYaw += lookInput.x * deltaTimeMultiplier;

            float lookY = invertLookY
                ? -lookInput.y
                : lookInput.y;

            cartCameraPitch += lookY * deltaTimeMultiplier;
        }

        cartCameraYaw = ClampAngle(
            cartCameraYaw,
            float.MinValue,
            float.MaxValue
        );

        cartCameraPitch = ClampAngle(
            cartCameraPitch,
            bottomClamp,
            topClamp
        );

        cartCameraTarget.transform.rotation = Quaternion.Euler(
            cartCameraPitch + cameraAngleOverride,
            cartCameraYaw,
            0f
        );
    }

    private void KeepPlayerInSeat()
    {
        if (!driving || playerTransform == null || seatPoint == null)
            return;

        playerTransform.position = seatPoint.position;
        playerTransform.rotation = seatPoint.rotation;
    }

    private void FixedUpdate()
    {
        if (!driving)
            return;

        ApplyDrivingPhysics();
        //ApplyAntiRoll();
    }

    private void LateUpdate()
    {
        if (!driving)
            return;

        UpdateWheelMeshes();

        CameraRotation();
    }

    // ============================================================
    // INPUT
    // ============================================================

    private void ReadInput()
    {
        if (moveAction != null)
            moveInput = moveAction.action.ReadValue<Vector2>();

        if (lookAction != null)
            lookInput = lookAction.action.ReadValue<Vector2>();
    }

    private void OnInteract(InputAction.CallbackContext context)
    {
        print("interact pressed");
        if (!context.started)
            return;

        if (driving)
        {
            ExitCart();
            return;
        }

        if (playerNearby)
        {
            EnterCart();
        }
    }

    // ============================================================
    // ENTER / EXIT
    // ============================================================

    private void EnterCart()
    {
        if (playerTransform == null || seatPoint == null)
            return;

        // ------------------------------------------------------------
        // SAVE PLAYER STATE
        // ------------------------------------------------------------

        previousPlayerParent = playerTransform.parent;
        previousPlayerPosition = playerTransform.position;
        previousPlayerRotation = playerTransform.rotation;

        // ------------------------------------------------------------
        // COMPLETELY DISABLE PLAYER
        // ------------------------------------------------------------

        if (thirdPersonController != null)
            thirdPersonController.enabled = false;

        if (characterController != null)
            characterController.enabled = false;

        if (golfController != null)
            golfController.enabled = false;



        SetPlayerCollidersEnabled(false);

        // ------------------------------------------------------------
        // REMOVE PLAYER FROM ANY PHYSICS HIERARCHY
        // ------------------------------------------------------------

        playerTransform.SetParent(null, true);

        // ------------------------------------------------------------
        // PLACE PLAYER IN SEAT
        // ------------------------------------------------------------

        playerTransform.position = seatPoint.position;
        playerTransform.rotation = seatPoint.rotation;

        // ------------------------------------------------------------
        // ENTER CART
        // ------------------------------------------------------------

        driving = true;

        currentThrottle = 0f;
        currentSteer = 0f;

        moveInput = Vector2.zero;
        lookInput = Vector2.zero;

        // ------------------------------------------------------------
        // CAMERA
        // ------------------------------------------------------------

        if (normalCamera != null)
        {
            previousCameraFollow = normalCamera.Follow;
            normalCamera.Follow = cartCameraTarget;
        }

        // ------------------------------------------------------------
        // ENGINE
        // ------------------------------------------------------------

        if (engineAudio != null)
        {
            engineAudio.pitch = idlePitch;
            engineAudio.volume = idleVolume;

            if (!engineAudio.isPlaying)
                engineAudio.Play();
        }
    }

    private void SetPlayerCollidersEnabled(bool enabled)
    {
        if (playerColliders == null)
            return;

        foreach (Collider collider in playerColliders)
        {
            if (collider == null)
                continue;

            collider.enabled = enabled;
        }
    }

    private void ExitCart()
    {
        if (!driving)
            return;

        driving = false;

        StopCart();

        if (engineAudio != null)
            engineAudio.Stop();

        // ------------------------------------------------------------
        // MOVE PLAYER OUTSIDE THE CART
        // ------------------------------------------------------------

        if (exitPoint != null)
        {
            playerTransform.position = exitPoint.position;
            playerTransform.rotation = exitPoint.rotation;
        }
        else
        {
            playerTransform.position = previousPlayerPosition;
            playerTransform.rotation = previousPlayerRotation;
        }

        playerTransform.SetParent(previousPlayerParent, true);

        // ------------------------------------------------------------
        // RESTORE PLAYER PHYSICS
        // ------------------------------------------------------------

        SetPlayerCollidersEnabled(true);

        // Enable CharacterController LAST.
        if (characterController != null)
            characterController.enabled = true;

        if (thirdPersonController != null)
            thirdPersonController.enabled = true;

        if (golfController != null)
            golfController.enabled = true;

        // ------------------------------------------------------------
        // CAMERA
        // ------------------------------------------------------------

        if (normalCamera != null)
            normalCamera.Follow = previousCameraFollow;

        moveInput = Vector2.zero;
        lookInput = Vector2.zero;
    }

    private void StopCart()
    {
        if (frontLeftWheel != null)
            frontLeftWheel.motorTorque = 0f;

        if (frontRightWheel != null)
            frontRightWheel.motorTorque = 0f;

        ApplyBrakeTorque(brakeTorque);
    }

    // ============================================================
    // DRIVING
    // ============================================================

    private void ApplyDrivingPhysics()
    {
        if (rb == null)
            return;

        float forwardSpeed = Vector3.Dot(
            rb.linearVelocity,
            transform.forward
        );

        float throttle = moveInput.y;

        if (Mathf.Abs(throttle) < throttleDeadZone)
            throttle = 0f;

        // --------------------------------------------------------
        // SPEED LIMIT
        // --------------------------------------------------------

        float targetMaxSpeed = throttle >= 0f
            ? maxForwardSpeed
            : maxReverseSpeed;

        bool movingTooFast = Mathf.Abs(forwardSpeed) >= targetMaxSpeed;

        // --------------------------------------------------------
        // MOTOR
        // --------------------------------------------------------

        float targetThrottle = throttle;

        if (movingTooFast &&
            Mathf.Sign(throttle) == Mathf.Sign(forwardSpeed))
        {
            targetThrottle = 0f;
        }

        currentThrottle = Mathf.MoveTowards(
            currentThrottle,
            targetThrottle,
            accelerationResponse * Time.fixedDeltaTime
        );

        float torque = currentThrottle * motorTorque;

        if (frontLeftWheel != null)
            frontLeftWheel.motorTorque = torque;

        if (frontRightWheel != null)
            frontRightWheel.motorTorque = torque;

        // --------------------------------------------------------
        // BRAKING
        // --------------------------------------------------------

        if (Mathf.Abs(throttle) < throttleDeadZone)
        {
            ApplyBrakeTorque(rollingBrakeTorque);
        }
        else if (Mathf.Abs(forwardSpeed) > 0.25f &&
                 Mathf.Sign(throttle) != Mathf.Sign(forwardSpeed))
        {
            ApplyBrakeTorque(brakeTorque);
        }
        else
        {
            ApplyBrakeTorque(0f);
        }

        // --------------------------------------------------------
        // STEERING
        // --------------------------------------------------------

        float speedPercent = Mathf.Clamp01(
            Mathf.Abs(forwardSpeed) / maxForwardSpeed
        );

        float steeringMultiplier = Mathf.Lerp(
            1f,
            highSpeedSteeringMultiplier,
            speedPercent
        );

        float targetSteer =
            moveInput.x *
            maxSteerAngle *
            steeringMultiplier;

        currentSteer = Mathf.Lerp(
            currentSteer,
            targetSteer,
            steeringResponse * Time.fixedDeltaTime
        );

        if (frontLeftWheel != null)
            frontLeftWheel.steerAngle = currentSteer;

        if (frontRightWheel != null)
            frontRightWheel.steerAngle = currentSteer;
    }

    private void ApplyBrakeTorque(float torque)
    {
        if (frontLeftWheel != null)
            frontLeftWheel.brakeTorque = torque;

        if (frontRightWheel != null)
            frontRightWheel.brakeTorque = torque;

        if (rearLeftWheel != null)
            rearLeftWheel.brakeTorque = torque;

        if (rearRightWheel != null)
            rearRightWheel.brakeTorque = torque;
    }

    // ============================================================
    // ANTI ROLL
    // ============================================================

    private void ApplyAntiRoll()
    {
        ApplyAntiRollToAxle(
            frontLeftWheel,
            frontRightWheel
        );

        ApplyAntiRollToAxle(
            rearLeftWheel,
            rearRightWheel
        );
    }

    private void ApplyAntiRollToAxle(
        WheelCollider left,
        WheelCollider right)
    {
        if (left == null || right == null)
            return;

        WheelHit hit;

        bool leftGrounded = left.GetGroundHit(out hit);
        float leftTravel = 1f;

        if (leftGrounded)
        {
            leftTravel =
                (-left.transform.InverseTransformPoint(hit.point).y -
                 left.radius) /
                left.suspensionDistance;
        }

        bool rightGrounded = right.GetGroundHit(out hit);
        float rightTravel = 1f;

        if (rightGrounded)
        {
            rightTravel =
                (-right.transform.InverseTransformPoint(hit.point).y -
                 right.radius) /
                right.suspensionDistance;
        }

        float antiRollForceValue =
            (leftTravel - rightTravel) * antiRollForce;

        if (leftGrounded)
        {
            rb.AddForceAtPosition(
                left.transform.up * -antiRollForceValue,
                left.transform.position
            );
        }

        if (rightGrounded)
        {
            rb.AddForceAtPosition(
                right.transform.up * antiRollForceValue,
                right.transform.position
            );
        }
    }

    private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
    {
        if (lfAngle < -360f) lfAngle += 360f;
        if (lfAngle > 360f) lfAngle -= 360f;
        return Mathf.Clamp(lfAngle, lfMin, lfMax);
    }

    // ============================================================
    // WHEELS
    // ============================================================

    private void UpdateWheelMeshes()
    {
        UpdateWheelMesh(
            frontLeftWheel,
            frontLeftMesh,
            leftWheelRotationOffset
        );

        UpdateWheelMesh(
            frontRightWheel,
            frontRightMesh,
            rightWheelRotationOffset
        );

        UpdateWheelMesh(
            rearLeftWheel,
            rearLeftMesh,
            leftWheelRotationOffset
        );

        UpdateWheelMesh(
            rearRightWheel,
            rearRightMesh,
            rightWheelRotationOffset
        );
    }

    private void UpdateWheelMesh(
    WheelCollider wheel,
    Transform mesh,
    Vector3 rotationOffset)
    {
        if (wheel == null || mesh == null)
            return;

        wheel.GetWorldPose(
            out Vector3 position,
            out Quaternion rotation
        );

        mesh.position = position;
        mesh.rotation = rotation * Quaternion.Euler(rotationOffset);
    }

    // ============================================================
    // ENGINE AUDIO
    // ============================================================

    private void UpdateEngineAudio()
    {
        if (engineAudio == null || rb == null)
            return;

        float speed = rb.linearVelocity.magnitude;

        float speedPercent = Mathf.Clamp01(
            speed / maxForwardSpeed
        );

        float targetPitch = Mathf.Lerp(
            idlePitch,
            maxPitch,
            speedPercent
        );

        float targetVolume = Mathf.Lerp(
            idleVolume,
            maxVolume,
            speedPercent
        );

        engineAudio.pitch = Mathf.Lerp(
            engineAudio.pitch,
            targetPitch,
            audioSmoothSpeed * Time.deltaTime
        );

        engineAudio.volume = Mathf.Lerp(
            engineAudio.volume,
            targetVolume,
            audioSmoothSpeed * Time.deltaTime
        );
    }

    // ============================================================
    // DUST
    // ============================================================

    private void UpdateDust()
    {
        UpdateWheelDust(frontLeftWheel, frontLeftDust);
        UpdateWheelDust(frontRightWheel, frontRightDust);
        UpdateWheelDust(rearLeftWheel, rearLeftDust);
        UpdateWheelDust(rearRightWheel, rearRightDust);
    }

    private void UpdateWheelDust(
        WheelCollider wheel,
        ParticleSystem dust)
    {
        if (wheel == null || dust == null)
            return;

        WheelHit hit;

        if (!wheel.GetGroundHit(out hit))
        {
            StopDust(dust);
            return;
        }

        float slip =
            Mathf.Max(
                Mathf.Abs(hit.forwardSlip),
                Mathf.Abs(hit.sidewaysSlip)
            );

        float speed = rb.linearVelocity.magnitude;

        bool shouldDust =
            speed >= dustMinimumSpeed &&
            slip >= dustSlipThreshold;

        if (shouldDust)
        {
            if (!dust.isPlaying)
                dust.Play();
        }
        else
        {
            StopDust(dust);
        }
    }

    private void StopDust(ParticleSystem dust)
    {
        if (dust.isPlaying)
            dust.Stop();
    }

    // ============================================================
    // PLAYER DETECTION
    // ============================================================
    public void SetPlayerNearby(Collider other, bool nearby)
    {
        if (playerTransform == null)
            return;

        if (other.transform.root != playerTransform.root)
            return;

        playerNearby = nearby;
    }

    // ============================================================
    // UTILITIES
    // ============================================================

    private float NormalizeAngle(float angle)
    {
        while (angle > 180f)
            angle -= 360f;

        while (angle < -180f)
            angle += 360f;

        return angle;
    }

    public bool IsDriving()
    {
        return driving;
    }

    public bool IsPlayerNearby()
    {
        return playerNearby;
    }
}