﻿using UnityEngine;

[CreateAssetMenu(fileName = "GlideSettings.asset", menuName = "Create/Glide Settings")]
public class GlideSettings : ScriptableObject
{
    public AnimationCurve tunnelVerticalFrictionBySpeed = AnimationCurve.Linear(0, 10, 20, 10);
    public AnimationCurve tunnelHorizontalFrictionBySpeed = AnimationCurve.Linear(0, 10, 20, 10);
    public AnimationCurve airResistanceBySpeed = AnimationCurve.Linear(0, 10, 20, 10);
    public float gravityMultiplier = 1.5f;
    public float maxSpeed = 25f;
    public float debugForceScale = 1f;
    public float verticalTurnLimit = 0.3f;

    public float tunnelToForwardHorizontalForceMultiplier = 1f;
    public float tunnelToForwardVerticalForceMultiplier = 0.1f;
}
