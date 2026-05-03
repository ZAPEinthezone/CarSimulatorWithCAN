using UnityEngine;
using UnityEngine.AI;
using System.Reflection;
using System.Collections.Generic;

public class NPC_AmbulanceDrive : NPC_WaypointDrive
{
    [Header("模式控制")]
    public bool isEmergency = false; 
    public float emergencySpeed = 15.0f;
    public float detectRadius = 25f;   
    public float intersectionDist = 50f; 

    [Header("自動導航邏輯 (直行後左轉)")]
    public bool useSmartNavigation = true;
    public int nodesToGoStraight = 1; 
    private int nodesPassed = 0;

    [Header("特效組件")]
    public GameObject sirenLights;   
    public AudioSource sirenAudio;   

    private bool lastEmergencyState;

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
        // --- 1. 停止邏輯 ---
        bool shouldStop = false;

        if (!isEmergency) {
            if (targetNode != null && targetNode.isStopLine && targetNode.currentIsRed) {
                if (Vector3.Distance(transform.position, targetNode.transform.position) < 8f) {
                    shouldStop = true;
                }
            }
            
            // 呼叫父類別防撞邏輯
            CheckForwardCollision(); 
            // 如果父類別邏輯讓 agent 停下，我們遵循它
            if (agent.isStopped) shouldStop = true; 
        } else {
            // 緊急模式
            NotifyNearbyCars();
            CheckForwardCollisionCustom(4.0f); 
            if (agent.isStopped) shouldStop = true;
        }

        // --- 2. 狀態應用 (修復關鍵) ---
        if (shouldStop) {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        } else {
            // 💡 修正：如果之前是停止的，現在要變回行走，必須強制 Resume 並重設目的地
            if (agent.isStopped) {
                agent.isStopped = false;
                // 確保有目的地可去，否則會原地發呆
                if (targetNode != null) agent.SetDestination(targetNode.transform.position);
            }
        }

        // --- 3. 導航目標決策 ---
        if (!agent.pathPending && agent.remainingDistance < 2.5f) {
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

    // 🔍 廣播給附近車輛 (傳入位置與朝向)
    private void NotifyNearbyCars() {
        // 1. 周圍廣播 (處理同向避讓)
        Collider[] nearby = Physics.OverlapSphere(transform.position, detectRadius);
        foreach (var col in nearby) {
            if (col.CompareTag("Car") && col.gameObject != gameObject) {
                var npc = col.GetComponent<NPC_WaypointDrive>();
                if (npc != null) {
                    npc.YieldForAmbulance(transform.position, transform.forward);
                }
            }
        }

        // 2. 路口清空廣播 (重點修正！)
        // 不要用 SendMessage，改用 GetComponent 呼叫，並傳入方向
        RaycastHit[] hits = Physics.SphereCastAll(transform.position, 6f, transform.forward, intersectionDist);
        foreach (var hit in hits) {
            if (hit.collider.CompareTag("Car") && hit.collider.gameObject != gameObject) {
                var npc = hit.collider.GetComponent<NPC_WaypointDrive>();
                if (npc != null) {
                    // 💡 這裡傳入 transform.forward，NPC 才能判定自己是不是對向
                    npc.IntersectionYield(transform.forward); 
                }
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