using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class NPC_WaypointDrive : MonoBehaviour
{
    protected NavMeshAgent agent;
    public TrafficNode targetNode; 

    [Header("防撞雷達")]
    public float sensorLength = 6f;
    public Vector3 sensorOffset = new Vector3(0, 0.5f, 2.5f);

    protected bool isYielding = false;
    protected float originalSpeed;

    void Start() {
        agent = GetComponent<NavMeshAgent>();
        if (agent != null) originalSpeed = agent.speed;
        if (targetNode != null) agent.SetDestination(targetNode.transform.position);
    }

    // 🚑 救護車呼叫：順向避讓 (靠右)
    public void YieldForAmbulance(Vector3 ambulancePos) {
        if (isYielding) return;
        isYielding = true;
        
        // 往右偏 1.2 米 (半個車道)
        Vector3 sidePos = transform.position + (transform.right * 1.2f);
        NavMeshHit hit;
        if (NavMesh.SamplePosition(sidePos, out hit, 2.0f, NavMesh.AllAreas)) {
            agent.SetDestination(hit.position);
        }

        agent.speed = originalSpeed * 0.5f; 
        CancelInvoke("ReturnToTrack");
        Invoke("ReturnToTrack", 4.0f);
    }

    // 🚑 救護車呼叫：遠處路口煞停
    public void IntersectionYield() {
        if (isYielding) return;
        isYielding = true;
        agent.isStopped = true;
        
        CancelInvoke("ReturnToTrack");
        Invoke("ReturnToTrack", 5.0f);
    }

    void ReturnToTrack() {
        isYielding = false;
        agent.isStopped = false;
        agent.speed = originalSpeed;
        if (targetNode != null) agent.SetDestination(targetNode.transform.position);
    }

    protected virtual void Update() {
        if (targetNode == null || agent == null) return;

        if (isYielding) {
            // 如果是在路邊避讓，到點後停下
            if (!agent.isStopped && agent.remainingDistance < 0.5f) agent.isStopped = true;
            return;
        }

        CheckForwardCollision();
        if (agent.isStopped) return;

        // 紅綠燈判斷
        if (targetNode.isStopLine && targetNode.currentIsRed) {
            if (Vector3.Distance(transform.position, targetNode.transform.position) < 5f) {
                agent.isStopped = true; return; 
            }
        } else {
            agent.isStopped = false; 
        }

        // 尋路邏輯
        if (!agent.isStopped && !agent.pathPending && agent.remainingDistance < 2f) {
            TrafficNode nextNode = targetNode.GetNextNode(); 
            if (nextNode != null) {
                targetNode = nextNode;
                agent.SetDestination(targetNode.transform.position);
            } else {
                Destroy(gameObject);
            }
        }
    }

    protected virtual void CheckForwardCollision() {
        RaycastHit hit;
        if (Physics.Raycast(transform.TransformPoint(sensorOffset), transform.forward, out hit, sensorLength)) {
            if (hit.collider.CompareTag("Car")) {
                agent.isStopped = true;
                return;
            }
        }
        agent.isStopped = false;
    }
}