using Codice.Client.Common.GameUI;
using UnityEngine;
using UnityEngine.InputSystem;
using System;


public class TankMovement : MonoBehaviour
{

    public Transform LeftDriveWheel;
    public Transform RightDriveWheel;

    [SerializeField] private float m_PitchRollDamping = 1.2f;

    [Header("Config")]
    [SerializeField] private TankConfig m_Config;

    [Header("Tracks")]
    [SerializeField] private TankTrack m_LeftTrack;
    [SerializeField] private TankTrack m_RightTrack;

    [SerializeField] private float m_LateralFriction = 0.85f;   // 0 = no kill, 1 = strong kill
    [SerializeField] private float m_LateralFrictionPerSecond = 8f; // try 6–15
    [SerializeField] private float m_MaxYawDegPerSec = 120f; // try 90–180
    [SerializeField] private float m_MaxForcePerTrack = 1200f; // start near your current feel


    [Header("Slope Grip")]
    [SerializeField] private LayerMask m_GroundMask = ~0;

    [SerializeField] private float m_HoldMaxAngleDeg = 18f;        // <= THIS is your “start slipping angle”
    [SerializeField] private float m_HoldSpeedThreshold = 0.25f;   // only hold when nearly stopped
    [SerializeField, Range(0f, 1f)] private float m_HoldStrength = 1.0f; // 1 = fully cancels downhill accel

    [SerializeField] private float m_SlipMaxAngleDeg = 40f;        // above this, tank will slide (more)
    [SerializeField, Range(0f, 1f)] private float m_SlipAssist = 0.35f;  // 0 = pure physics slide, 1 = strong assist

    [SerializeField] private float m_NoSlipAngleDeg = 8f;      // <= gentle ramps: never creep
    [SerializeField] private float m_MinGroundedRatio = 0.8f;  // require most arms grounded

    [Header("Slip Detect (for UI)")]
    [SerializeField] private float m_SlipUiMinDownhillSpeed = 0.35f; // m/s needed before we call it slipping
    [SerializeField] private float m_SlipUiOnDelay = 0.25f;          // seconds sustained
    [SerializeField] private float m_SlipUiOffDelay = 0.35f;

    public bool IsSlipping { get; private set; }
    public float LastSlopeAngleDeg { get; private set; }
    private float m_SlipTimer;



    [Header("Steering (MoveRotation)")]
    [SerializeField] private float m_TurnSpeedDegPerSec = 90f;     // try 60–140
    [SerializeField] private float m_TurnAtHighSpeed = 0.4f;       // like your friend
    [SerializeField] private bool m_AllowPivotTurn = true;
    private Rigidbody m_Rigidbody;
    [Header("Base Drive Values")]
    [SerializeField] private float m_maxTrackForce;
    [SerializeField] private float m_maxSpeed;
    [SerializeField] private float m_Speed;

    [Header("Tuning")]
    [SerializeField] private float m_Acceleration = 8f;
    [SerializeField] private float m_TurnSharpness = 1.0f;     // how strong differential steering is

    // The multipliers at high and low speed 
    [SerializeField] private float m_SteerAtLowSpeed = 1.0f;   
    [SerializeField] private float m_SteerAtHighSpeed = 0.4f;

    [SerializeField] private float m_TurnDrag = 2.5f;   // tweak 1–5

    [SerializeField] private float m_CoastingDrag = 0.5f;  // try 0.5–1.5

    private float m_Throttle = 0f;
    private float m_Steer = 0f;

    private float m_TargetSpeed;

    public float CurrentSpeed => Vector3.Dot(m_Rigidbody.linearVelocity, transform.forward);
    //public static event Action<Vector3,Vector3> onDriveWheelRaycast;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Awake()
    {
        m_Rigidbody = GetComponent<Rigidbody>();

        if (m_Config == null)
        {
            Debug.LogError($"{name}: TankConfig is not assigned.", this);
        }

        if (m_LeftTrack == null || m_RightTrack == null)
        {
            Debug.LogError($"{name}: Left/Right TankTrack references not assigned.", this);
        }
    }

    public void SetInput(float throttle, float steer)
    {
        m_Throttle = Mathf.Clamp(throttle, -1f, 1f);
        m_Steer = Mathf.Clamp(steer, -1f, 1f);
    }

