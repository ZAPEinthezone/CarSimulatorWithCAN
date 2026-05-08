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
        public bool IsYielding => isYielding;

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

   public void YieldForAmbulance(Vector3 ambulancePos, Vector3 ambulanceForward) {
        // 如果已經在閃了，就不要理會
        if (isYielding || currentYieldState != YieldState.None) return;

        // 距離與方向過濾
        float dist = Vector3.Distance(transform.position, ambulancePos);
        if (dist > 35f) return;
        if (Vector3.Dot(transform.forward, ambulanceForward) < 0.5f) return;

        // 💡 關鍵：啟動 S 型避讓
        StartCoroutine(S_CurveYieldRoutine());
    }

private IEnumerator S_CurveYieldRoutine() {
    isYielding = true;
    currentYieldState = YieldState.MovingToSide;
    
    agent.isStopped = false; 
    agent.speed = originalSpeed * 1.2f; 
    
    // 💡 降低加速度與轉向速度，這是徹底防止「原地繞圈、神經質抽動」的關鍵
    agent.acceleration = 60f; 
    agent.angularSpeed = 750f; 

    // 1. 探測極限偏移量 (確保不撞牆)
    float targetOffset = 3.8f; // 目標偏移 3 米 (約一個車道)
    NavMeshHit nmHit;
    if (agent.Raycast(transform.position + transform.right * targetOffset, out nmHit)) {
        targetOffset = Mathf.Max(0.5f, nmHit.distance - 0.8f);
    }

    float currentOffset = 0f;
    float timer = 0f;
    float yieldDuration = 6.0f; // 避讓總時間

    // 2. 進入動態避讓循環
    while (timer < yieldDuration) {
        timer += Time.deltaTime;

        if (targetNode != null) {
            // 💡 【防繞圈機制】
            // 如果距離 Node 小於 5 米，或者 Node 已經在車子側面/後方 (Dot < 0.2)
            // 就立刻提早切換到下一個 Node，絕對不讓車子有機會回頭找點
            Vector3 toNode = targetNode.transform.position - transform.position;
            if (toNode.magnitude < 5.0f || Vector3.Dot(transform.forward, toNode.normalized) < 0.2f) {
                TrafficNode next = targetNode.GetNextNode();
                if (next != null) {
                    targetNode = next;
                    toNode = targetNode.transform.position - transform.position; // 更新方向
                }
            }

            // 💡 【弧度跟隨機制】
            // 計算目前這段路 (從車子到 Node) 的絕對右側
            Vector3 roadDir = toNode.normalized;
            Vector3 roadRight = Vector3.Cross(Vector3.up, roadDir).normalized;

            // 💡 【完美 S 型核心：Lerp 平滑過渡】
            // 讓 currentOffset 像踩油門一樣，平滑地從 0 逐漸增加到 targetOffset
            currentOffset = Mathf.Lerp(currentOffset, targetOffset, Time.deltaTime * 4.0f);

            // 最終目的地 = 目標 Node 本身的位置 + 往右偏移
            // 因為 targetNode 會沿著道路彎曲，所以這個偏移點也會完美貼合道路弧度！
            Vector3 dynamicTarget = targetNode.transform.position + (roadRight * currentOffset);
            
            // 每幀微調目的地，Agent 就會畫出一條極其柔順的曲線
            agent.SetDestination(dynamicTarget);
        }
        
        // 避讓中期 (2秒後)，開始平滑降速讓救護車通過
        if (timer > 2.0f) {
            agent.speed = Mathf.Lerp(agent.speed, originalSpeed * 0.4f, Time.deltaTime);
        }

        yield return null;
    }

    ReturnToTrack();
}

protected virtual void ReturnToTrack() {
    
    currentYieldState = YieldState.None;
    isYielding = false;
    
    agent.isStopped = false;
    agent.speed = originalSpeed;
    agent.acceleration = 12f; // 恢復時稍微加速

    if (targetNode != null) {
        // 💡 核心：如果目前的點已經在屁股後面，就跳到下一個
        int safety = 0;
        while (safety < 5) {
            Vector3 toNode = targetNode.transform.position - transform.position;
            // 只要 Dot < 0.2 代表點已經在側面或後方，直接放棄追這個點
            if (Vector3.Dot(transform.forward, toNode.normalized) < 0.2f || toNode.magnitude < 5.0f) {
                TrafficNode next = targetNode.GetNextNode();
                if (next != null) {
                    targetNode = next;
                    safety++;
                } else break;
            } else break;
        }
        
        agent.ResetPath(); // 徹底清除舊的 S 型路徑殘留
        agent.SetDestination(targetNode.transform.position);
    }
}
    // 🚑 救護車呼叫：路口強制清空 (救護車靠近中)
    // 請確保傳入 ambulanceForward 參數
   public void IntersectionYield(Vector3 ambulanceForward) {
    // 💡 修正：如果我已經在執行 S 型靠右避讓了，路口的「停下」指令直接無視！
    // 這樣就不會出現「先停在路中間才靠右」的笨動作
    if (isYielding || currentYieldState != YieldState.None) return;

    float directionMatch = Vector3.Dot(transform.forward, ambulanceForward);
    if (directionMatch < 0.5f) return;

    // 執行停等邏輯 (只有在沒地方閃的時候才跑這裡)
    if (IsInIntersection()) {
        agent.speed = originalSpeed * 0.5f; 
        agent.isStopped = false;
    } else {
        agent.isStopped = true;
        agent.velocity = Vector3.zero;
    }
    
    isYielding = true;
    // 啟動自動恢復計時器
    StartCoroutine(WaitAndReturn(5.0f)); 
}

// 輔助協程：等救護車過去後自動恢復
private IEnumerator WaitAndReturn(float delay) {
    yield return new WaitForSeconds(delay);
    ReturnToTrack();
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

    // --- 🚨 第一層級：避讓狀態監控 ---
    // 只要是在避讓（S型換道中 或 避讓後緩行），Update 絕對不插手導航與煞車
    if (currentYieldState != YieldState.None || isYielding) {
        // 在此狀態下，所有的 agent.SetDestination 和 isStopped 都由協程 (S_CurveYieldRoutine) 控制
        // 這樣可以保證「一邊開一邊滑過去」，不會被 Update 的停止指令卡住
        return; 
    }

    // --- 🚦 第二層級：正常行駛判斷 (紅綠燈與防撞) ---
    // 只有在「非避讓」狀態下，才執行這些判定
    HandleTrafficLights();    // 檢查紅燈
    CheckForwardCollision(); // 檢查前面有沒有車

    // --- 🛣️ 第三層級：節點導航邏輯 ---
    // 如果沒被雷達或紅燈叫停，就繼續追下一個 Node
    if (!agent.isStopped) {
        // 如果快要到達當前目標 Node (距離 < 2.5米)
        if (!agent.pathPending && agent.remainingDistance < 2.5f) {
            TrafficNode nextNode = targetNode.GetNextNode(); 
            if (nextNode != null) {
                targetNode = nextNode;
                agent.SetDestination(targetNode.transform.position);
            } else {
                // 沒路了就刪除車子
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