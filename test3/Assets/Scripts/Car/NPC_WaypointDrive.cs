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

        // 距離與方向過濾 (救護車在後方才需要讓)
        float dist = Vector3.Distance(transform.position, ambulancePos);
        if (dist > 55f) return; // 💡 修正：與救護車的 detectRadius (55f) 保持一致，確保能收到遠方訊號
        Vector3 toAmbulance = (ambulancePos - transform.position).normalized;
        // 關鍵：判斷救護車是否在「我後面」，而不是同向。Dot < -0.3 代表在我車尾方向
        if (Vector3.Dot(transform.forward, toAmbulance) > -0.3f) return;

        // 💡 關鍵：啟動 S 型避讓前，先強制解除 agent 的煞車狀態
        // 這樣可以避免車子因為前方有車而卡住，無法執行避讓動作
        agent.isStopped = false;
        StartCoroutine(S_CurveYieldRoutine());
    }

private IEnumerator S_CurveYieldRoutine() {
    isYielding = true;
    currentYieldState = YieldState.MovingToSide;
    
    agent.isStopped = false; 
    
    // 💡 【終極手段】在賦予速度前，先用 ResetPath() 徹底清除 agent 當前可能存在的
    // 任何路徑或煞停指令 (例如等紅燈)。這能確保接下來的速度賦予不會被舊狀態干擾。
    agent.ResetPath();

    // --- 消除停頓感的關鍵 ---
    // 1. 關閉自動煞車，讓車輛在接近目標點時不會自己減速，使動作更連貫。
    agent.autoBraking = false;
    // 2. 直接賦予一個初始速度，強制打破 agent 的靜止或慢速狀態，讓車輛立即動起來。
    agent.velocity = transform.forward * agent.speed; 
    
    agent.speed = originalSpeed * 1.8f; // 避讓時稍微加速
    agent.acceleration = 120f; // 💡 再次提高加速度，讓起步更果斷
    agent.angularSpeed =800f;

    // 1. 探測極限偏移量 (確保不撞牆)
    float targetOffset = 3.5f; // 目標偏移 3 米 (約一個車道)

    NavMeshHit nmHit;
    if (agent.Raycast(transform.position + transform.right * targetOffset, out nmHit)) {
        targetOffset = Mathf.Max(0.5f, nmHit.distance - 0.8f);
    }

    float currentOffset = 0f;
    float timer = 0f;
    float yieldDuration = 8.0f; // 避讓總時間

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
    agent.autoBraking = true; // 恢復正常的自動煞車行為
    
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
        if (isYielding || currentYieldState != YieldState.None) return;

        float directionMatch = Vector3.Dot(transform.forward, ambulanceForward);
        if (directionMatch < 0.5f) return;

        // 💡 關鍵過濾：如果我根本還沒靠近路口，就無視這個「強制停下」的廣播
        if (targetNode != null && targetNode.isStopLine) {
            float distToStopLine = Vector3.Distance(transform.position, targetNode.transform.position);
            // 如果距離路口大於 15 米，我當作沒聽到，繼續開我的直行車
            if (distToStopLine > 15f && !IsInIntersection()) return; 
        } else if (!IsInIntersection()) {
            return; // 目標根本不是路口，無視
        }

        // 執行停等邏輯
        if (IsInIntersection()) {
            agent.speed = originalSpeed * 0.5f; 
            agent.isStopped = false;
        } else {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }
        
        isYielding = true;
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

        // 避讓中，Update 絕對不插手
        // 💡 【關鍵修正】一旦 isYielding 為 true，就立刻返回，不執行任何後續的駕駛邏輯。
        // 這可以防止 Update 中的紅綠燈或防撞邏輯干擾 S_CurveYieldRoutine 協程的執行，
        // 徹底解決因指令衝突造成的「原地發呆」問題。
        if (isYielding) return; 

        // 💡 關鍵修正：先問紅綠燈要不要停
        bool stoppedByLight = HandleTrafficLights();

        // 💡 如果紅綠燈沒叫我停，我才用雷達看前面有沒有車
        if (!stoppedByLight) {
            CheckForwardCollision(); 
        }

        // 節點導航邏輯
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

    protected virtual bool HandleTrafficLights() {
        if (isYielding || currentYieldState != YieldState.None) return false; 

        if (targetNode.isStopLine && targetNode.currentIsRed) {
            float dist = Vector3.Distance(transform.position, targetNode.transform.position);
            
            if (dist <= 3.5f) {
                // 🛑 真的壓到停止線了，徹底煞死
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
                return true; // 告訴 Update 我正在等紅燈
            } else {
                // 🟢 還沒到線 (就算是 4 米外)，給我繼續開！
                agent.isStopped = false;
                return false;
            }
        }
        
        return false; // 綠燈或一般節點
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
                        if (!isYielding && currentYieldState == YieldState.None) {
                            StartCoroutine(S_CurveYieldRoutine());
                        }
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

    // 🚑 救護車 V2X 指令：加速逃離
    public void V2X_Accelerate() 
    {
        if (agent != null)
        {
            agent.isStopped = false;
            agent.speed = originalSpeed * 2.0f; // 加速兩倍
            agent.acceleration = 50f; // 讓起步變快
        }
    }

    // 🚑 救護車 V2X 指令：原地強制煞停
    public void V2X_ForceStop() 
    {
        if (agent != null)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }
    }

    // 🚑 救護車過後恢復正常
    public void V2X_Reset()
    {
        if (agent != null)
        {
            agent.isStopped = false;
            agent.speed = originalSpeed;
            agent.acceleration = 8f; // 恢復正常加速度
        }
    }
    
}