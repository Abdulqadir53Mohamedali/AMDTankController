using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Represents one track side: collects suspension arms, computes traction,
/// and exposes drive points for applying engine force.
/// </summary>
public class TankTrack : MonoBehaviour
{
    [Header("Drive Points")]
    [SerializeField] private List<Transform> m_DrivePoints = new();

    private TankSuspesnionArm[] m_SuspensionArmsGroup;

    //0–1 how many arms are supporting the track
    public float GroundedRatio { get; private set; }

    //Average compression across grounded arms (0–1)
    public float AverageCompression { get; private set; }

    //Combined traction factor you can use for scaling forces
    public float TractionFactor => GroundedRatio * Mathf.Lerp(0.5f, 1f, AverageCompression);

    public IReadOnlyList<Transform> DrivePoints => m_DrivePoints;

    private void Awake()
    {
        m_SuspensionArmsGroup = GetComponentsInChildren<TankSuspesnionArm>();

        if (m_DrivePoints.Count == 0)
        {
            // Auto-pick children named "Wheel" as drive points 
            foreach (Transform t in GetComponentsInChildren<Transform>())
            {
                if (t.name.Contains("DriveWheel"))
                {
                    m_DrivePoints.Add(t);
                }
            }
        }
    }

    private void FixedUpdate()
    {
        UpdateTraction();
    }

    private void UpdateTraction()
    {
        if (m_SuspensionArmsGroup == null || m_SuspensionArmsGroup.Length == 0)
        {
            GroundedRatio = 0f;
            AverageCompression = 0f;
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

        GroundedRatio = (float)grounded / m_SuspensionArmsGroup.Length;
        AverageCompression = grounded > 0 ? compressionSum / grounded : 0f;
    }
}