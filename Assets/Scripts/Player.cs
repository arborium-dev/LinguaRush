using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

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

    [Header("Surface Penalties")]
    [SerializeField] private string notDrivableTag = "notDrivable";
    [SerializeField] private float notDrivableSpeedCap = 2.5f;
    [SerializeField] private float notDrivableEntrySpeedMultiplier = 0.5f;

    [Header("Lap Checkpoints")]
    [SerializeField] private GameObject halfwayCheckpoint;
    [SerializeField] private GameObject finishlineCheckpoint;

    [Header("Race Timer")]
    [SerializeField] private TMP_Text raceTimerText;

    private Rigidbody2D rb;
    private float throttleInput; // -1..1
    private float steerInput;    // -1..1
    private bool brakeInput;
    private float driftFactor;

    private InputAction _moveAction;
    private InputAction _brakeAction;
    private int _notDrivableContactCount;
    private bool _halfwayReachedThisLap;
    private bool _raceTimerStarted;
    private bool _firstLapFinished;
    private float _raceElapsedSeconds;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.linearDamping = linearDrag;
        rb.angularDamping = 4f;
        
        _moveAction = InputSystem.actions.FindAction("Move");
        _brakeAction = InputSystem.actions.FindAction("Brake");

        if (halfwayCheckpoint == null || finishlineCheckpoint == null)
        {
            Debug.LogWarning("Checkpoint GameObjects are not assigned on Player.", this);
        }

        if (raceTimerText == null)
        {
            Debug.LogWarning("Race timer text is not assigned on Player.", this);
        }

        UpdateRaceTimerText();
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

        TryStartRaceTimer();
        TickRaceTimer();
    }

    private void FixedUpdate()
    {
        ApplyEngineForce();
        ApplySteering();
        ApplyLateralGrip();
        ApplySurfaceSpeedLimit();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        UpdateNotDrivableContact(other, true);
        HandleCheckpointTrigger(other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        UpdateNotDrivableContact(other, false);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        UpdateNotDrivableContact(collision.collider, true);
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        UpdateNotDrivableContact(collision.collider, false);
    }

    private void HandleCheckpointTrigger(Collider2D other)
    {
        if (other == null)
        {
            return;
        }

        if (MatchesCheckpoint(other, halfwayCheckpoint))
        {
            _halfwayReachedThisLap = true;
            Debug.Log("Halfway reached.", this);
            return;
        }

        if (!MatchesCheckpoint(other, finishlineCheckpoint))
        {
            return;
        }

        bool goalIsValid = _halfwayReachedThisLap;
        Debug.Log(goalIsValid
            ? "Goal reached: VALID (halfway was hit first)."
            : "Goal reached: INVALID (halfway was not hit yet).", this);

        if (goalIsValid)
        {
            // Reset for the next lap cycle after a valid goal crossing.
            _halfwayReachedThisLap = false;
            StopTimerOnFirstLapFinish();
        }
    }

    private static bool MatchesCheckpoint(Collider2D other, GameObject checkpoint)
    {
        if (other == null || checkpoint == null)
        {
            return false;
        }

        Transform otherTransform = other.transform;
        Transform checkpointTransform = checkpoint.transform;

        return other.gameObject == checkpoint
            || otherTransform.IsChildOf(checkpointTransform)
            || checkpointTransform.IsChildOf(otherTransform);
    }

    private void TryStartRaceTimer()
    {
        if (_raceTimerStarted || _firstLapFinished)
        {
            return;
        }

        bool hasInput = Mathf.Abs(throttleInput) > 0.01f
            || Mathf.Abs(steerInput) > 0.01f
            || brakeInput;

        if (!hasInput)
        {
            return;
        }

        _raceTimerStarted = true;
    }

    private void TickRaceTimer()
    {
        if (!_raceTimerStarted || _firstLapFinished)
        {
            return;
        }

        _raceElapsedSeconds += Time.deltaTime;
        UpdateRaceTimerText();
    }

    private void StopTimerOnFirstLapFinish()
    {
        if (_firstLapFinished)
        {
            return;
        }

        _firstLapFinished = true;
        UpdateRaceTimerText();
        Debug.Log($"First lap time: {FormatRaceTime(_raceElapsedSeconds)}", this);
    }

    private void UpdateRaceTimerText()
    {
        if (raceTimerText == null)
        {
            return;
        }

        raceTimerText.text = FormatRaceTime(_raceElapsedSeconds);
    }

    private static string FormatRaceTime(float seconds)
    {
        float clamped = Mathf.Max(0f, seconds);
        int totalMilliseconds = Mathf.FloorToInt(clamped * 1000f);
        int minutes = totalMilliseconds / 60000;
        int secondsPart = (totalMilliseconds / 1000) % 60;
        int milliseconds = totalMilliseconds % 1000;
        return $"{minutes:00}:{secondsPart:00}:{milliseconds:000}";
    }

    private void OnDisable()
    {
        _notDrivableContactCount = 0;
        _halfwayReachedThisLap = false;
        _raceTimerStarted = false;
        _firstLapFinished = false;
        _raceElapsedSeconds = 0f;
    }

    private void UpdateNotDrivableContact(Collider2D other, bool entered)
    {
        if (other == null || !other.CompareTag(notDrivableTag))
        {
            return;
        }

        if (entered)
        {
            bool wasOnNotDrivable = _notDrivableContactCount > 0;
            _notDrivableContactCount++;

            if (!wasOnNotDrivable)
            {
                float speedMultiplier = Mathf.Clamp01(notDrivableEntrySpeedMultiplier);
                rb.linearVelocity *= speedMultiplier;
            }

            return;
        }

        _notDrivableContactCount = Mathf.Max(0, _notDrivableContactCount - 1);
    }

    private void ApplyEngineForce()
    {
        Vector2 up = transform.up;
        float speedAlongForward = Vector2.Dot(rb.linearVelocity, up);
        bool onNotDrivable = _notDrivableContactCount > 0;

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

        float activeForwardCap = onNotDrivable ? Mathf.Min(maxForwardSpeed, notDrivableSpeedCap) : maxForwardSpeed;
        float activeReverseCap = onNotDrivable ? Mathf.Min(maxReverseSpeed, notDrivableSpeedCap) : maxReverseSpeed;

        // Clamp forward/reverse speed along facing direction
        float clamped = Mathf.Clamp(speedAlongForward, -activeReverseCap, activeForwardCap);
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

    private void ApplySurfaceSpeedLimit()
    {
        if (_notDrivableContactCount <= 0)
        {
            return;
        }

        // Clamp total velocity so sideways drift cannot bypass the off-road speed cap.
        float speedCap = Mathf.Max(0f, notDrivableSpeedCap);
        rb.linearVelocity = Vector2.ClampMagnitude(rb.linearVelocity, speedCap);
    }
}
