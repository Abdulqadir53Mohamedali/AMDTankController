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
        ApplySideFriction();
        ApplyForwardDrag();
        ApplyPitchRollDamping();
        //ApplyTurnDamping();        // NEW

        LimitForwardSpeed();
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
    private void UpdateDrive()
    {
        float currentForward = CurrentSpeed;
        float desiredSpeed = m_Throttle * m_Config.maxSpeed;

        // Smooths target speed for better acceleration/ acceleration feel
        m_TargetSpeed = Mathf.MoveTowards(  m_TargetSpeed, desiredSpeed, m_Acceleration * Time.fixedDeltaTime );

        float speedDelta = m_TargetSpeed - currentForward;

        // base drive force (before steering split)
        Vector3 baseForce = transform.forward * (speedDelta * m_Config.maxTrackForce);

        // Steering: determine how much to bias left vs right
        float speed = Mathf.InverseLerp(0f, m_Config.maxSpeed, Mathf.Abs(currentForward));
        float steerScale = Mathf.Lerp(m_SteerAtLowSpeed, m_SteerAtHighSpeed, speed);

        float steerAmount = m_Steer * steerScale * m_TurnSharpness;

        // Differential: left gets more force when steering right, etc.
        float leftFactor = Mathf.Clamp01(1f - steerAmount);
        float rightFactor = Mathf.Clamp01(1f + steerAmount);

        ApplyForceToTrack(m_LeftTrack, baseForce * leftFactor);
        ApplyForceToTrack(m_RightTrack, baseForce * rightFactor);
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
        float lateral = localVel.x;

        // Blend towards zero sideways speed: more traction => stronger correction.
        float frictionStrength = m_LateralFriction * avgTraction;
        localVel.x = Mathf.Lerp(lateral, 0f, frictionStrength * Time.fixedDeltaTime);

        // Convert back to world space.
        m_Rigidbody.linearVelocity = transform.TransformDirection(localVel);
    }

    //private void ApplyTurnDamping()
    //{
    //    // only when you're not actively steering
    //    if (Mathf.Abs(m_Steer) > 0.05f)
    //        return;

    //    Vector3 angVel = m_Rigidbody.angularVelocity;
    //    // kill yaw (around global up)
    //    float yaw = Vector3.Dot(angVel, Vector3.up);
    //    float decay = Mathf.Exp(-m_TurnDrag * Time.fixedDeltaTime);
    //    yaw *= decay;

    //    // rebuild angular velocity with damped yaw
    //    Vector3 lateral = angVel - Vector3.up * yaw;
    //    m_Rigidbody.angularVelocity = lateral + Vector3.up * yaw;
    //}
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

        // Scale by traction and mass so behaviour is mass independent-ish
        Vector3 TotalForce = totalForce * traction;
        Vector3 forcePerPoint = TotalForce / drivePoints.Count;

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

        Vector3 lateral = m_Rigidbody.linearVelocity - transform.forward * forward;
        float clampedForwardSpeed = Mathf.Clamp(forward, -max, max);
        m_Rigidbody.linearVelocity = transform.forward * clampedForwardSpeed + lateral;
    }

}
//public void TotalTrackForce()
//{

//    //Throttle = Mathf.Clamp(Throttle,-1,0f, 1.0f);
//    //Steer = Mathf.Clamp(Steer,-1,0f, 1.0f);

//    float LeftForce = (currentThrottle - currentSteer) * m_maxTrackForce;
//    float RightForce = (currentThrottle + currentSteer) * m_maxTrackForce;

//    //LeftForce = Mathf.Clamp(LeftForce,-LeftForce,m_maxTrackForce);
//    //RightForce = Mathf.Clamp(RightForce,RightForce,m_maxTrackForce);

//    Vector3 forceDirection = transform.forward;

//    Vector3 leftforce = forceDirection * LeftForce;
//    Vector3 rightforce = forceDirection * RightForce;


//    Debug.Log($"throttle={currentThrottle:F2} steer={currentSteer:F2} " +
//      $"m_maxTrackForce={m_maxTrackForce:F1} LeftForce={LeftForce:F1} RightForce={RightForce:F1} " +
//      $"| leftMag={leftforce.magnitude:F1} rightMag={rightforce.magnitude:F1}");

//    //onSuspensionRaycast
//    onDriveWheelRaycast?.Invoke( leftforce, rightforce );
//    //m_Rigidbody.AddForceAtPosition(leftforce, LeftDriveWheel.position);
//    //m_Rigidbody.AddForceAtPosition(rightforce, RightDriveWheel.position);

//}
// Update is called once per frame