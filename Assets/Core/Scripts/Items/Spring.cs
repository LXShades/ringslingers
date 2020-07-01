using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Spring : MonoBehaviour
{
    [Header("Spring properties")]
    // red: 32, yellow: 20, blue: 11
    public float springForce = (32 * 35);

    [Header("Sounds")]
    public GameSound springSound = new GameSound();

    private Animator animator;

    void Start()
    {
        animator = GetComponent<Animator>();
    }

    private void OnTriggerStay(Collider other)
    {
        CharacterMovement movement = other.GetComponent<CharacterMovement>();

        if (movement && movement.velocity.y < springForce * 0.7f)
        {
            movement.SpringUp(springForce, transform.up);
            animator.SetTrigger("DoSpring");

            Debug.Log($"SPRING@{Frame.current.time.ToString("#.00")}");
            //if (!Netplay.singleton.freezeReplay || !Netplay.singleton.replayMode)
            //   Debug.Break();

            GameSounds.PlaySound(gameObject, springSound);
        }
    }
}
