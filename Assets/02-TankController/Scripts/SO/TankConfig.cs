using UnityEngine;

[CreateAssetMenu(fileName = "TankConfig", menuName = "Scriptable Objects/TankConfig")]
public class TankConfig : ScriptableObject
{
    [Header("Drive")]
    // Maximum track force in which a track can push agaianst the ground
    public float maxTrackForce = 2500f;
    // Limit to max speed
    public float maxSpeed = 12f;


    // Hookes Law = 
    [Header("Suspension")]
    // Length of spring when not in use 
    public float restLength = 0.6f;
    // How hard is it to squish the spring , bigger vlaue leads to stiffer more gorunded spring , lesser // softer lead  loose/floaty spring
    public float springStiffnes = 350000f;    
    // Pushback force on spring 
    public float damperStrength = 4500f; 
    // How much the spring has been squished | Displacment , how mch ahs it strehced form the natural length
    public float maxCompression  = 0.3f;

    public float wheelRadius = 0.18f;  

    //[Header("Rotation")]
    //public float turretSpeed;
    //public float hullRotateLag;

}
