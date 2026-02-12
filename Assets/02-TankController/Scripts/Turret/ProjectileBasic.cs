using UnityEngine;

public class ProjectileBasic : MonoBehaviour
{
    private int m_DefaultLayer;

    private void Awake()
    {
        m_DefaultLayer = LayerMask.NameToLayer("Default"); 
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.layer == m_DefaultLayer)
        {
            Destroy(gameObject);
        }
    }
}
