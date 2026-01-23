using JetBrains.Annotations;
using UnityEngine;
using System;
using UnityEditor;


public class TankSuspesnionArm : MonoBehaviour
{

    public float m_Distance;
    public Vector3 m_SurfaceNormal;
    public Vector3 m_HitPoint;
    public TankConfig m_TankConfig;
    private float m_SpringForce;
    private float m_DampForce;

    private float m_Comrpession;
    
    private float m_ComrpessionSpeed;
    public Transform m_WheelPos;

    [Header("Base Spring Values")]
    [SerializeField] private float m_RestLength;
    [SerializeField] private float m_SpringStiffness;
    [SerializeField] private float m_DamperStrength;
    [SerializeField] private float m_MaxCompression;

    private Rigidbody m_Rigidbody;

    //public static event Action<float,Vector3,Vector3> onSuspensionRaycast;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
   
    void Start()
    {

    }
    private void Awake()
    {
        m_RestLength = m_TankConfig.restLength;
        m_SpringStiffness = m_TankConfig.springStiffnes;
        m_DamperStrength = m_TankConfig.damperStrength;
        m_MaxCompression = m_TankConfig.maxCompression;

        m_Rigidbody = GetComponentInParent<Rigidbody>();
        
    }
    // Update is called once per frame
    void Update()
    {
        
    }

    private void FixedUpdate()
    {
        SpringCheck();
        TotalForce();
    }
    public void SpringCheck()
    {
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 2f))
        {
            m_Distance = hit.distance;
            m_SurfaceNormal = hit.normal;
            m_HitPoint = hit.point;
        }

    }


    public float HookesCalculation()
    {

        m_Comrpession = m_RestLength * m_Distance;

        m_Comrpession = Mathf.Clamp(m_Comrpession,0.0f,m_MaxCompression);

      

        return m_SpringStiffness * m_Comrpession;
    }

    public float TotalForce()
    {

        m_SpringForce = HookesCalculation();
        m_DampForce = DampeningCalculation();

        return m_SpringForce + m_DampForce;

    }

    public float DampeningCalculation()
    {

        Vector3 WheelPos = m_WheelPos.position;
        m_ComrpessionSpeed = Vector3.Dot(m_Rigidbody.GetPointVelocity(WheelPos), m_SurfaceNormal);

        
        return m_DamperStrength * -m_ComrpessionSpeed;
    }
}
