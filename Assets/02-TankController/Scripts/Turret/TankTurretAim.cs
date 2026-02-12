using UnityEngine;

/// <summary>
/// Aims the tank turret and barrel based on the camera's forward direction
/// - Turret yaw: camera forward is projected onto the turret "horizontal" plane and smoothed with Slerp.
/// - Barrel pitch: camera forward is projected onto the turret pitch plane, converted to a signed pitch angle,
///   clamped to limits, then smoothed with Slerp
/// 
/// LateUpdate is used so aiming happens after camera/player movement for the frame
/// </summary>
public class TankTurretAimer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform m_Turret;
    [SerializeField] private Transform m_Barrel;
    [SerializeField] private Transform m_Camera;

    [Header("Turret (Yaw) - Slerp smoothing")]
    [SerializeField] private float m_TurretTurnSpeed = 6f; 

    [Header("Barrel (Pitch) - Slerp smoothing")]
    [SerializeField] private float m_MinPitch = -50f;
    [SerializeField] private float m_MaxPitch = 45f;
    [SerializeField] private float m_BarrelPitchSpeed = 6f; 

    private void Awake()
    {
        if (m_Turret == null)
        {
            m_Turret = transform;
        }
    }

    private void LateUpdate()
    {
        if (m_Turret == null || m_Barrel == null || m_Camera == null)
        {
            return;
        }

        UpdateTurretYaw();
        UpdateBarrelPitch();
    }

    private void UpdateTurretYaw()
    {
        Vector3 camForward = m_Camera.forward;

        // This gives a flat direction used for yaw only rotation
        Vector3 projectedForward = Vector3.ProjectOnPlane(camForward, m_Turret.up);

        if (projectedForward.sqrMagnitude < 0.0001f)
        {
            return;
        }

        projectedForward.Normalize();

        // Builds a target rotation that looks in the flattened direction, keeping turret.up as the up axis
        Quaternion targetRotation = Quaternion.LookRotation(projectedForward, m_Turret.up);

        float yawLerp = Time.deltaTime * m_TurretTurnSpeed;
        m_Turret.rotation = Quaternion.Slerp(m_Turret.rotation, targetRotation, yawLerp);
    }

    private void UpdateBarrelPitch()
    {
        // camera forward projected onto pitch plane, then converted in to pitch angle
        Vector3 turretFwd = m_Turret.forward;
        Vector3 cameraFwd = m_Camera.forward;

        // Project camera forward onto the plane where pitch is allowed (plane whose normal is turret.right)
        Vector3 projection = Vector3.ProjectOnPlane(cameraFwd, m_Turret.right);
        if (projection.sqrMagnitude < 0.0001f)
        {
            return;
        }

        projection.Normalize();

        // Signed pitch angle around turret.right, then clamp to barrel limits
        float targetPitch = Vector3.SignedAngle(turretFwd, projection, m_Turret.right);
        targetPitch = Mathf.Clamp(targetPitch, m_MinPitch, m_MaxPitch);

        // Builds the target barrel world rotation by applying pitch relative to the turret rotation
        Quaternion pitchRotation = Quaternion.AngleAxis(targetPitch, m_Turret.right);
        Quaternion targetWorldRotation = pitchRotation * m_Turret.rotation;

        float pitchLerp = Time.deltaTime * m_BarrelPitchSpeed;
        m_Barrel.rotation = Quaternion.Slerp(m_Barrel.rotation, targetWorldRotation, pitchLerp);
    }
}
