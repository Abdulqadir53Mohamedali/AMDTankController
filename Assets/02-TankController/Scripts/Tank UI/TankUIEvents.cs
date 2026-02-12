using System;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
/// <summary>
/// Event "bridge" between gameplay systems and the HUD
/// Polls tank state each frame and only raises events when values change beyond small thresholds
/// (to reduce UI spam and unnecessary updates)
/// - Speed + movement direction from Rigidbody velocity
/// - Heading from transform yaw
/// - Gun elevation from barrel orientation relative to turret
/// - Slipping state from TankMovement
/// Also forwards weapon events (ready state changes + fired)
/// </summary>
public class TankUIEvents: MonoBehaviour
{
    public enum MoveDir { Idle, Forward, Reverse }

    [Header("Refs")]
    [SerializeField] private TankWeapon m_Weapon;
    [SerializeField] private TankMovement m_Movement;


    [Header("Gun Refs (for elevation)")]
    [SerializeField] private Transform m_Turret;
    [SerializeField] private Transform m_Barrel;

    [Header("Thresholds")]
    [SerializeField] private float m_SpeedEpsilon = 0.05f;     // m/s
    [SerializeField] private float m_HeadingEpsilon = 0.5f;    // deg
    [SerializeField] private float m_ElevationEpsilon = 0.25f; // deg

    public event Action<float> SpeedChanged;
    public event Action<MoveDir> DirectionChanged;
    public event Action<float> HeadingChanged;

    // degrees (+ up, - down)
    public event Action<float> GunElevationChanged; 

    public event Action<bool> SlippingChanged;

    public event Action<bool> WeaponReadyChanged;
    public event Action WeaponFired;

    private Rigidbody m_Rb;

    // Cached last values so we can publish only on change (with epsilons)
    private float m_LastSpeed = float.NaN;
    private float m_LastHeading = float.NaN;
    private float m_LastElevation = float.NaN;
    private MoveDir m_LastDir = (MoveDir)(-1);
    private bool m_LastSlip = false;


    private void Awake()
    {
        m_Rb = GetComponent<Rigidbody>();
    }

    private void OnEnable()
    {
        // Forward weapon events instead of the HUD needing to subscribe to the weapon directly
        if (m_Weapon != null)
        {
            m_Weapon.ReadyStateChanged += HandleWeaponReadyChanged;
            m_Weapon.Fired += HandleWeaponFired;
        }
    }

    private void OnDisable()
    {
        if (m_Weapon != null)
        {
            m_Weapon.ReadyStateChanged -= HandleWeaponReadyChanged;
            m_Weapon.Fired -= HandleWeaponFired;
        }
    }

    private void Update()
    {
        PublishSpeedAndDir();
        PublishHeading();
        PublishGunElevation();
        PublishSlipping();

    }
    private void PublishSlipping()
    {
        if (m_Movement == null)
        {
            return;
        }

        bool slip = m_Movement.IsSlipping;

        if (slip != m_LastSlip)
        {
            m_LastSlip = slip;
            SlippingChanged?.Invoke(slip);
        }
    }
    private void PublishSpeedAndDir()
    {
        Vector3 v = m_Rb.linearVelocity;

        float speed = v.magnitude;

        // Signed speed along tank forward axis (used to decide forward/reverse/idle)
        float forwardSpeed = Vector3.Dot(v, transform.forward);

        // Speed is noisy, so only publish if it changes more than an epsilon
        MoveDir dir =  Mathf.Abs(forwardSpeed) < 0.05f ? MoveDir.Idle : (forwardSpeed > 0f ? MoveDir.Forward : MoveDir.Reverse);

        if (float.IsNaN(m_LastSpeed) || Mathf.Abs(speed - m_LastSpeed) > m_SpeedEpsilon)
        {
            m_LastSpeed = speed;
            SpeedChanged?.Invoke(speed);
        }

        if (dir != m_LastDir)
        {
            m_LastDir = dir;
            DirectionChanged?.Invoke(dir);
        }
    }

    private void PublishHeading()
    {
        // Euler yaw is 0-360, the DeltaAngle handles wrap-around cleanly
        float heading = transform.eulerAngles.y; 

        if (float.IsNaN(m_LastHeading) || Mathf.Abs(Mathf.DeltaAngle(m_LastHeading, heading)) > m_HeadingEpsilon)
        {
            m_LastHeading = heading;
            HeadingChanged?.Invoke(heading);
        }
    }

    private void PublishGunElevation()
    {
        if (m_Turret == null || m_Barrel == null)
        {
            return;
        }

        // Convert barrel forward into turretlocal space , so  the up/down is relative to the turret
        Vector3 localBarrelForward = m_Turret.InverseTransformDirection(m_Barrel.forward); 
        localBarrelForward.Normalize();

        // Pitch/elevation in degrees: + up, - down
        float elevationDeg = Mathf.Asin(Mathf.Clamp(localBarrelForward.y, -1f, 1f)) * Mathf.Rad2Deg;

        if (float.IsNaN(m_LastElevation) || Mathf.Abs(elevationDeg - m_LastElevation) > m_ElevationEpsilon)
        {
            m_LastElevation = elevationDeg;
            GunElevationChanged?.Invoke(elevationDeg);
        }
    }

    private void HandleWeaponReadyChanged(bool ready) => WeaponReadyChanged?.Invoke(ready);
    private void HandleWeaponFired() => WeaponFired?.Invoke();
}
