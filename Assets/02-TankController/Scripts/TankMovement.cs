using Codice.Client.Common.GameUI;
using UnityEngine;
using UnityEngine.InputSystem;
using System;


public class TankMovement : MonoBehaviour
{

    public Transform LeftDriveWheel;
    public Transform RightDriveWheel;


    public TankConfig m_TankConfig;

    private Rigidbody m_Rigidbody;
    [Header("Base Drive Values")]
    [SerializeField] private float m_maxTrackForce;
    [SerializeField] private float m_maxSpeed;
    [SerializeField] private float m_Speed;


    private float currentThrottle = 0f;
    private float currentSteer = 0f;

    public static event Action<Vector3,Vector3> onDriveWheelRaycast;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Awake()
    {
        m_maxSpeed = m_TankConfig.maxSpeed;
        m_maxTrackForce = m_TankConfig.maxTrackForce;

        m_Rigidbody = GetComponent<Rigidbody>();
    }
    void Start()
    {
        
    }


    public void SetInput(float throttle, float steer)
    {
        
        currentThrottle = throttle;
        currentSteer = steer;
    }



    public void TotalTrackForce()
    {

        //Throttle = Mathf.Clamp(Throttle,-1,0f, 1.0f);
        //Steer = Mathf.Clamp(Steer,-1,0f, 1.0f);

        float LeftForce = (currentThrottle - currentSteer) * m_maxTrackForce;
        float RightForce = (currentThrottle + currentSteer) * m_maxTrackForce;

        //LeftForce = Mathf.Clamp(LeftForce,-LeftForce,m_maxTrackForce);
        //RightForce = Mathf.Clamp(RightForce,RightForce,m_maxTrackForce);
        
        Vector3 forceDirection = transform.forward;

        Vector3 leftforce = forceDirection * LeftForce;
        Vector3 rightforce = forceDirection * RightForce;


        Debug.Log($"throttle={currentThrottle:F2} steer={currentSteer:F2} " +
          $"m_maxTrackForce={m_maxTrackForce:F1} LeftForce={LeftForce:F1} RightForce={RightForce:F1} " +
          $"| leftMag={leftforce.magnitude:F1} rightMag={rightforce.magnitude:F1}");

        //onSuspensionRaycast
        onDriveWheelRaycast?.Invoke( leftforce, rightforce );
        //m_Rigidbody.AddForceAtPosition(leftforce, LeftDriveWheel.position);
        //m_Rigidbody.AddForceAtPosition(rightforce, RightDriveWheel.position);

    }
    // Update is called once per frame
    void Update()
    {
        
    }
    private void FixedUpdate()
    {
        TotalTrackForce();
    }
}
