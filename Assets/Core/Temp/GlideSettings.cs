using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "GlideSettings.asset", menuName = "Create/Glide Settings")]
public class GlideSettings : ScriptableObject
{
    [FormerlySerializedAs("tunnelFrictionBySpeed")]
    public AnimationCurve tunnelVerticalFrictionBySpeed = AnimationCurve.Linear(0, 10, 20, 10);
    public AnimationCurve tunnelHorizontalFrictionBySpeed = AnimationCurve.Linear(0, 10, 20, 10);
    public AnimationCurve airResistanceBySpeed = AnimationCurve.Linear(0, 10, 20, 10);
    public float gravityMultiplier = 1.5f;
    public float maxSpeed = 25f;
    public float debugForceScale = 1f;

    public float tunnelToForwardHorizontalForceMultiplier = 1f;
    public float tunnelToForwardVerticalForceMultiplier = 0.1f;
}
