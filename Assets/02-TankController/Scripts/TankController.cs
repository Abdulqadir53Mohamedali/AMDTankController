using UnityEngine;
using UnityEngine.InputSystem;

public class TankController : MonoBehaviour
{
	private AM_02Tank m_ActionMap; //input
	private TankMovement m_Movement;

    private TankCameraOrbit m_CameraOrbit;
    private TankWeapon m_Weapon;

    private Vector2 m_Look;

    private float m_Throttle = 0f;
    private float m_Steer = 0f;
    private void Awake()
	{
		m_ActionMap = new AM_02Tank();
		m_Movement = GetComponent<TankMovement>();
        m_CameraOrbit = GetComponent<TankCameraOrbit>(); 
        m_Weapon = GetComponent<TankWeapon>();
    }
    void Update()
    {
        m_Movement.SetInput(m_Throttle, m_Steer);


        if (m_Movement != null)
		{
            m_Movement.SetInput(m_Throttle, m_Steer);
        }

        // look/mouse input is then taken in by the camera orbit (orbit runs in LateUpdate in that script)
        if (m_CameraOrbit != null)
		{
            m_CameraOrbit.SetLookInput(m_Look);
        }

    }
    private void OnEnable()
	{
		m_ActionMap.Enable();

		m_ActionMap.Default.Accelerate.performed += Handle_AcceleratePerformed;
		m_ActionMap.Default.Accelerate.canceled += Handle_AccelerateCanceled;
		m_ActionMap.Default.Steer.performed += Handle_SteerPerformed;
		m_ActionMap.Default.Steer.canceled += Handle_SteerCanceled;
		m_ActionMap.Default.Fire.performed += Handle_FirePerformed;
		m_ActionMap.Default.Fire.canceled += Handle_FireCanceled;
		m_ActionMap.Default.Aim.performed += Handle_AimPerformed;
        m_ActionMap.Default.Aim.canceled += Handle_AimCanceled;

        m_ActionMap.Default.Zoom.performed += Handle_ZoomPerformed;
	}
	private void OnDisable()
	{
		m_ActionMap.Disable();

		m_ActionMap.Default.Accelerate.performed -= Handle_AcceleratePerformed;
		m_ActionMap.Default.Accelerate.canceled -= Handle_AccelerateCanceled;
		m_ActionMap.Default.Steer.performed -= Handle_SteerPerformed;
		m_ActionMap.Default.Steer.canceled -= Handle_SteerCanceled;
		m_ActionMap.Default.Fire.performed -= Handle_FirePerformed;
		m_ActionMap.Default.Fire.canceled -= Handle_FireCanceled;
		m_ActionMap.Default.Aim.performed -= Handle_AimPerformed;
        m_ActionMap.Default.Aim.canceled -= Handle_AimCanceled;

        m_ActionMap.Default.Zoom.performed -= Handle_ZoomPerformed;
	}

	private void Handle_AcceleratePerformed(InputAction.CallbackContext context)
	{
		m_Throttle = context.ReadValue<float>();
	}

	private void Handle_AccelerateCanceled(InputAction.CallbackContext context)
	{
		m_Throttle = 0.0f;
	}

	private void Handle_SteerPerformed(InputAction.CallbackContext context)
	{
		m_Steer = context.ReadValue<float>();
	}

	private void Handle_SteerCanceled(InputAction.CallbackContext context)
	{
		m_Steer = 0.0f;
	}

    private void Handle_FirePerformed(InputAction.CallbackContext context)
    {
        if (m_Weapon != null)
		{
            m_Weapon.TryFire();
        }
    }


    private void Handle_FireCanceled(InputAction.CallbackContext context)
	{

	}

    private void Handle_AimPerformed(InputAction.CallbackContext context)
    {
        m_Look = context.ReadValue<Vector2>();
    }

    private void Handle_AimCanceled(InputAction.CallbackContext context)
    {
        m_Look = Vector2.zero;
    }

    private void Handle_ZoomPerformed(InputAction.CallbackContext context)
	{

	}
}