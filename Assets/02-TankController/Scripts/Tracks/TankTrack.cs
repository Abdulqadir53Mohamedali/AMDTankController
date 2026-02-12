using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Represents one track side: collects suspension arms, computes traction,
/// and exposes drive points for applying engine force
/// </summary>
public class TankTrack : MonoBehaviour
{
    [Header("Drive Points")]
    [SerializeField] private List<Transform> m_DrivePoints = new();

    // Cached arms so we don't repeatedly search the hierarchy every physics tick
    private TankSuspesnionArm[] m_SuspensionArmsGroup;

    //0–1 how many arms are supporting the track
    public float m_GroundedRatio { get; private set; }

    //Average compression across grounded arms (0–1)
    public float m_AverageCompression { get; private set; }


    // Combined traction scalar:
    // - More grounded arms increases traction.
    // - More compression increases traction slightly
    public float TractionFactor => m_GroundedRatio * Mathf.Lerp(0.5f, 1f, m_AverageCompression);

    // Read-only view so other scripts can use drive points but not replace the list at runtime
    public IReadOnlyList<Transform> DrivePoints => m_DrivePoints;

    private void Awake()
    {
        m_SuspensionArmsGroup = GetComponentsInChildren<TankSuspesnionArm>();
    }

    private void FixedUpdate()
    {
        UpdateTraction();
    }

    private void UpdateTraction()
    {
        // If no arms exist, traction is effectively zero
        if (m_SuspensionArmsGroup == null || m_SuspensionArmsGroup.Length == 0)
        {
            m_GroundedRatio = 0f;
            m_AverageCompression = 0f;
            return;
        }

        int grounded = 0;
        float compressionSum = 0f;

        foreach (var arm in m_SuspensionArmsGroup)
        {
            if (arm.IsGrounded)
            {
                grounded++;
                compressionSum += arm.NormalisedCompression;
            }
        }

        m_GroundedRatio = (float)grounded / m_SuspensionArmsGroup.Length;

        // Only average compression across grounded arms, airborne arms shouldn’t dilute the value
        m_AverageCompression = grounded > 0 ? compressionSum / grounded : 0f;
    }
}