using UnityEngine;
using UnityEngine.InputSystem;

public class GolfController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CharacterController characterController;
    [SerializeField] private Animator animator;
    [SerializeField] private Transform cameraTransform;

    [Header("Input")]
    [SerializeField] private InputActionReference prepareShotAction;
    [SerializeField] private InputActionReference shootAction;

    [Header("Golf Ball")]
    [SerializeField] private LayerMask ballLayer;
    [SerializeField] private float ballDetectionRadius = 1.0f;

    [Header("Shot")]
    [SerializeField] private float maxShotForce = 20f;
    [SerializeField] private float shotAngle = 45f;
    [SerializeField] private float chargeSpeed = 20f;

    [Header("Golf Camera")]
    [SerializeField] private float golfCameraDistance = 4f;
    [SerializeField] private float golfCameraHeight = 1.6f;
    [SerializeField] private float golfCameraSmoothness = 15f;

    [Header("Prepare Setup")]
    [SerializeField] private float prepareMoveSpeed = 3.5f;
    [SerializeField] private float prepareRotationSpeed = 8f;
    [SerializeField] private float prepareCameraPitch = 15f;
    [SerializeField] private float prepareCameraLerpSpeed = 5f;

    private Vector3 golfBallCenter;
    private float orbitAngle;
    private float orbitRadius;
    private Quaternion originalCameraRotation;

    // ============================================================
    // SWING SETTINGS
    // ============================================================

    [Header("Swing Animation")]
    [SerializeField] private float downSwingSpeed = 3.0f;
    [SerializeField] private float hitFrame = 35f;

    private bool swingPlaying;
    private bool ballHit;
    private float swingFrame;

    private const float SwingTopFrame = 26f;
    private const float SwingTotalFrames = 60f;

    private bool swingReleased;

    [Header("Shot Positioning")]
    [SerializeField] private float orbitSpeed = 2.0f;
    [SerializeField] private float playerBallDistance = 0.8f;
    [SerializeField] private float maxPrepareDistance = 1.25f;

    [Header("Animator Parameters")]
    [SerializeField] private string preparingParameter = "Preparing";
    [SerializeField] private string chargeParameter = "Charge";

    private Rigidbody golfBall;
    private bool preparingShot;
    private float chargeAmount;
    private static readonly int SwingState = Animator.StringToHash("GolfSwing");
    private Vector3 shotBallPosition;
    private StarterAssets.StarterAssetsInputs starterInputs;
    [SerializeField] private float preparePositionLerpSpeed = 8f;

    private bool movingToPreparePosition;
    private Vector3 prepareTargetPosition;

    private void Awake()
    {
        if (characterController == null)
            characterController = GetComponent<CharacterController>();

        starterInputs = GetComponent<StarterAssets.StarterAssetsInputs>();
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
        if (!preparingShot)
            return;

        // Completely disable normal character movement.
        if (starterInputs != null)
        {
            starterInputs.MoveInput(Vector2.zero);
        }

        // Remove vertical camera input.
        LockVerticalCameraInput();

        UpdateBallTarget();

        if (golfBall == null)
            return;

        if (movingToPreparePosition)
        {
            MoveToPreparePosition();
            return;
        }

        OrbitBall();
        UpdateCharge();
    }

    private void LockVerticalCameraInput()
    {
        if (starterInputs == null)
            return;

        Vector2 look = starterInputs.look;

        // Allow horizontal camera movement.
        // Completely disable vertical camera movement.
        look.y = 0f;

        starterInputs.LookInput(look);
    }

    private void MoveToPreparePosition()
    {
        Vector3 currentPosition = transform.position;

        Vector3 targetPosition = prepareTargetPosition;
        targetPosition.y = currentPosition.y;

        // Move at a constant speed instead of asymptotic Lerp.
        Vector3 newPosition = Vector3.MoveTowards(
            currentPosition,
            targetPosition,
            prepareMoveSpeed * Time.deltaTime
        );

        Vector3 movement = newPosition - currentPosition;
        movement.y = 0f;

        characterController.Move(movement);

        // Smoothly face the ball.
        Vector3 lookDirection = golfBallCenter - transform.position;
        lookDirection.y = 0f;

        if (lookDirection.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation =
                Quaternion.LookRotation(lookDirection);

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                prepareRotationSpeed * Time.deltaTime
            );
        }

        // Stop once we reach the target.
        if (Vector3.Distance(transform.position, targetPosition) <= 0.01f)
        {
            movingToPreparePosition = false;

            characterController.Move(
                targetPosition - transform.position
            );

            FaceBallImmediately();
        }
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

        // Current distance from player to ball.
        Vector3 offset =
            transform.position - golfBallCenter;

        offset.y = 0f;

        orbitRadius = offset.magnitude;

        if (orbitRadius < 0.1f)
        {
            preparingShot = false;
            golfBall = null;
            return;
        }

        // --------------------------------------------------------
        // MOVE PLAYER TO CAMERA'S LEFT SIDE OF BALL
        // --------------------------------------------------------

        Vector3 cameraLeft = -cameraTransform.right;

        cameraLeft.y = 0f;
        cameraLeft.Normalize();

        Vector3 targetPosition =
            golfBallCenter +
            cameraLeft * orbitRadius;

        // Temporarily disable CharacterController so we can
        // reposition without it interfering.
        prepareTargetPosition = targetPosition;
        movingToPreparePosition = true;

        // Recalculate orbit angle.
        Vector3 newOffset =
            transform.position - golfBallCenter;

        newOffset.y = 0f;

        orbitAngle = Mathf.Atan2(
            newOffset.z,
            newOffset.x
        );

        animator.SetBool(
            preparingParameter,
            true
        );

        if (starterInputs != null)
        {
            starterInputs.MoveInput(Vector2.zero);
            starterInputs.LookInput(Vector2.zero);
        }

        UpdateGolfCamera(true);
    }

    private void PrepareCanceled(InputAction.CallbackContext context)
    {
        preparingShot = false;

        chargeAmount = 0f;

        animator.SetBool(preparingParameter, false);
        animator.SetFloat(chargeParameter, 0f);

        if (starterInputs != null)
        {
            starterInputs.MoveInput(Vector2.zero);
            starterInputs.LookInput(Vector2.zero);
        }

        golfBall = null;
    }

    // ============================================================
    // FIND BALL
    // ============================================================

    private void FindGolfBall()
    {
        Collider[] hits = Physics.OverlapSphere(
            transform.position,
            ballDetectionRadius,
            ballLayer
        );

        float closestDistance = float.MaxValue;
        Rigidbody closestBall = null;

        foreach (Collider hit in hits)
        {
            Rigidbody rb = hit.attachedRigidbody;

            if (rb == null)
                continue;

            float distance = Vector3.Distance(
                transform.position,
                rb.position
            );

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestBall = rb;
            }
        }

        golfBall = closestBall;
    }

    private void UpdateBallTarget()
    {
        if (golfBall == null)
        {
            FindGolfBall();
            return;
        }

        float distance = Vector3.Distance(
            transform.position,
            golfBall.position
        );

        if (distance > maxPrepareDistance)
        {
            golfBall = null;
            preparingShot = false;

            animator.SetBool(preparingParameter, false);
        }
    }

    // ============================================================
    // CAMERA
    // ============================================================

    private void UpdateGolfCamera(bool instant = false)
    {
        if (cameraTransform == null)
            return;

        // Camera should be directly behind the player.
        Vector3 behindPlayer =
            -transform.forward;

        Vector3 targetPosition =
            transform.position +
            behindPlayer * golfCameraDistance;

        targetPosition.y += golfCameraHeight;

        if (instant)
        {
            cameraTransform.position =
                targetPosition;

            cameraTransform.rotation =
                Quaternion.LookRotation(
                    transform.position +
                    Vector3.up * 1.2f -
                    cameraTransform.position
                );

            return;
        }

        cameraTransform.position =
            Vector3.Lerp(
                cameraTransform.position,
                targetPosition,
                golfCameraSmoothness * Time.deltaTime
            );

        Vector3 lookTarget =
            transform.position +
            Vector3.up * 1.2f;

        Quaternion targetRotation =
            Quaternion.LookRotation(
                lookTarget -
                cameraTransform.position
            );

        cameraTransform.rotation =
            Quaternion.Slerp(
                cameraTransform.rotation,
                targetRotation,
                golfCameraSmoothness * Time.deltaTime
            );
    }

    // ============================================================
    // ORBIT BALL
    // ============================================================

    private void OrbitBall()
    {
        if (golfBall == null || starterInputs == null)
            return;

        float horizontalLook = starterInputs.look.x;

        if (Mathf.Abs(horizontalLook) < 0.001f)
        {
            // Still keep the player exactly on the orbit.
            LockPlayerToOrbit();
            return;
        }

        // Rotate around the ball.
        orbitAngle +=
            horizontalLook *
            orbitSpeed *
            Time.deltaTime;

        LockPlayerToOrbit();
    }

    private void FaceBallImmediately()
    {
        Vector3 direction =
            golfBallCenter -
            transform.position;

        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
            return;

        transform.rotation =
            Quaternion.LookRotation(direction);
    }

    private void LockPlayerToOrbit()
    {
        Vector3 orbitOffset = new Vector3(
            Mathf.Cos(orbitAngle),
            0f,
            Mathf.Sin(orbitAngle)
        );

        Vector3 targetPosition =
            golfBallCenter +
            orbitOffset * orbitRadius;

        // CharacterController movement.
        Vector3 movement =
            targetPosition -
            transform.position;

        movement.y = 0f;

        characterController.Move(movement);

        // Force the player to face the ball.
        FaceBallImmediately();
    }

    // ============================================================
    // CHARGE
    // ============================================================

    private void UpdateCharge()
    {
        // --------------------------------------------------------
        // HOLDING SHOOT = CHARGE
        // --------------------------------------------------------

        if (shootAction.action.IsPressed() && !swingReleased)
        {
            chargeAmount += chargeSpeed * Time.deltaTime;
            chargeAmount = Mathf.Clamp(chargeAmount, 0f, 100f);

            animator.SetFloat(
                chargeParameter,
                chargeAmount
            );

            Debug.Log($"Golf Charge: {chargeAmount:F1}%");

            // Charge controls frames 0 -> 26.
            float normalizedTime =
                (chargeAmount / 100f) *
                (SwingTopFrame / SwingTotalFrames);

            animator.Play(
                SwingState,
                1,
                normalizedTime
            );

            animator.Update(0f);

            // Keep track of the current animation frame.
            swingFrame = chargeAmount / 100f * SwingTopFrame;

            return;
        }

        // --------------------------------------------------------
        // RELEASED = PLAY DOWNSWING
        // --------------------------------------------------------

        if (swingPlaying)
        {
            UpdateDownSwing();
        }
    }

    // ============================================================
    // START SWING
    // ============================================================

    private void ShootStarted(InputAction.CallbackContext context)
    {
        if (!preparingShot)
            return;

        if (golfBall == null)
            return;

        chargeAmount = 0f;
        swingReleased = false;
        swingPlaying = false;
        ballHit = false;
        swingFrame = 0f;

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
    // RELEASE SHOOT
    // ============================================================

    private void ShootCanceled(InputAction.CallbackContext context)
    {
        if (!preparingShot)
            return;

        if (golfBall == null)
            return;

        if (chargeAmount <= 0f)
            return;

        swingReleased = true;
        swingPlaying = true;
        ballHit = false;

        // Current frame is whatever the player charged to.
        swingFrame =
            (chargeAmount / 100f) *
            SwingTopFrame;

        Debug.Log(
            $"Swing released at frame {swingFrame:F1}"
        );
    }

    // ============================================================
    // PLAY DOWNSWING
    // ============================================================

    private void UpdateDownSwing()
    {
        // Advance the animation.
        //
        // downSwingSpeed = 1 means normal speed.
        // downSwingSpeed = 3 means 3x speed.
        // downSwingSpeed = 5 means 5x speed.
        swingFrame +=
            downSwingSpeed *
            Time.deltaTime *
            (SwingTotalFrames / 1f);

        // --------------------------------------------------------
        // HIT THE BALL AT FRAME 35
        // --------------------------------------------------------

        if (!ballHit && swingFrame >= hitFrame)
        {
            ballHit = true;

            HitGolfBall();

            Debug.Log(
                $"GOLF BALL HIT AT FRAME {swingFrame:F1}"
            );
        }

        // --------------------------------------------------------
        // PLAY ANIMATION
        // --------------------------------------------------------

        float normalizedTime =
            swingFrame / SwingTotalFrames;

        normalizedTime =
            Mathf.Clamp01(normalizedTime);

        animator.Play(
            SwingState,
            1,
            normalizedTime
        );

        animator.Update(0f);

        // --------------------------------------------------------
        // SWING FINISHED
        // --------------------------------------------------------

        if (swingFrame >= SwingTotalFrames)
        {
            swingFrame = SwingTotalFrames;

            swingPlaying = false;
            swingReleased = false;

            animator.Play(
                SwingState,
                1,
                1f
            );

            animator.Update(0f);

            chargeAmount = 0f;

            animator.SetFloat(
                chargeParameter,
                0f
            );
        }
    }

    // ============================================================
    // HIT BALL
    // ============================================================

    private void HitGolfBall()
    {
        if (golfBall == null)
            return;

        // Camera's horizontal facing direction.
        Vector3 forward = cameraTransform.forward;

        forward.y = 0f;

        forward.Normalize();

        // Convert the horizontal direction into a 45 degree shot.
        float angle = shotAngle * Mathf.Deg2Rad;

        Vector3 shotDirection =
            forward * Mathf.Cos(angle) +
            Vector3.up * Mathf.Sin(angle);

        shotDirection.Normalize();

        // Convert 0-100 charge into 0-1.
        float chargeMultiplier = chargeAmount / 100f;

        float force = maxShotForce * chargeMultiplier;

        golfBall.AddForce(
            shotDirection * force,
            ForceMode.Impulse
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