using UnityEngine;

[System.Serializable]
public class BT_BeelinePathFollower : BT_PathFollower
{
    public override void Update(in BotTaskParams taskParams, ref CharacterInput input)
    {
        base.Update(in taskParams, ref input);
        if (currentTargetIndex >= targets.Count)
            return;
        Vector3 targetPosition = targets[currentTargetIndex];
        Vector3 moveIntentionDirection = targetPosition - taskParams.characterObject.transform.position;
        Vector3 intendedAim = Vector3.forward;
        input = new CharacterInput()
        {
            aimDirection = intendedAim,
        };

        input.worldMovementDirection = moveIntentionDirection;
    }
}