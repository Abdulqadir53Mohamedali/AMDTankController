using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class TankController : MonoBehaviour
{
	private AM_02Tank m_ActionMap; //input

	private void Awake()
	{
		m_ActionMap = new AM_02Tank();
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
		m_ActionMap.Default.Zoom.performed -= Handle_ZoomPerformed;
	}

	private void Handle_AcceleratePerformed(InputAction.CallbackContext context)
	{

	}

	private void Handle_AccelerateCanceled(InputAction.CallbackContext context)
	{

	}

	private void Handle_SteerPerformed(InputAction.CallbackContext context)
	{

	}

	private void Handle_SteerCanceled(InputAction.CallbackContext context)
	{

	}

	private void Handle_FirePerformed(InputAction.CallbackContext context)
	{

	}

	private void Handle_FireCanceled(InputAction.CallbackContext context)
	{

	}

	private void Handle_AimPerformed(InputAction.CallbackContext context)
	{

	}

	private void Handle_ZoomPerformed(InputAction.CallbackContext context)
	{

	}
}