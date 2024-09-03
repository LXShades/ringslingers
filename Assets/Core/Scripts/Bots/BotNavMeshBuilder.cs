using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

public class BotNavMeshBuilder : MonoBehaviour
{
    public bool regenerate = false;

    public int agentTypeId = 1;

    [Header("Generation settings")]
    public float jumpHeight = 1.5f;
    public float jumpDistance = 7f;
    public float jumpCostModifier = 1f;

    public struct NavLink
    {
        public Vector3 startPosition;
        public Vector3 endPosition;
        public float costModifier;
    }

    // not actually serialized but still used at edit time for preview
    private List<NavLink> navLinks = new List<NavLink>();
    private List<NavMeshLinkInstance> navLinkInstances = new List<NavMeshLinkInstance>();
    private static Dictionary<int, List<NavLink>> navLinkLookup = new Dictionary<int, List<NavLink>>();

    private bool hasGenerated = false;

    private NavMeshSurface navMeshSurface;

    public static void EnsureInit()
    {
        foreach (BotNavMeshBuilder botNavMeshBuilder in FindObjectsByType<BotNavMeshBuilder>(FindObjectsSortMode.None))
        {
            if (!botNavMeshBuilder.hasGenerated)
                botNavMeshBuilder.Regenerate();
        }
    }

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
    }

    private void OnDestroy()
    {
        ClearGeneratedNavLinks();
    }

    private void Regenerate()
    {
        navMeshSurface = GetComponent<NavMeshSurface>();
        ClearGeneratedNavLinks();

        // Generate the base nav mesh (todo: we should only do that if there are bots, right?)
        navMeshSurface.BuildNavMesh();

        // Use existing drop-down points to create possible jump-up points
        // NVM LMAO UNITY DOESN'T LET YOU READ THOSE AND HASN'T FOR NEARLY 10 YEARS
        // wtf?? why is it so hard to let the user simply access the nav mesh links?
        // ??????????
        // guess we'll make our own
        NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();

        var vertices = triangulation.vertices;
        for (int vA = 0; vA < vertices.Length; vA++)
        {
            for (int vB = vA + 1; vB < vertices.Length; vB++)
            {
                var pointA = vertices[vA];
                var pointB = vertices[vB];

                if (pointB.y != pointA.y && Mathf.Abs(pointB.y - pointA.y) < jumpHeight)
                {
                    if (Vector3.Distance(pointA, pointB) < jumpDistance)
                    {
                        Vector3 startPosition = pointB.y >= pointA.y ? pointA : pointB;
                        Vector3 endPosition = pointB.y >= pointA.y ? pointB : pointA;

                        Vector3 upwardRay = new Vector3(startPosition.x, endPosition.y + 0.01f, startPosition.z) - startPosition;
                        Vector3 alongRay = endPosition - (startPosition + upwardRay);

                        // ensure it's not a jump through a wall or ceiling
                        if (Physics.Raycast(startPosition, upwardRay, upwardRay.magnitude, ~0, QueryTriggerInteraction.Ignore) ||
                            Physics.Raycast(startPosition + upwardRay, alongRay, alongRay.magnitude, ~0, QueryTriggerInteraction.Ignore))
                        {
                            continue;
                        }

                        navLinks.Add(new NavLink()
                        {
                            startPosition = startPosition,
                            endPosition = endPosition,
                            costModifier = jumpCostModifier
                        });
                    }
                }
            }
        }

        // Generate nav mesh links from areas that can be reached by springs
        foreach (Spring spring in FindObjectsByType<Spring>(FindObjectsSortMode.None))
        {
            float speed = 20f;

            for (float direction = 0; direction < 359.9f; direction += 45f)
            {
                for (float t = 0; t < 1; t += 0.2f)
                {
                    Vector3 offset = new Vector3(Mathf.Sin(direction * Mathf.Deg2Rad) * t * speed, t * spring.springForce + t * t * -9.57f, Mathf.Cos(direction * Mathf.Deg2Rad) * t * speed);
                    if (NavMesh.SamplePosition(spring.transform.position + offset, out NavMeshHit hit, 100, ~0))
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

        foreach (NavLink link in navLinks)
        {
            navLinkInstances.Add(NavMesh.AddLink(new NavMeshLinkData()
            {
                startPosition = link.startPosition,
                endPosition = link.endPosition,
                costModifier = link.costModifier,
                agentTypeID = agentTypeId
            }));

            int startHash = MakeHashFromPosition(link.startPosition);
            int endHash = MakeHashFromPosition(link.endPosition);
            if (!navLinkLookup.TryGetValue(startHash, out List<NavLink> startLinks))
                navLinkLookup.Add(startHash, startLinks = new List<NavLink>());
            if (!navLinkLookup.TryGetValue(endHash, out List<NavLink> endLinks))
                navLinkLookup.Add(endHash, endLinks = new List<NavLink>());
            startLinks.Add(link);
            endLinks.Add(link);
        }

        navMeshSurface.UpdateNavMesh(navMeshSurface.navMeshData);
        hasGenerated = true;
    }

    private void ClearGeneratedNavLinks()
    {
        foreach (NavMeshLinkInstance linkInstance in navLinkInstances)
            NavMesh.RemoveLink(linkInstance); // [LF] another reason to complain, there's no clear nav links function???? we need to do this???
        navLinkInstances.Clear();
        navLinks.Clear();
        navLinkLookup.Clear();
    }

    public static bool TryGetNavLink(Vector3 source, Vector3 target, out NavLink link)
    {
        int startHash = MakeHashFromPosition(source);
        const float tolerance = 0.1f;
        
        if (navLinkLookup.TryGetValue(startHash, out List<NavLink> navLinksAtPosition))
        {
            foreach (var theLink in navLinksAtPosition)
            {
                if (Vector3.Distance(theLink.startPosition, source) < tolerance && Vector3.Distance(theLink.startPosition, target) < tolerance)
                {
                    link = theLink;
                    return true;
                }
            }
        }

        link = default;
        return false;
    }

    private static int MakeHashFromPosition(Vector3 position)
    {
        const float quantization = 0.25f;
        int xInt = (int)(position.x / quantization);
        int yInt = (int)(position.y / quantization);
        int zInt = (int)(position.z / quantization);

        return (xInt << 12) ^ (xInt ^ yInt) ^ zInt | (yInt << 16); // rationale: i dunno
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
