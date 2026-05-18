using UnityEngine;
using UnityEngine.AI;
using System.Reflection;
using System.Collections.Generic;

public class NPC_AmbulanceDrive : NPC_WaypointDrive
{
    [Header("模式控制")]
    public bool isEmergency = false; 
    public float emergencySpeed = 15.0f;
    public float detectRadius = 55f;   // 💡 再次增加偵測半徑，讓 NPC 更早反應
    public float intersectionDist = 50f; 

    [Header("自動導航邏輯 (直行後左轉)")]
    public bool useSmartNavigation = true;
    public int nodesToGoStraight = 1; 
    private int nodesPassed = 0;

    [Header("特效組件")]
    public GameObject sirenLights;   
    public AudioSource sirenAudio;   

    private bool lastEmergencyState;
    private bool isWaitingAtRedLight = false; // 💡【關鍵新增】紅燈等待狀態鎖
    private bool isFullyStopped = false;      // 💡【關鍵新增】徹底停止狀態鎖

    protected override void Start() {
        base.Start();
        lastEmergencyState = isEmergency;
    }

    protected override void Update() {
        if (agent == null) return;

        HandleStateChange();
        HandleEffects();

        // 執行導航與停止邏輯
        if (useSmartNavigation) {
            SmartNavigation();
        } else {
            base.Update();
        }
    }

    private void SmartNavigation() {
        // --- 1. 停止邏輯 (紅綠燈與防撞) ---
        bool shouldStop = false;

        if (!isEmergency) {
            // 普通模式：判斷紅燈
            if (targetNode != null && targetNode.isStopLine && targetNode.currentIsRed) {
                isWaitingAtRedLight = true; // 只要是紅燈，就進入等待狀態
                float dist = Vector3.Distance(transform.position, targetNode.transform.position);
                if (dist < 4.0f) { // 稍微放寬停車判斷距離
                    // 💡【新方法】改用 isStopped 和 ResetPath() 來凍結車輛，避免 Editor 報錯
                    shouldStop = true;
                    agent.isStopped = true;
                    agent.velocity = Vector3.zero;
                    agent.ResetPath();
                    isFullyStopped = true;
                    return; 
                } else if (dist <= 15f) {
                    // 在 15 米內，就開始根據距離降低速度，準備停車
                    agent.speed = Mathf.Lerp(0, originalSpeed, dist / 15f);
                    agent.isStopped = false;
                    return; // 💡【關鍵修正】直接返回，讓減速指令不被干擾
                }
            } else {
                // 從紅燈變綠燈的瞬間
                if (isWaitingAtRedLight) {
                    isWaitingAtRedLight = false;
                    isFullyStopped = false;
                    agent.isStopped = false;
                }
            }
            
            // 普通模式：判斷前方碰撞 (雷達)
            // 注意：這裡不直接 return，而是設定狀態
            CheckForwardCollision(); 
            if (agent.isStopped) shouldStop = true; 

        } else {
            // 緊急模式：強制不准停（除非快撞到車）
            agent.isStopped = false;
            NotifyNearbyCars();
            CheckForwardCollisionCustom(4.0f); 
            if (agent.isStopped) shouldStop = true;
        }

        // 應用停止狀態
        if (shouldStop) {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
            // 💡 重點：即使停下也不要 return，讓下方的尋路邏輯維持目標更新的準備
        } else {
            agent.isStopped = false;
        }

        // --- 2. 導航目標決策 (維持運作) ---
        // 即使在等紅燈，也要確保目標節點正確切換
        // 💡【關鍵防禦】只有在非等待紅燈的狀態下，才允許切換節點
        if (!isWaitingAtRedLight && !agent.pathPending && agent.remainingDistance < 2.5f) {
            nodesPassed++;
            TrafficNode nextNode = null;
            List<TrafficNode> choices = targetNode.nextNodes;

            if (choices != null && choices.Count > 0) {
                if (nodesPassed <= nodesToGoStraight || choices.Count == 1) {
                    nextNode = FindBestForwardNode(choices);
                } else {
                    nextNode = FindMostLeftNode(choices);
                }
            }

            if (nextNode != null) {
                targetNode = nextNode;
                agent.SetDestination(targetNode.transform.position);
            }
        }
    }

