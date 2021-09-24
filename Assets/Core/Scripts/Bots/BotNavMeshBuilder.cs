using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class BotNavMeshBuilder : MonoBehaviour
{
    public bool regenerate = false;

    public struct NavLink
    {
        public Vector3 startPosition;
        public Vector3 endPosition;
        public float costModifier;
    }

    // not actually serialized but still used at edit time for preview
    private List<NavLink> navLinks = new List<NavLink>();

    private void OnValidate()
    {
        if (regenerate)
        {
            regenerate = false;
            Regenerate();
        }
    }

    private void Awake()
    {
        Regenerate();

        foreach (NavLink link in navLinks)
        {
            NavMesh.AddLink(new NavMeshLinkData()
            {
                startPosition = link.startPosition,
                endPosition = link.endPosition,
                costModifier = link.costModifier,
            });
        }
    }

    private void Regenerate()
    {
        navLinks.Clear();

        // Generate nav mesh links
        foreach (Spring spring in FindObjectsOfType<Spring>())
        {
            float speed = 20f;

            for (float direction = 0; direction < 359.9f; direction += 45f)
            {
                for (float t = 0; t < 1; t += 0.2f)
                {
                    Vector3 offset = new Vector3(Mathf.Sin(direction * Mathf.Deg2Rad) * t * speed, t * spring.springForce + t * t * -9.57f, Mathf.Cos(direction * Mathf.Deg2Rad) * t * speed);
                    if (NavMesh.SamplePosition(spring.transform.position + offset, out UnityEngine.AI.NavMeshHit hit, 100, ~0))
                    {
                        navLinks.Add(new NavLink()
                        {
                            startPosition = spring.transform.position,
                            endPosition = hit.position,
                            costModifier = t,
                        });
                    }
                }
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.blue;
        for (int i = 0; i < navLinks.Count; i++)
        {
            Gizmos.DrawLine(navLinks[i].startPosition + Vector3.up * 0.5f, navLinks[i].endPosition + Vector3.up * 0.5f);
        }
    }
}
