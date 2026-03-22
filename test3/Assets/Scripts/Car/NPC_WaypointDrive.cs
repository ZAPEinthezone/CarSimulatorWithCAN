using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class NPC_WaypointDrive : MonoBehaviour
{
    private NavMeshAgent agent;

    [Header("目前的導航目標")]
    public TrafficNode targetNode; // 現在只要記住「下一個點」是誰就好了！

    [Header("防撞雷達")]
    public float sensorLength = 6f;
    public Vector3 sensorOffset = new Vector3(0, 0.5f, 0.5f);

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();

        if (targetNode != null)
        {
            agent.Warp(transform.position);
            agent.SetDestination(targetNode.transform.position);
        }
    }

    void Update()
    {
        if (targetNode == null) return;

        CheckForwardCollision();

        // 抵達目標節點時，向節點「問路」！
        if (!agent.pathPending && agent.remainingDistance < 2f)
        {
            TrafficNode nextNode = targetNode.GetNextNode(); // 老闆，下一步去哪？

            if (nextNode != null)
            {
                // 如果還有路，就繼續往下一站開
                targetNode = nextNode;
                agent.SetDestination(targetNode.transform.position);
            }
            else
            {
                // 🚗 核心魔法：如果 nextNode 是空的 (代表沒路了)
                // 任務完成，直接把這台車從遊戲中刪除！
                Destroy(gameObject);
            }
        }
    }

    void CheckForwardCollision()
    {
        RaycastHit hit;
        if (Physics.Raycast(transform.TransformPoint(sensorOffset), transform.forward, out hit, sensorLength))
        {
            if (hit.collider.CompareTag("Car"))
            {
                agent.isStopped = true; // 遇到前車乖乖煞車
                Debug.DrawLine(transform.position, hit.point, Color.red);
                return;
            }
        }

        agent.isStopped = false;
        Debug.DrawLine(transform.position, transform.position + transform.forward * sensorLength, Color.green);
    }
}