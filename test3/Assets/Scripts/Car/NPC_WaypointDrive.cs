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

        protected bool v2xForceGo = false;
        protected bool v2xForceStop = false;

        private enum YieldState { None, MovingToSide, AligningForward }
        private YieldState currentYieldState = YieldState.None;

        protected virtual void Awake() {
            agent = GetComponent<NavMeshAgent>();
        }

        protected virtual void Start() {
            if (agent != null) {
                // 💡【關鍵修改】增加 NavMeshAgent 的避讓半徑，減少車輛重疊
                // 這會讓車輛在導航時，本能地與其他 Agent 保持更遠的距離。
                agent.radius = 1.5f;

                originalSpeed = agent.speed;
                if (targetNode != null) agent.SetDestination(targetNode.transform.position);
            }
        }

    public void YieldForAmbulance(NPC_AmbulanceDrive ambulance)
    {
        // 如果已經在閃了，就不要理會
        if (isYielding || currentYieldState != YieldState.None || ambulance == null) return;

        Vector3 ambulancePos = ambulance.transform.position;
        Vector3 ambulanceForward = ambulance.transform.forward;

        // 距離與方向過濾 (救護車在後方才需要讓)
        float dist = Vector3.Distance(transform.position, ambulancePos);
        if (dist > 55f) return;

        Vector3 toAmbulance = (ambulancePos - transform.position).normalized;

        // 💡【雙重關鍵修正】防止對向車誤判
        // 1. 位置判斷：救護車必須在我的後方 (與車頭夾角大於90度，Dot < 0)
        if (Vector3.Dot(transform.forward, toAmbulance) > 0) return;

        // 2. 方向判斷：救護車必須與我同方向行駛 (車頭方向夾角小於60度，Dot > 0.5)
        //    這樣可以徹底排除對向來車的情況。
        float directionMatch = Vector3.Dot(transform.forward, ambulance.transform.forward);
        if (directionMatch < 0.5f) return;

        // 💡【關鍵新增】如果車子正準備開往一個有停止線的路口，而且距離很近了，就不要執行S型避讓，避免在路口亂切
        if (targetNode != null && targetNode.isStopLine)
        {
            float distToStopLine = Vector3.Distance(transform.position, targetNode.transform.position);
            // 💡【關鍵修改】如果距離停止線小於 50 米，不執行S型避讓，而是稍微加速通過路口
            if (distToStopLine < 50f)
            {
                // 確保車輛在網格上且未被其他指令停止
                if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh && !agent.isStopped) {
                    agent.speed = originalSpeed * 1.5f; // 稍微加速
                }
                return; // 返回，不執行後續的 S_CurveYieldRoutine
            }
        }

        //  如果車子已經在路口內，絕對不要做 S 型避讓！直接踩油門衝過去清空！
        if (IsInIntersection())
        {
            V2X_Accelerate();
            return;
        }

        // 啟動 S 型避讓前，先確保在網格上，並強制解除煞車
        if (agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
        }
        StartCoroutine(S_CurveYieldRoutine(ambulance));
    }

    private IEnumerator S_CurveYieldRoutine(NPC_AmbulanceDrive ambulance)
    {
        isYielding = true;
        currentYieldState = YieldState.MovingToSide;

        agent.isStopped = false;
        agent.ResetPath();
        agent.autoBraking = false;
        agent.velocity = transform.forward * agent.speed;

        agent.speed = originalSpeed * 1.8f;
        agent.acceleration = 120f;
        agent.angularSpeed = 800f;

        float targetOffset = 3.4f; // 目標偏移 3.4 米

        Vector3 initialRoadDir = targetNode != null ? (targetNode.transform.position - transform.position).normalized : transform.forward;
        Vector3 roadRight = Vector3.Cross(Vector3.up, initialRoadDir).normalized;
        float rightClear = targetOffset;
        float leftClear = targetOffset;

        // 🛠️ 關鍵修正 2：把雷達發射點「往上抬高 0.8 米」！
        // 避免雷達掃到地板或車子自己的碰撞體，導致誤判左右沒空間
        Vector3 rayOrigin = transform.position + Vector3.up * 0.8f;
        RaycastHit sideHit;

        if (Physics.Raycast(rayOrigin, roadRight, out sideHit, targetOffset))
        {
            // 如果撞到自己的車，忽略它
            if (sideHit.collider.transform.root != this.transform.root)
                rightClear = Mathf.Max(0.5f, sideHit.distance - 0.8f);
        }
        if (Physics.Raycast(rayOrigin, -roadRight, out sideHit, targetOffset))
        {
            if (sideHit.collider.transform.root != this.transform.root)
                leftClear = Mathf.Max(0.5f, sideHit.distance - 0.8f);
        }

        // 💡【關鍵修改】一律靠右避讓
        // 不再判斷左邊空間，強制往右邊 (roadRight) 避讓。
        targetOffset = rightClear;
        Vector3 offsetDir = roadRight;

        if (rightClear < 1.0f && leftClear < 1.0f)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
            yield return new WaitForSeconds(0.5f);
        }

        float currentOffset = 0f;
        float timer = 0f;
        float yieldDuration = 4.5f; // 💡 縮短避讓總時間

        while (timer < yieldDuration)
        {
            timer += Time.deltaTime;

            if (ambulance != null)
            {
                Vector3 toCar = transform.position - ambulance.transform.position;
                if (toCar.magnitude > 10f && Vector3.Dot(ambulance.transform.forward, toCar.normalized) > 0.5f)
                {
                    break;
                }
            }

            if (targetNode != null)
            {
                Vector3 toNode = targetNode.transform.position - transform.position;
                if (toNode.magnitude < 5.0f || Vector3.Dot(transform.forward, toNode.normalized) < 0.3f)
                {
                    TrafficNode next = targetNode.GetNextNode();
                    if (next != null)
                    {
                        targetNode = next;
                        toNode = targetNode.transform.position - transform.position;
                    }
                }

                currentOffset = Mathf.Lerp(currentOffset, targetOffset, Time.deltaTime * 4.0f);
                Vector3 dynamicTarget = targetNode.transform.position + (offsetDir * currentOffset);

                if (agent.isActiveAndEnabled && agent.isOnNavMesh)
                {
                    agent.SetDestination(dynamicTarget);
                }
                else
                {
                    break;
                }
            }

            if (timer > 2.0f)
            {
                agent.speed = Mathf.Lerp(agent.speed, originalSpeed * 0.4f, Time.deltaTime);
            }

            yield return null;
        }

        ReturnToTrack();
    }

    protected bool EnsureAgentOnNavMesh() {
        if (agent == null) return false;
        if (agent.isActiveAndEnabled && agent.isOnNavMesh) return true;

        NavMeshHit hit;
        if (NavMesh.SamplePosition(transform.position, out hit, 3.0f, NavMesh.AllAreas)) {
            agent.Warp(hit.position);
            return true;
        }

        return false;
    }

    protected virtual void ReturnToTrack() {
        currentYieldState = YieldState.None;
        isYielding = false;
        agent.autoBraking = true; // 恢復正常的自動煞車行為
        
        // 🛡️ 【關鍵防呆】確保車輛還在導航網格上才能恢復，否則會噴紅字錯誤死當！
        if (agent.isActiveAndEnabled && agent.isOnNavMesh) {
            agent.isStopped = false;
            agent.speed = originalSpeed;
            agent.acceleration = 12f; // 恢復時稍微加速

            if (targetNode != null) {
                // 如果目前的點已經在屁股後面，就跳到下一個
                int safety = 0;
                while (safety < 5) {
                    Vector3 toNode = targetNode.transform.position - transform.position;
                    if (Vector3.Dot(transform.forward, toNode.normalized) < 0.3f || toNode.magnitude < 5.0f) {
                        TrafficNode next = targetNode.GetNextNode();
                        if (next != null) {
                            targetNode = next;
                            safety++;
                            continue;
                        }
                    }
                    break;
                }
                
                agent.ResetPath(); // 徹底清除舊的 S 型路徑殘留
                if (agent.isOnNavMesh) agent.SetDestination(targetNode.transform.position);
            }
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
        if (isYielding) return; 

        if (v2xForceStop) {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
            if (targetNode != null && !agent.hasPath) agent.SetDestination(targetNode.transform.position);
            return;
        }

        if (v2xForceGo) {
            agent.isStopped = false;
        }

        // 先問紅綠燈要不要停，但如果強制通行或強制停車，不要讓一般紅綠燈覆寫
        bool stoppedByLight = v2xForceGo ? false : HandleTrafficLights();

        if (!stoppedByLight && !v2xForceGo) {
            CheckForwardCollision(); 
        }

        // 節點導航邏輯
        if (!agent.isStopped) {
            float distToTarget = Vector3.Distance(transform.position, targetNode.transform.position);
            
            // 必須「真的有路徑且到達」或「物理距離真的很近」才算抵達節點
            bool reachedByNav = (!agent.pathPending && agent.hasPath && agent.remainingDistance < 2.5f);
            bool reachedByPhysics = (distToTarget < 3.0f);

            if (reachedByNav || reachedByPhysics) {
                TrafficNode nextNode = targetNode.GetNextNode(); 
                if (nextNode != null) {
                    targetNode = nextNode;
                    if (agent.isOnNavMesh) agent.SetDestination(targetNode.transform.position);
                } else {
                    Destroy(gameObject); // 真的開到道路盡頭才銷毀
                }
            }
            // 🛡️ 防呆：如果路徑斷了(hasPath=false)，但離目標還很遠，強制重新導航，而不是自毀
            else if (!agent.hasPath && !agent.pathPending && distToTarget >= 3.0f) {
                if (agent.isOnNavMesh) agent.SetDestination(targetNode.transform.position);
            }
        }
    }

    protected virtual bool HandleTrafficLights() {
        if (v2xForceGo) return false;
        if (v2xForceStop) {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
            return true;
        }
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
        // 定義感測器的「寬度」：左右各 1.2 米 (總寬 2.4 米)，涵蓋大部分車道寬度
        Vector3 boxHalfExtents = new Vector3(1.2f, 0.5f, 0.5f);

        if (v2xForceGo) {
            RaycastHit hitGo;
            if (Physics.BoxCast(transform.TransformPoint(sensorOffset), boxHalfExtents, transform.forward, out hitGo, transform.rotation, 10f)) {
                if (hitGo.collider.transform.root != this.transform.root && hitGo.collider.CompareTag("Car")) {
                    float dist = hitGo.distance;
                    if (dist < 5.0f) {
                        agent.speed = originalSpeed * 0.6f;
                        return;
                    }
                }
            }
            return;
        }

        if (v2xForceStop) {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
            return;
        }

        RaycastHit hit;
        // 🚗 關鍵升級：使用 BoxCast 掃描前方整個車道的寬度
        if (Physics.BoxCast(transform.TransformPoint(sensorOffset), boxHalfExtents, transform.forward, out hit, transform.rotation, sensorLength)) {
            
            // 確保掃到的不是自己的車體，且目標是車輛
            if (hit.collider.transform.root != this.transform.root && hit.collider.CompareTag("Car")) {
                float dist = hit.distance;

                // 💡【關鍵修正】簡化並統一防撞邏輯，移除複雜的對向判斷，確保穩定性
                if (dist < 6.0f) {
                    // 6米內：完全煞停
                    if (IsInIntersection()) {
                        agent.isStopped = false;
                        agent.speed = originalSpeed * 0.4f;
                    } else {
                        agent.isStopped = true;
                        agent.velocity = Vector3.zero;
                    }
                } else if (dist < 8.0f) { // 6-8米：中度減速
                    agent.speed = originalSpeed * 0.6f;
                    agent.isStopped = false;
                } else if (dist < 12.0f) { // 8-12米：輕微減速
                    agent.speed = originalSpeed * 0.8f;
                    agent.isStopped = false;
                } else {
                    // 保持正常速度
                    agent.isStopped = false;
                }
                return;
            }
        }
        
        if (!isYielding) {
            agent.isStopped = false;
            agent.speed = originalSpeed;
        }
    }

    // 順便把 HasObstacleInFront 也升級成 BoxCast (如果有用到的話)
    protected bool HasObstacleInFront() {
        Vector3 boxHalfExtents = new Vector3(1.2f, 0.5f, 0.1f);
        RaycastHit hit;
        if (Physics.BoxCast(transform.TransformPoint(sensorOffset), boxHalfExtents, transform.forward, out hit, transform.rotation, sensorLength)) {
            if (hit.collider.transform.root != this.transform.root && hit.collider.CompareTag("Car")) return true;
        }
        return false;
    }

    protected bool IsInIntersection() {
        if (targetNode == null) return false;
        float dist = Vector3.Distance(transform.position, targetNode.transform.position);
        return dist > 4f && !targetNode.isStopLine;
    }

    // 更新輔助線，讓你在 Scene 視窗可以看到這個「方塊感測器」長怎樣
    private void OnDrawGizmosSelected() {
        Gizmos.color = new Color(1, 0, 0, 0.5f);
        Vector3 sensorPos = transform.TransformPoint(sensorOffset);
        Gizmos.matrix = Matrix4x4.TRS(sensorPos, transform.rotation, Vector3.one);
        // 畫出掃描的範圍
        Gizmos.DrawWireCube(Vector3.forward * (sensorLength / 2f), new Vector3(2.4f, 1f, sensorLength));
    }

    // 🚑 救護車 V2X 指令：加速逃離
    public void V2X_Accelerate() 
    {
        // 加上 isOnNavMesh 防呆，確保不會噴紅字
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            v2xForceGo = true;
            v2xForceStop = false;
            currentYieldState = YieldState.None;
            isYielding = false;
            
            agent.isStopped = false;
            agent.speed = Mathf.Max(agent.speed, originalSpeed * 1.5f); // 速度再拉高一點
            agent.acceleration = 60f;
            
            // 💡 【關鍵修改】關閉自動煞車！確保車子在路口內逃亡時，經過節點絕對不會卡頓減速
            agent.autoBraking = false; 
            
            if (targetNode != null && !agent.hasPath)
                agent.SetDestination(targetNode.transform.position);
        }
    }

    // 🚑 救護車 V2X 指令：原地強制煞停
    public void V2X_ForceStop() 
    {
        if (agent != null)
        {
            v2xForceGo = false;
            v2xForceStop = true;
            currentYieldState = YieldState.None;
            isYielding = false;
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }
    }

    // 🚑 救護車過後恢復正常
    public void V2X_Reset()
    {
        if (agent != null)
        {
            v2xForceGo = false;
            v2xForceStop = false;
            agent.isStopped = false;
            agent.speed = originalSpeed;
            agent.acceleration = 8f; // 恢復正常加速度
            agent.autoBraking = true;
            if (targetNode != null) agent.SetDestination(targetNode.transform.position);
        }
    }
    
}