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



    public bool IsGrounded { get; private set; }
    public float NormalisedCompression { get; private set; }  // 0–1

    private float m_LastHitDistance;  // for gizmos


    //public float m_Distance;
    //public Vector3 m_SurfaceNormal;
    //public Vector3 m_HitPoint;
    //public TankConfig m_TankConfig;
    //private float m_SpringForce;
    //private float m_DampForce;

    private float m_Comrpession;
    
    private float m_ComrpessionSpeed;
    public Transform m_WheelPos;

    //[Header("Base Spring Values")]
    //[SerializeField] private float m_RestLength;
    //[SerializeField] private float m_SpringStiffness;
    //[SerializeField] private float m_DamperStrength;
    //[SerializeField] private float m_MaxCompression;

    //[Header("Base Spring Values")]
    //[SerializeField] private LayerMask m_IgnoreLayers; 
    //private int m_RaycastMask;


    private Rigidbody m_Rigidbody;

    //public static event Action<float,Vector3,Vector3> onSuspensionRaycast;
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
        //m_RestLength = m_TankConfig.restLength;
        //m_SpringStiffness = m_TankConfig.springStiffnes;
        //m_DamperStrength = m_TankConfig.damperStrength;
        //m_MaxCompression = m_TankConfig.maxCompression;


        //m_RaycastMask = ~m_IgnoreLayers.value; // invert so raycast hits everything EXCEPT ignored layers

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
        float maxSuspensionLength = m_Config.restLength + m_Config.maxCompression;

        Vector3 origin = transform.position;
        Vector3 direction = -transform.up;

        if (!Physics.Raycast(origin, direction, out RaycastHit hit, maxSuspensionLength, m_GroundMask, QueryTriggerInteraction.Ignore))
        {
            // fully extended, airborne
            IsGrounded = false;
            NormalisedCompression = 0f;
            m_LastHitDistance = maxSuspensionLength;

            MoveWheel(m_Config.restLength + m_MaxWheelExtension);
            return;
        }

        IsGrounded = true;
        m_LastHitDistance = hit.distance;

        // Compression: how much shorter than rest length
        float currentLength = hit.distance;
        float compression = Mathf.Clamp(m_Config.restLength - currentLength, 0f, m_Config.maxCompression);

        // Normalised for traction / effects
        NormalisedCompression = m_Config.maxCompression > 0.0001f ? compression / m_Config.maxCompression : 0f;

        // Spring + damper
        float springForceMag = m_Config.springStiffnes * compression;

        //// relative velocity along the contact normal
        Vector3 contactVelocity = m_Rigidbody.GetPointVelocity(hit.point);
        //float relativeSpeed = Vector3.Dot(contactVelocity, hit.normal); // +ve when moving along normal
        float dampForceMag = DampeningCalculation(contactVelocity, hit.normal);

        float totalForceMag = Mathf.Max(0f, springForceMag + dampForceMag); // no pulling force

        Vector3 force = hit.normal * totalForceMag;
        m_Rigidbody.AddForceAtPosition(force, hit.point, ForceMode.Force);

        // Wheel visual: keep it resting on ground but never clip
        float wheelOnGround = currentLength - m_Config.wheelRadius;
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
    //public void SpringCheck()
    //{
    //    if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 2.5f, m_RaycastMask, QueryTriggerInteraction.Ignore))
    //    {
    //        m_Distance = hit.distance;
    //        m_SurfaceNormal = hit.normal;
    //        m_HitPoint = hit.point;

    //        Debug.DrawLine(transform.position, hit.point, Color.green); // hit point in green

    //    }

    //}


    //public float HookesCalculation()
    //{

    //    m_Comrpession = m_RestLength * m_Distance;

    //    m_Comrpession = Mathf.Clamp(m_Comrpession,0.0f,m_MaxCompression);



    //    return m_SpringStiffness * m_Comrpession;
    //}

    //public float TotalForce()
    //{

    //    m_SpringForce = HookesCalculation();
    //    m_DampForce = DampeningCalculation();

    //    return m_SpringForce + m_DampForce;

    //}


}
