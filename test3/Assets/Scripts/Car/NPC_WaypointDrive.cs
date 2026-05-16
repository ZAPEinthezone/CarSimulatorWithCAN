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
        
        [Header("避讓設定")]
        public float maxYieldDistance = 55f;
        public float yieldSideOffset = 3.2f;
        public float yieldProbeDistance = 6f;
        public float yieldMoveDistance = 8.0f;
        public float yieldHoldTime = 3.0f;
        public float yieldSpeedMultiplier = 1.2f;
        public float yieldDuration = 4.0f;
        public float followSpeedRatio = 0.4f;
        public float stopLineSlowDistance = 8.0f;

        protected bool isYielding = false;
        protected float originalSpeed;
        public bool IsYielding => isYielding;

        private enum YieldState { None, MovingToSide, AlignToForward }
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

        if (!CanPerformSideYield(ambulancePos, ambulanceForward))
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
            return;
        }

        // 💡 關鍵：啟動側移前，先強制解除 agent 的煞車狀態
        agent.isStopped = false;
        StartCoroutine(S_CurveYieldRoutine());
    }

    private bool CanPerformSideYield(Vector3 ambulancePos, Vector3 ambulanceForward)
    {
        if (targetNode == null) return false;
        if (Vector3.Distance(transform.position, ambulancePos) > maxYieldDistance) return false;
        if (targetNode.isStopLine && targetNode.currentIsRed) return false;
        if (IsInIntersection()) return false;
        if (HasObstacleInFront(4f)) return false;
        return IsSideClear(transform.right) || IsSideClear(-transform.right);
    }

    private bool IsSideClear(Vector3 sideDirection)
    {
        Vector3 origin = transform.TransformPoint(sensorOffset);
        Vector3 probeDirForward = (sideDirection + transform.forward * 0.4f).normalized;
        Vector3 probeDirSide = (sideDirection + transform.forward * 0.1f).normalized;
        bool forwardClear = !Physics.Raycast(origin, probeDirForward, yieldProbeDistance);
        bool sideClear = !Physics.Raycast(origin, probeDirSide, yieldProbeDistance * 0.8f);
        return forwardClear && sideClear;
    }

    private IEnumerator S_CurveYieldRoutine() {
        isYielding = true;
        currentYieldState = YieldState.MovingToSide;
        
        agent.isStopped = false; 
        agent.ResetPath();
        agent.autoBraking = false;
        agent.velocity = transform.forward * agent.speed; 
        
        agent.speed = originalSpeed * yieldSpeedMultiplier;
        agent.acceleration = 120f;
        agent.angularSpeed = 800f;

        bool rightClear = IsSideClear(transform.right);
        bool leftClear = IsSideClear(-transform.right);
        if (!rightClear && !leftClear)
        {
            ReturnToTrack();
            yield break;
        }

        float avoidSign = rightClear ? 1f : -1f;
        Vector3 laneTarget = transform.position + transform.forward * yieldMoveDistance + transform.right * avoidSign * yieldSideOffset;

        while (!HasReached(laneTarget, 1.2f)) {
            agent.SetDestination(laneTarget);
            yield return null;
        }

        currentYieldState = YieldState.AlignToForward;
        float holdTimer = 0f;
        while (holdTimer < yieldHoldTime) {
            holdTimer += Time.deltaTime;
            Vector3 sideHoldPoint = transform.position + transform.forward * 2f + transform.right * avoidSign * yieldSideOffset;
            agent.SetDestination(sideHoldPoint);
            agent.speed = Mathf.Lerp(agent.speed, originalSpeed * followSpeedRatio, Time.deltaTime * 3f);
            yield return null;
        }

        currentYieldState = YieldState.None;
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
            float slowDistance = stopLineSlowDistance;
            float stopDistance = 2.5f;

            if (dist <= stopDistance) {
                // 🛑 真的壓到停止線了，徹底煞死
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
                agent.speed = 0f;
                return true;
            }
            else if (dist <= slowDistance) {
                // ⚠️ 紅燈接近中，提前減速跟車，避免衝線
                agent.isStopped = false;
                agent.speed = Mathf.Lerp(agent.speed, originalSpeed * 0.2f, Time.deltaTime * 5f);
                agent.SetDestination(targetNode.transform.position);
                return false;
            }
            else {
                // 🟢 還沒到減速範圍，正常前進
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
                    // 前方有車時，不要亂繞過去，直接停下或慢速跟車
                    agent.isStopped = true;
                    agent.velocity = Vector3.zero;
                    agent.speed = 0f;
                } else {
                    agent.isStopped = false;
                    agent.speed = originalSpeed * 0.4f;
                }
                return;
            }
        }
        
        if (!isYielding) {
            agent.isStopped = false;
            agent.speed = originalSpeed;
        }
    }

    protected bool HasObstacleInFront(float distance = -1f) {
        float rayDistance = distance > 0f ? distance : sensorLength;
        RaycastHit hit;
        if (Physics.Raycast(transform.TransformPoint(sensorOffset), transform.forward, out hit, rayDistance)) {
            if (hit.collider.CompareTag("Car")) return true;
        }
        return false;
    }

    private bool HasReached(Vector3 destination, float threshold)
    {
        return Vector3.Distance(transform.position, destination) <= threshold;
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