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

            // 🛑 紅綠燈煞車系統：如果前面的點是「停止線」且「目前是紅燈」
            if (targetNode.isStopLine && targetNode.currentIsRed)
            {
                // 計算車頭到停止線的距離
                float distanceToNode = Vector3.Distance(transform.position, targetNode.transform.position);
                
                // 如果距離停止線小於 4 公尺，強制煞車！
                if (distanceToNode < 4f)
                {
                    agent.isStopped = true; 
                    return; // 終止這回合的動作，車子乖乖等待
                }
            }
            else
            {
                // 綠燈，或是普通路段，解除煞車繼續開
                agent.isStopped = false; 
            }

            // 🚗 原本的尋路邏輯
            if (!agent.pathPending && agent.remainingDistance < 2f)
            {
                TrafficNode nextNode = targetNode.GetNextNode(); 
                
                if (nextNode != null)
                {
                    targetNode = nextNode; 
                    agent.SetDestination(targetNode.transform.position);
                }
                else
                {
                    Destroy(gameObject); // 沒路了就消失
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