   private void NotifyNearbyCars() {
        // --- 1. 廣播給路口大腦 (控制紅綠燈) ---
        IntersectionV2X[] intersections = FindObjectsOfType<IntersectionV2X>();
        foreach (var brain in intersections) {
            if (brain == null) continue;
            // 💡【關鍵修改】拉長通知路口的距離，從 40 米增加到 65 米，提早開綠燈
            if (Vector3.Distance(transform.position, brain.transform.position) > 70f) continue;
            brain.AmbulanceApproach(this);
        }

        // --- 2. 廣播給附近車輛 (💡 修改這裡：精準的超車與避讓指令) ---
        Collider[] hits = Physics.OverlapSphere(transform.position, detectRadius);
        foreach (var hit in hits) {
            var vehicleRoot = hit.transform.root.gameObject;
            if (!vehicleRoot.CompareTag("Car") || vehicleRoot == this.gameObject) continue;

            var npc = vehicleRoot.GetComponent<NPC_WaypointDrive>();
            if (npc != null) {
                // 💡 【修正】不要再強制所有人加速了！統一發送「救護車來了」的警告
                // 讓 NPC 自己的腳本去判斷它現在應該「靠邊停」還是「加速過路口」
                npc.YieldForAmbulance(this); 
            }
        }
    }

    // 🔍 向量運算：尋找最左邊的點
    private TrafficNode FindMostLeftNode(List<TrafficNode> choices) {
        TrafficNode bestNode = choices[0];
        float minX = float.MaxValue;
        foreach (var node in choices) {
            Vector3 relativePos = transform.InverseTransformPoint(node.transform.position);
            if (relativePos.x < minX) {
                minX = relativePos.x;
                bestNode = node;
            }
        }
        return bestNode;
    }

    // 🔍 向量運算：尋找最直行的點
    private TrafficNode FindBestForwardNode(List<TrafficNode> choices) {
        TrafficNode bestNode = choices[0];
        float maxDot = -2.0f;
        foreach (var node in choices) {
            Vector3 dirToNode = (node.transform.position - transform.position).normalized;
            float dot = Vector3.Dot(transform.forward, dirToNode);
            if (dot > maxDot) {
                maxDot = dot;
                bestNode = node;
            }
        }
        return bestNode;
    }

    private void HandleStateChange() {
        if (isEmergency != lastEmergencyState) {
            agent.ResetPath();
            if (!isEmergency) {
                agent.speed = originalSpeed;
                agent.acceleration = 8f;
                agent.angularSpeed = 120f;
            } else {
                agent.speed = emergencySpeed;
                agent.acceleration = 40f;
                agent.angularSpeed = 1000f;
                 // 💡【關鍵修正】切換到緊急模式時，強制清除所有紅燈等待狀態
                isWaitingAtRedLight = false;
                isFullyStopped = false;
                agent.isStopped = false;

            }
            if (targetNode != null) agent.SetDestination(targetNode.transform.position);
            lastEmergencyState = isEmergency;
        }
    }

    protected void CheckForwardCollisionCustom(float dist) {
        RaycastHit hit;
        if (Physics.Raycast(transform.TransformPoint(sensorOffset), transform.forward, out hit, dist)) {
            if (hit.collider.CompareTag("Car")) {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
                return;
            }
        }
        agent.isStopped = false;
    }

    private void HandleEffects() {
        if (sirenLights != null && sirenLights.activeSelf != isEmergency)
            sirenLights.SetActive(isEmergency);
        
        if (isEmergency && sirenLights != null) {
            LightManager manager = sirenLights.GetComponent<LightManager>();
            if (manager != null) {
                FieldInfo field = typeof(LightManager).GetField("sirenMode", BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null) field.SetValue(manager, 2); 
                Light[] lights = sirenLights.GetComponentsInChildren<Light>(true);
                foreach (Light l in lights) l.enabled = true;
            }
        }
        
        if (sirenAudio != null) {
            if (isEmergency && !sirenAudio.isPlaying) sirenAudio.Play();
            else if (!isEmergency && sirenAudio.isPlaying) sirenAudio.Stop();
        }
    }
}