    private void FixedUpdate()
    {
        if (m_Config == null || m_LeftTrack == null || m_RightTrack == null)
        {
            return;
        }

        UpdateDrive();
        ApplySteeringMoveRotation();   // NEW

        ApplySideFriction();
        ApplyForwardDrag();
        ApplyPitchRollDamping();
        ApplyTurnDamping();        // NEW
        LimitYawRate();
        ApplySlopeGripAssist();
        LimitForwardSpeed();
    }
    private void ApplySlopeGripAssist()
    {
        // Only assist when player isn't asking to move
        if (Mathf.Abs(m_Throttle) > 0.05f) { UpdateSlipState(false); return; }

        // Need decent contact, otherwise you get “air brakes”
        float traction = (m_LeftTrack.TractionFactor + m_RightTrack.TractionFactor) * 0.5f;
        if (traction < 0.5f) return;

        // Raycast down to find the supporting surface normal. [web:492]
        Vector3 origin = m_Rigidbody.worldCenterOfMass;
        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 3.0f, m_GroundMask, QueryTriggerInteraction.Ignore))
            return; // [web:492]

        // Slope angle from the ground normal. [web:504]
        float slopeAngle = Vector3.Angle(hit.normal, Vector3.up); // [web:504]

        // Gravity component along the slope plane. [web:496][web:481]
        Vector3 gravity = Physics.gravity; // [web:496]
        Vector3 gravityAlongSlope = Vector3.ProjectOnPlane(gravity, hit.normal); // [web:481]

        // If gravity is basically perpendicular to the plane, nothing to do.
        if (gravityAlongSlope.sqrMagnitude < 0.0001f) 
        {
            UpdateSlipState(false);
            return; 
        }
        LastSlopeAngleDeg = slopeAngle;


        Vector3 slopeDownDir = gravityAlongSlope.normalized; // [web:557]
        float downhillSpeed = Vector3.Dot(m_Rigidbody.linearVelocity, slopeDownDir); // [web:552]

        bool slippingNow =
            slopeAngle > m_HoldMaxAngleDeg &&
            downhillSpeed > m_SlipUiMinDownhillSpeed;

        // If you're in the "no slip" zone, never show slipping UI
        if (slopeAngle <= m_NoSlipAngleDeg)
            slippingNow = false;

        UpdateSlipState(slippingNow);
        // 1) Near-flat / mild slopes: “hill hold”
        if (slopeAngle <= m_NoSlipAngleDeg)
        {
            // Cancel gravity along slope fully (no traction scaling). [web:503][web:531]
            m_Rigidbody.AddForce(-gravityAlongSlope, ForceMode.Acceleration); // [web:503][web:531]

            // Remove along-slope velocity so it cannot accumulate. [web:525]
            Vector3 downDir = gravityAlongSlope.normalized;
            Vector3 v = m_Rigidbody.linearVelocity; // [web:525]
            float along = Vector3.Dot(v, downDir);
            m_Rigidbody.linearVelocity = v - downDir * along; // [web:525]
            return;
        }


        // 2) Steeper slopes: allow sliding, but optionally reduce it (tank has “grip”)
        // Blend assist between hold angle and slip max angle.
        float t = Mathf.InverseLerp(m_HoldMaxAngleDeg, m_SlipMaxAngleDeg, slopeAngle);
        float assist = Mathf.Lerp(0f, m_SlipAssist, t);

        m_Rigidbody.AddForce(-gravityAlongSlope * assist * traction, ForceMode.Acceleration); // [web:503][web:505]
    }

    private void UpdateSlipState(bool slippingNow)
    {
        if (slippingNow)
        {
            m_SlipTimer = Mathf.Min(m_SlipUiOnDelay, m_SlipTimer + Time.fixedDeltaTime); // [web:559]
            if (m_SlipTimer >= m_SlipUiOnDelay) IsSlipping = true;
        }
        else
        {
            m_SlipTimer = Mathf.Max(-m_SlipUiOffDelay, m_SlipTimer - Time.fixedDeltaTime); // [web:559]
            if (m_SlipTimer <= -m_SlipUiOffDelay) IsSlipping = false;
        }
    }
    private void LimitYawRate()
    {
        float maxYawRad = m_MaxYawDegPerSec * Mathf.Deg2Rad;

        Vector3 angVel = m_Rigidbody.angularVelocity;
        float yaw = Vector3.Dot(angVel, Vector3.up);
        yaw = Mathf.Clamp(yaw, -maxYawRad, maxYawRad);

        // keep pitch/roll unchanged
        Vector3 pr = angVel - Vector3.up * Vector3.Dot(angVel, Vector3.up);
        m_Rigidbody.angularVelocity = pr + Vector3.up * yaw;
    }
    private void ApplyPitchRollDamping()
    {
        // Only when not actively accelerating (so it doesn't feel like glue while driving)
        if (Mathf.Abs(m_Throttle) > 0.05f) return;

        Vector3 angVel = m_Rigidbody.angularVelocity;

        // Remove pitch (around tank right) and roll (around tank forward)
        Vector3 right = transform.right;
        Vector3 forward = transform.forward;

        float pitch = Vector3.Dot(angVel, right);
        float roll = Vector3.Dot(angVel, forward);

        float decay = Mathf.Exp(-m_PitchRollDamping * Time.fixedDeltaTime);

        pitch *= decay;
        roll *= decay;

        // Rebuild angular velocity: keep yaw as-is, damp pitch/roll
        Vector3 yawComponent = Vector3.Project(angVel, Vector3.up);
        m_Rigidbody.angularVelocity = yawComponent + right * pitch + forward * roll;
    }
    private void ApplyTurnDamping()
    {
        // only when you're not actively steering
        if (Mathf.Abs(m_Steer) > 0.05f && Mathf.Abs(m_Throttle) > 0.05f) return;


        Vector3 angVel = m_Rigidbody.angularVelocity;
        // kill yaw (around global up)
        float yaw = Vector3.Dot(angVel, Vector3.up);
        float decay = Mathf.Exp(-m_TurnDrag * Time.fixedDeltaTime);
        yaw *= decay;

        // rebuild angular velocity with damped yaw
        Vector3 lateral = angVel - Vector3.up * yaw;
        m_Rigidbody.angularVelocity = lateral + Vector3.up * yaw;
    }
    private void UpdateDrive()
    {
        float currentForward = Vector3.Dot(m_Rigidbody.linearVelocity, transform.forward);
        float desiredSpeed = m_Throttle * m_Config.maxSpeed;

        m_TargetSpeed = Mathf.MoveTowards(m_TargetSpeed, desiredSpeed, m_Acceleration * Time.fixedDeltaTime);
        float speedDelta = m_TargetSpeed - currentForward;

        Vector3 driveForce = transform.forward * (speedDelta * m_Config.maxTrackForce);

        ApplyForceToTrack(m_LeftTrack, driveForce);
        ApplyForceToTrack(m_RightTrack, driveForce);
    }

    private void ApplySideFriction()
    {
        // If both tracks are almost airborne, don't touch velocity.
        float avgTraction = (m_LeftTrack.TractionFactor + m_RightTrack.TractionFactor) * 0.5f;
        if (avgTraction < 0.05f)
            return;

        // Convert current velocity into the tank's local space.
        Vector3 localVel = transform.InverseTransformDirection(m_Rigidbody.linearVelocity);

        // x = sideways, z = forward in tank local space.
        float k = Mathf.Exp(-m_LateralFrictionPerSecond * avgTraction * Time.fixedDeltaTime);
        localVel.x *= k;

        // Convert back to world space.
        m_Rigidbody.linearVelocity = transform.TransformDirection(localVel);
    }
    private void ApplySteeringMoveRotation()
    {
        // If you don't allow pivot turns, require some throttle like your friend's script does.
        if (!m_AllowPivotTurn && Mathf.Abs(m_Throttle) < 0.05f)
            return;

        // Optional: also reduce steering if traction is low (prevents spinning in air)
        float traction = (m_LeftTrack.TractionFactor + m_RightTrack.TractionFactor) * 0.5f;
        if (traction < 0.05f)
            return;

        float forwardSpeed = Mathf.Abs(Vector3.Dot(m_Rigidbody.linearVelocity, transform.forward));
        float speed01 = Mathf.InverseLerp(0f, m_Config.maxSpeed, forwardSpeed);

        float steeringScale = Mathf.Lerp(1f, m_TurnAtHighSpeed, speed01);

        float turnDeg = m_Steer * m_TurnSpeedDegPerSec * steeringScale * traction * Time.fixedDeltaTime;

        Quaternion delta = Quaternion.Euler(0f, turnDeg, 0f);
        m_Rigidbody.MoveRotation(m_Rigidbody.rotation * delta);
    }

    private void ApplyForwardDrag()
    {
        // when no throttle, gently slow forward velocity
        if (Mathf.Abs(m_Throttle) > 0.05f)
            return;

        Vector3 vel = m_Rigidbody.linearVelocity;
        Vector3 forward = transform.forward;

        float forwardSpeed = Vector3.Dot(vel, forward);
        Vector3 lateral = vel - forward * forwardSpeed;

        // exponential decay towards zero
        float drag = Mathf.Exp(-m_CoastingDrag * Time.fixedDeltaTime);
        forwardSpeed *= drag;

        m_Rigidbody.linearVelocity = forward * forwardSpeed + lateral;
    }
    private void ApplyForceToTrack(TankTrack track, Vector3 totalForce)
    {
        float traction = track.TractionFactor;

        if (traction <= 0.01f)
        {
            return;
        }

        var drivePoints = track.DrivePoints;
        if (drivePoints == null || drivePoints.Count == 0)
        {
            return;
        }


        // Scale by traction
        Vector3 total = totalForce * traction;

        // Cap the magnitude so one frame can’t inject a huge force spike
        total = Vector3.ClampMagnitude(total, m_MaxForcePerTrack);

        Vector3 forcePerPoint = total / drivePoints.Count;

        foreach (var point in drivePoints)
        {
            m_Rigidbody.AddForceAtPosition(forcePerPoint, point.position, ForceMode.Force);
        }
        //// Scale by traction and mass so behaviour is mass independent-ish
        //Vector3 TotalForce = totalForce * traction;
        //Vector3 forcePerPoint = TotalForce / drivePoints.Count;

        //foreach (var point in drivePoints)
        //{
        //    m_Rigidbody.AddForceAtPosition(forcePerPoint, point.position, ForceMode.Force);
        //}
    }

    private void LimitForwardSpeed()
    {
        float forward = Vector3.Dot(m_Rigidbody.linearVelocity, transform.forward);
        float max = m_Config.maxSpeed;

        if (Mathf.Abs(forward) <= max)
        {
            return;
        }

        Vector3 lateral = m_Rigidbody.linearVelocity - transform.forward * forward;
        float clampedForwardSpeed = Mathf.Clamp(forward, -max, max);
        m_Rigidbody.linearVelocity = transform.forward * clampedForwardSpeed + lateral;
    }

}


