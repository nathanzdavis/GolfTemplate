using System;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using StarterAssets;
using System.Runtime.CompilerServices;

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

    [Header("Animation")]
    [SerializeField] private string inCartParameter = "InCart";

    [Header("Vehicle Player Collision")]
    [SerializeField] private Collider[] cartColliders;
    private Collider[] playerColliders;

    [Header("Cart")]
    [SerializeField] private Rigidbody rb;
    [SerializeField] private Transform seatPoint;
    [SerializeField] private Transform exitPoint;

    [Header("Camera")]
    [SerializeField] private CinemachineVirtualCamera normalCamera;
    [SerializeField] private Transform normalCameraTarget;
    [SerializeField] private Vector3 cartCameraShoulderOffset;

    [Tooltip("How far in degrees can you move the camera up")]
    public float TopClamp = 70.0f;

    [Tooltip("How far in degrees can you move the camera down")]
    public float BottomClamp = -30.0f;

    [Tooltip("Additional degress to override the camera. Useful for fine tuning camera position when locked")]
    public float CameraAngleOverride = 0.0f;

    [Tooltip("For locking the camera position on all axis")]
    public bool LockCameraPosition = false;

    private float _cinemachineTargetYaw;
    private float _cinemachineTargetPitch;
    private Vector3 originalShoulderOffset;
    private Vector3 previousCameraRotation;

    [Header("Input")]
    private PlayerInput _playerInput;
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference lookAction;
    [SerializeField] private InputActionReference interactAction;
    [SerializeField] private InputActionReference shootAction;
    private const float _threshold = 0.01f;

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

    [SerializeField] private float idleVolume = 0.2f;
    [SerializeField] private float maxVolume = 0.8f;

    [SerializeField] private float audioSmoothSpeed = 5f;

    [Header("Engine Fade")]
    [SerializeField] private float engineFadeSpeed = 2f;
    [SerializeField] private float engineStopSpeed = 0.1f;

    [Header("Horn")]
    [SerializeField] private AudioSource hornAudio;

    // ============================================================
    // DUST
    // ============================================================

    [Header("Wheel Dust")]
    [SerializeField] private float dustMinimumSpeed = 2f;
    [SerializeField] private float dustSlipThreshold = 0.25f;

    // ============================================================
    // LIGHTS
    // ============================================================

    [Header("Cart Lights")]
    [SerializeField] private Light[] brakeLights;

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

    private Transform previousPlayerParent;
    private Vector3 previousPlayerPosition;
    private Quaternion previousPlayerRotation;

    [HideInInspector]
    public bool IsCurrentDeviceMouse
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            return _playerInput.currentControlScheme == "KeyboardMouse";
#else
				return false;
