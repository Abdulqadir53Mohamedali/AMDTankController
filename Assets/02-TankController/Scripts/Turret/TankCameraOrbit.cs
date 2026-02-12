using UnityEngine;

public class TankCameraOrbit : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform m_Camera;
    [SerializeField] private Transform m_SpringArmPivot;

    [Header("Orbit")]
    [SerializeField] private float m_Sensitivity = 100f;
    [SerializeField] private float m_MinPitch = -40f;
    [SerializeField] private float m_MaxPitch = 60f;

    [Header("Distance / Offset")]
    [SerializeField] private float m_DefaultDistance = 6f;
    [SerializeField] private float m_LocalHeight = 0f; // set pivot height in the pivot transform, or use this offset

    [Header("Collision")]
    [SerializeField] private float m_CollisionRadius = 0.3f;
    [SerializeField] private LayerMask m_CollisionMask = ~0;

    private Vector2 m_LookInput;
    private float m_Pitch;
    private float m_Yaw;

    public Transform CameraTransform => m_Camera;

    public void SetLookInput(Vector2 look)
    {
        m_LookInput = look;
    }

    private void LateUpdate()
    {
        if (m_Camera == null || m_SpringArmPivot == null)
            return;

        UpdateOrbit(Time.deltaTime);
        ResolveCollision();
    }

    private void UpdateOrbit(float dt)
    {
        m_Yaw += m_LookInput.x * m_Sensitivity * dt;
        m_Pitch -= m_LookInput.y * m_Sensitivity * dt;
        m_Pitch = Mathf.Clamp(m_Pitch, m_MinPitch, m_MaxPitch);

        m_SpringArmPivot.rotation = Quaternion.Euler(m_Pitch, m_Yaw, 0f);
    }

    private void ResolveCollision()
    {
        Vector3 desiredLocalPos = new Vector3(0f, m_LocalHeight, -m_DefaultDistance);

        Vector3 pivot = m_SpringArmPivot.position;
        Vector3 desiredWorldPos = m_SpringArmPivot.TransformPoint(desiredLocalPos);

        Vector3 delta = desiredWorldPos - pivot;
        float dist = delta.magnitude;
        if (dist < 0.0001f)
            return;

        Vector3 dir = delta / dist;

        if (Physics.SphereCast(pivot, m_CollisionRadius, dir, out RaycastHit hit, dist, m_CollisionMask, QueryTriggerInteraction.Ignore))
        {
            float safeDist = Mathf.Max(0f, hit.distance - m_CollisionRadius);
            m_Camera.position = pivot + dir * safeDist;
        }
        else
        {
            m_Camera.position = desiredWorldPos;
        }
    }
}
