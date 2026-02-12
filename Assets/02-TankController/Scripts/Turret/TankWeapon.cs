using System;
using UnityEngine;

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
    public event Action<bool> ReadyStateChanged; // true = ready, false = reloadin
    public bool IsReady => Time.time >= m_LastFireTime + m_FireCooldown;
    public float Cooldown01
    {
        get
        {
            if (IsReady) return 1f;
            return Mathf.Clamp01((Time.time - m_LastFireTime) / Mathf.Max(0.0001f, m_FireCooldown));
        }
    }

    private void Update()
    {
        bool ready = IsReady;
        if (ready != m_LastReadyState)
        {
            m_LastReadyState = ready;
            ReadyStateChanged?.Invoke(ready);
        }
    }

    public void TryFire()
    {
        if (!IsReady)
            return;

        if (m_Muzzle == null || m_ProjectilePrefab == null)
            return;

        GameObject projectile = Instantiate(m_ProjectilePrefab, m_Muzzle.position, m_Muzzle.rotation);

        Rigidbody rb = projectile.GetComponent<Rigidbody>();
        if (rb == null)
            rb = projectile.GetComponentInChildren<Rigidbody>();

        if (rb == null)
        {
            Debug.LogError($"Projectile '{projectile.name}' has no Rigidbody. Cannot launch.");
            return;
        }

        rb.isKinematic = false;
        rb.constraints = RigidbodyConstraints.None;
        rb.WakeUp();

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        rb.AddForce(m_Muzzle.forward * m_MuzzleVelocity, ForceMode.Impulse);

        m_LastFireTime = Time.time;
        m_LastReadyState = false;
        ReadyStateChanged?.Invoke(false);
        Fired?.Invoke();
    }
}
