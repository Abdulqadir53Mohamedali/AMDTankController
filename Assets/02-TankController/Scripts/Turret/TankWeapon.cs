using System;
using UnityEngine;

/// <summary>
/// Simple projectile weapon for the tank
/// - Spawns a projectile prefab at the muzzle and launches it using an impulse
/// - Enforces a fire cooldown and exposes a 0 - 1 cooldown progress value for UI
/// - Raises events when the weapon fires and when the ready/reloading state changes
/// </summary>
public class TankWeapon : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform m_Muzzle;
    [SerializeField] private GameObject m_ProjectilePrefab;

    [Header("Ballistics")]
    [SerializeField] private float m_MuzzleVelocity = 60f;

    [Header("Timing")]
    [SerializeField] public float m_FireCooldown = 0.5f;

    private float m_LastFireTime = -999f;
    private bool m_LastReadyState = true;

    public event Action Fired;

    // true = ready, false = reloading
    public event Action<bool> ReadyStateChanged; 

    public bool IsReady => Time.time >= m_LastFireTime + m_FireCooldown;
    
    // 0-1 progress where 0 = just fired, 1 = fully ready (used by HUD bar/segments)
    public float Cooldown
    {
        get
        {
            if (IsReady)
            {
                return 1f;
            }
            // Protect against divide by zero if the cooldown is set very low in the inspector
            return Mathf.Clamp01((Time.time - m_LastFireTime) / Mathf.Max(0.0001f, m_FireCooldown));
        }
    }

    private void Update()
    {
        bool ready = IsReady;


        // Publish ready state only when it changes (so the listeners don't get spammed every frame)
        if (ready != m_LastReadyState)
        {
            m_LastReadyState = ready;
            ReadyStateChanged?.Invoke(ready);
        }
    }

    public void TryFire()
    {
        if (!IsReady)
        {
            return;
        }

        if (m_Muzzle == null || m_ProjectilePrefab == null)
        {
            return;

        }

        GameObject projectile = Instantiate(m_ProjectilePrefab, m_Muzzle.position, m_Muzzle.rotation);

        // RB grabbed so we can launch
        Rigidbody projectileRb = projectile.GetComponent<Rigidbody>();
        if (projectileRb == null)
        {
            projectileRb = projectile.GetComponentInChildren<Rigidbody>();
        }

        if (projectileRb == null)
        {
            Debug.LogError($"Projectile '{projectile.name}' no rigidboDy , launch will not occur");
            return;
        }

        // Ensures the projectile is simulated by physics (in case prefab defaults were changed)
        projectileRb.isKinematic = false;
        projectileRb.constraints = RigidbodyConstraints.None;
        projectileRb.WakeUp();

        // Reset velocities so repeated reuse / odd prefab state doesn’t affect launch
        projectileRb.linearVelocity = Vector3.zero;
        projectileRb.angularVelocity = Vector3.zero;

        projectileRb.AddForce(m_Muzzle.forward * m_MuzzleVelocity, ForceMode.Impulse);

        // Update cooldown state immediately so UI reacts in the same frame
        m_LastFireTime = Time.time;
        m_LastReadyState = false;
        ReadyStateChanged?.Invoke(false);
        Fired?.Invoke();
    }
}
