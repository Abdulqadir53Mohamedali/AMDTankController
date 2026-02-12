using UnityEngine;

public class TankTurretAimer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform m_Turret;
    [SerializeField] private Transform m_Barrel;
    [SerializeField] private Transform m_Camera;

    [Header("Turret (Yaw) - Slerp smoothing")]
    [SerializeField] private float m_TurretTurnSpeed = 6f; // same meaning as your friend's script (Slerp factor)

    [Header("Barrel (Pitch) - Slerp smoothing")]
    [SerializeField] private float m_MinPitch = -50f;
    [SerializeField] private float m_MaxPitch = 45f;
    [SerializeField] private float m_BarrelPitchSpeed = 6f; // same meaning as your friend's script (Slerp factor)

    // Optional: if your barrel's local forward isn't aligned, you may need to swap axis logic,
    // but this matches your friend's approach.
    private void Awake()
    {
        if (m_Turret == null)
            m_Turret = transform;
    }

    private void LateUpdate()
    {
        if (m_Turret == null || m_Barrel == null || m_Camera == null)
            return;

        UpdateTurretYaw();
        UpdateBarrelPitch();
    }

    private void UpdateTurretYaw()
    {
        Vector3 camForward = m_Camera.forward;
        Vector3 projectedForward = Vector3.ProjectOnPlane(camForward, m_Turret.up);

        if (projectedForward.sqrMagnitude < 0.0001f)
            return;

        projectedForward.Normalize();

        Quaternion targetRotation = Quaternion.LookRotation(projectedForward, m_Turret.up);

        // Matches friend's style: Slerp(current, target, dt * speed)
        float t = Time.deltaTime * m_TurretTurnSpeed;
        m_Turret.rotation = Quaternion.Slerp(m_Turret.rotation, targetRotation, t);
    }

    private void UpdateBarrelPitch()
    {
        // Use camera forward projected onto pitch plane, then convert to pitch angle
        Vector3 turretFwd = m_Turret.forward;
        Vector3 camFwd = m_Camera.forward;

        Vector3 proj = Vector3.ProjectOnPlane(camFwd, m_Turret.right);
        if (proj.sqrMagnitude < 0.0001f)
            return;

        proj.Normalize();

        float targetPitch = Vector3.SignedAngle(turretFwd, proj, m_Turret.right);
        targetPitch = Mathf.Clamp(targetPitch, m_MinPitch, m_MaxPitch);

        Quaternion pitchRotation = Quaternion.AngleAxis(targetPitch, m_Turret.right);
        Quaternion targetWorldRotation = pitchRotation * m_Turret.rotation;

        float t = Time.deltaTime * m_BarrelPitchSpeed;
        m_Barrel.rotation = Quaternion.Slerp(m_Barrel.rotation, targetWorldRotation, t);
    }
}
