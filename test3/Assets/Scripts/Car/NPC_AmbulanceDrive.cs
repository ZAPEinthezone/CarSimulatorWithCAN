using UnityEngine;
using UnityEngine.AI;
using System.Reflection; 

public class NPC_AmbulanceDrive : NPC_WaypointDrive
{
    [Header("緊急狀態控制")]
    public bool isEmergency = false; 

    [Header("物理漂移設定")]
    [Tooltip("緊急模式的速度，越高噴越遠")]
    public float emergencySpeed = 11.0f; 
    [Tooltip("判定轉彎太急的門檻 (0到1，越大越敏感)")]
    public float driftThreshold = 0.75f; 
    [Tooltip("打滑時轉向力剩餘多少 (0.1代表轉很慢，會噴出去)")]
    public float driftSteerFactor = 0.2f;

    [Header("救護車特效組件")]
    public GameObject sirenLights;   
    public AudioSource sirenAudio;   

    private float normalSpeed;
    private float normalAngularSpeed;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (agent != null) 
        {
            normalSpeed = agent.speed;
            normalAngularSpeed = agent.angularSpeed; // 紀錄原本轉向速度
        }
    }

    protected override void Update()
    {
        if (targetNode == null || agent == null) return;

        HandleEffects();

        if (isEmergency) 
        {
            // 執行緊急模式邏輯
            RunEmergencyPhysics();
        } 
        else 
        {
            // 普通模式：恢復原始速度與轉向
            agent.speed = normalSpeed;
            agent.angularSpeed = normalAngularSpeed;
            base.Update(); 
            return;
        }

        // 到達節點換下一個 (緊急模式)
        if (!agent.isStopped && !agent.pathPending && agent.remainingDistance < 2f) 
        {
            TrafficNode nextNode = targetNode.GetNextNode(); 
            if (nextNode != null) {
                targetNode = nextNode; 
                agent.SetDestination(targetNode.transform.position);
            } else {
                Destroy(gameObject);
            }
        }
    }

    private void RunEmergencyPhysics()
    {
        // 1. 設定高速
        agent.speed = emergencySpeed;

        // 2. 檢測轉彎急促程度
        // steeringTarget 是 Agent 目前想去的路徑轉折點
        Vector3 moveDir = agent.velocity.normalized;
        Vector3 targetDir = (agent.steeringTarget - transform.position).normalized;
        
        // 計算當前前進方向與目標方向的重合度 (1=直線, 0=90度彎)
        float turnMatch = Vector3.Dot(transform.forward, targetDir);

        if (turnMatch < driftThreshold && agent.velocity.magnitude > 5f)
        {
            // 【噴出去的關鍵】大幅降低轉向速度，讓它轉不過來
            agent.angularSpeed = normalAngularSpeed * driftSteerFactor;
            // 甚至可以稍微加快一點點線速度，增加失控感
            agent.speed = emergencySpeed * 1.1f;
        }
        else
        {
            // 直線或小彎，恢復正常轉向
            agent.angularSpeed = normalAngularSpeed;
        }

        // 3. 障礙物偵測
        CheckForwardCollision();
        if (!IsPathBlockedByCar()) agent.isStopped = false; 
    }

    private void HandleEffects()
    {
        if (sirenLights != null) 
        {
            if (sirenLights.activeSelf != isEmergency) sirenLights.SetActive(isEmergency);

            if (isEmergency)
            {
                LightManager manager = sirenLights.GetComponent<LightManager>();
                if (manager != null)
                {
                    FieldInfo field = typeof(LightManager).GetField("sirenMode", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null) field.SetValue(manager, 2); 

                    Light[] allLights = sirenLights.GetComponentsInChildren<Light>(true);
                    foreach (Light l in allLights) l.enabled = true;
                }
            }
        }
        
        if (sirenAudio != null) 
        {
            if (isEmergency && !sirenAudio.isPlaying) sirenAudio.Play();
            else if (!isEmergency && sirenAudio.isPlaying) sirenAudio.Stop();
        }
    }

    private bool IsPathBlockedByCar() 
    {
        RaycastHit hit;
        Vector3 sensorPos = transform.TransformPoint(sensorOffset);
        if (Physics.Raycast(sensorPos, transform.forward, out hit, sensorLength)) {
            if (hit.collider.CompareTag("Car")) return true;
        }
        return false;
    }
}