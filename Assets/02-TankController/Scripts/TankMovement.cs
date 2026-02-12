using UnityEngine;
using System;

/// <summary>
/// Physics based tank movement controller
/// - Converts throttle into a target forward speed and applies drive force through both tracks (scaled by traction)
/// - Uses differential steering (MoveRotation) with speed/traction scaling, plus yaw/pitch/roll damping for stability
/// - Applies lateral friction to reduce sideways sliding (scaled by traction)
/// - Adds "slope grip" logic: no-slip on gentle slopes, reduced sliding on steeper slopes
/// - Detects sustained downhill sliding ("slipping") for UI feedback with debounce (on/off delays)
/// </summary>
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

    [SerializeField] private float m_LateralFrictionPerSecond = 8f; 
    [SerializeField] private float m_MaxYawDegPerSec = 120f; 
    [SerializeField] private float m_MaxForcePerTrack = 1200f; 


    [Header("Slope Grip")]
    [SerializeField] private LayerMask m_GroundMask = ~0;

    // Above this angle, sliding is allowed (and can optionally reduce it using m_SlipAssist)
    [SerializeField] private float m_HoldMaxAngleDeg = 18f;       
    
    [SerializeField] private float m_SlipMaxAngleDeg = 40f;        // above this, tank will slide (more)
    [SerializeField, Range(0f, 1f)] private float m_SlipAssist = 0.35f;  // 0 = pure physics slide, 1 = strong assist

    // When slopeAngle <= this, tank should not creep downhill at all
    [SerializeField] private float m_NoSlipAngleDeg = 8f;      

    [Header("Slip Detect (for UI)")]
    [SerializeField] private float m_SlipUiMinDownhillSpeed = 0.35f; // m/s needed before we can call it slipping
    [SerializeField] private float m_SlipUiOnDelay = 0.25f;          // seconds sustained
    [SerializeField] private float m_SlipUiOffDelay = 0.35f;

    public bool IsSlipping { get; private set; }
    public float LastSlopeAngleDeg { get; private set; }
    private float m_SlipTimer;

    private Rigidbody m_Rigidbody;


    [Header("Steering (MoveRotation)")]
    [SerializeField] private float m_TurnSpeedDegPerSec = 90f;    
    [SerializeField] private float m_TurnAtHighSpeed = 0.4f;       
    [SerializeField] private bool m_AllowPivotTurn = true;


    [Header("Tuning")]
    [SerializeField] private float m_Acceleration = 8f;
    [SerializeField] private float m_TurnDrag = 2.5f;  
    [SerializeField] private float m_CoastingDrag = 0.5f;  

    private float m_Throttle = 0f;
    private float m_Steer = 0f;

    // Smoothed speed target so acceleration feels controlled rather than instantly snapping
    private float m_TargetSpeed;

    public float CurrentSpeed => Vector3.Dot(m_Rigidbody.linearVelocity, transform.forward);


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
        ApplySteeringMoveRotation();   

        ApplySideFriction();
        ApplyForwardDrag();
        ApplyPitchRollDamping();
        ApplyTurnDamping();       
        LimitYawRate();
        ApplySlopeGripAssist();

        // Clamp last so no other step pushes speed beyond max afterwards
        LimitForwardSpeed();
    }
    private void ApplySlopeGripAssist()
    {
        // If player is actively driving, slipping is false (so UI doesn’t stick on)
        if (Mathf.Abs(m_Throttle) > 0.05f) 
        { 
            UpdateSlipState(false); return;
        }

        // Traction is used as a "ground contact confidence" here
        float traction = (m_LeftTrack.TractionFactor + m_RightTrack.TractionFactor) * 0.5f;
        if (traction < 0.5f)
        {
            return;
        }

        // Raycast down to find the ground normal underneath the tank
        Vector3 origin = m_Rigidbody.worldCenterOfMass;
        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 3.0f, m_GroundMask, QueryTriggerInteraction.Ignore))
        {
            return;

        }

        // Angle between the ground normal and "up", 0 = flat ground, higher = steeper slope
        float slopeAngle = Vector3.Angle(hit.normal, Vector3.up);

        // Project gravity onto the slope plane to get the downhill acceleration direction / magnitude
        Vector3 gravity = Physics.gravity;
        Vector3 gravityAlongSlope = Vector3.ProjectOnPlane(gravity, hit.normal);

        // Flat ground leads to no downhill component
        if (gravityAlongSlope.sqrMagnitude < 0.0001f) 
        {
            UpdateSlipState(false);
            return; 
        }
        LastSlopeAngleDeg = slopeAngle;


        Vector3 slopeDownDir = gravityAlongSlope.normalized;

        // Positive when moving downhill, negative when moving uphill.
        float downhillSpeed = Vector3.Dot(m_Rigidbody.linearVelocity, slopeDownDir); 

        bool slippingNow =   slopeAngle > m_HoldMaxAngleDeg &&  downhillSpeed > m_SlipUiMinDownhillSpeed;

        // Never show slipping UI in the no slip zone
        if (slopeAngle <= m_NoSlipAngleDeg)
        {
            slippingNow = false;
        }

        UpdateSlipState(slippingNow);

        if (slopeAngle <= m_NoSlipAngleDeg)
        {
            m_Rigidbody.AddForce(-gravityAlongSlope, ForceMode.Acceleration); 

            // Remove along slope velocity so it cannot accumulate, canceling downhill acceleration
            Vector3 downDir = gravityAlongSlope.normalized;
            Vector3 v = m_Rigidbody.linearVelocity; 
            float along = Vector3.Dot(v, downDir);
            m_Rigidbody.linearVelocity = v - downDir * along; 
            return;
        }


        // Steeper slopes allow sliding
        // Blend assist between hold angle and slip max angle
        float t = Mathf.InverseLerp(m_HoldMaxAngleDeg, m_SlipMaxAngleDeg, slopeAngle);
        float assist = Mathf.Lerp(0f, m_SlipAssist, t);

        m_Rigidbody.AddForce(-gravityAlongSlope * assist * traction, ForceMode.Acceleration); 
    }

    private void UpdateSlipState(bool slippingNow)
    {
        if (slippingNow)
        {
            m_SlipTimer = Mathf.Min(m_SlipUiOnDelay, m_SlipTimer + Time.fixedDeltaTime); 
            if (m_SlipTimer >= m_SlipUiOnDelay)
            {
                IsSlipping = true;
            }
        }
        else
        {
            m_SlipTimer = Mathf.Max(-m_SlipUiOffDelay, m_SlipTimer - Time.fixedDeltaTime); 
            if (m_SlipTimer <= -m_SlipUiOffDelay)
            {
                IsSlipping = false;
            }
        }
    }
    private void LimitYawRate()
    {
        // Clamp only the yaw component of angular velocity while leaving pitch / roll unchnaged
        float maxYawRad = m_MaxYawDegPerSec * Mathf.Deg2Rad;

        Vector3 angVel = m_Rigidbody.angularVelocity;
        float yaw = Vector3.Dot(angVel, Vector3.up);
        yaw = Mathf.Clamp(yaw, -maxYawRad, maxYawRad);

        Vector3 pr = angVel - Vector3.up * Vector3.Dot(angVel, Vector3.up);
        m_Rigidbody.angularVelocity = pr + Vector3.up * yaw;
    }
    private void ApplyPitchRollDamping()
    {
        // Only when not actively accelerating (so it doesn't feel like glue while driving)
        if (Mathf.Abs(m_Throttle) > 0.05f)
        {
            return;
        }

        Vector3 angVel = m_Rigidbody.angularVelocity;

        Vector3 right = transform.right;
        Vector3 forward = transform.forward;

        float pitch = Vector3.Dot(angVel, right);
        float roll = Vector3.Dot(angVel, forward);

        // Exponential decay gives stable damping independent-ish of framerate
        float decay = Mathf.Exp(-m_PitchRollDamping * Time.fixedDeltaTime);

        pitch *= decay;
        roll *= decay;

        // Rebuild the angular velocity
        Vector3 yawComponent = Vector3.Project(angVel, Vector3.up);
        m_Rigidbody.angularVelocity = yawComponent + right * pitch + forward * roll;
    }
    private void ApplyTurnDamping()
    {
        // Only when not actively steering (and usually when not throttling)
        if (Mathf.Abs(m_Steer) > 0.05f && Mathf.Abs(m_Throttle) > 0.05f)
        {
            return;
        }


        Vector3 angVel = m_Rigidbody.angularVelocity;

        float yaw = Vector3.Dot(angVel, Vector3.up);
        float decay = Mathf.Exp(-m_TurnDrag * Time.fixedDeltaTime);
        yaw *= decay;

        Vector3 lateral = angVel - Vector3.up * yaw;
        m_Rigidbody.angularVelocity = lateral + Vector3.up * yaw;
    }
    private void UpdateDrive()
    {
        float currentForward = Vector3.Dot(m_Rigidbody.linearVelocity, transform.forward);
        float desiredSpeed = m_Throttle * m_Config.maxSpeed;

        // Convert input (-1 - 1) into desired speed, then smoothed using MoveTowards
        m_TargetSpeed = Mathf.MoveTowards(m_TargetSpeed, desiredSpeed, m_Acceleration * Time.fixedDeltaTime);

        float speedDelta = m_TargetSpeed - currentForward;
        Vector3 driveForce = transform.forward * (speedDelta * m_Config.maxTrackForce);

        ApplyForceToTrack(m_LeftTrack, driveForce);
        ApplyForceToTrack(m_RightTrack, driveForce);
    }

    private void ApplySideFriction()
    {
        // Skip if both tracks are essentially airborne
        float avgTraction = (m_LeftTrack.TractionFactor + m_RightTrack.TractionFactor) * 0.5f;
        if (avgTraction < 0.05f)
        {
            return;
        }

        // Convert current velocity into the tank's local space
        Vector3 localVel = transform.InverseTransformDirection(m_Rigidbody.linearVelocity);

        // x = sideways, z = forward in tank local space
        float k = Mathf.Exp(-m_LateralFrictionPerSecond * avgTraction * Time.fixedDeltaTime);
        localVel.x *= k;

        // Convert back to world space
        m_Rigidbody.linearVelocity = transform.TransformDirection(localVel);
    }
    private void ApplySteeringMoveRotation()
    {
        // If pivot turns are disabled, require some throttle to rotate
        if (!m_AllowPivotTurn && Mathf.Abs(m_Throttle) < 0.05f)
        {
            return;
        }

        float traction = (m_LeftTrack.TractionFactor + m_RightTrack.TractionFactor) * 0.5f;
        if (traction < 0.05f)
        {
            return;
        }

        float forwardSpeed = Mathf.Abs(Vector3.Dot(m_Rigidbody.linearVelocity, transform.forward));
        float speed = Mathf.InverseLerp(0f, m_Config.maxSpeed, forwardSpeed);

        // Reduces turn rate at high speed 
        float steeringScale = Mathf.Lerp(1f, m_TurnAtHighSpeed, speed);

        float turnDeg = m_Steer * m_TurnSpeedDegPerSec * steeringScale * traction * Time.fixedDeltaTime;

        Quaternion delta = Quaternion.Euler(0f, turnDeg, 0f);
        m_Rigidbody.MoveRotation(m_Rigidbody.rotation * delta);
    }

    private void ApplyForwardDrag()
    {
        if (Mathf.Abs(m_Throttle) > 0.05f)
        {
            return;
        }

        Vector3 vel = m_Rigidbody.linearVelocity;
        Vector3 forward = transform.forward;

        float forwardSpeed = Vector3.Dot(vel, forward);
        Vector3 lateral = vel - forward * forwardSpeed;

        // Only damp forward/back (keep lateral, gravity and suspension behaviour intact)
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


        // Scale by traction first, then cap so one frame can’t inject a huge force spike
        Vector3 total = totalForce * traction;
        total = Vector3.ClampMagnitude(total, m_MaxForcePerTrack);

        Vector3 forcePerPoint = total / drivePoints.Count;

        foreach (var point in drivePoints)
        {
            m_Rigidbody.AddForceAtPosition(forcePerPoint, point.position, ForceMode.Force);
        }
    }

    private void LimitForwardSpeed()
    {
        float forward = Vector3.Dot(m_Rigidbody.linearVelocity, transform.forward);
        float max = m_Config.maxSpeed;

        if (Mathf.Abs(forward) <= max)
        {
            return;
        }

        // Keep lateral velocity, clamp only forward axis
        Vector3 lateral = m_Rigidbody.linearVelocity - transform.forward * forward;
        float clampedForwardSpeed = Mathf.Clamp(forward, -max, max);
        m_Rigidbody.linearVelocity = transform.forward * clampedForwardSpeed + lateral;
    }

}


