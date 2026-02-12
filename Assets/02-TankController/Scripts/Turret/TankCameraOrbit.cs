using UnityEngine;


/// <summary>
///  Main Camera for tank orbit 
/// - Applies look input to yaw/pitch a pivot (pitch clamped)
/// - Positions the camera behind the pivot at a default distance
/// - Uses a sphere cast to stop the camera clipping through level geometry
/// </summary>
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

    //Done for Personal tweaking , but if i place correctly in scene then I do not need to touch this but nice to have for testing 
    [SerializeField] private float m_LocalHeight = 0f; 

    [Header("Collision")]
    [SerializeField] private float m_CollisionRadius = 0.3f;
    [SerializeField] private LayerMask m_CollisionMask = ~0;

    private Vector2 m_LookInput;
    private float m_Pitch;
    private float m_Yaw;

    public Transform CameraTransform => m_Camera;

   
    public void SetLookInput(Vector2 mouseLook)
    {
        m_LookInput = mouseLook;
    }

    // Late update used to keep the camera reacting after the movment scripts have run this frame 
    private void LateUpdate()
    {
        if (m_Camera == null || m_SpringArmPivot == null)
        {
            return;
        }

        UpdateCameraOrbit(Time.deltaTime);
        ResolveCollision();
    }

    private void UpdateCameraOrbit(float TimeSinceLastUpdate)
    {
        m_Yaw += m_LookInput.x * m_Sensitivity * TimeSinceLastUpdate;
        m_Pitch -= m_LookInput.y * m_Sensitivity * TimeSinceLastUpdate;

        //I clamp the pitch to avoid some weird flipping thta occurs 
        m_Pitch = Mathf.Clamp(m_Pitch, m_MinPitch, m_MaxPitch);

        m_SpringArmPivot.rotation = Quaternion.Euler(m_Pitch, m_Yaw, 0f);
    }

    // Sphere casts from the pivot to the desired camera point and pulls the camera in if obstructed
    private void ResolveCollision()
    {
        Vector3 desiredLocalPos = new Vector3(0f, m_LocalHeight, -m_DefaultDistance);

        Vector3 pivot = m_SpringArmPivot.position;
        Vector3 desiredWorldPos = m_SpringArmPivot.TransformPoint(desiredLocalPos);

        Vector3 delta = desiredWorldPos - pivot;
        float distance = delta.magnitude;

        if (distance < 0.0001f)
        {
            return;
        }

        Vector3 direction = delta / distance;

        if (Physics.SphereCast(pivot, m_CollisionRadius, direction, out RaycastHit hit, distance, m_CollisionMask, QueryTriggerInteraction.Ignore))
        {
            // safeDist ensures the camera is never fully on the surface by subtracting the cast radius 
            float safeDist = Mathf.Max(0f, hit.distance - m_CollisionRadius);
            m_Camera.position = pivot + direction * safeDist;
        }
        else
        {
            m_Camera.position = desiredWorldPos;
        }
    }
}
