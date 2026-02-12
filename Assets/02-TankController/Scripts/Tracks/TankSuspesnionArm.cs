using JetBrains.Annotations;
using UnityEngine;
using System;
using UnityEditor;


public class TankSuspesnionArm : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private TankConfig m_Config;
    [SerializeField] private LayerMask m_GroundMask = ~0;   // what counts as ground


    [Header("Visual")]
    [SerializeField] private float m_MaxWheelExtension = 0.35f;
    [SerializeField] private float m_WheelLerpSpeed = 12f;
    
    [SerializeField] private float m_MaxCompressionSpeed = 2.0f; // metres per second
    private float m_CurrentCompression; // per arm

    //[Header("Traction")]
    //[SerializeField] private float m_LateralGrip = 8000f;           // try 6000–15000
    //[SerializeField, Range(0f, 1f)] private float m_IdleGripScale = 0.2f; // grip when not loaded
    //[SerializeField] private float m_SpeedGripBoost = 0.08f;
    [SerializeField] private float m_MaxSuspensionForce = 150000f;//  dont worry in the insepctor thsi s chnaged ot be lower but 

    public bool IsGrounded { get; private set; }
    public float NormalisedCompression { get; private set; }  // 0–1

    private float m_LastHitDistance;  // for gizmos


    private float m_Comrpession;
    
    private float m_ComrpessionSpeed;
    public Transform m_WheelPos;




    private Rigidbody m_Rigidbody;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
   
    void Start()
    {

    }
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
    // Update is called once per frame
    void Update()
    {
        
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
        float maxSuspensionLength = m_Config.restLength + m_Config.maxCompression + m_Config.wheelRadius;
        Vector3 direction = -m_Rigidbody.transform.up; // stable reference
        Vector3 origin = transform.position - direction * 0.10f; // lift origin 10cm "up" 


        if (!Physics.Raycast(origin, direction, out RaycastHit hit, maxSuspensionLength, m_GroundMask, QueryTriggerInteraction.Ignore))
        {
            // fully extended, airborne
            IsGrounded = false;
            NormalisedCompression = 0f;
            m_LastHitDistance = maxSuspensionLength;

            MoveWheel(m_Config.restLength + m_MaxWheelExtension);
            return;
        }
        if (hit.collider != null)
        {
            var ar = hit.collider.attachedRigidbody;
            Debug.Log($"[Arm:{name}] hit={hit.collider.name} layer={LayerMask.LayerToName(hit.collider.gameObject.layer)} " +
                      $"attachedRb={(ar ? ar.name : "null")} sameRb={(ar == m_Rigidbody)} dist={hit.distance:F3}");
        }

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

        // Compression: how much shorter than rest length
        float currentLength = hit.distance;
        // Spring length should be measured to wheel centre, not to contact point.
        float springLen = Mathf.Max(0f, hit.distance - m_Config.wheelRadius);

        // Compression: how much shorter than rest length (in metres)
        float targetCompression = Mathf.Clamp(m_Config.restLength - springLen, 0f, m_Config.maxCompression);
        m_CurrentCompression = Mathf.MoveTowards(
            m_CurrentCompression,
            targetCompression,
            m_MaxCompressionSpeed * Time.fixedDeltaTime
        );
        float compression = m_CurrentCompression;

        // Normalised for traction / effects
        NormalisedCompression = m_Config.maxCompression > 0.0001f ? compression / m_Config.maxCompression : 0f;

        // Spring + damper
        float springForceMag = m_Config.springStiffnes * compression;

        Vector3 contactVelocity = m_Rigidbody.GetPointVelocity(hit.point);

        // Damping along the suspension axis (arm up), not the surface normal.
        float suspensionSpeed = Vector3.Dot(contactVelocity, transform.up);
        float dampForceMag = -m_Config.damperStrength * suspensionSpeed;

        float totalForceMag = Mathf.Max(0f, springForceMag + dampForceMag);
        totalForceMag = Mathf.Min(totalForceMag, m_MaxSuspensionForce);
        Vector3 force = transform.up * totalForceMag;
        m_Rigidbody.AddForceAtPosition(force, hit.point, ForceMode.Force);

        // Wheel visual: keep it resting on ground but never clip
        float wheelOnGround = springLen;
        // do not go *above* restLength + MaxWheelExtension
        float clamped = Mathf.Clamp(wheelOnGround,
                                    m_Config.restLength - m_Config.maxCompression,
                                    m_Config.restLength + m_MaxWheelExtension);
        MoveWheel(clamped);
    }

    private void MoveWheel(float distanceFromArm)
    {
        if (m_WheelPos == null)
        {
            return;
        }

        // local down direction is -Y
        Vector3 targetLocal = new Vector3(0f, -distanceFromArm, 0f);
        m_WheelPos.localPosition = Vector3.Lerp( m_WheelPos.localPosition, targetLocal, Time.fixedDeltaTime * m_WheelLerpSpeed);
    }


    private void OnDrawGizmos()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        Gizmos.color = IsGrounded ? Color.green : Color.red;
        Vector3 end = transform.position - transform.up * m_LastHitDistance;
        Gizmos.DrawLine(transform.position, end);
    }

    public float DampeningCalculation(Vector3 v1, Vector3 v2)
    {

        m_ComrpessionSpeed = Vector3.Dot(v1, v2);

        return -m_Config.damperStrength * m_ComrpessionSpeed;
    }


}


