using UnityEngine;
using UnityEngine.AI;
using System.Collections;

[RequireComponent(typeof(NavMeshAgent))]
public class NPC_WaypointDrive : MonoBehaviour
{
    protected NavMeshAgent agent;
    public TrafficNode targetNode; 

    [Header("行車與雷達")]
    public float sensorLength = 10f;
    public Vector3 sensorOffset = new Vector3(0, 0.5f, 2.5f);

    protected bool isYielding = false;
    protected float originalSpeed;

    private enum YieldState { None, MovingToSide, AligningForward }
    private YieldState currentYieldState = YieldState.None;

    protected virtual void Awake() {
        agent = GetComponent<NavMeshAgent>();
    }

    protected virtual void Start() {
        if (agent != null) {
            originalSpeed = agent.speed;
            if (targetNode != null) agent.SetDestination(targetNode.transform.position);
        }
    }

    // 🚑 救護車呼叫：S 型避讓邏輯
    public void YieldForAmbulance(Vector3 ambulancePos, Vector3 ambulanceForward) {
        if (currentYieldState != YieldState.None) return;

        // 1. 【嚴格過濾】
        // Dot > 0.4 代表同向；Dot < 0 代表對向或橫向。
        // 這樣對向車道就「絕對不會」停下來。
        float directionMatch = Vector3.Dot(transform.forward, ambulanceForward);
        if (directionMatch < 0.5f) return; 

        // 2. 開始執行避讓
        StartCoroutine(S_CurveYieldRoutine());
    }

    private IEnumerator S_CurveYieldRoutine() {
        agent.isStopped = false; 
        isYielding = true;
        currentYieldState = YieldState.MovingToSide;
        
        agent.speed = originalSpeed * 1.5f; 
        agent.acceleration = 120f; 

        // 💡 智慧邊界探測：從車中心往右量 1.5 米
        float desiredRight = 0.6f; 
        NavMeshHit nmHit;
        
        // 如果右邊 1.5 米內有任何 NavMesh 邊界 (邊緣)
        if (agent.Raycast(transform.position + transform.right * desiredRight, out nmHit)) {
            // 抓到邊界了！將偏移量縮減為邊界距離的 60%，留出 40% 的緩衝區給車身
            // 這能保證車體絕對不會「蹭」上人行道
            desiredRight = nmHit.distance * 0.2f;
        }

        // 計算目標點
        Vector3 sideTarget = transform.position + (transform.right * desiredRight) + (transform.forward * 10.0f);
        
        // 確保點是在路面上
        if (NavMesh.SamplePosition(sideTarget, out nmHit, 2.0f, NavMesh.AllAreas)) {
            sideTarget = nmHit.position;
        }

        agent.SetDestination(sideTarget);

        // 平滑切換 (預判轉正)
        while (agent.remainingDistance > 3.5f && currentYieldState == YieldState.MovingToSide) {
            yield return null;
        }

        currentYieldState = YieldState.AligningForward;
        agent.SetDestination(transform.position + (transform.forward * 25.0f));
        agent.speed = originalSpeed * 0.5f; 

        yield return new WaitForSeconds(3.0f);
        ReturnToTrack();
    }

    // 🚑 救護車呼叫：路口強制清空 (救護車靠近中)
    // 請確保傳入 ambulanceForward 參數
    public void IntersectionYield(Vector3 ambulanceForward) {
        if (isYielding) return;

        // 💡 關鍵修正：路口廣播也要過濾對向車！
        float directionMatch = Vector3.Dot(transform.forward, ambulanceForward);
        if (directionMatch < 0.5f) return; // 對向車直接無視，繼續開

        if (IsInIntersection()) {
            agent.speed = originalSpeed * 0.5f; 
            agent.isStopped = false;
        } else {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }
        
        isYielding = true;
        CancelInvoke("ReturnToTrack");
        Invoke("ReturnToTrack", 5.0f);
    }

    // 合併後的恢復函數
    protected virtual void ReturnToTrack() {
        StopAllCoroutines();
        ResetToNormal();
    }

    protected void ResetToNormal() {
        currentYieldState = YieldState.None;
        isYielding = false;
        agent.isStopped = false;
        agent.speed = originalSpeed;
        agent.acceleration = 8f; 
        if (targetNode != null) agent.SetDestination(targetNode.transform.position);
    }

    protected virtual void Update() {
        if (targetNode == null || agent == null) return;

        // 💡 修正 A：只要在執行 S 型避讓 (currentYieldState 不是 None)，Update 就徹底交出控制權
        // 這能防止 S 型路徑計算到一半被下方的 isYielding 邏輯截斷
        if (currentYieldState != YieldState.None) return;

        // 一般紅燈/路口避讓停止
        if (isYielding) {
            // 💡 修正 B：這裡必須判斷是否「不在執行 S 型路徑」中，才執行路口停下
            // 這樣能解決「偵測到救護車先停下來才避讓」的問題
            if (!agent.pathPending && agent.remainingDistance < 0.8f) {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
            }
            return;
        }

        // 只有在非避讓狀態下，才執行雷達與紅綠燈判定
        CheckForwardCollision();
        HandleTrafficLights();

        if (!agent.isStopped) {
            if (!agent.pathPending && agent.remainingDistance < 2.5f) {
                TrafficNode nextNode = targetNode.GetNextNode(); 
                if (nextNode != null) {
                    targetNode = nextNode;
                    agent.SetDestination(targetNode.transform.position);
                } else {
                    Destroy(gameObject);
                }
            }
        }
    }
    protected virtual void HandleTrafficLights() {
        if (isYielding) return; 

        if (targetNode.isStopLine && targetNode.currentIsRed) {
            float dist = Vector3.Distance(transform.position, targetNode.transform.position);
            if (dist < 6f) {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
                return;
            }
        }

        if (!HasObstacleInFront()) {
            agent.isStopped = false;
        }
    }

    protected virtual void CheckForwardCollision() {
        RaycastHit hit;
        if (Physics.Raycast(transform.TransformPoint(sensorOffset), transform.forward, out hit, 10f)) {
            if (hit.collider.CompareTag("Car")) {
                float dist = hit.distance;
                if (dist < 4f) {
                    if (IsInIntersection()) {
                        agent.isStopped = false;
                        agent.speed = originalSpeed * 0.4f;
                    } else {
                        agent.isStopped = true;
                        agent.velocity = Vector3.zero;
                    }
                } else {
                    agent.speed = originalSpeed * 0.5f; 
                }
                return;
            }
        }
        
        if (!isYielding) {
            agent.isStopped = false;
            agent.speed = originalSpeed;
        }
    }

    protected bool HasObstacleInFront() {
        RaycastHit hit;
        if (Physics.Raycast(transform.TransformPoint(sensorOffset), transform.forward, out hit, sensorLength)) {
            if (hit.collider.CompareTag("Car")) return true;
        }
        return false;
    }

    protected bool IsInIntersection() {
        if (targetNode == null) return false;
        float dist = Vector3.Distance(transform.position, targetNode.transform.position);
        return dist > 4f && !targetNode.isStopLine;
    }

    private void OnDrawGizmosSelected() {
        Gizmos.color = Color.red;
        Vector3 sensorPos = transform.TransformPoint(sensorOffset);
        Gizmos.DrawLine(sensorPos, sensorPos + transform.forward * sensorLength);
    }
}