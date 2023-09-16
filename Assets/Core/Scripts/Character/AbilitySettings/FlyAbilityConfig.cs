using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "FlyAbilityConfig.asset", menuName = "Fly Ability Config")]
public class FlyAbilityConfig : ScriptableObject
{
    public float acceleration;
    public float maxSpeed;
    public float ascentBoostAmount;
    public float ascentMaxSpeed;
    public float duration;
}
