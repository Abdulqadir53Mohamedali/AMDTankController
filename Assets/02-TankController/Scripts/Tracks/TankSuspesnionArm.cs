using UnityEngine;


/// <summary>
/// Per-wheel suspension arm for the tank
/// - Raycasts down to find ground distance under this arm
/// - Computes spring compression (restLength vs current spring length) and applies spring + damper force
///   to the tank rigidbody at the contact point
/// - Exposes grounded/compression values for traction and other systems
/// - Moves the wheel mesh / transform to match the suspension length (visual only)
/// </summary>
public class TankSuspesnionArm : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private TankConfig m_Config;
    [SerializeField] private LayerMask m_GroundMask = ~0;   


    [Header("Visual")]
    [SerializeField] private float m_MaxWheelExtension = 0.35f;
    [SerializeField] private float m_WheelLerpSpeed = 12f;

    // Limits how fast compression can change to avoid "teleporting" suspension on sudden terrain changes
    [SerializeField] private float m_MaxCompressionSpeed = 2.0f;
    // current compression for this arm (metres)
    private float m_CurrentCompression; 

    // Hard cap so forces can't explode if something goes wrong (or on extreme hits)
    [SerializeField] private float m_MaxSuspensionForce = 500f;

    public bool IsGrounded { get; private set; }

    // 0-1 compression amount used by traction / effects (0 = extended, 1 = fully compressed)
    public float NormalisedCompression { get; private set; }

    private float m_LastHitDistance;
    private float m_Comrpession;

    private float m_ComrpessionSpeed;

    // Wheel visual transform (child object that moves up/down)
    public Transform m_WheelPos;

    private Rigidbody m_Rigidbody;

    private void Awake()
    {
        m_Rigidbody = GetComponentInParent<Rigidbody>();

        if (m_Config == null)
        {
            Debug.LogError("TankConfig is not assigned.");
        }
        if (m_WheelPos == null && transform.childCount > 0)
        {
            // fall back to first child as wheel , in this instance it works becuase wheel is our only child
            m_WheelPos = transform.GetChild(0);
        }
    }

    private void FixedUpdate()
    {
        if (m_Config == null || m_Rigidbody == null)
        {
            return;

        }

        ApplySuspension();

    }

    private void ApplySuspension()
    {
        // Max ray length = rest + max compression + wheel radius
        // (wheelRadius is included so the ray can reach the ground even when the wheel is "below" the arm origin)
        float maxSuspensionLength = m_Config.restLength + m_Config.maxCompression + m_Config.wheelRadius;
        Vector3 direction = -m_Rigidbody.transform.up;
        
        // Slight offset to avoid starting the ray inside the ground/collider
        Vector3 origin = transform.position - direction * 0.10f;

        // No hit likely meaning airborne: reset compression info and extend wheel visually
        if (!Physics.Raycast(origin, direction, out RaycastHit hit, maxSuspensionLength, m_GroundMask, QueryTriggerInteraction.Ignore))
        {
            IsGrounded = false;
            NormalisedCompression = 0f;
            m_LastHitDistance = maxSuspensionLength;

            MoveWheel(m_Config.restLength + m_MaxWheelExtension);
            return;
        }
        if (hit.collider != null)
        {
            var ar = hit.collider.attachedRigidbody;
        }

        // if the hit is extremely close, treat as invalid (prevents huge compression spikes / jitter)
        const float MinValidHitDist = 0.05f;
        if (hit.distance < MinValidHitDist)
        {
            IsGrounded = false;
            NormalisedCompression = 0f;
            m_LastHitDistance = hit.distance;
            m_CurrentCompression = 0f;

            MoveWheel(m_Config.restLength + m_MaxWheelExtension);
            return;
        }
        IsGrounded = true;
        m_LastHitDistance = hit.distance;

        float currentLength = hit.distance;
        
        // Spring length should be measured to wheel centre, not to the contact point
        float springLen = Mathf.Max(0f, hit.distance - m_Config.wheelRadius);

        // Target compression = how much shorter than rest length
        float targetCompression = Mathf.Clamp(m_Config.restLength - springLen, 0f, m_Config.maxCompression);

        m_CurrentCompression = Mathf.MoveTowards( m_CurrentCompression, targetCompression, m_MaxCompressionSpeed * Time.fixedDeltaTime);
        float compression = m_CurrentCompression;

        // Normalised for traction
        NormalisedCompression = m_Config.maxCompression > 0.0001f ? compression / m_Config.maxCompression : 0f;

        float springForceMag = m_Config.springStiffnes * compression;


        // Damper force, oppose velocity along the suspension axis
        Vector3 contactVelocity = m_Rigidbody.GetPointVelocity(hit.point);
        float suspensionSpeed = Vector3.Dot(contactVelocity, transform.up);
        float dampForceMag = -m_Config.damperStrength * suspensionSpeed;

        float totalForceMag = Mathf.Max(0f, springForceMag + dampForceMag);
        totalForceMag = Mathf.Min(totalForceMag, m_MaxSuspensionForce);
        Vector3 force = transform.up * totalForceMag;
        m_Rigidbody.AddForceAtPosition(force, hit.point, ForceMode.Force);

        float wheelOnGround = springLen;
        
        //Clamped to ensure it never over extneds or clipped
        float clamped = Mathf.Clamp(wheelOnGround, m_Config.restLength - m_Config.maxCompression, m_Config.restLength + m_MaxWheelExtension);
        MoveWheel(clamped);
    }

    private void MoveWheel(float distanceFromArm)
    {
        if (m_WheelPos == null)
        {
            return;
        }

        // The wheel mesh moves down along the arm's local -Y axis
        Vector3 targetLocal = new Vector3(0f, -distanceFromArm, 0f);

        // Lerp for smooth wheel movement (visual only)
        m_WheelPos.localPosition = Vector3.Lerp( m_WheelPos.localPosition, targetLocal, Time.fixedDeltaTime * m_WheelLerpSpeed);
    }




    public float DampeningCalculation(Vector3 v1, Vector3 v2)
    {

        m_ComrpessionSpeed = Vector3.Dot(v1, v2);

        return -m_Config.damperStrength * m_ComrpessionSpeed;
    }


}
//private void OnDrawGizmos()
//{
//    if (!Application.isPlaying)
//    {
//        return;
//    }

//    Gizmos.color = IsGrounded ? Color.green : Color.red;
//    Vector3 end = transform.position - transform.up * m_LastHitDistance;
//    Gizmos.DrawLine(transform.position, end);
//}