#endif
        }
    }

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

        if (engineAudio != null)
        {
            engineAudio.loop = true;
            engineAudio.playOnAwake = false;
        }

        if (playerTransform != null)
        {
            playerColliders = playerTransform.GetComponentsInChildren<Collider>();
        }

        originalShoulderOffset = normalCamera.GetComponentInChildren<Cinemachine3rdPersonFollow>().ShoulderOffset;

        _playerInput = thirdPersonController.GetComponent<PlayerInput>();
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

        if (shootAction != null)
        {
            shootAction.action.Enable();
            shootAction.action.started += OnShoot;
        }
    }

    private void OnDisable()
    {
        if (interactAction != null)
            interactAction.action.started -= OnInteract;

        if (shootAction != null)
            shootAction.action.started -= OnShoot;

        if (moveAction != null)
            moveAction.action.Disable();

        if (lookAction != null)
            lookAction.action.Disable();

        if (interactAction != null)
            interactAction.action.Disable();

        if (shootAction != null)
            shootAction.action.Disable();
    }

    private void Update()
    {
        if (driving)
        {
            KeepPlayerInSeat();

            ReadInput();

            UpdateEngineAudio();
            UpdateDust();
        }
        else
        {
            UpdateExitedEngineAudio();
        }
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

    private void CameraRotation()
    {
        // if there is an input and camera position is not fixed
        if (lookInput.sqrMagnitude >= _threshold)
        {
            //Don't multiply mouse input by Time.deltaTime;
            float deltaTimeMultiplier = IsCurrentDeviceMouse ? 1.0f : Time.deltaTime;

            _cinemachineTargetYaw += lookInput.x * deltaTimeMultiplier;
            _cinemachineTargetPitch += lookInput.y * deltaTimeMultiplier;
        }

        // clamp our rotations so our values are limited 360 degrees
        _cinemachineTargetYaw = ClampAngle(_cinemachineTargetYaw, float.MinValue, float.MaxValue);
        _cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);

        // Cinemachine will follow this target
        normalCameraTarget.transform.rotation = Quaternion.Euler(_cinemachineTargetPitch + CameraAngleOverride,
            _cinemachineTargetYaw, 0.0f);
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
        if (driving)
        {
            Interact();
        }
    }

    public void Interact()
    {
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

    private void OnShoot(InputAction.CallbackContext context)
    {
        if (!context.started)
            return;

        if (!driving)
            return;

        PlayHorn();
    }

    private void PlayHorn()
    {
        if (hornAudio == null || hornAudio.clip == null)
            return;

        hornAudio.PlayOneShot(hornAudio.clip);
    }

    // ============================================================
    // ENTER / EXIT
    // ============================================================

    private void EnterCart()
    {
        if (playerTransform == null || seatPoint == null)
            return;

        if (playerAnimator != null)
            playerAnimator.SetBool(inCartParameter, true);

        previousCameraRotation = normalCameraTarget.eulerAngles;

        normalCamera.GetComponentInChildren<Cinemachine3rdPersonFollow>().ShoulderOffset = cartCameraShoulderOffset;

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
        {
            golfController.driving = true;
            golfController.enabled = false;
        }

        SetPlayerCollidersEnabled(false);

        SetBrakeLights(false);

        // ------------------------------------------------------------
        // REMOVE PLAYER FROM ANY PHYSICS HIERARCHY
        // ------------------------------------------------------------

        playerTransform.SetParent(null, true);

        // ------------------------------------------------------------
        // PLACE PLAYER IN SEAT
        // ------------------------------------------------------------

        playerTransform.position = seatPoint.position;
        playerTransform.rotation = seatPoint.rotation;

        normalCameraTarget.eulerAngles = previousCameraRotation;
        _cinemachineTargetYaw = previousCameraRotation.y;
        _cinemachineTargetPitch = previousCameraRotation.x;

        // ------------------------------------------------------------
        // ENTER CART
        // ------------------------------------------------------------

        driving = true;

        currentThrottle = 0f;
        currentSteer = 0f;

        moveInput = Vector2.zero;
        lookInput = Vector2.zero;

        // ------------------------------------------------------------
        // ENGINE
        // ------------------------------------------------------------

        if (engineAudio != null)
        {
            if (!engineAudio.isPlaying)
            {
                engineAudio.pitch = idlePitch;
                engineAudio.volume = 0f;
                engineAudio.Play();
            }
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

        if (playerAnimator != null)
            playerAnimator.SetBool(inCartParameter, false);

        previousCameraRotation = normalCameraTarget.eulerAngles;

        driving = false;

        StopCart();

        StopAllDust();
        SetBrakeLights(false);

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
        {
            golfController.driving = false;
            golfController.enabled = true;
        }

        normalCamera.GetComponentInChildren<Cinemachine3rdPersonFollow>().ShoulderOffset = originalShoulderOffset;
        normalCameraTarget.eulerAngles = previousCameraRotation;
        thirdPersonController.SetCameraAngles(previousCameraRotation.y, previousCameraRotation.x);

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

        bool isBraking = false;

        if (Mathf.Abs(throttle) < throttleDeadZone)
        {
            // Vehicle is coasting.
            ApplyBrakeTorque(rollingBrakeTorque);

            // Only show brake lights if the cart is actually moving.
            isBraking = Mathf.Abs(forwardSpeed) > 0.25f;
        }
        else if (Mathf.Abs(forwardSpeed) > 0.25f &&
                 Mathf.Sign(throttle) != Mathf.Sign(forwardSpeed))
        {
            // Player is pressing the opposite direction.
            ApplyBrakeTorque(brakeTorque);

            isBraking = true;
        }
        else
        {
            ApplyBrakeTorque(0f);
        }

        SetBrakeLights(isBraking);

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

        // ------------------------------------------------------------
        // PITCH
        // ------------------------------------------------------------

        float targetPitch = Mathf.Lerp(
            idlePitch,
            maxPitch,
            speedPercent
        );

        engineAudio.pitch = Mathf.Lerp(
            engineAudio.pitch,
            targetPitch,
            audioSmoothSpeed * Time.deltaTime
        );

        // ------------------------------------------------------------
        // VOLUME
        // ------------------------------------------------------------

        float targetVolume = Mathf.Lerp(
            idleVolume,
            maxVolume,
            speedPercent
        );

        engineAudio.volume = Mathf.Lerp(
            engineAudio.volume,
            targetVolume,
            audioSmoothSpeed * Time.deltaTime
        );
    }

    private void UpdateExitedEngineAudio()
    {
        if (engineAudio == null || rb == null)
            return;

        float speed = rb.linearVelocity.magnitude;

        // Cart is still rolling.
        if (speed > engineStopSpeed)
        {

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

            return;
        }

        // ------------------------------------------------------------
        // CART HAS STOPPED
        // ------------------------------------------------------------

        engineAudio.volume = Mathf.MoveTowards(
            engineAudio.volume,
            0f,
            engineFadeSpeed * Time.deltaTime
        );

        // Stop the AudioSource only AFTER it has
        // faded completely to zero.
        if (engineAudio.volume <= 0.001f)
        {
            engineAudio.volume = 0f;
            engineAudio.Stop();
        }
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

    private void SetBrakeLights(bool enabled)
    {
        if (brakeLights == null)
            return;

        foreach (Light brakeLight in brakeLights)
        {
            if (brakeLight != null)
                brakeLight.enabled = enabled;
        }
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

    private void StopAllDust()
    {
        StopDust(frontLeftDust);
        StopDust(frontRightDust);
        StopDust(rearLeftDust);
        StopDust(rearRightDust);
    }
}