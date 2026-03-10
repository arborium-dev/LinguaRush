using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class Player : MonoBehaviour
{
    [Header("Engine")]
    [SerializeField] private float acceleration = 18f;
    [SerializeField] private float reverseAcceleration = 10f;
    [SerializeField] private float maxForwardSpeed = 12f;
    [SerializeField] private float maxReverseSpeed = 6f;
    [SerializeField] private float brakeStrength = 10f;

    [Header("Steering")]
    [SerializeField] private float steeringStrength = 320f; // deg/sec at full input
    [SerializeField] private float minSteerSpeed = 1.5f;
    [SerializeField] private float steeringAtHighSpeed = 0.55f;
    [SerializeField] private float lateralGrip = 20f; // higher = snaps back faster
    [SerializeField] private float driftGrip = 2.5f;  // lower = holds slide longer
    [SerializeField] private float driftSteerBoost = 1.2f;
    [SerializeField] private float driftStartSpeed = 4f;
    [SerializeField] private float driftSidewaysThreshold = 1.2f;
    [SerializeField] private float driftYawAssist = 70f;
    [SerializeField] private float driftSideForce = 10f;

    [Header("Stability")]
    [SerializeField] private float linearDrag = 1f;

    private Rigidbody2D rb;
    private float throttleInput; // -1..1
    private float steerInput;    // -1..1
    private bool brakeInput;
    private float driftFactor;

    private InputAction _moveAction;
    private InputAction _brakeAction;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.linearDamping = linearDrag;
        rb.angularDamping = 4f;
        
        _moveAction = InputSystem.actions.FindAction("Move");
        _brakeAction = InputSystem.actions.FindAction("Brake");
    }

    private void Update()
    {
        // Temporary classic input; swap to Input System later.
        // throttleInput = Input.GetAxisRaw("Vertical");
        // steerInput = Input.GetAxisRaw("Horizontal");
        // brakeInput = Input.GetKey(KeyCode.Space);
        throttleInput = _moveAction.ReadValue<Vector2>().y;
        steerInput = _moveAction.ReadValue<Vector2>().x;
        brakeInput = _brakeAction.IsPressed();
    }

    private void FixedUpdate()
    {
        ApplyEngineForce();
        ApplySteering();
        ApplyLateralGrip();
    }

    private void ApplyEngineForce()
    {
        Vector2 up = transform.up;
        float speedAlongForward = Vector2.Dot(rb.linearVelocity, up);

        if (brakeInput)
        {
            // Handbrake behavior: bleed forward speed but keep lateral slide for drifting.
            Vector2 forwardVelocity = up * speedAlongForward;
            Vector2 lateralVelocity = rb.linearVelocity - forwardVelocity;
            forwardVelocity = Vector2.MoveTowards(
                forwardVelocity, Vector2.zero, brakeStrength * Time.fixedDeltaTime);
            rb.linearVelocity = forwardVelocity + lateralVelocity;

            // Small sideways force helps break traction so drifting is noticeable.
            if (Mathf.Abs(steerInput) > 0.05f && Mathf.Abs(speedAlongForward) > driftStartSpeed)
            {
                rb.AddForce((Vector2)transform.right * (steerInput * driftSideForce), ForceMode2D.Force);
            }

            return;
        }

        float accel = throttleInput >= 0f ? acceleration : reverseAcceleration;
        rb.AddForce(up * (throttleInput * accel), ForceMode2D.Force);

        // Clamp forward/reverse speed along facing direction
        float clamped = Mathf.Clamp(speedAlongForward, -maxReverseSpeed, maxForwardSpeed);
        Vector2 forwardVel = up * clamped;
        Vector2 lateralVel = rb.linearVelocity - (up * speedAlongForward);
        rb.linearVelocity = forwardVel + lateralVel;
    }

    private void ApplySteering()
    {
        float forwardSpeed = Vector2.Dot(rb.linearVelocity, transform.up);
        float absForwardSpeed = Mathf.Abs(forwardSpeed);
        float speedFactor = Mathf.Clamp01((absForwardSpeed - minSteerSpeed) / Mathf.Max(0.01f, maxForwardSpeed - minSteerSpeed));

        // Less twitchy at high speed, full control at low speed.
        float steerBySpeed = Mathf.Lerp(1f, steeringAtHighSpeed, speedFactor);

        // Reverse steering direction when traveling backward.
        float reverseSteer = forwardSpeed < -0.1f ? -1f : 1f;
        float driftMultiplier = Mathf.Lerp(1f, driftSteerBoost, driftFactor);

        float steerAmount = -steerInput * reverseSteer * steeringStrength * steerBySpeed * driftMultiplier * Time.fixedDeltaTime;

        if (brakeInput && absForwardSpeed > driftStartSpeed)
        {
            steerAmount += -steerInput * driftYawAssist * Time.fixedDeltaTime;
        }

        rb.MoveRotation(rb.rotation + steerAmount);
    }

    private void ApplyLateralGrip()
    {
        Vector2 up = transform.up;
        Vector2 right = transform.right;

        float forward = Vector2.Dot(rb.linearVelocity, up);
        float sideways = Vector2.Dot(rb.linearVelocity, right);

        bool driftActive = brakeInput && Mathf.Abs(forward) > driftStartSpeed && Mathf.Abs(sideways) > driftSidewaysThreshold;
        float targetDrift = driftActive ? 1f : 0f;
        driftFactor = Mathf.MoveTowards(driftFactor, targetDrift, 4f * Time.fixedDeltaTime);

        // Exponential damping keeps grip behavior consistent if Fixed Timestep changes.
        float gripRate = Mathf.Lerp(lateralGrip, driftGrip, driftFactor);
        float sideMultiplier = Mathf.Exp(-gripRate * Time.fixedDeltaTime);
        sideways *= sideMultiplier;

        rb.linearVelocity = up * forward + right * sideways;
    }
}
