using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class NPC_WaypointDrive : MonoBehaviour
{
    protected NavMeshAgent agent;

    [Header("目前的導航目標")]
    public TrafficNode targetNode; 

    [Header("防撞雷達")]
    public float sensorLength = 6f;
    public Vector3 sensorOffset = new Vector3(0, 0.5f, 2.5f); // Z 預設調大，防止自撞

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        if (targetNode != null)
        {
            agent.SetDestination(targetNode.transform.position);
        }
    }

    protected virtual void Update()
    {
        if (targetNode == null) return;

        CheckForwardCollision();

        // 如果雷達叫我停，後面紅綠燈就不用看了
        if (agent.isStopped) return;

        // 🛑 紅綠燈判斷
        if (targetNode.isStopLine && targetNode.currentIsRed)
        {
            float distanceToNode = Vector3.Distance(transform.position, targetNode.transform.position);
            if (distanceToNode < 5f)
            {
                agent.isStopped = true; 
                return; 
            }
        }
        else
        {
            agent.isStopped = false; 
        }

        // 🚗 尋路切換點
        if (!agent.isStopped && !agent.pathPending && agent.remainingDistance < 2f)
        {
            TrafficNode nextNode = targetNode.GetNextNode(); 
            if (nextNode != null)
            {
                targetNode = nextNode; 
                agent.SetDestination(targetNode.transform.position);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }

    protected virtual void CheckForwardCollision()
    {
        RaycastHit hit;
        if (Physics.Raycast(transform.TransformPoint(sensorOffset), transform.forward, out hit, sensorLength))
        {
            if (hit.collider.CompareTag("Car"))
            {
                // 🚑 關鍵：偵測到前方如果是「緊急模式」的救護車，立刻停讓
                NPC_AmbulanceDrive amb = hit.collider.GetComponentInParent<NPC_AmbulanceDrive>();
                if (amb != null && amb.isEmergency)
                {
                    agent.isStopped = true;
                    Debug.DrawLine(transform.position, hit.point, Color.yellow);
                    return;
                }

                agent.isStopped = true;
                Debug.DrawLine(transform.position, hit.point, Color.red);
                return;
            }
        }
        agent.isStopped = false;
        Debug.DrawRay(transform.TransformPoint(sensorOffset), transform.forward * sensorLength, Color.green);
    }
}