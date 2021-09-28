using UnityEngine;

[CreateAssetMenu(fileName = "GlideSettings.asset", menuName = "Glide Settings")]
public class GlideSettings : ScriptableObject
{
    public float minSpeed = 5f;
    public float startSpeed = 8f;
    public float maxSpeed = 25f;
    public float climbSpeed = 3f;

    public AnimationCurve accelerationBySpeed = AnimationCurve.Linear(0f, 1f, 25f, 1f);
    public AnimationCurve turnSpeedBySpeed = AnimationCurve.Linear(0f, 180f, 25f, 180f);
    public AnimationCurve fallSpeedBySpeed = AnimationCurve.Linear(0f, 5f, 25f, 5f);
}
